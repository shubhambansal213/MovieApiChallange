using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApiApplication.Database.Entities;
using ApiApplication.Database.Repositories.Abstractions;
using ApiApplication.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiApplication.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IShowtimesRepository _showtimesRepository;
        private readonly ITicketsRepository _ticketsRepository;
        private readonly IAuditoriumsRepository _auditoriumsRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReservationService> _logger;

        public ReservationService(
            IShowtimesRepository showtimesRepository,
            ITicketsRepository ticketsRepository,
            IAuditoriumsRepository auditoriumsRepository,
            IConfiguration configuration,
            ILogger<ReservationService> logger)
        {
            _showtimesRepository = showtimesRepository;
            _ticketsRepository = ticketsRepository;
            _auditoriumsRepository = auditoriumsRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ReservationResponse?> ReserveSeatsAsync(ReserveSeatRequest request, CancellationToken cancellationToken)
        {
            // Validate input
            if (request.Seats == null || !request.Seats.Any())
            {
                _logger.LogWarning("No seats provided for reservation");
                return null;
            }

            // Check if seats are contiguous
            if (!AreSeatsContiguous(request.Seats))
            {
                _logger.LogWarning("Seats are not contiguous: {Seats}", string.Join(", ", request.Seats.Select(s => $"{s.Row}-{s.SeatNumber}")));
                return null;
            }

            // Get showtime with movie information
            var showtime = await _showtimesRepository.GetWithMoviesByIdAsync(request.ShowtimeId, cancellationToken);
            if (showtime == null)
            {
                _logger.LogWarning("Showtime {ShowtimeId} not found", request.ShowtimeId);
                return null;
            }

            // Get auditorium to verify seats exist
            var auditorium = await _auditoriumsRepository.GetAsync(showtime.AuditoriumId, cancellationToken);
            if (auditorium == null)
            {
                _logger.LogWarning("Auditorium {AuditoriumId} not found", showtime.AuditoriumId);
                return null;
            }

            // Verify all requested seats exist in the auditorium
            var requestedSeats = new List<SeatEntity>();
            foreach (var seatSelection in request.Seats)
            {
                var seat = auditorium.Seats?.FirstOrDefault(s => s.Row == seatSelection.Row && s.SeatNumber == seatSelection.SeatNumber);
                if (seat == null)
                {
                    _logger.LogWarning("Seat {Row}-{SeatNumber} not found in auditorium {AuditoriumId}", 
                        seatSelection.Row, seatSelection.SeatNumber, showtime.AuditoriumId);
                    return null;
                }
                requestedSeats.Add(seat);
            }

            // Get all tickets for this showtime
            var existingTickets = await _ticketsRepository.GetEnrichedAsync(request.ShowtimeId, cancellationToken);
            var expirationMinutes = _configuration.GetValue<int>("ReservationSettings:ExpirationMinutes", 10);
            var now = DateTime.UtcNow;

            foreach (var seatSelection in request.Seats)
            {
                foreach (var ticket in existingTickets)
                {
                    // Check if seat is already sold
                    if (ticket.Paid && ticket.Seats.Any(s => s.Row == seatSelection.Row && s.SeatNumber == seatSelection.SeatNumber))
                    {
                        _logger.LogWarning("Seat {Row}-{SeatNumber} is already sold", seatSelection.Row, seatSelection.SeatNumber);
                        return null;
                    }

                    // Check if seat is reserved and not expired
                    if (!ticket.Paid && ticket.Seats.Any(s => s.Row == seatSelection.Row && s.SeatNumber == seatSelection.SeatNumber))
                    {
                        var reservationAge = now - ticket.CreatedTime;
                        if (reservationAge.TotalMinutes < expirationMinutes)
                        {
                            _logger.LogWarning("Seat {Row}-{SeatNumber} is currently reserved (expires in {Minutes} minutes)",
                                seatSelection.Row, seatSelection.SeatNumber,
                                expirationMinutes - reservationAge.TotalMinutes);
                            return null;
                        }
                    }
                }
            }

            // Create reservation (ticket)
            var newTicket = await _ticketsRepository.CreateAsync(showtime, requestedSeats, cancellationToken);

            var expiresAt = newTicket.CreatedTime.AddMinutes(expirationMinutes);

            return new ReservationResponse
            {
                ReservationId = newTicket.Id,
                NumberOfSeats = requestedSeats.Count,
                AuditoriumId = showtime.AuditoriumId,
                MovieTitle = showtime.Movie?.Title,
                SessionDate = showtime.SessionDate,
                ExpiresAt = expiresAt,
                Seats = request.Seats
            };
        }

        public async Task<ConfirmPaymentResponse> ConfirmReservationAsync(Guid reservationId, CancellationToken cancellationToken)
        {
            var ticket = await _ticketsRepository.GetAsync(reservationId, cancellationToken);

            if (ticket == null)
            {
                return new ConfirmPaymentResponse
                {
                    ReservationId = reservationId,
                    Success = false,
                    Message = "Reservation not found"
                };
            }

            // Check if already paid
            if (ticket.Paid)
            {
                return new ConfirmPaymentResponse
                {
                    ReservationId = reservationId,
                    Success = false,
                    Message = "Reservation has already been confirmed"
                };
            }

            // Check if expired
            var expirationMinutes = _configuration.GetValue<int>("ReservationSettings:ExpirationMinutes", 10);
            var reservationAge = DateTime.UtcNow - ticket.CreatedTime;

            if (reservationAge.TotalMinutes > expirationMinutes)
            {
                return new ConfirmPaymentResponse
                {
                    ReservationId = reservationId,
                    Success = false,
                    Message = $"Reservation has expired (older than {expirationMinutes} minutes)"
                };
            }

            // Confirm payment
            await _ticketsRepository.ConfirmPaymentAsync(ticket, cancellationToken);

            return new ConfirmPaymentResponse
            {
                ReservationId = reservationId,
                Success = true,
                Message = "Payment confirmed successfully"
            };
        }

        public async Task<ReservationResponse?> GetReservationByIdAsync(Guid reservationId, CancellationToken cancellationToken)
        {
            var ticket = await _ticketsRepository.GetEnrichedByIdAsync(reservationId, cancellationToken);
            
            if (ticket == null)
            {
                return null;
            }

            var expirationMinutes = _configuration.GetValue<int>("ReservationSettings:ExpirationMinutes", 10);
            var expiresAt = ticket.CreatedTime.AddMinutes(expirationMinutes);
            var isExpired = DateTime.UtcNow > expiresAt;

            return new ReservationResponse
            {
                ReservationId = ticket.Id,
                NumberOfSeats = ticket.Seats?.Count ?? 0,
                AuditoriumId = ticket.Showtime?.AuditoriumId ?? 0,
                MovieTitle = ticket.Showtime?.Movie?.Title,
                SessionDate = ticket.Showtime?.SessionDate ?? DateTime.MinValue,
                ExpiresAt = expiresAt,
                Seats = ticket.Seats?.Select(s => new SeatSelection { Row = s.Row, SeatNumber = s.SeatNumber }).ToList() ?? new List<SeatSelection>()
            };
        }

        public bool AreSeatsContiguous(List<SeatSelection> seats)
        {
            if (seats == null || seats.Count <= 1)
                return true;

            // Sort seats by row and seat number
            var sortedSeats = seats.OrderBy(s => s.Row).ThenBy(s => s.SeatNumber).ToList();

            // All seats must be in the same row
            if (sortedSeats.Select(s => s.Row).Distinct().Count() > 1)
                return false;

            // Check if seat numbers are consecutive
            for (int i = 1; i < sortedSeats.Count; i++)
            {
                if (sortedSeats[i].SeatNumber != sortedSeats[i - 1].SeatNumber + 1)
                    return false;
            }

            return true;
        }
    }
}

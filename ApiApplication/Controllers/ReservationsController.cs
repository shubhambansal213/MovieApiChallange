using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApiApplication.Models;
using ApiApplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiApplication.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;
        private readonly ILogger<ReservationsController> _logger;

        public ReservationsController(
            IReservationService reservationService,
            ILogger<ReservationsController> logger)
        {
            _reservationService = reservationService;
            _logger = logger;
        }

        /// <summary>
        /// Reserve seats for a showtime
        /// </summary>
        /// <remarks>
        /// Rules:
        /// - All seats must be contiguous (consecutive seats in the same row)
        /// - Cannot reserve already reserved seats (within 10 minutes)
        /// - Cannot reserve already sold seats
        /// - Reservation expires after 10 minutes
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReserveSeats([FromBody] ReserveSeatRequest request, CancellationToken cancellationToken)
        {
            if (request.ShowtimeId <= 0)
            {
                return BadRequest(new { message = "Invalid ShowtimeId" });
            }

            if (request.Seats == null || !request.Seats.Any())
            {
                return BadRequest(new { message = "At least one seat must be selected" });
            }

            // Check if seats are contiguous
            if (!_reservationService.AreSeatsContiguous(request.Seats))
            {
                return BadRequest(new
                {
                    message = "All seats must be contiguous (consecutive seats in the same row)",
                    selectedSeats = request.Seats
                });
            }

            var reservation = await _reservationService.ReserveSeatsAsync(request, cancellationToken);

            if (reservation == null)
            {
                return Conflict(new
                {
                    message = "Unable to reserve seats. They may be already reserved or sold, or the showtime/auditorium may not exist.",
                    requestedSeats = request.Seats
                });
            }

            _logger.LogInformation("Created reservation {ReservationId} for showtime {ShowtimeId} with {SeatCount} seats",
                reservation.ReservationId, request.ShowtimeId, reservation.NumberOfSeats);

            return CreatedAtAction(nameof(GetReservation), new { id = reservation.ReservationId }, reservation);
        }

        /// <summary>
        /// Get reservation details by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReservation(Guid id, CancellationToken cancellationToken)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id, cancellationToken);
            
            if (reservation == null)
            {
                return NotFound(new { message = $"Reservation {id} not found" });
            }

            return Ok(reservation);
        }
    }
}

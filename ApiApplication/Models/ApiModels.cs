using System;
using System.Collections.Generic;

namespace ApiApplication.Models
{
    public class MovieDto
    {
        public string? Id { get; set; }
        public string? Rank { get; set; }
        public string? Title { get; set; }
        public string? FullTitle { get; set; }
        public string? Year { get; set; }
        public string? Image { get; set; }
        public string? Crew { get; set; }
        public string? ImDbRating { get; set; }
        public string? ImDbRatingCount { get; set; }
        
        // Legacy properties for backward compatibility
        public string? ImDbId { get; set; }
        public string? Stars { get; set; }
        public string? ReleaseDate { get; set; }
    }

    public class CreateShowtimeRequest
    {
        public string? MovieId { get; set; }
        public DateTime SessionDate { get; set; }
        public int AuditoriumId { get; set; }
    }

    public class CreateShowtimeResponse
    {
        public int Id { get; set; }
        public MovieDto? Movie { get; set; }
        public DateTime SessionDate { get; set; }
        public int AuditoriumId { get; set; }
    }

    public class ReserveSeatRequest
    {
        public int ShowtimeId { get; set; }
        public List<SeatSelection> Seats { get; set; } = new();
    }

    public class SeatSelection
    {
        public short Row { get; set; }
        public short SeatNumber { get; set; }
    }

    public class ReservationResponse
    {
        public Guid ReservationId { get; set; }
        public int NumberOfSeats { get; set; }
        public int AuditoriumId { get; set; }
        public string? MovieTitle { get; set; }
        public DateTime SessionDate { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<SeatSelection> Seats { get; set; } = new();
    }

    public class ConfirmPaymentRequest
    {
        public Guid ReservationId { get; set; }
    }

    public class ConfirmPaymentResponse
    {
        public Guid ReservationId { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace ApiApplication.Database.Entities
{
    public class TicketEntity
    {
        public TicketEntity()
        {
            CreatedTime = DateTime.UtcNow;
            Paid = false;
            Seats = new List<SeatEntity>();
            Showtime = null!;
        }

        public Guid Id { get; set; }
        public int ShowtimeId { get; set; }
        public ICollection<SeatEntity> Seats { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool Paid { get; set; }
        public ShowtimeEntity Showtime { get; set; }
    }
}

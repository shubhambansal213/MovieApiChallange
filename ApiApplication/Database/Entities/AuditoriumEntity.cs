using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiApplication.Database.Entities
{
    public class AuditoriumEntity
    {
        public AuditoriumEntity()
        {
            Showtimes = new List<ShowtimeEntity>();
            Seats = new List<SeatEntity>();
        }

        public int Id { get; set; }
        public List<ShowtimeEntity> Showtimes { get; set; }
        public ICollection<SeatEntity> Seats { get; set; }
       
    }
}

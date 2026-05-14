using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiApplication.Database.Entities
{
    public class MovieEntity
    {
        public MovieEntity()
        {
            Title = string.Empty;
            ImdbId = string.Empty;
            Stars = string.Empty;
            Showtimes = new List<ShowtimeEntity>();
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string ImdbId { get; set; }
        public string Stars { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<ShowtimeEntity> Showtimes { get; set; }
    }
}

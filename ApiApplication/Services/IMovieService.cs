using System.Collections.Generic;
using System.Threading.Tasks;
using ApiApplication.Models;

namespace ApiApplication.Services
{
    public interface IMovieService
    {
        Task<MovieDto?> GetMovieByIdAsync(string movieId);
        Task<List<MovieDto>> GetAllMoviesAsync();
    }
}

using System.Collections.Generic;
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
    public class MoviesController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(IMovieService movieService, ILogger<MoviesController> logger)
        {
            _movieService = movieService;
            _logger = logger;
        }

        /// <summary>
        /// Get all available movies from Provided API (with caching)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<MovieDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var movies = await _movieService.GetAllMoviesAsync();
            return Ok(movies);
        }

        /// <summary>
        /// Get movie by ID from Provided API (with caching)
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MovieDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(string id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            
            if (movie == null)
            {
                return NotFound(new { message = $"Movie with ID {id} not found" });
            }

            return Ok(movie);
        }
    }
}

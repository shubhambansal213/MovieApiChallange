using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApiApplication.Database.Entities;
using ApiApplication.Database.Repositories.Abstractions;
using ApiApplication.Models;
using ApiApplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiApplication.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShowtimesController : ControllerBase
    {
        private readonly IShowtimesRepository _showtimesRepository;
        private readonly IAuditoriumsRepository _auditoriumsRepository;
        private readonly IMovieService _movieService;
        private readonly ILogger<ShowtimesController> _logger;

        public ShowtimesController(
            IShowtimesRepository showtimesRepository,
            IAuditoriumsRepository auditoriumsRepository,
            IMovieService movieService,
            ILogger<ShowtimesController> logger)
        {
            _showtimesRepository = showtimesRepository;
            _auditoriumsRepository = auditoriumsRepository;
            _movieService = movieService;
            _logger = logger;
        }

        /// <summary>
        /// Get all showtimes
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CreateShowtimeResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var showtimes = await _showtimesRepository.GetAllAsync(null!, cancellationToken);
            
            var response = showtimes.Select(s => new CreateShowtimeResponse
            {
                Id = s.Id,
                SessionDate = s.SessionDate,
                AuditoriumId = s.AuditoriumId,
                Movie = s.Movie != null ? new MovieDto
                {
                    Id = s.Movie.ImdbId,
                    Title = s.Movie.Title,
                    ImDbId = s.Movie.ImdbId,
                    Stars = s.Movie.Stars,
                    ReleaseDate = s.Movie.ReleaseDate.ToString("yyyy")
                } : null
            });

            return Ok(response);
        }

        /// <summary>
        /// Get showtime by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CreateShowtimeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var showtime = await _showtimesRepository.GetWithMoviesByIdAsync(id, cancellationToken);
            
            if (showtime == null)
            {
                return NotFound(new { message = $"Showtime with ID {id} not found" });
            }

            var response = new CreateShowtimeResponse
            {
                Id = showtime.Id,
                SessionDate = showtime.SessionDate,
                AuditoriumId = showtime.AuditoriumId,
                Movie = showtime.Movie != null ? new MovieDto
                {
                    Id = showtime.Movie.ImdbId,
                    Title = showtime.Movie.Title,
                    ImDbId = showtime.Movie.ImdbId,
                    Stars = showtime.Movie.Stars,
                    ReleaseDate = showtime.Movie.ReleaseDate.ToString("yyyy")
                } : null
            };

            return Ok(response);
        }

        /// <summary>
        /// Create a new showtime with movie data from Provided API
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateShowtimeResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateShowtimeRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.MovieId))
            {
                return BadRequest(new { message = "MovieId is required" });
            }

            // Verify auditorium exists
            var auditorium = await _auditoriumsRepository.GetAsync(request.AuditoriumId, cancellationToken);
            if (auditorium == null)
            {
                return NotFound(new { message = $"Auditorium with ID {request.AuditoriumId} not found" });
            }

            // Get movie data from Provided API (with caching and fallback)
            var movieDto = await _movieService.GetMovieByIdAsync(request.MovieId);
            if (movieDto == null)
            {
                return NotFound(new { message = $"Movie with ID {request.MovieId} not found in Provided API" });
            }

            // Parse release date
            DateTime releaseDate = DateTime.MinValue;
            if (!string.IsNullOrWhiteSpace(movieDto.ReleaseDate))
            {
                DateTime.TryParse(movieDto.ReleaseDate, out releaseDate);
            }

            // Create movie entity
            var movieEntity = new MovieEntity
            {
                Title = movieDto.Title ?? "Unknown",
                ImdbId = movieDto.ImDbId ?? movieDto.Id ?? "unknown",
                Stars = movieDto.Stars ?? "",
                ReleaseDate = releaseDate
            };

            // Create showtime
            var showtimeEntity = new ShowtimeEntity
            {
                Movie = movieEntity,
                SessionDate = request.SessionDate,
                AuditoriumId = request.AuditoriumId
            };

            var createdShowtime = await _showtimesRepository.CreateShowtime(showtimeEntity, cancellationToken);

            var response = new CreateShowtimeResponse
            {
                Id = createdShowtime.Id,
                SessionDate = createdShowtime.SessionDate,
                AuditoriumId = createdShowtime.AuditoriumId,
                Movie = new MovieDto
                {
                    Id = movieEntity.ImdbId,
                    Title = movieEntity.Title,
                    ImDbId = movieEntity.ImdbId,
                    Stars = movieEntity.Stars,
                    ReleaseDate = movieEntity.ReleaseDate.ToString("yyyy")
                }
            };

            _logger.LogInformation("Created showtime {ShowtimeId} for movie {MovieTitle}", 
                createdShowtime.Id, movieEntity.Title);

            return CreatedAtAction(nameof(GetById), new { id = createdShowtime.Id }, response);
        }
    }
}

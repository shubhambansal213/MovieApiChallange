using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ApiApplication.Models;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ProtoDefinitions;

namespace ApiApplication.Services
{
    public class MovieService : IMovieService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MovieService> _logger;
        private readonly IConfiguration _configuration;

        public MovieService(
            HttpClient httpClient,
            ICacheService cacheService,
            ILogger<MovieService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _logger = logger;
            _configuration = configuration;

            var baseUrl = _configuration["ProvidedApi:BaseUrl"] ?? "http://localhost:7172";
            _httpClient.BaseAddress = new Uri(baseUrl);
            
            // Add API Key header for authentication
            var apiKey = _configuration["ProvidedApi:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Apikey", apiKey);
            }
        }

        public async Task<MovieDto?> GetMovieByIdAsync(string movieId)
        {
            var cacheKey = $"movie:{movieId}";

            try
            {
                // Try GRPC first (faster)
                var movie = await GetMovieByIdViaGrpcAsync(movieId);
                if (movie != null)
                {
                    await _cacheService.SetAsync(cacheKey, movie, TimeSpan.FromHours(1));
                    return movie;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GRPC call failed for movie {MovieId}, trying HTTP", movieId);
            }

            try
            {
                // Fallback to HTTP
                var movie = await GetMovieByIdViaHttpAsync(movieId);
                if (movie != null)
                {
                    await _cacheService.SetAsync(cacheKey, movie, TimeSpan.FromHours(1));
                    return movie;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP call failed for movie {MovieId}, checking cache", movieId);
            }

            // Final fallback to cache
            var cachedMovie = await _cacheService.GetAsync<MovieDto>(cacheKey);
            if (cachedMovie != null)
            {
                _logger.LogInformation("Returning cached movie {MovieId}", movieId);
                return cachedMovie;
            }

            _logger.LogError("Movie {MovieId} not found in API or cache", movieId);
            return null;
        }

        public async Task<List<MovieDto>> GetAllMoviesAsync()
        {
            var cacheKey = "movies:all";

            try
            {
                // Try GRPC first
                var movies = await GetAllMoviesViaGrpcAsync();
                if (movies != null && movies.Any())
                {
                    await _cacheService.SetAsync(cacheKey, movies, TimeSpan.FromMinutes(30));
                    return movies;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GRPC call failed for all movies, trying HTTP");
            }

            try
            {
                // Fallback to HTTP
                var movies = await GetAllMoviesViaHttpAsync();
                if (movies != null && movies.Any())
                {
                    await _cacheService.SetAsync(cacheKey, movies, TimeSpan.FromMinutes(30));
                    return movies;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP call failed for all movies, checking cache");
            }

            // Final fallback to cache
            var cachedMovies = await _cacheService.GetAsync<List<MovieDto>>(cacheKey);
            if (cachedMovies != null)
            {
                _logger.LogInformation("Returning cached movies list");
                return cachedMovies;
            }

            _logger.LogError("Movies not found in API or cache");
            return new List<MovieDto>();
        }

        private async Task<MovieDto?> GetMovieByIdViaGrpcAsync(string movieId)
        {
            var grpcUrl = _configuration["ProvidedApi:GrpcUrl"] ?? "https://localhost:7443";
            var apiKey = _configuration["ProvidedApi:ApiKey"];
            
            var httpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            var client = new MoviesApi.MoviesApiClient(channel);
            
            // Add API Key to GRPC metadata
            var headers = new Grpc.Core.Metadata();
            if (!string.IsNullOrEmpty(apiKey))
            {
                headers.Add("X-Apikey", apiKey);
            }

            var response = await client.GetByIdAsync(new IdRequest { Id = movieId }, headers);

            if (response.Success && response.Data != null)
            {
                if (response.Data.TryUnpack<showResponse>(out var show))
                {
                    return MapToMovieDto(show);
                }
            }

            return null;
        }

        private async Task<List<MovieDto>> GetAllMoviesViaGrpcAsync()
        {
            var grpcUrl = _configuration["ProvidedApi:GrpcUrl"] ?? "https://localhost:7443";
            var apiKey = _configuration["ProvidedApi:ApiKey"];
            
            var httpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            var client = new MoviesApi.MoviesApiClient(channel);
            
            // Add API Key to GRPC metadata
            var headers = new Grpc.Core.Metadata();
            if (!string.IsNullOrEmpty(apiKey))
            {
                headers.Add("X-Apikey", apiKey);
            }

            var response = await client.GetAllAsync(new Empty(), headers);

            if (response.Success && response.Data != null)
            {
                if (response.Data.TryUnpack<showListResponse>(out var showList))
                {
                    return showList.Shows.Select(MapToMovieDto).ToList();
                }
            }

            return new List<MovieDto>();
        }

        private async Task<MovieDto?> GetMovieByIdViaHttpAsync(string movieId)
        {
            var response = await _httpClient.GetAsync($"/v1/movies/{movieId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var movie = JsonConvert.DeserializeObject<MovieDto>(content);

            return movie;
        }

        private async Task<List<MovieDto>> GetAllMoviesViaHttpAsync()
        {
            var response = await _httpClient.GetAsync("/v1/movies");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var movies = JsonConvert.DeserializeObject<List<MovieDto>>(content);

            return movies ?? new List<MovieDto>();
        }

        private MovieDto MapToMovieDto(showResponse show)
        {
            return new MovieDto
            {
                Id = show.Id,
                Title = show.Title,
                FullTitle = show.FullTitle,
                Year = show.Year,
                Crew = show.Crew,
                Image = show.Image,
                ImDbRating = show.ImDbRating,
                ImDbRatingCount = show.ImDbRatingCount,
                // Legacy properties
                ImDbId = show.Id,
                Stars = show.Crew,
                ReleaseDate = show.Year
            };
        }
    }
}

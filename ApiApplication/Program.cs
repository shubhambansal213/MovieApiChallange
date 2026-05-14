using System;
using ApiApplication.Database;
using ApiApplication.Database.Repositories;
using ApiApplication.Database.Repositories.Abstractions;
using ApiApplication.Middleware;
using ApiApplication.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApiApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Cinema API",
                    Version = "v1",
                    Description = "API for managing cinema showtimes, reservations, and ticket purchases"
                });
            });

            // Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Database
            builder.Services.AddDbContext<CinemaContext>(options =>
            {
                options.UseInMemoryDatabase("CinemaDb")
                    .EnableSensitiveDataLogging()
                    .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            // Repositories
            builder.Services.AddScoped<IShowtimesRepository, ShowtimesRepository>();
            builder.Services.AddScoped<ITicketsRepository, TicketsRepository>();
            builder.Services.AddScoped<IAuditoriumsRepository, AuditoriumsRepository>();

            // Services
            builder.Services.AddScoped<IMovieService, MovieService>();
            builder.Services.AddScoped<IReservationService, ReservationService>();
            builder.Services.AddHttpClient<IMovieService, MovieService>();

            // Redis with error handling
            var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
            try
            {
                builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
                builder.Services.AddScoped<ICacheService, RedisCacheService>();
            }
            catch (Exception ex)
            {
                // Log but don't fail - app can work without cache
                Console.WriteLine($"Warning: Redis connection failed: {ex.Message}. Cache will be disabled.");
                builder.Services.AddScoped<ICacheService, RedisCacheService>(); // Will handle Redis errors gracefully
            }

            // Logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cinema API v1");
                    c.RoutePrefix = string.Empty; // Swagger at root
                });
            }

            // Global exception handler (must be first)
            app.UseMiddleware<GlobalExceptionMiddleware>();

            // Performance tracking middleware
            app.UseMiddleware<PerformanceMiddleware>();

            app.UseCors("AllowAll");
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            // Initialize sample data with error handling
            try
            {
                SampleData.Initialize(app);
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Failed to initialize sample data");
                // Continue anyway - app can work without sample data
            }

            app.Run();
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace ApiApplication.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                if (value.IsNullOrEmpty)
                    return null;

                return JsonConvert.DeserializeObject<T>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving from cache with key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var serialized = JsonConvert.SerializeObject(value);
                await _database.StringSetAsync(key, serialized, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache with key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache with key: {Key}", key);
            }
        }
    }
}

namespace Kumbara.Risk.Infrastructure.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Kumbara.Risk.Application.Interfaces;
using Kumbara.Risk.Domain.Interfaces;

public class RedisVelocityService : Kumbara.Risk.Application.Interfaces.IVelocityService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisVelocityService> _logger;
    private readonly object _lock = new();

    private IConnectionMultiplexer? _redis;
    private bool _connectionAttempted;

    // In-memory fallbacks (thread-safe sliding window tracking)
    private readonly ConcurrentDictionary<Guid, DailyVelocityTracker> _inMemoryDaily = new();
    private readonly ConcurrentDictionary<Guid, FrequencyTracker> _inMemoryFreq = new();

    public RedisVelocityService(IConfiguration configuration, ILogger<RedisVelocityService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private IConnectionMultiplexer? GetRedisConnection()
    {
        if (_connectionAttempted)
        {
            return _redis;
        }

        lock (_lock)
        {
            if (_connectionAttempted)
            {
                return _redis;
            }

            _connectionAttempted = true;

            try
            {
                var redisConfig = _configuration["Redis:Configuration"]
                               ?? _configuration["Redis__Configuration"]
                               ?? _configuration.GetConnectionString("Redis")
                               ?? "localhost:6379";

                var options = ConfigurationOptions.Parse(redisConfig);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 2000;
                options.AsyncTimeout = 2000;

                _redis = ConnectionMultiplexer.Connect(options);

                if (_redis.IsConnected)
                {
                    _logger.LogInformation("Connected to Redis successfully at {Endpoint}.", redisConfig);
                }
                else
                {
                    _logger.LogWarning("Redis at {Endpoint} is not reachable. Operating with in-memory velocity fallback.", redisConfig);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not initialize Redis connection. Falling back to in-memory velocity tracking.");
                _redis = null;
            }

            return _redis;
        }
    }

    public async Task<bool> IncrementAndCheckDailyLimitAsync(
        Guid accountId,
        decimal amount,
        decimal dailyLimit = 100000m,
        CancellationToken cancellationToken = default)
    {
        var redis = GetRedisConnection();
        if (redis != null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                var key = $"kumbara:velocity:daily:{accountId}";

                // Increment total transferred amount for 24h
                var newTotal = (decimal)await db.StringIncrementAsync(key, (double)amount);

                // Ensure TTL of 24 hours is set
                var ttl = await db.KeyTimeToLiveAsync(key);
                if (ttl == null || ttl < TimeSpan.Zero)
                {
                    await db.KeyExpireAsync(key, TimeSpan.FromHours(24));
                }

                if (newTotal > dailyLimit)
                {
                    _logger.LogWarning(
                        "Redis: Daily velocity exceeded for account {AccountId}. NewTotal={Total}, Limit={Limit}",
                        accountId, newTotal, dailyLimit);

                    // Roll back increment so rejected transaction doesn't count against limit
                    await db.StringDecrementAsync(key, (double)amount);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis daily velocity check failed; falling back to in-memory check for account {AccountId}.", accountId);
            }
        }

        // In-memory fallback
        return CheckDailyLimitInMemory(accountId, amount, dailyLimit);
    }

    public async Task<bool> IncrementAndCheckFrequencyAsync(
        Guid accountId,
        int maxAllowedCount = 5,
        TimeSpan? window = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveWindow = window ?? TimeSpan.FromMinutes(5);
        var redis = GetRedisConnection();

        if (redis != null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                var key = $"kumbara:velocity:freq:{accountId}";
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var windowStartMs = nowMs - (long)effectiveWindow.TotalMilliseconds;

                // Remove timestamps outside sliding window
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, windowStartMs);

                var currentCount = await db.SortedSetLengthAsync(key);
                if (currentCount >= maxAllowedCount)
                {
                    _logger.LogWarning(
                        "Redis: Frequency limit exceeded for account {AccountId}. Count={Count}, Max={Max} within {Window} min",
                        accountId, currentCount, maxAllowedCount, effectiveWindow.TotalMinutes);
                    return false;
                }

                // Add current transaction timestamp
                await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), nowMs);
                await db.KeyExpireAsync(key, effectiveWindow);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis frequency check failed; falling back to in-memory check for account {AccountId}.", accountId);
            }
        }

        // In-memory fallback
        return CheckFrequencyInMemory(accountId, maxAllowedCount, effectiveWindow);
    }

    private bool CheckDailyLimitInMemory(Guid accountId, decimal amount, decimal dailyLimit)
    {
        var tracker = _inMemoryDaily.GetOrAdd(accountId, _ => new DailyVelocityTracker());
        lock (tracker.SyncRoot)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            tracker.Transactions.RemoveAll(t => t.Timestamp < cutoff);

            var currentTotal = tracker.Transactions.Sum(t => t.Amount);
            if (currentTotal + amount > dailyLimit)
            {
                _logger.LogWarning(
                    "InMemory: Daily velocity exceeded for account {AccountId}. CurrentTotal={Total}, Amount={Amount}, Limit={Limit}",
                    accountId, currentTotal, amount, dailyLimit);
                return false;
            }

            tracker.Transactions.Add((DateTimeOffset.UtcNow, amount));
            return true;
        }
    }

    private bool CheckFrequencyInMemory(Guid accountId, int maxAllowedCount, TimeSpan window)
    {
        var tracker = _inMemoryFreq.GetOrAdd(accountId, _ => new FrequencyTracker());
        lock (tracker.SyncRoot)
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(window);
            tracker.Timestamps.RemoveAll(t => t < cutoff);

            if (tracker.Timestamps.Count >= maxAllowedCount)
            {
                _logger.LogWarning(
                    "InMemory: Frequency limit exceeded for account {AccountId}. Count={Count}, Max={Max}",
                    accountId, tracker.Timestamps.Count, maxAllowedCount);
                return false;
            }

            tracker.Timestamps.Add(DateTimeOffset.UtcNow);
            return true;
        }
    }

    private sealed class DailyVelocityTracker
    {
        public readonly object SyncRoot = new();
        public readonly List<(DateTimeOffset Timestamp, decimal Amount)> Transactions = new();
    }

    private sealed class FrequencyTracker
    {
        public readonly object SyncRoot = new();
        public readonly List<DateTimeOffset> Timestamps = new();
    }
}

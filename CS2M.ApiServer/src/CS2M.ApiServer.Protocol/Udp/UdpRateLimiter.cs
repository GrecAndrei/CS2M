// SPDX-License-Identifier: MIT
// Per-source-IP token-bucket rate limiter for the UDP listener.
// Sits between the listener and the dispatcher as an IUdpDatagramSink
// decorator.

using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CS2M.ApiServer.Protocol.Udp;

public sealed class UdpRateLimiterOptions
{
    public int MaxMessagesPerSecondPerIp { get; set; } = 10;
    public int MaxBurst { get; set; } = 10;
    public int CleanupIntervalSeconds { get; set; } = 60;
}

public sealed class UdpRateLimiter : IUdpDatagramSink, IDisposable
{
    private readonly IUdpDatagramSink _inner;
    private readonly ILogger<UdpRateLimiter> _logger;
    private readonly UdpRateLimiterOptions _options;
    private readonly Timer _cleanupTimer;

    // Per-IP token buckets. ConcurrentDictionary is safe for the
    // concurrent receive loop; each bucket is accessed under a lock.
    private readonly ConcurrentDictionary<IPAddress, TokenBucket> _buckets = new();

    public UdpRateLimiter(
        IUdpDatagramSink inner,
        IOptions<UdpRateLimiterOptions> options,
        ILogger<UdpRateLimiter> logger)
    {
        _inner = inner;
        _options = options.Value;
        _logger = logger;
        _cleanupTimer = new Timer(
            _ => CleanupStaleBuckets(),
            null,
            TimeSpan.FromSeconds(_options.CleanupIntervalSeconds),
            TimeSpan.FromSeconds(_options.CleanupIntervalSeconds));
    }

    public void Handle(UdpDatagram datagram)
    {
        var ip = datagram.RemoteEndPoint.Address;
        var bucket = _buckets.GetOrAdd(ip, _ => new TokenBucket(_options.MaxBurst));

        if (!bucket.TryConsume(_options.MaxMessagesPerSecondPerIp))
        {
            _logger.LogWarning(
                "Rate limit exceeded for {Ip}; dropping datagram",
                ip);
            return;
        }

        _inner.Handle(datagram);
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private void CleanupStaleBuckets()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _buckets)
        {
            if (now - kvp.Value.LastAccess > TimeSpan.FromMinutes(5))
            {
                _buckets.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    ///     Simple sliding-window token bucket using a tick-based counter.
    /// </summary>
    private sealed class TokenBucket
    {
        private readonly object _lock = new();
        private readonly int _maxTokens;
        private int _tokens;
        private long _lastRefillTicks;

        public DateTimeOffset LastAccess { get; private set; }

        public TokenBucket(int maxTokens)
        {
            _maxTokens = maxTokens;
            _tokens = maxTokens;
            _lastRefillTicks = Environment.TickCount64;
            LastAccess = DateTimeOffset.UtcNow;
        }

        public bool TryConsume(int tokensPerSecond)
        {
            lock (_lock)
            {
                LastAccess = DateTimeOffset.UtcNow;
                Refill(tokensPerSecond);
                if (_tokens <= 0) return false;
                _tokens--;
                return true;
            }
        }

        private void Refill(int tokensPerSecond)
        {
            var now = Environment.TickCount64;
            var elapsed = now - _lastRefillTicks;
            if (elapsed < 100) return; // <100ms, no refill
            _lastRefillTicks = now;
            // Refill at tokensPerSecond / 1000 per ms.
            var add = (int)(elapsed * tokensPerSecond / 1000);
            if (add > 0)
            {
                _tokens = Math.Min(_maxTokens, _tokens + add);
            }
        }
    }
}
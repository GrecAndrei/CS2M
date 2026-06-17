using System;
using System.Collections.Concurrent;
using System.Threading;
using LiteNetLib;

namespace CS2M.Helpers
{
    /// <summary>
    ///     Collects and analyzes network statistics for monitoring and debugging
    /// </summary>
    public static class NetworkStatistics
    {
        private static readonly ConcurrentDictionary<int, PeerStats> _peerStats = new();
        private static long _totalBytesSent = 0;
        private static long _totalBytesReceived = 0;
        private static int _totalPacketsSent = 0;
        private static int _totalPacketsReceived = 0;
        private static long _averageLatencyBits = 0;

        private const int MAX_HISTORY_SIZE = 300;
        private static readonly ConcurrentQueue<double> _latencyHistory = new();
        private static readonly ConcurrentQueue<long> _bandwidthHistory = new();

        /// <summary>
        ///     Record bytes sent for a specific peer
        /// </summary>
        public static void RecordBytesSent(int peerId, long bytes)
        {
            Interlocked.Add(ref _totalBytesSent, bytes);

            _peerStats.AddOrUpdate(peerId, id => new PeerStats
            {
                BytesSent = bytes,
                TotalTransactions = 1
            }, (id, existing) =>
            {
                Interlocked.Add(ref existing.BytesSent, bytes);
                Interlocked.Increment(ref existing.TotalTransactions);
                return existing;
            });

            _bandwidthHistory.Enqueue(bytes);
            while (_bandwidthHistory.Count > MAX_HISTORY_SIZE)
            {
                _bandwidthHistory.TryDequeue(out _);
            }
        }

        /// <summary>
        ///     Record bytes received
        /// </summary>
        public static void RecordBytesReceived(long bytes)
        {
            Interlocked.Add(ref _totalBytesReceived, bytes);

            _bandwidthHistory.Enqueue(bytes);
            while (_bandwidthHistory.Count > MAX_HISTORY_SIZE)
            {
                _bandwidthHistory.TryDequeue(out _);
            }
        }

        public static void RecordPacketSent()
        {
            Interlocked.Increment(ref _totalPacketsSent);
        }

        /// <summary>
        ///     Record packet received
        /// </summary>
        public static void RecordPacketReceived()
        {
            Interlocked.Increment(ref _totalPacketsReceived);
        }

        /// <summary>
        ///     Update latency measurement for a peer
        /// </summary>
        public static void UpdateLatency(int peerId, int latencyMs)
        {
            _peerStats.AddOrUpdate(peerId, id => new PeerStats
            {
                AverageLatency = latencyMs,
                LastActivity = DateTime.UtcNow
            }, (id, existing) =>
            {
                existing.AverageLatency = latencyMs;
                existing.LastActivity = DateTime.UtcNow;
                return existing;
            });

            long oldAvg = Volatile.Read(ref _averageLatencyBits);
            long newAvg = (((long)oldAvg) + (long)latencyMs) / 2L;
            Volatile.Write(ref _averageLatencyBits, newAvg);

            _latencyHistory.Enqueue(latencyMs);
            while (_latencyHistory.Count > MAX_HISTORY_SIZE)
            {
                _latencyHistory.TryDequeue(out _);
            }
        }

        /// <summary>
        ///     Get total statistics across all peers
        /// </summary>
        public static NetworkStatsSummary GetSummary()
        {
            return new NetworkStatsSummary
            {
                TotalBytesSent = Interlocked.Read(ref _totalBytesSent),
                TotalBytesReceived = Interlocked.Read(ref _totalBytesReceived),
                TotalPacketsSent = Volatile.Read(ref _totalPacketsSent),
                TotalPacketsReceived = Volatile.Read(ref _totalPacketsReceived),
                AverageLatency = Volatile.Read(ref _averageLatencyBits),
                ActivePeers = _peerStats.Count
            };
        }

        /// <summary>
        ///     Get bandwidth in bytes per second over the last minute
        /// </summary>
        public static double GetBandwidthPerSecond()
        {
            if (_bandwidthHistory.IsEmpty)
                return 0;

            long total = 0;
            long count = 0;
            foreach (var val in _bandwidthHistory)
            {
                total += val;
                count++;
            }
            return count == 0 ? 0 : (double)total / count;
        }

        /// <summary>
        ///     Clear all statistics
        /// </summary>
        public static void Reset()
        {
            _peerStats.Clear();
            Interlocked.Exchange(ref _totalBytesSent, 0);
            Interlocked.Exchange(ref _totalBytesReceived, 0);
            Interlocked.Exchange(ref _totalPacketsSent, 0);
            Interlocked.Exchange(ref _totalPacketsReceived, 0);
            Volatile.Write(ref _averageLatencyBits, 0);

            while (_latencyHistory.TryDequeue(out _)) { }
            while (_bandwidthHistory.TryDequeue(out _)) { }

            Log.Debug("Network statistics reset");
        }

        /// <summary>
        ///     Get detailed stats for a specific peer
        /// </summary>
        public static PeerStats GetPeerStats(int peerId)
        {
            return _peerStats.TryGetValue(peerId, out var stats) ? stats : new PeerStats();
        }

        /// <summary>
        ///     Logs current statistics (useful for diagnostics)
        /// </summary>
        public static void LogCurrentStats()
        {
            var summary = GetSummary();

            Log.Info($"Network Stats: Peers={summary.ActivePeers}, " +
                    $"Tx={summary.TotalBytesSent}B/{summary.TotalPacketsSent}pkts, " +
                    $"Rx={summary.TotalBytesReceived}B/{summary.TotalPacketsReceived}pkts, " +
                    $"AvgLat={summary.AverageLatency:F1}ms");
        }
    }

    /// <summary>
    ///     Statistical data for a single peer
    /// </summary>
    public sealed class PeerStats
    {
        public long BytesSent;
        public long BytesReceived;
        public int PacketsSent;
        public int PacketsReceived;
        public int TotalTransactions;
        public double AverageLatency;
        public DateTime LastActivity;
    }

    /// <summary>
    ///     Summary of overall network statistics
    /// </summary>
    public struct NetworkStatsSummary
    {
        public long TotalBytesSent;
        public long TotalBytesReceived;
        public int TotalPacketsSent;
        public int TotalPacketsReceived;
        public double AverageLatency;
        public int ActivePeers;
    }
}

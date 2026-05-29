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
        private static double _averageLatency = 0.0;
        
        private const int MAX_HISTORY_SIZE = 300; // 5 minutes at 1-second intervals
        private static readonly System.Collections.Generic.List<double> _latencyHistory = new(MAX_HISTORY_SIZE);
        private static readonly System.Collections.Generic.List<long> _bandwidthHistory = new(MAX_HISTORY_SIZE);

        /// <summary>
        ///     Record bytes sent for a specific peer
        /// </summary>
        public static void RecordBytesSent(int peerId, long bytes)
        {
            Interlocked.Add(ref _totalBytesSent, bytes);
            
            var stats = _peerStats.GetOrAdd(peerId, _ => new PeerStats());
            lock (stats)
            {
                stats.BytesSent += bytes;
                stats.TotalTransactions++;
                
                if (_bandwidthHistory.Count >= MAX_HISTORY_SIZE)
                    _bandwidthHistory.RemoveAt(0);
                
                _bandwidthHistory.Add(bytes);
            }
        }

        /// <summary>
        ///     Record bytes received
        /// </summary>
        public static void RecordBytesReceived(long bytes)
        {
            Interlocked.Add(ref _totalBytesReceived, bytes);
            
            if (_bandwidthHistory.Count >= MAX_HISTORY_SIZE)
                _bandwidthHistory.RemoveAt(0);
            
            _bandwidthHistory.Add(bytes);
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
            var stats = _peerStats.GetOrAdd(peerId, _ => new PeerStats());
            lock (stats)
            {
                // Simple moving average
                int oldAvg = (int)_averageLatency;
                _averageLatency = (oldAvg + latencyMs) / 2.0;
                
                stats.AverageLatency = latencyMs;
                stats.LastActivity = DateTime.UtcNow;
                
                if (_latencyHistory.Count >= MAX_HISTORY_SIZE)
                    _latencyHistory.RemoveAt(0);
                
                _latencyHistory.Add(latencyMs);
            }
        }

        /// <summary>
        ///     Get total statistics across all peers
        /// </summary>
        public static NetworkStatsSummary GetSummary()
        {
            return new NetworkStatsSummary
            {
                TotalBytesSent = _totalBytesSent,
                TotalBytesReceived = _totalBytesReceived,
                TotalPacketsSent = _totalPacketsSent,
                TotalPacketsReceived = _totalPacketsReceived,
                AverageLatency = _averageLatency,
                ActivePeers = _peerStats.Count
            };
        }

        /// <summary>
        ///     Get bandwidth in bytes per second over the last minute
        /// </summary>
        public static double GetBandwidthPerSecond()
        {
            if (_bandwidthHistory.Count == 0)
                return 0;

            long total = 0;
            foreach (var val in _bandwidthHistory)
                total += val;

            return total / _bandwidthHistory.Count;
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
            _averageLatency = 0;
            
            lock (_latencyHistory)
                _latencyHistory.Clear();
            
            lock (_bandwidthHistory)
                _bandwidthHistory.Clear();
            
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
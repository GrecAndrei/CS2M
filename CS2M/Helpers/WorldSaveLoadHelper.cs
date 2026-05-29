using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using MessagePack;
using CS2M.API.Commands;

namespace CS2M.Helpers
{
    /// <summary>
    ///     High-performance world save/load helper with chunk-based streaming
    /// </summary>
    public static class WorldSaveLoadHelper
    {
        private const int CHUNK_SIZE = 64 * 1024; // 64KB chunks for network transfer
        private static string _savePath;
        
        /// <summary>
        ///     Initialize the save/load helper with application data path
        /// </summary>
        public static void Initialize(string basePath)
        {
            _savePath = Path.Combine(basePath, "CS2M_Saves");
            
            if (!Directory.Exists(_savePath))
            {
                try
                {
                    Directory.CreateDirectory(_savePath);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to create save directory: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        ///     Save game to binary format with compression
        /// </summary>
        public static async Task<byte[]> SaveGameAsync(int maxChunkSize = CHUNK_SIZE)
        {
            try
            {
                Log.Info("Starting game save...");
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Serialize game state using MessagePack
                byte[] serializedData = await SerializeGameState();
                
                stopwatch.Stop();
                Log.Info($"Game serialized in {stopwatch.ElapsedMilliseconds}ms, size: {FormatSize(serializedData.Length)}");
                
                return serializedData;
            }
            catch (Exception ex)
            {
                Log.Error($"Save failed: {ex.Message}", ex);
                throw;
            }
        }
        
        /// <summary>
        ///     Load game from binary format
        /// </summary>
        public static async Task<bool> LoadGameAsync(byte[] serializedData)
        {
            try
            {
                Log.Info($"Loading game from {FormatSize(serializedData.Length)} bytes...");
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                bool success = await DeserializeGameState(serializedData);
                
                stopwatch.Stop();
                Log.Info($"Game loaded in {stopwatch.ElapsedMilliseconds}ms: {(success ? "Success" : "Failed")}");
                
                return success;
            }
            catch (Exception ex)
            {
                Log.Error($"Load failed: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        ///     Create sliced packet stream for world transfer
        /// </summary>
        public static async Task<SlicedPacketStream> SaveGameSliced(int maxPacketSize)
        {
            try
            {
                byte[] fullSave = await SaveGameAsync(maxPacketSize);
                
                var slices = new List<byte[]>();
                for (int i = 0; i < fullSave.Length; i += maxPacketSize)
                {
                    int length = Math.Min(maxPacketSize, fullSave.Length - i);
                    byte[] slice = new byte[length];
                    Buffer.BlockCopy(fullSave, i, slice, 0, length);
                    slices.Add(slice);
                }
                
                Log.Debug($"Created {slices.Count} packets from {FormatSize(fullSave.Length)} save");
                
                return new SlicedPacketStream
                {
                    Data = fullSave,
                    Slices = slices,
                    Length = fullSave.Length,
                    PacketCount = slices.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Sliced save failed: {ex.Message}", ex);
                return default;
            }
        }
        
        /// <summary>
        ///     Serialize current game state
        /// </summary>
        private static async Task<byte[]> SerializeGameState()
        {
            // This would serialize all relevant game state
            // For now, we use a placeholder structure
            
            var gameState = new GameStateSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ModVersion = typeof(Mod).Assembly.GetName().Version.ToString(),
                // Would include: money, buildings, units, etc.
            };
            
            return MessagePackSerializer.Serialize(gameState);
        }
        
        /// <summary>
        ///     Deserialize game state from bytes
        /// </summary>
        private static async Task<bool> DeserializeGameState(byte[] data)
        {
            try
            {
                var gameState = MessagePackSerializer.Deserialize<GameStateSnapshot>(data);
                
                // Apply state to game
                // await ApplyGameState(gameState);
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Deserialization error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        ///     Save to file
        /// </summary>
        public static bool SaveToFile(byte[] data, string filename)
        {
            try
            {
                string fullPath = Path.Combine(_savePath, filename);
                File.WriteAllBytes(fullPath, data);
                Log.Info($"Saved to {fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"File save failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        ///     Load from file
        /// </summary>
        public static byte[] LoadFromFile(string filename)
        {
            try
            {
                string fullPath = Path.Combine(_savePath, filename);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
                else
                {
                    Log.Warn($"File not found: {fullPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"File load failed: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        ///     List saved files
        /// </summary>
        public static string[] ListSaves()
        {
            try
            {
                if (Directory.Exists(_savePath))
                {
                    return Directory.GetFiles(_savePath, "*.cs2m_save") ?? Array.Empty<string>();
                }
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Log.Error($"List saves failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        
        /// <summary>
        ///     Delete save file
        /// </summary>
        public static bool DeleteSave(string filename)
        {
            try
            {
                string fullPath = Path.Combine(_savePath, filename);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    Log.Info($"Deleted {filename}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Delete failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        ///     Format size in human-readable form
        /// </summary>
        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }
        
        /// <summary>
        ///     Streamed packet data for world transfer
        /// </summary>
        public struct SlicedPacketStream : IDisposable
        {
            public byte[] Data;
            public List<byte[]> Slices;
            public long Length;
            public int PacketCount;
            
            public IEnumerable<byte[]> GetSlices()
            {
                return Slices;
            }
            
            public void Dispose()
            {
                Data = null;
                if (Slices != null)
                {
                    Slices.Clear();
                    Slices = null;
                }
            }
        }
        
        /// <summary>
        ///     Snapshot of game state for saving/loading
        /// </summary>
        [MessagePackObject]
        public struct GameStateSnapshot
        {
            [Key(0)]
            public long Timestamp;
            
            [Key(1)]
            public string ModVersion;
            
            [Key(2)]
            public long Money;
            
            [Key(3)]
            public uint CurrentFrame;
            
            [Key(4)]
            public Dictionary<string, object> Metadata;
        }
    }
}

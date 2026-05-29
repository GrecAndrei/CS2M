using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.SceneFlow;
using Game.Serialization;
using Game.Settings;
using HarmonyLib;
using Unity.Jobs;
using UnityEngine;
using Hash128 = Colossal.Hash128;
using Version = Game.Version;

namespace CS2M.Helpers
{
    /// <summary>
    ///     This class implements a byte stream of sliced data packets.
    /// </summary>
    public class SlicedPacketStream : Stream
    {
        private readonly int _sliceLength;
        private readonly byte[] _sliceBuffer;

        private readonly List<byte[]> _slices = new();
        private int _sliceReadIndex;
        private int _sliceReadOffset;
        private int _sliceWriteOffset;

        private int _streamLength;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _streamLength;

        public SlicedPacketStream(int sliceLength)
        {
            if (sliceLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sliceLength), "Slice length must be greater than zero.");
            }

            _sliceLength = sliceLength;
            _sliceBuffer = new byte[_sliceLength];
        }

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            if (_sliceWriteOffset > 0)
            {
                byte[] slice = new byte[_sliceWriteOffset];
                Array.Copy(_sliceBuffer, 0, slice, 0, _sliceWriteOffset);
                _slices.Add(slice);
                _sliceWriteOffset = 0;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            Flush();

            int readBytes = 0;
            while (readBytes < count && _sliceReadIndex < _slices.Count)
            {
                byte[] slice = _slices[_sliceReadIndex];
                int remain = slice.Length - _sliceReadOffset;
                if (remain <= 0)
                {
                    _sliceReadIndex++;
                    _sliceReadOffset = 0;
                    continue;
                }

                int toRead = Math.Min(remain, count - readBytes);
                Array.Copy(slice, _sliceReadOffset, buffer, offset + readBytes, toRead);
                _sliceReadOffset += toRead;
                readBytes += toRead;

                if (_sliceReadOffset >= slice.Length)
                {
                    _sliceReadIndex++;
                    _sliceReadOffset = 0;
                }
            }

            return readBytes;
        }

        public unsafe int Read(byte* pTarget, int bytes)
        {
            // Flush any remaining write bytes
            Flush();

            int readBytes = 0;
            while (readBytes < bytes && _sliceReadIndex < _slices.Count)
            {
                byte[] slice = _slices[_sliceReadIndex];
                int remain = slice.Length - _sliceReadOffset;
                if (remain <= 0)
                {
                    _sliceReadIndex++;
                    _sliceReadOffset = 0;
                    continue;
                }

                int toRead = Math.Min(remain, bytes - readBytes);

                fixed (byte* pSource = slice)
                {
                    // Copy the specified number of bytes from source to target.
                    for (int i = 0; i < toRead; i++)
                    {
                        pTarget[readBytes + i] = pSource[_sliceReadOffset + i];
                    }
                }

                readBytes += toRead;
                _sliceReadOffset += toRead;

                if (_sliceReadOffset >= slice.Length)
                {
                    _sliceReadIndex++;
                    _sliceReadOffset = 0;
                }
            }

            return readBytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            _streamLength += count;
            while (count > 0)
            {
                int sliceRemain = _sliceBuffer.Length - _sliceWriteOffset;
                int addHere = Math.Min(sliceRemain, count);
                Array.Copy(buffer, offset, _sliceBuffer, _sliceWriteOffset, addHere);
                _sliceWriteOffset += addHere;
                offset += addHere;
                count -= addHere;

                if (_sliceWriteOffset == _sliceBuffer.Length)
                {
                    _slices.Add(_sliceBuffer.ToArray());
                    _sliceWriteOffset = 0;
                }
            }
        }

        public List<byte[]> GetSlices()
        {
            Flush();
            return _slices;
        }

        public bool AppendSlice(byte[] slice)
        {
            if (slice == null || slice.Length == 0 || slice.Length > _sliceLength)
            {
                return false;
            }

            // Always copy to avoid accidental mutation of buffers owned by the networking layer.
            var copy = new byte[slice.Length];
            Array.Copy(slice, copy, slice.Length);
            _slices.Add(copy);

            _streamLength += slice.Length;
            return true;
        }

        public void Clear()
        {
            _slices.Clear();
            _sliceReadIndex = 0;
            _sliceReadOffset = 0;
            _sliceWriteOffset = 0;
            _streamLength = 0;
        }
    }

    /// <summary>
    ///     Simple wrapper class around SaveGameData to simulate a save game.
    /// </summary>
    internal class SaveWrapper : SaveGameData
    {
        public SaveWrapper()
        {
            id = Identifier.None;
        }

        public override string ToString()
        {
            return "Multiplayer Save Game";
        }
    }

    public partial class SaveLoadHelper : GameSystemBase
    {
        private SaveGameSystem _saveGameSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _saveGameSystem = World.GetOrCreateSystemManaged<SaveGameSystem>();
            Enabled = false;
        }

        public async Task<SlicedPacketStream> SaveGame(int sliceLength)
        {
            if (sliceLength <= 0)
            {
                Log.Warn($"[SaveGame] Invalid slice length {sliceLength}");
                return null;
            }

            // See GameManager::Save
            const int waitTimeoutMs = 10000;
            var waitWatch = Stopwatch.StartNew();
            while (_saveGameSystem.Enabled)
            {
                if (waitWatch.ElapsedMilliseconds >= waitTimeoutMs)
                {
                    Log.Warn($"[SaveGame] Save system remained busy for {waitTimeoutMs}ms, aborting transfer save.");
                    return null;
                }

                await Task.Delay(10);
            }

            var watch = new Stopwatch();
            watch.Start();

            // Disable auto-save so it doesn't collide with our save process
            bool autoSaveEnabled = SharedSettings.instance.general.autoSave;
            SharedSettings.instance.general.autoSave = false;

            try
            {
                // Cleanup memory
                Resources.UnloadUnusedAssets();
                GC.Collect();

                Log.Debug($"[SaveGame] GC took {watch.ElapsedMilliseconds}ms");
                watch.Restart();

                // Save game to packet stream
                var stream = new SlicedPacketStream(sliceLength);
                _saveGameSystem.stream = stream;
                _saveGameSystem.context = new Context(Purpose.SaveGame, Version.current, Hash128.Empty);
                await _saveGameSystem.RunOnce();
                stream.Flush();

                if (stream.Length == 0)
                {
                    Log.Warn("[SaveGame] Save stream is empty, aborting world transfer.");
                    return null;
                }

                Log.Debug($"[SaveGame] Save took {watch.ElapsedMilliseconds}ms");
                return stream;
            }
            catch (Exception ex)
            {
                Log.Warn($"[SaveGame] Save failed: {ex}");
                return null;
            }
            finally
            {
                _saveGameSystem.stream = null;
                SharedSettings.instance.general.autoSave = autoSaveEnabled;
            }
        }

        public async Task<bool> LoadGame(SlicedPacketStream data)
        {
            if (data == null || data.Length <= 0)
            {
                Log.Warn("[LoadGame] Refusing to load empty multiplayer stream.");
                return false;
            }

            var saveGame = new SaveWrapper();
            try
            {
                ReadSystemPatch.Stream = data;
                AssetDataPatch.OverrideAssetData = true;
                return await GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, saveGame);
            }
            catch (Exception ex)
            {
                Log.Warn($"[LoadGame] Multiplayer stream load failed: {ex}");
                return false;
            }
            finally
            {
                AssetDataPatch.OverrideAssetData = false;
                ReadSystemPatch.Stream = null;
            }
        }

        protected override void OnUpdate()
        {
            // This helper only runs on explicit async calls.
        }
    }

    /// <summary>
    ///     This patch overrides the ReadBytes method that is called while deserializing a save game.
    ///     Instead of loading it from file, the bytes are extracted from our PacketStream.
    /// </summary>
    [HarmonyPatch]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class ReadSystemPatch
    {
        public static SlicedPacketStream Stream;

        public static unsafe bool Prefix(void* data, int bytes)
        {
            if (Stream == null)
            {
                return true;
            }

            int readBytes = Stream.Read((byte*)data, bytes);
            if (readBytes != bytes)
            {
                throw new IOException("Failed to read from multiplayer stream!");
            }

            return false;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(StreamBinaryReader).GetMethod("ReadBytes",
                new[] { typeof(void).MakePointerType(), typeof(int) });
            yield return typeof(StreamBinaryReader).GetMethod("ReadBytes",
                new[] { typeof(void).MakePointerType(), typeof(int), typeof(JobHandle).MakeByRefType() });
        }
    }

    /// <summary>
    ///     This patch makes our custom SaveWrapper usable without throwing an exception.
    /// </summary>
    [HarmonyPatch(typeof(AssetData))]
    [HarmonyPatch(nameof(AssetData.GetAsyncReadDescriptor))]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class AssetDataPatch
    {
        public static bool OverrideAssetData;

        // ReSharper disable once InconsistentNaming
        public static bool Prefix(ref AsyncReadDescriptor __result)
        {
            if (!OverrideAssetData)
            {
                return true;
            }

            __result = new AsyncReadDescriptor("Multiplayer", "multiplayer");
            return false;
        }
    }
}

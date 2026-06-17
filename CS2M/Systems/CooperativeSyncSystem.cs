using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Globalization;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CS2M.API;
using CS2M.API.Networking;
using CS2M.BaseGame.Commands;
using CS2M.Helpers;
using CS2M.Networking;
using CS2M.UI;

namespace CS2M.Systems
{
    public class RemoteCursorState
    {
        public int PlayerId { get; set; }
        public string Username { get; set; } = "";
        public float3 Position { get; set; }
        public float3 CameraFocus { get; set; }
        public string ActiveTool { get; set; } = "";
        public string ActivePrefab { get; set; } = "";
        public long LastUpdateTime { get; set; }
        public long Latency { get; set; }
    }

    public class RemotePingState
    {
        public int PlayerId { get; set; }
        public string Username { get; set; } = "";
        public float3 Position { get; set; }
        public int PingType { get; set; }
        public long StartTime { get; set; }
        public float RemainingDuration { get; set; } = 6f;
    }

    public class CooperativeActivity
    {
        public string Username { get; set; }
        public string ActionText { get; set; }
        public float3 Position { get; set; }
        public long Timestamp { get; set; }
    }

    internal static class Vector3Bridge
    {
        private static readonly Type _vector3Type = typeof(Camera).Assembly.GetType("UnityEngine.Vector3");
        private static readonly ConstructorInfo _ctor = _vector3Type?.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
        private static readonly FieldInfo _xField = _vector3Type?.GetField("x");
        private static readonly FieldInfo _yField = _vector3Type?.GetField("y");
        private static readonly FieldInfo _zField = _vector3Type?.GetField("z");
        private static readonly MethodInfo _worldToScreenPoint = _vector3Type != null ? typeof(Camera).GetMethod("WorldToScreenPoint", new[] { _vector3Type }) : null;
        private static readonly FieldInfo _rayOriginField = typeof(Ray).GetField("m_Origin");
        private static readonly FieldInfo _rayDirectionField = typeof(Ray).GetField("m_Direction");

        public static bool TryWorldToScreenPoint(Camera cam, float3 worldPos, float screenW, float screenH, out float screenX, out float screenY)
        {
            screenX = 0f;
            screenY = 0f;
            if (cam == null || _ctor == null || _worldToScreenPoint == null || screenW <= 0f || screenH <= 0f)
                return false;

            object v3 = _ctor.Invoke(new object[] { worldPos.x, worldPos.y, worldPos.z });
            object sp = _worldToScreenPoint.Invoke(cam, new[] { v3 });
            float z = (float)_zField.GetValue(sp);
            if (z > 0f)
            {
                float x = (float)_xField.GetValue(sp);
                float y = (float)_yField.GetValue(sp);
                screenX = (x / screenW) * 100f;
                screenY = (1f - (y / screenH)) * 100f;
                return true;
            }
            return false;
        }

        public static float3 RaycastToGround(Ray ray)
        {
            if (_rayOriginField == null || _rayDirectionField == null || _xField == null || _yField == null || _zField == null)
                return float3.zero;
            object originObj = _rayOriginField.GetValue(ray);
            object dirObj = _rayDirectionField.GetValue(ray);
            float ox = (float)_xField.GetValue(originObj);
            float oy = (float)_yField.GetValue(originObj);
            float oz = (float)_zField.GetValue(originObj);
            float dx = (float)_xField.GetValue(dirObj);
            float dy = (float)_yField.GetValue(dirObj);
            float dz = (float)_zField.GetValue(dirObj);
            if (Math.Abs(dy) <= 1e-6f) return float3.zero;
            float t = -oy / dy;
            if (t < 0f) return float3.zero;
            return new float3(ox + dx * t, oy + dy * t, oz + dz * t);
        }

        public static float3 GetTransformPosition(Transform t)
        {
            object pos = _rayOriginField != null ? null : null;
            PropertyInfo p = typeof(Transform).GetProperty("position");
            object v = p?.GetValue(t);
            if (v == null || _xField == null) return float3.zero;
            return new float3((float)_xField.GetValue(v), (float)_yField.GetValue(v), (float)_zField.GetValue(v));
        }
    }

    public partial class CooperativeSyncSystem : SystemBase
    {
        private static CooperativeSyncSystem _instance;

        private readonly Dictionary<int, RemoteCursorState> _remoteCursors = new();
        private readonly Queue<RemotePingState> _remotePings = new();
        private readonly Queue<CooperativeActivity> _activityLog = new();
        private readonly object _lockObj = new();

        private const int MaxRemoteCursors = 32;
        private const int MaxActivityLog = 80;
        private const int UiUpdateIntervalFrames = 6;

        private static readonly Lazy<Type> _cameraControllerSystemType = new Lazy<Type>(() =>
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Game")
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == "CameraControllerSystem")
                        {
                            return type;
                        }
                    }
                }
            }
            return null;
        });

        private long _lastCursorBroadcastTime;
        private float3 _lastBroadcastedPosition;
        private float3 _lastBroadcastedFocus;
        private string _lastBroadcastedTool = "";
        private string _lastBroadcastedPrefab = "";

        private int _uiUpdateFrameCounter;

        private Action<string, string, float, float, float> _activityHandler;

        protected override void OnCreate()
        {
            base.OnCreate();
            _instance = this;
            Log.Info("CooperativeSyncSystem: Initialized");

            _activityHandler = (username, actionText, x, y, z) =>
            {
                RegisterActivity(username, actionText, new float3(x, y, z));
            };
            CS2M.API.CooperativeActivityRegistry.OnActivityRegistered += _activityHandler;
        }

        protected override void OnDestroy()
        {
            if (_activityHandler != null)
            {
                CS2M.API.CooperativeActivityRegistry.OnActivityRegistered -= _activityHandler;
                _activityHandler = null;
            }
            if (_instance == this)
            {
                _instance = null;
            }
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (NetworkInterface.Instance.LocalPlayer.PlayerStatus != PlayerStatus.PLAYING)
            {
                lock (_lockObj)
                {
                    _remoteCursors.Clear();
                    _remotePings.Clear();
                    _activityLog.Clear();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                TriggerLocalPing();
            }

            float dt = UnityEngine.Time.deltaTime;
            lock (_lockObj)
            {
                int count = _remotePings.Count;
                for (int i = 0; i < count; i++)
                {
                    var ping = _remotePings.Dequeue();
                    ping.RemainingDuration -= dt;
                    if (ping.RemainingDuration > 0f)
                    {
                        _remotePings.Enqueue(ping);
                    }
                }
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastCursorBroadcastTime >= 100)
            {
                BroadcastLocalCursor(now);
            }

            _uiUpdateFrameCounter++;
            if (_uiUpdateFrameCounter >= UiUpdateIntervalFrames)
            {
                _uiUpdateFrameCounter = 0;
                UpdateUiBindings();
            }
        }

        public static void RegisterActivity(string username, string actionText, float3 position)
        {
            var inst = _instance;
            if (inst == null) return;
            lock (inst._lockObj)
            {
                inst._activityLog.Enqueue(new CooperativeActivity
                {
                    Username = username,
                    ActionText = actionText,
                    Position = position,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                while (inst._activityLog.Count > MaxActivityLog)
                {
                    inst._activityLog.Dequeue();
                }
            }
        }

        public static string ResolveUsername(int senderId)
        {
            if (senderId == -1) return "Server";
            if (NetworkInterface.Instance == null || NetworkInterface.Instance.PlayerListJoined == null)
            {
                return $"Player {senderId}";
            }
            try
            {
                for (int i = 0; i < NetworkInterface.Instance.PlayerListJoined.Count; i++)
                {
                    var p = NetworkInterface.Instance.PlayerListJoined[i];
                    if (p != null && p.PlayerId == senderId)
                    {
                        return p.Username ?? $"Player {senderId}";
                    }
                }
            }
            catch {}
            return $"Player {senderId}";
        }

        public static void UpdateRemoteCursor(PlayerCursorCommand command)
        {
            if (command == null) return;
            if (command.TargetPlayerId == -1) return;

            var inst = _instance;
            if (inst == null) return;
            lock (inst._lockObj)
            {
                inst._remoteCursors[command.TargetPlayerId] = new RemoteCursorState
                {
                    PlayerId = command.TargetPlayerId,
                    Username = command.TargetUsername,
                    Position = new float3(command.PositionX, command.PositionY, command.PositionZ),
                    CameraFocus = new float3(command.CameraFocusX, command.CameraFocusY, command.CameraFocusZ),
                    ActiveTool = command.ActiveTool,
                    ActivePrefab = command.ActivePrefab,
                    LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Latency = 0
                };

                if (inst._remoteCursors.Count > MaxRemoteCursors)
                {
                    int oldestId = -1;
                    long oldestTime = long.MaxValue;
                    foreach (var kvp in inst._remoteCursors)
                    {
                        if (kvp.Value.LastUpdateTime < oldestTime)
                        {
                            oldestTime = kvp.Value.LastUpdateTime;
                            oldestId = kvp.Key;
                        }
                    }
                    if (oldestId != -1)
                    {
                        inst._remoteCursors.Remove(oldestId);
                    }
                }
            }
        }

        public static void TriggerRemotePing(MapPingCommand command)
        {
            if (command == null) return;

            float3 pos = new float3(command.PositionX, command.PositionY, command.PositionZ);

            var inst = _instance;
            if (inst != null)
            {
                lock (inst._lockObj)
                {
                    inst._remotePings.Enqueue(new RemotePingState
                    {
                        PlayerId = command.TargetPlayerId,
                        Username = command.TargetUsername,
                        Position = pos,
                        PingType = command.PingType,
                        StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        RemainingDuration = 6f
                    });
                }
            }

            Chat.Instance?.PrintGameMessage($"{command.TargetUsername} pinged an area at ({pos.x:F0}, {pos.z:F0})");
        }

        private void TriggerLocalPing()
        {
            float3 terrainPoint = GetLocalCursorTerrainPoint();
            if (math.length(terrainPoint) < 1f) return;

            string localUser = NetworkInterface.Instance.LocalPlayer.Username ?? "Local";
            int localId = NetworkInterface.Instance.LocalPlayer.PlayerId;

            var cmd = new MapPingCommand
            {
                TargetPlayerId = localId,
                TargetUsername = localUser,
                PositionX = terrainPoint.x,
                PositionY = terrainPoint.y,
                PositionZ = terrainPoint.z,
                PingType = 0
            };

            NetworkInterface.Instance.SendToAll(cmd);
            TriggerRemotePing(cmd);
        }

        private void BroadcastLocalCursor(long now)
        {
            float3 terrainPoint = GetLocalCursorTerrainPoint();
            if (math.length(terrainPoint) < 1f) return;

            float3 cameraFocus = GetLocalCameraFocusPoint();

            string toolName = "None";
            string prefabName = "";

            var toolSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.ToolSystem>();
            var activeTool = toolSystem?.activeTool;
            if (activeTool != null)
            {
                toolName = activeTool.GetType().Name;
                try
                {
                    object prefabObj = ReflectionHelper.GetProp(activeTool, "prefab");
                    if (prefabObj != null)
                    {
                        prefabName = ReflectionHelper.GetProp<string>(prefabObj, "name") ?? "";
                    }
                }
                catch {}
            }

            if (math.distance(terrainPoint, _lastBroadcastedPosition) < 0.5f &&
                math.distance(cameraFocus, _lastBroadcastedFocus) < 0.5f &&
                toolName == _lastBroadcastedTool &&
                prefabName == _lastBroadcastedPrefab)
            {
                return;
            }

            _lastCursorBroadcastTime = now;
            _lastBroadcastedPosition = terrainPoint;
            _lastBroadcastedFocus = cameraFocus;
            _lastBroadcastedTool = toolName;
            _lastBroadcastedPrefab = prefabName;

            var cmd = new PlayerCursorCommand
            {
                TargetPlayerId = NetworkInterface.Instance.LocalPlayer.PlayerId,
                TargetUsername = NetworkInterface.Instance.LocalPlayer.Username ?? "Local",
                PositionX = terrainPoint.x,
                PositionY = terrainPoint.y,
                PositionZ = terrainPoint.z,
                ActiveTool = toolName,
                ActivePrefab = prefabName,
                CameraFocusX = cameraFocus.x,
                CameraFocusY = cameraFocus.y,
                CameraFocusZ = cameraFocus.z
            };

            NetworkInterface.Instance.SendToAll(cmd);
        }

        private float3 GetLocalCursorTerrainPoint()
        {
            var toolSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.ToolSystem>();
            var activeTool = toolSystem?.activeTool;

            if (activeTool != null)
            {
                try
                {
                    object lastRaycastObj = ReflectionHelper.GetAttr(activeTool, "m_LastRaycastPoint");
                    if (lastRaycastObj != null)
                    {
                        if (lastRaycastObj is Game.Tools.ControlPoint cp)
                        {
                            return cp.m_Position;
                        }

                        object posObj = ReflectionHelper.GetAttr(lastRaycastObj, "m_Position");
                        if (posObj is float3 f3)
                        {
                            return f3;
                        }
                    }
                }
                catch {}
            }

            var camera = Camera.main;
            if (camera != null)
            {
                try
                {
                    Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                    return Vector3Bridge.RaycastToGround(ray);
                }
                catch (Exception ex)
                {
                    Log.Error($"Fallback raycast failed: {ex}");
                }
            }

            return float3.zero;
        }

        private float3 GetLocalCameraFocusPoint()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return float3.zero;

                Type cameraSystemType = _cameraControllerSystemType.Value;
                if (cameraSystemType == null) return float3.zero;

                object cameraSystem = world.GetExistingSystemManaged(cameraSystemType);
                if (cameraSystem == null) return float3.zero;

                System.Reflection.PropertyInfo pivotProp = cameraSystemType.GetProperty("pivot", ReflectionHelper.AllAccessFlags);
                System.Reflection.FieldInfo pivotField = cameraSystemType.GetField("m_Pivot", ReflectionHelper.AllAccessFlags);

                if (pivotProp != null)
                {
                    object pivotObj = pivotProp.GetValue(cameraSystem, null);
                    if (pivotObj is float3 f3) return f3;
                }
                else if (pivotField != null)
                {
                    object pivotObj = pivotField.GetValue(cameraSystem);
                    if (pivotObj is float3 f3) return f3;
                }
            }
            catch {}
            return float3.zero;
        }

        public static void TeleportCameraToPlayer(int playerId)
        {
            float3 targetPosition = float3.zero;
            bool found = false;

            var inst = _instance;
            if (inst != null)
            {
                lock (inst._lockObj)
                {
                    if (playerId == NetworkInterface.Instance.LocalPlayer.PlayerId)
                    {
                        return;
                    }

                    if (inst._remoteCursors.TryGetValue(playerId, out var cursor))
                    {
                        targetPosition = cursor.Position;
                        found = true;
                    }
                }
            }

            if (found)
            {
                TeleportCamera(targetPosition);
            }
            else
            {
                Log.Warn($"TeleportCameraToPlayer: No active cursor coordinates cached for player {playerId}");
            }
        }

        public static void TeleportCamera(float3 position)
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return;

                Type cameraSystemType = _cameraControllerSystemType.Value;
                if (cameraSystemType == null)
                {
                    Log.Warn("TeleportCamera: CameraControllerSystem not found in Game assembly.");
                    return;
                }

                object cameraSystem = world.GetExistingSystemManaged(cameraSystemType);
                if (cameraSystem == null)
                {
                    Log.Warn("TeleportCamera: Active CameraControllerSystem instance not found in ECS world.");
                    return;
                }

                System.Reflection.PropertyInfo pivotProp = cameraSystemType.GetProperty("pivot", ReflectionHelper.AllAccessFlags);
                System.Reflection.FieldInfo pivotField = cameraSystemType.GetField("m_Pivot", ReflectionHelper.AllAccessFlags);

                if (pivotProp != null)
                {
                    pivotProp.SetValue(cameraSystem, position, null);
                }
                else if (pivotField != null)
                {
                    pivotField.SetValue(cameraSystem, position);
                }

                System.Reflection.PropertyInfo posProp = cameraSystemType.GetProperty("position", ReflectionHelper.AllAccessFlags);
                System.Reflection.FieldInfo posField = cameraSystemType.GetField("m_Position", ReflectionHelper.AllAccessFlags);

                float3 offsetPos = position + new float3(0f, 250f, -250f);
                if (posProp != null)
                {
                    posProp.SetValue(cameraSystem, offsetPos, null);
                }
                else if (posField != null)
                {
                    posField.SetValue(cameraSystem, offsetPos);
                }

                Log.Info($"TeleportCamera: Set camera pivot to {position}");
            }
            catch (Exception ex)
            {
                Log.Error($"TeleportCamera reflection failed: {ex}");
            }
        }

        private void UpdateUiBindings()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cam = Camera.main;
            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. Cursors List
            sb.Append("\"cursors\":[");
            int cCount = 0;
            lock (_lockObj)
            {
                foreach (var cursor in _remoteCursors.Values)
                {
                    if (now - cursor.LastUpdateTime > 4000) continue;

                    if (cCount > 0) sb.Append(",");
                    sb.Append("{");
                    sb.Append($"\"playerId\":{cursor.PlayerId},");
                    sb.Append($"\"username\":\"{Escaped(cursor.Username)}\",");
                    sb.Append($"\"x\":{cursor.Position.x.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"y\":{cursor.Position.y.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"z\":{cursor.Position.z.ToString(CultureInfo.InvariantCulture)},");

                    bool visible = Vector3Bridge.TryWorldToScreenPoint(cam, cursor.Position, screenW, screenH, out float screenX, out float screenY);
                    bool focusVisible = Vector3Bridge.TryWorldToScreenPoint(cam, cursor.CameraFocus, screenW, screenH, out float focusScreenX, out float focusScreenY);

                    sb.Append($"\"screenX\":{screenX.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"screenY\":{screenY.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"visible\":{(visible ? "true" : "false")},");

                    sb.Append($"\"focusX\":{cursor.CameraFocus.x.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"focusY\":{cursor.CameraFocus.y.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"focusZ\":{cursor.CameraFocus.z.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"focusScreenX\":{focusScreenX.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"focusScreenY\":{focusScreenY.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"focusVisible\":{(focusVisible ? "true" : "false")},");

                    sb.Append($"\"tool\":\"{Escaped(cursor.ActiveTool)}\",");
                    sb.Append($"\"prefab\":\"{Escaped(cursor.ActivePrefab)}\"");
                    sb.Append("}");
                    cCount++;
                }
            }
            sb.Append("],");

            // 2. Pings List
            sb.Append("\"pings\":[");
            int pCount = 0;
            lock (_lockObj)
            {
                foreach (var ping in _remotePings)
                {
                    if (pCount > 0) sb.Append(",");
                    sb.Append("{");
                    sb.Append($"\"playerId\":{ping.PlayerId},");
                    sb.Append($"\"username\":\"{Escaped(ping.Username)}\",");
                    sb.Append($"\"x\":{ping.Position.x.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"y\":{ping.Position.y.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"z\":{ping.Position.z.ToString(CultureInfo.InvariantCulture)},");

                    bool visible = Vector3Bridge.TryWorldToScreenPoint(cam, ping.Position, screenW, screenH, out float screenX, out float screenY);
                    float distance = 0f;
                    if (cam != null)
                    {
                        float3 camPos = Vector3Bridge.GetTransformPosition(cam.transform);
                        distance = math.distance(camPos, ping.Position);
                    }

                    sb.Append($"\"screenX\":{screenX.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"screenY\":{screenY.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"visible\":{(visible ? "true" : "false")},");
                    sb.Append($"\"distance\":{distance.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"type\":{ping.PingType},");
                    sb.Append($"\"remaining\":{ping.RemainingDuration.ToString(CultureInfo.InvariantCulture)}");
                    sb.Append("}");
                    pCount++;
                }
            }
            sb.Append("],");

            // 3. Active Players Roster
            sb.Append("\"players\":[");
            int plCount = 0;
            var joinedList = NetworkInterface.Instance.PlayerListJoined;
            for (int i = 0; i < joinedList.Count; i++)
            {
                var player = joinedList[i];
                if (plCount > 0) sb.Append(",");
                sb.Append("{");
                sb.Append($"\"playerId\":{player.PlayerId},");
                sb.Append($"\"username\":\"{Escaped(player.Username)}\",");
                sb.Append($"\"type\":\"{player.PlayerType}\",");
                sb.Append($"\"latency\":{player.Latency},");

                string tool = "None";
                string prefab = "";

                if (player.PlayerId == NetworkInterface.Instance.LocalPlayer.PlayerId)
                {
                    var toolSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.ToolSystem>();
                    var activeTool = toolSystem?.activeTool;
                    if (activeTool != null)
                    {
                        tool = activeTool.GetType().Name;
                        try
                        {
                            object prefabObj = ReflectionHelper.GetProp(activeTool, "prefab");
                            if (prefabObj != null)
                            {
                                prefab = ReflectionHelper.GetProp<string>(prefabObj, "name") ?? "";
                            }
                        }
                        catch {}
                    }
                }
                else
                {
                    lock (_lockObj)
                    {
                        if (_remoteCursors.TryGetValue(player.PlayerId, out var cursor))
                        {
                            tool = cursor.ActiveTool;
                            prefab = cursor.ActivePrefab;
                        }
                    }
                }

                sb.Append($"\"tool\":\"{Escaped(tool)}\",");
                sb.Append($"\"prefab\":\"{Escaped(prefab)}\"");
                sb.Append("}");
                plCount++;
            }
            sb.Append("],");

            // 4. Activities Timeline Log Ledger
            sb.Append("\"activities\":[");
            int aCount = 0;
            lock (_lockObj)
            {
                CooperativeActivity[] acts = _activityLog.ToArray();
                for (int i = acts.Length - 1; i >= 0; i--)
                {
                    var act = acts[i];
                    if (aCount > 0) sb.Append(",");
                    sb.Append("{");
                    sb.Append($"\"username\":\"{Escaped(act.Username)}\",");
                    sb.Append($"\"action\":\"{Escaped(act.ActionText)}\",");
                    sb.Append($"\"x\":{act.Position.x.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"y\":{act.Position.y.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"z\":{act.Position.z.ToString(CultureInfo.InvariantCulture)},");
                    sb.Append($"\"time\":\"{DateTimeOffset.FromUnixTimeMilliseconds(act.Timestamp).DateTime.ToShortTimeString()}\"");
                    sb.Append("}");
                    aCount++;
                }
            }
            sb.Append("]");

            sb.Append("}");

            UISystem.CooperativeDataBinding?.Update(sb.ToString());
        }

        private static string Escaped(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}

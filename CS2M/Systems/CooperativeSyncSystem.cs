using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CS2M.API;
using CS2M.API.Networking;
using CS2M.API.Commands;
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

    public partial class CooperativeSyncSystem : SystemBase
    {
        private static readonly Dictionary<int, RemoteCursorState> _remoteCursors = new();
        private static readonly List<RemotePingState> _remotePings = new();
        private static readonly List<CooperativeActivity> _activityLog = new();
        private static readonly object LockObj = new();

        private long _lastCursorBroadcastTime;
        private float3 _lastBroadcastedPosition;
        private float3 _lastBroadcastedFocus;
        private string _lastBroadcastedTool = "";
        private string _lastBroadcastedPrefab = "";

        protected override void OnCreate()
        {
            base.OnCreate();
            Log.Info("CooperativeSyncSystem: Initialized");
            
            // Bind decouple registry to receive activities from CS2M.BaseGame or API levels
            CS2M.API.CooperativeActivityRegistry.OnActivityRegistered = (username, actionText, x, y, z) =>
            {
                RegisterActivity(username, actionText, new float3(x, y, z));
            };
        }

        protected override void OnUpdate()
        {
            // Only update when actively playing in multiplayer
            if (NetworkInterface.Instance.LocalPlayer.PlayerStatus != PlayerStatus.PLAYING)
            {
                lock (LockObj)
                {
                    _remoteCursors.Clear();
                    _remotePings.Clear();
                    _activityLog.Clear();
                }
                return;
            }

            // Handle map ping hotkey 'G'
            if (Input.GetKeyDown(KeyCode.G))
            {
                TriggerLocalPing();
            }

            // Manage remote pings expiration timers
            float dt = UnityEngine.Time.deltaTime;
            lock (LockObj)
            {
                for (int i = _remotePings.Count - 1; i >= 0; i--)
                {
                    _remotePings[i].RemainingDuration -= dt;
                    if (_remotePings[i].RemainingDuration <= 0)
                    {
                        _remotePings.RemoveAt(i);
                    }
                }
            }

            // Capture and broadcast local cursor and camera focus
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastCursorBroadcastTime >= 100) // 10 ticks per second limit
            {
                BroadcastLocalCursor(now);
            }

            // Build and send JSON binding data to React UI
            UpdateUiBindings();
        }

        public static void RegisterActivity(string username, string actionText, float3 position)
        {
            lock (LockObj)
            {
                _activityLog.Add(new CooperativeActivity
                {
                    Username = username,
                    ActionText = actionText,
                    Position = position,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                
                // Keep only recent 80 entries
                while (_activityLog.Count > 80)
                {
                    _activityLog.RemoveAt(0);
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
            
            lock (LockObj)
            {
                _remoteCursors[command.TargetPlayerId] = new RemoteCursorState
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
            }
        }

        public static void TriggerRemotePing(MapPingCommand command)
        {
            if (command == null) return;

            float3 pos = new float3(command.PositionX, command.PositionY, command.PositionZ);
            
            lock (LockObj)
            {
                _remotePings.Add(new RemotePingState
                {
                    PlayerId = command.TargetPlayerId,
                    Username = command.TargetUsername,
                    Position = pos,
                    PingType = command.PingType,
                    StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    RemainingDuration = 6f
                });
            }

            // Log chat alert
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
                PingType = 0 // General ping
            };

            // Send packet to other players
            NetworkInterface.Instance.SendToAll(cmd);

            // Execute locally too
            TriggerRemotePing(cmd);
        }

        private void BroadcastLocalCursor(long now)
        {
            float3 terrainPoint = GetLocalCursorTerrainPoint();
            if (math.length(terrainPoint) < 1f) return;

            float3 cameraFocus = GetLocalCameraFocusPoint();

            // Resolve active tool and prefab
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

            // Throttling: only send if cursor/focus moved or tool changed
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

            // Fallback plane raycast at average terrain height y=0 using dynamic reflection
            var camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                try
                {
                    Type v3Type = typeof(UnityEngine.Component).Assembly.GetType("UnityEngine.Vector3");
                    object mousePosInstance = Input.mousePosition;
                    var screenPtRayMethod = camera.GetType().GetMethod("ScreenPointToRay", new[] { v3Type });
                    if (screenPtRayMethod != null)
                    {
                        object rayObj = screenPtRayMethod.Invoke(camera, new[] { mousePosInstance });
                        if (rayObj != null)
                        {
                            object originObj = rayObj.GetType().GetField("m_Origin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(rayObj);
                            object directionObj = rayObj.GetType().GetField("m_Direction", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(rayObj);

                            if (originObj == null) originObj = rayObj.GetType().GetProperty("origin")?.GetValue(rayObj, null);
                            if (directionObj == null) directionObj = rayObj.GetType().GetProperty("direction")?.GetValue(rayObj, null);

                            if (originObj != null && directionObj != null)
                            {
                                float originY = Convert.ToSingle(originObj.GetType().GetField("y")?.GetValue(originObj) ?? originObj.GetType().GetProperty("y")?.GetValue(originObj, null));
                                float directionY = Convert.ToSingle(directionObj.GetType().GetField("y")?.GetValue(directionObj) ?? directionObj.GetType().GetProperty("y")?.GetValue(directionObj, null));

                                if (directionY != 0)
                                {
                                    float t = -originY / directionY;
                                    if (t >= 0)
                                    {
                                        float originX = Convert.ToSingle(originObj.GetType().GetField("x")?.GetValue(originObj) ?? originObj.GetType().GetProperty("x")?.GetValue(originObj, null));
                                        float originZ = Convert.ToSingle(originObj.GetType().GetField("z")?.GetValue(originObj) ?? originObj.GetType().GetProperty("z")?.GetValue(originObj, null));
                                        
                                        float directionX = Convert.ToSingle(directionObj.GetType().GetField("x")?.GetValue(directionObj) ?? directionObj.GetType().GetProperty("x")?.GetValue(directionObj, null));
                                        float directionZ = Convert.ToSingle(directionObj.GetType().GetField("z")?.GetValue(directionObj) ?? directionObj.GetType().GetProperty("z")?.GetValue(directionObj, null));

                                        float ptX = originX + directionX * t;
                                        float ptY = originY + directionY * t;
                                        float ptZ = originZ + directionZ * t;
                                        return new float3(ptX, ptY, ptZ);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Fallback raycast reflection failed: {ex}");
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

                Type cameraSystemType = null;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name == "Game")
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "CameraControllerSystem")
                            {
                                cameraSystemType = type;
                                break;
                            }
                        }
                    }
                    if (cameraSystemType != null) break;
                }

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

            lock (LockObj)
            {
                if (playerId == NetworkInterface.Instance.LocalPlayer.PlayerId)
                {
                    return;
                }

                if (_remoteCursors.TryGetValue(playerId, out var cursor))
                {
                    targetPosition = cursor.Position;
                    found = true;
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

                Type cameraSystemType = null;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name == "Game")
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "CameraControllerSystem")
                            {
                                cameraSystemType = type;
                                break;
                            }
                        }
                    }
                    if (cameraSystemType != null) break;
                }

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

                // Snap pivot/target
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

                // Snap camera position with tilt view look-down offset
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

            // 1. Cursors List
            sb.Append("\"cursors\":[");
            int cCount = 0;
            lock (LockObj)
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

                    float screenX = 0f;
                    float screenY = 0f;
                    bool visible = false;
                    
                    float focusScreenX = 0f;
                    float focusScreenY = 0f;
                    bool focusVisible = false;

                    var cam = UnityEngine.Camera.main;
                    if (cam != null)
                    {
                        try
                        {
                            Type v3Type = typeof(UnityEngine.Component).Assembly.GetType("UnityEngine.Vector3");
                            var w2sMethod = cam.GetType().GetMethod("WorldToScreenPoint", new[] { v3Type });
                            if (w2sMethod != null)
                            {
                                // Cursor Projection
                                object v3Cursor = Activator.CreateInstance(v3Type, cursor.Position.x, cursor.Position.y, cursor.Position.z);
                                object screenPosObj = w2sMethod.Invoke(cam, new[] { v3Cursor });
                                if (screenPosObj != null)
                                {
                                    float screenPosX = Convert.ToSingle(screenPosObj.GetType().GetField("x")?.GetValue(screenPosObj) ?? screenPosObj.GetType().GetProperty("x")?.GetValue(screenPosObj, null));
                                    float screenPosY = Convert.ToSingle(screenPosObj.GetType().GetField("y")?.GetValue(screenPosObj) ?? screenPosObj.GetType().GetProperty("y")?.GetValue(screenPosObj, null));
                                    float screenPosZ = Convert.ToSingle(screenPosObj.GetType().GetField("z")?.GetValue(screenPosObj) ?? screenPosObj.GetType().GetProperty("z")?.GetValue(screenPosObj, null));

                                    if (screenPosZ > 0)
                                    {
                                        screenX = (screenPosX / Screen.width) * 100f;
                                        screenY = (1f - (screenPosY / Screen.height)) * 100f;
                                        visible = true;
                                    }
                                }

                                // Camera Focus Projection
                                object v3Focus = Activator.CreateInstance(v3Type, cursor.CameraFocus.x, cursor.CameraFocus.y, cursor.CameraFocus.z);
                                object screenFocusObj = w2sMethod.Invoke(cam, new[] { v3Focus });
                                if (screenFocusObj != null)
                                {
                                    float screenPosX = Convert.ToSingle(screenFocusObj.GetType().GetField("x")?.GetValue(screenFocusObj) ?? screenFocusObj.GetType().GetProperty("x")?.GetValue(screenFocusObj, null));
                                    float screenPosY = Convert.ToSingle(screenFocusObj.GetType().GetField("y")?.GetValue(screenFocusObj) ?? screenFocusObj.GetType().GetProperty("y")?.GetValue(screenFocusObj, null));
                                    float screenPosZ = Convert.ToSingle(screenFocusObj.GetType().GetField("z")?.GetValue(screenFocusObj) ?? screenFocusObj.GetType().GetProperty("z")?.GetValue(screenFocusObj, null));

                                    if (screenPosZ > 0)
                                    {
                                        focusScreenX = (screenPosX / Screen.width) * 100f;
                                        focusScreenY = (1f - (screenPosY / Screen.height)) * 100f;
                                        focusVisible = true;
                                    }
                                }
                            }
                        }
                        catch {}
                    }

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
            lock (LockObj)
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

                    float screenX = 0f;
                    float screenY = 0f;
                    bool visible = false;
                    float distance = 0f;
                    var cam = UnityEngine.Camera.main;
                    if (cam != null)
                    {
                        try
                        {
                            float3 camPos = new float3(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z);
                            distance = math.distance(camPos, ping.Position);

                            Type v3Type = typeof(UnityEngine.Component).Assembly.GetType("UnityEngine.Vector3");
                            object v3Instance = Activator.CreateInstance(v3Type, ping.Position.x, ping.Position.y, ping.Position.z);
                            var w2sMethod = cam.GetType().GetMethod("WorldToScreenPoint", new[] { v3Type });
                            if (w2sMethod != null)
                            {
                                object screenPosObj = w2sMethod.Invoke(cam, new[] { v3Instance });
                                if (screenPosObj != null)
                                {
                                    float screenPosX = Convert.ToSingle(screenPosObj.GetType().GetField("x")?.GetValue(screenPosObj) ?? screenPosObj.GetType().GetProperty("x")?.GetValue(screenPosObj, null));
                                    float screenPosY = Convert.ToSingle(screenPosObj.GetType().GetField("y")?.GetValue(screenPosObj) ?? screenPosObj.GetType().GetProperty("y")?.GetValue(screenPosObj, null));
                                    float screenPosZ = Convert.ToSingle(screenPosObj.GetType().GetField("z")?.GetValue(screenPosObj) ?? screenPosObj.GetType().GetProperty("z")?.GetValue(screenPosObj, null));

                                    if (screenPosZ > 0)
                                    {
                                        screenX = (screenPosX / Screen.width) * 100f;
                                        screenY = (1f - (screenPosY / Screen.height)) * 100f;
                                        visible = true;
                                    }
                                }
                            }
                        }
                        catch {}
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
                    lock (LockObj)
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

            // 4. Activities Timeline Log Ledger (Newest entries first)
            sb.Append("\"activities\":[");
            int aCount = 0;
            lock (LockObj)
            {
                for (int i = _activityLog.Count - 1; i >= 0; i--)
                {
                    var act = _activityLog[i];
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

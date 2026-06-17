using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CS2M.BaseGame.Commands;
using CS2M.Helpers;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace CS2M.BaseGame
{
    internal static class ModularUpgradeService
    {
        private static readonly Dictionary<string, Entity> PrefabEntityCache = new(StringComparer.Ordinal);
        private static int _upgradeNonceCounter;

        public static bool TryApplyUpgrade(ModularUpgradeCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.UpgradePrefabName))
            {
                return false;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                return false;
            }

            PrefabSystem prefabSystem = world.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null)
            {
                Log.Warn("ModularUpgrade: PrefabSystem not available.");
                return false;
            }

            if (!TryResolvePrefabEntity(prefabSystem, command.UpgradePrefabName, out Entity prefabEntity))
            {
                Log.Warn($"ModularUpgrade: Could not resolve prefab '{command.UpgradePrefabName}'.");
                return false;
            }

            EntityManager entityManager = world.EntityManager;
            Entity definitionEntity = entityManager.CreateEntity();

            Entity parentEntity = new Entity { Index = command.ParentEntityIndex, Version = command.ParentEntityVersion };
            float3 position = new(command.PositionX, command.PositionY, command.PositionZ);
            quaternion rotation = new(command.RotationX, command.RotationY, command.RotationZ, command.RotationW);

            entityManager.AddComponentData(definitionEntity, new CreationDefinition
            {
                m_Prefab = prefabEntity,
                m_SubPrefab = Entity.Null,
                m_Original = Entity.Null,
                m_Owner = parentEntity, // Attached owner building
                m_Attached = Entity.Null,
                m_Flags = CreationFlags.Permanent,
                m_RandomSeed = Environment.TickCount
            });

            entityManager.AddComponentData(definitionEntity, new OwnerDefinition
            {
                m_Prefab = prefabEntity,
                m_Position = position,
                m_Rotation = rotation
            });

            entityManager.AddComponentData(definitionEntity, new ObjectDefinition
            {
                m_Position = position,
                m_LocalPosition = float3.zero,
                m_Scale = new float3(1f, 1f, 1f),
                m_Rotation = rotation,
                m_LocalRotation = quaternion.identity,
                m_Elevation = 0f,
                m_Intensity = 1f,
                m_Age = 0f,
                m_ParentMesh = -1,
                m_GroupIndex = 0,
                m_Probability = 100,
                m_PrefabSubIndex = 0
            });

            entityManager.AddComponentData(definitionEntity, new PrefabRef
            {
                m_Prefab = prefabEntity
            });

            entityManager.AddComponentData(definitionEntity, new Temp
            {
                m_Original = Entity.Null,
                m_CurvePosition = 0f,
                m_Value = 0,
                m_Cost = 0,
                m_Flags = TempFlags.Create
            });

            Log.Debug($"ModularUpgrade: Placed upgrade '{command.UpgradePrefabName}' onto building entity {command.ParentEntityIndex}:{command.ParentEntityVersion}.");
            return true;
        }

        public static int NextUpgradeNonce()
        {
            return Interlocked.Increment(ref _upgradeNonceCounter);
        }

        private static bool TryResolvePrefabEntity(PrefabSystem prefabSystem, string prefabName, out Entity prefabEntity)
        {
            if (PrefabEntityCache.TryGetValue(prefabName, out prefabEntity) && prefabEntity != Entity.Null)
            {
                return true;
            }

            IEnumerable<PrefabBase> prefabs = ReflectionHelper.GetProp<IEnumerable<PrefabBase>>(prefabSystem, "prefabs");
            if (prefabs == null)
            {
                prefabEntity = Entity.Null;
                return false;
            }

            PrefabBase prefab = prefabs
                .FirstOrDefault(p => string.Equals(p.name, prefabName, StringComparison.Ordinal));

            if (prefab == null || !prefabSystem.TryGetEntity(prefab, out prefabEntity))
            {
                prefabEntity = Entity.Null;
                return false;
            }

            PrefabEntityCache[prefabName] = prefabEntity;
            return true;
        }
    }
}

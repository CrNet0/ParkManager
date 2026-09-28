using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using ParkManager.Geometry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ParkManager.Tools
{
    /// <summary>
    /// Experimental lake build after the park itself is complete: the planned
    /// lake outline is excavated with the game's own level brushes, then a
    /// constant-level water source fills it. The water level stays
    /// <see cref="LakeFreeboard"/> below the lowest shore point, so the lake
    /// cannot overflow into the surrounding city.
    /// </summary>
    public sealed partial class ParkToolSystem
    {
        private const float LakeDepth = 5f;
        private const float LakeFreeboard = 1f;
        private const float LakeBrushRadius = 12f;
        private const float LakeLevelTolerance = 0.3f;
        private const float LakeBrushTime = 0.1f;
        private const int LakeSettleFrames = 10;
        private const int LakeExcavationTimeoutFrames = 900;

        private enum LakeBuildPhase
        {
            Idle,
            Excavating,
        }

        private LakeBuildPhase _lakeBuildPhase;
        private List<float2> _lakeDabs = new List<float2>();
        private readonly List<Entity> _lakeBrushDefinitions = new List<Entity>();
        private float _lakeBottom;
        private float _lakeWaterLevel;
        private int _lakeStartedFrame;
        private Entity _levelTerraformPrefab = Entity.Null;
        private Entity _lakeBrushPrefab = Entity.Null;
        private EntityQuery _allPrefabsQuery;

        private bool LakeBuildBusy => _lakeBuildPhase != LakeBuildPhase.Idle;

        /// <summary>Starts excavation once paths and furnishing are built.</summary>
        private void StartLakeBuild()
        {
            var lake = _decorationPlan?.Lake;
            if (IsPlaza || lake == null || lake.Count < 3 || !HasBuiltPaths) return;
            if (!ResolveLakeTools())
            {
                PublishState(UiText.Of("lake.toolsMissing"));
                return;
            }
            var heights = _terrainSystem.GetHeightData(waitForPending: true);
            var rim = float.MaxValue;
            for (var i = 0; i < lake.Count; i++)
                rim = math.min(rim, SampleTerrain(ref heights, lake[i]));
            _lakeDabs = ParkLakePlanner.ExcavationDabs(lake, LakeBrushRadius);
            if (!math.isfinite(rim) || _lakeDabs.Count == 0) return;
            _lakeBottom = rim - LakeDepth;
            _lakeWaterLevel = rim - LakeFreeboard;
            _lakeStartedFrame = UnityEngine.Time.frameCount;
            _lakeBuildPhase = LakeBuildPhase.Excavating;
            Mod.Log.Info($"ParkManager LAKE start park={_lastBuildRecord} "
                + $"rim={rim:F2} bottom={_lakeBottom:F2} water={_lakeWaterLevel:F2} "
                + $"brushes={_lakeDabs.Count} radius={LakeBrushRadius:F1} "
                + $"legacyWater={World.GetOrCreateSystemManaged<WaterSystem>().UseLegacyWaterSources}.");
            PublishState(UiText.Of("lake.excavating"));
            PublishDecorationState(UiText.Of("lake.excavating"));
        }

        /// <summary>
        /// One excavation frame: re-emits a level brush for every spot that is
        /// still above the lake bottom. Fills the lake once the basin is dug or
        /// after the timeout, whichever comes first.
        /// </summary>
        private bool ProcessLakeBuild()
        {
            if (!LakeBuildBusy) return false;
            DiscardLakeBrushes();
            var heights = _terrainSystem.GetHeightData();
            var elapsed = UnityEngine.Time.frameCount - _lakeStartedFrame;
            var pending = new List<float3>();
            var highest = float.MinValue;
            for (var i = 0; i < _lakeDabs.Count; i++)
            {
                var height = SampleTerrain(ref heights, _lakeDabs[i]);
                highest = math.max(highest, height);
                if (height > _lakeBottom + LakeLevelTolerance)
                    pending.Add(new float3(_lakeDabs[i].x, height, _lakeDabs[i].y));
            }
            if (elapsed % 60 == 0)
                Mod.Log.Info($"ParkManager LAKE excavating frame={elapsed} "
                    + $"pending={pending.Count}/{_lakeDabs.Count} "
                    + $"highest={highest:F2} target={_lakeBottom:F2}.");

            var done = pending.Count == 0 && elapsed > LakeSettleFrames;
            var timedOut = elapsed > LakeExcavationTimeoutFrames;
            if (done || timedOut)
            {
                applyMode = ApplyMode.None;
                if (timedOut)
                    Mod.Log.Warn($"ParkManager LAKE excavation timed out with "
                        + $"{pending.Count}/{_lakeDabs.Count} spots above target.");
                FillLake();
                return true;
            }

            for (var i = 0; i < pending.Count; i++)
                EmitLevelBrush(pending[i]);
            applyMode = ApplyMode.Apply;
            return true;
        }

        private void EmitLevelBrush(float3 position)
        {
            var definition = EntityManager.CreateEntity();
            EntityManager.AddComponentData(definition, new CreationDefinition
            {
                m_Prefab = _lakeBrushPrefab,
            });
            EntityManager.AddComponentData(definition, new BrushDefinition
            {
                m_Tool = _levelTerraformPrefab,
                m_Line = new Line3.Segment(position, position),
                m_Size = LakeBrushRadius * 2f,
                m_Angle = 0f,
                m_Strength = 1f,
                m_Time = LakeBrushTime,
                m_Target = new float3(position.x, _lakeBottom, position.z),
                m_Start = position,
            });
            EntityManager.AddComponent<Updated>(definition);
            _lakeBrushDefinitions.Add(definition);
        }

        private void DiscardLakeBrushes()
        {
            for (var i = 0; i < _lakeBrushDefinitions.Count; i++)
            {
                var entity = _lakeBrushDefinitions[i];
                if (EntityManager.Exists(entity)
                    && !EntityManager.HasComponent<Deleted>(entity))
                    EntityManager.AddComponent<Deleted>(entity);
            }
            _lakeBrushDefinitions.Clear();
        }

        /// <summary>
        /// Adds one constant-level water source at the deepest-inside point.
        /// The legacy water system reads the level as an absolute elevation;
        /// the current one as a height above the source position.
        /// </summary>
        private void FillLake()
        {
            DiscardLakeBrushes();
            _lakeBuildPhase = LakeBuildPhase.Idle;
            var lake = _decorationPlan?.Lake;
            if (lake == null || lake.Count < 3 || !HasBuiltPaths) return;
            var anchor = ParkLakePlanner.WaterAnchor(lake, out var innerRadius);
            var heights = _terrainSystem.GetHeightData(waitForPending: true);
            var bottom = SampleTerrain(ref heights, anchor);
            if (!math.isfinite(bottom) || _lakeWaterLevel - bottom < 0.5f)
            {
                Mod.Log.Warn($"ParkManager LAKE basin not deep enough "
                    + $"(ground {bottom:F2}, water {_lakeWaterLevel:F2}); no water added.");
                PublishState(UiText.Of("lake.noWater"));
                PublishDecorationState(UiText.Of("lake.noWater"));
                return;
            }

            var waterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            var legacy = waterSystem.UseLegacyWaterSources;
            var position = new float3(anchor.x, bottom, anchor.y);
            var source = new Game.Simulation.WaterSourceData
            {
                m_ConstantDepth = legacy ? 1 : 0,
                m_Height = legacy ? _lakeWaterLevel : _lakeWaterLevel - bottom,
                m_Radius = math.max(5f, innerRadius * 0.9f),
                m_Polluted = 0f,
                m_Id = waterSystem.GetNextSourceId(),
                m_Modifier = 1f,
            };
            // A multiplier of 1 marks a radius too small for the simulation.
            source.m_Multiplier = WaterSystem.CalculateSourceMultiplier(source, position);
            for (var attempt = 0; attempt < 200 && source.m_Multiplier == 1f; attempt++)
            {
                source.m_Radius += 1f;
                source.m_Multiplier = WaterSystem.CalculateSourceMultiplier(source, position);
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, source);
            EntityManager.AddComponentData(entity, new Game.Objects.Transform
            {
                m_Position = position,
                m_Rotation = quaternion.identity,
            });
            EntityManager.AddComponent<Created>(entity);
            EntityManager.AddComponent<Updated>(entity);
            EntityManager.AddComponentData(entity, new ParkPathMember
            {
                Park = _lastBuildRecord,
                ElementId = NextMemberElementId(_lastBuildRecord),
                Kind = ParkPathMemberKind.LakeWater,
            });
            RefreshEditableBaseline(_lastBuildRecord);
            Mod.Log.Info($"ParkManager LAKE water source {entity}: legacy={legacy} "
                + $"constantDepth={source.m_ConstantDepth} height={source.m_Height:F2} "
                + $"radius={source.m_Radius:F1} multiplier={source.m_Multiplier:F3} "
                + $"position={position}.");
            PublishState(UiText.Of("lake.filled"));
            PublishDecorationState(UiText.Of("lake.filled"));
        }

        private void AbortLakeBuild()
        {
            if (!LakeBuildBusy) return;
            DiscardLakeBrushes();
            _lakeBuildPhase = LakeBuildPhase.Idle;
            applyMode = ApplyMode.None;
            Mod.Log.Warn("ParkManager LAKE excavation stopped because the tool was left.");
        }

        /// <summary>Finds the vanilla height-level tool and a round brush.</summary>
        private bool ResolveLakeTools()
        {
            if (EntityManager.Exists(_levelTerraformPrefab)
                && EntityManager.Exists(_lakeBrushPrefab)) return true;
            _levelTerraformPrefab = Entity.Null;
            _lakeBrushPrefab = Entity.Null;
            if (_allPrefabsQuery == default)
                _allPrefabsQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>());
            using var prefabs = _allPrefabsQuery.ToEntityArray(Allocator.Temp);
            var brushName = string.Empty;
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (!_pathPrefabSystem.TryGetPrefab<PrefabBase>(prefabs[i], out var prefab)
                    || prefab == null) continue;
                if (prefab is TerraformingPrefab terraform
                    && terraform.m_Type == TerraformingType.Level
                    && terraform.m_Target == TerraformingTarget.Height
                    && _levelTerraformPrefab == Entity.Null)
                    _levelTerraformPrefab = prefabs[i];
                // Prefer a soft brush: its falloff shapes a natural bank.
                else if (prefab is BrushPrefab && (_lakeBrushPrefab == Entity.Null
                    || prefab.name.ToLowerInvariant().Contains("soft")
                        && !brushName.ToLowerInvariant().Contains("soft")))
                {
                    _lakeBrushPrefab = prefabs[i];
                    brushName = prefab.name;
                }
            }
            Mod.Log.Info($"ParkManager LAKE tools: level={PrefabName(_levelTerraformPrefab)} "
                + $"brush='{brushName}'.");
            return _levelTerraformPrefab != Entity.Null && _lakeBrushPrefab != Entity.Null;
        }

        private static float SampleTerrain(ref TerrainHeightData heights, float2 point)
            => TerrainUtils.SampleHeight(ref heights, new float3(point.x, 0f, point.y));
    }
}

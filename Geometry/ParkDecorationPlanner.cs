using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace ParkManager.Geometry
{
    /// <summary>
    /// Deterministic, bounded first furnishing pass. Vegetation forms
    /// single-species groves with a bush fringe, a boundary belt and sparse
    /// solitaires, using separate random streams; furniture follows paths and
    /// the optional fence follows the boundary while leaving gate gaps.
    /// </summary>
    internal static class ParkDecorationPlanner
    {
        // Safety caps only: counts normally follow the park area and density.
        // They stay well above what a large park needs so the furnishing
        // never thins out, while bounding planning and build time.
        private const int MaximumTrees = 4000;
        private const int MaximumBushes = 6000;
        private const int MaximumFurniture = 1200;
        private const int MaximumTrashBins = 250;
        private const int MaximumFencePieces = 2000;
        private const int MaximumGroves = 120;
        private const double AreaPerAnimalSpawner = 20000.0;
        private const int MaximumAnimalSpawners = 8;
        // Share of the park area covered by groves; the rest stays open lawn.
        private const float GroveCoverage = 0.28f;
        private const float FenceInset = 0.20f;
        private const float AccentSpeciesChance = 0.1f;
        private const float YoungTreeChance = 0.08f;
        private const float AdultTreeChance = 0.2f;
        private const float SolitaireShare = 0.12f;

        internal static ParkDecorationPlan Generate(IReadOnlyList<float2> polygon,
            ParkPathPlan paths, IReadOnlyList<float2> entrances, int seed,
            float builtPathWidth, bool fenceEnabled, int vegetationDensity,
            int furnitureDensity, int enabledMask, bool planLake = false,
            bool planAnimals = false)
        {
            var result = new List<ParkDecorationPlacement>();
            if (polygon == null || polygon.Count < 3)
                return new ParkDecorationPlan(seed, fenceEnabled, result);

            var area = Math.Abs(PolygonMath.SignedArea(polygon));
            PolygonMath.Bounds(polygon, out var min, out var max);
            var layoutSeed = Seeds.Mix(seed, 0x3c6ef372u);
            // The lake takes the largest open space between the paths; plants
            // are counted for the remaining land only.
            var lake = planLake
                ? ParkLakePlanner.Generate(polygon, paths, entrances, builtPathWidth,
                    Seeds.Mix(seed, 0x2545f491u))
                : new List<float2>();
            var plantedArea = area - Math.Abs(PolygonMath.SignedArea(lake));

            var densityScale = math.clamp(vegetationDensity, 25, 200) / 100.0;
            var furnitureScale = math.clamp(furnitureDensity, 25, 200) / 100f;
            var trees = math.clamp((int)Math.Round(plantedArea / 380.0 * densityScale),
                1, MaximumTrees);
            var bushes = math.clamp((int)Math.Round(plantedArea / 230.0 * densityScale),
                1, MaximumBushes);
            var groves = BuildGroves(polygon, lake, min, max, area, layoutSeed);
            var belt = new BoundaryBelt(polygon, area, layoutSeed);
            // Furniture is planned first so both vegetation layers can reserve
            // its footprint. The random streams are independent, therefore the
            // ordering does not make either layer non-deterministic.
            if (IsEnabled(enabledMask, ParkDecorationKind.Bench))
                SampleFurniture(result, polygon, paths, entrances,
                    ParkDecorationKind.Bench, 34f / furnitureScale, 0.75f,
                    builtPathWidth, Seeds.Mix(seed, 0x7f4a7c15u));
            if (IsEnabled(enabledMask, ParkDecorationKind.Lamp))
                SampleFurniture(result, polygon, paths, entrances,
                    ParkDecorationKind.Lamp, 23f / furnitureScale, 0.45f,
                    builtPathWidth, Seeds.Mix(seed, 0x94d049bbu));
            if (IsEnabled(enabledMask, ParkDecorationKind.TrashBin))
                SampleTrashBins(result, polygon, paths, entrances, builtPathWidth,
                    furnitureScale, Seeds.Mix(seed, 0xa54ff53au));
            if (IsEnabled(enabledMask, ParkDecorationKind.Tree))
                SampleVegetation(result, polygon, paths, entrances, lake, min, max,
                    groves, belt, trees, ParkDecorationKind.Tree,
                    builtPathWidth, Seeds.Mix(seed, 0x51f15e21u));
            if (IsEnabled(enabledMask, ParkDecorationKind.Bush))
                SampleVegetation(result, polygon, paths, entrances, lake, min, max,
                    groves, belt, bushes, ParkDecorationKind.Bush,
                    builtPathWidth, Seeds.Mix(seed, 0x9e3779b9u));
            if (fenceEnabled && IsEnabled(enabledMask, ParkDecorationKind.Fence))
                SampleFence(result, polygon, entrances, builtPathWidth,
                    Seeds.Mix(seed, 0xd1b54a35u));
            if (planAnimals)
                PlaceAnimalSpawners(result, polygon, paths, lake, groves, area,
                    builtPathWidth);
            return new ParkDecorationPlan(seed, fenceEnabled, result, lake);
        }

        private static bool IsEnabled(int mask, ParkDecorationKind kind)
            => (mask & (1 << ((int)kind - 1))) != 0;

        /// <summary>
        /// Puts animal spawners into the largest groves, where wildlife reads
        /// as living in the wood rather than on the lawn: one per
        /// <see cref="AreaPerAnimalSpawner"/>, at most
        /// <see cref="MaximumAnimalSpawners"/>. Small parks get none.
        /// </summary>
        private static void PlaceAnimalSpawners(List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, ParkPathPlan paths,
            IReadOnlyList<float2> lake, IReadOnlyList<Grove> groves, double area,
            float builtPathWidth)
        {
            var target = math.min(MaximumAnimalSpawners,
                (int)Math.Round(area / AreaPerAnimalSpawner));
            var placed = 0;
            foreach (var grove in groves.OrderByDescending(grove => grove.Area))
            {
                if (placed >= target) break;
                if (!PolygonMath.PointInside(grove.Center, polygon)
                    || InLake(grove.Center, lake, 4f)
                    || !HasPathClearance(grove.Center, 4f, paths, builtPathWidth))
                    continue;
                result.Add(new ParkDecorationPlacement
                {
                    Kind = ParkDecorationKind.AnimalSpawner,
                    Position = grove.Center,
                    Size = 2f,
                });
                placed++;
            }
        }

        /// <summary>True when a plant of <paramref name="radius"/> would touch the water.</summary>
        private static bool InLake(float2 point, IReadOnlyList<float2> lake, float radius)
            => lake.Count >= 3 && (PolygonMath.PointInside(point, lake)
                || PolygonMath.DistanceToBoundarySquared(point, lake) < radius * radius);

        private static void SampleVegetation(List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, ParkPathPlan paths,
            IReadOnlyList<float2> entrances, IReadOnlyList<float2> lake,
            float2 min, float2 max, IReadOnlyList<Grove> groves,
            BoundaryBelt belt, int target, ParkDecorationKind kind,
            float builtPathWidth, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            // Large parks carry thousands of plants; a spatial grid keeps the
            // neighbour checks local instead of scanning every placement.
            var grid = new PlacementGrid(result);
            var nearby = new List<int>();
            var acceptedCount = 0;
            var isTree = kind == ParkDecorationKind.Tree;
            var boundaryClearance = isTree ? 4f : 2.2f;
            var baseSpacing = isTree ? 7.5f : 4f;
            var groveShare = groves.Count > 0 ? (isTree ? 0.55f : 0.5f) : 0f;
            var beltShare = isTree ? 0.3f : 0.4f;
            // Larger groves receive proportionally more plants.
            var groveWeights = new float[groves.Count];
            var totalGroveWeight = 0f;
            for (var i = 0; i < groves.Count; i++)
                groveWeights[i] = totalGroveWeight += groves[i].Area;
            // Each grove and belt section is dominated by one species; the
            // selector is resolved modulo the chosen assets in the ECS layer.
            var groveVariants = new uint[groves.Count];
            for (var i = 0; i < groveVariants.Length; i++)
                groveVariants[i] = unchecked((uint)random.NextInt());
            var beltSpeciesSeed = unchecked((uint)random.NextInt());
            var maximumSolitaires = Math.Max(1,
                (int)Math.Round(target * SolitaireShare));
            var solitaires = 0;
            var attempts = Math.Max(80, target * 90);
            for (var attempt = 0; attempt < attempts && acceptedCount < target; attempt++)
            {
                float2 point;
                uint variant;
                float spacing;
                var solitaire = false;
                var source = random.NextFloat();
                var accent = random.NextFloat() < AccentSpeciesChance;
                var randomVariant = unchecked((uint)random.NextInt());
                if (source < groveShare)
                {
                    var index = WeightedIndex(groveWeights,
                        random.NextFloat(0f, totalGroveWeight));
                    var grove = groves[index];
                    var angle = random.NextFloat(0f, math.PI * 2f);
                    // Trees crowd towards the core and thin out at the edge;
                    // bushes form the understory fringe around the outline.
                    var radial = isTree
                        ? math.pow(random.NextFloat(), 0.6f)
                        : random.NextFloat(0.85f, 1.3f);
                    point = grove.Point(angle, radial);
                    variant = accent ? randomVariant : groveVariants[index];
                    spacing = baseSpacing * grove.Spacing;
                }
                else if (source < groveShare + beltShare && belt.Perimeter > 0f)
                {
                    var along = random.NextFloat(0f, belt.Perimeter);
                    // Open stretches keep views into the park; elsewhere the
                    // planted band widens and narrows along the boundary.
                    if (random.NextFloat() > belt.Coverage(along)) continue;
                    point = belt.Point(along, boundaryClearance
                        + belt.MaximumDepth(isTree) * belt.DepthFactor(along)
                            * math.pow(random.NextFloat(), 0.8f));
                    variant = accent ? randomVariant
                        : belt.SectionVariant(along, beltSpeciesSeed);
                    spacing = baseSpacing;
                }
                else
                {
                    // Scattered solitaires are capped and keep a wide berth
                    // so open lawns remain between the groves and the belt.
                    point = random.NextFloat2(min, max);
                    variant = randomVariant;
                    spacing = isTree ? 14f : 6f;
                    solitaire = true;
                    if (solitaires >= maximumSolitaires) continue;
                }
                var size = kind == ParkDecorationKind.Tree
                    ? random.NextFloat(5f, 8.5f)
                    : random.NextFloat(2.3f, 4.2f);
                var collisionRadius = kind == ParkDecorationKind.Tree
                    ? math.max(2.5f, size * 0.45f)
                    : math.max(1.2f, size * 0.45f);
                // All checks must pass, so their order does not change the
                // result; the cheap grid lookup runs before the path scan.
                if (!PolygonMath.PointInside(point, polygon)
                    || PolygonMath.DistanceToBoundarySquared(point, polygon)
                        < boundaryClearance * boundaryClearance
                    || PolygonMath.DistanceToPointsSquared(point, entrances) < 100f
                    || InLake(point, lake, collisionRadius)
                    || TooCloseToPlaced(result, grid, nearby, point, kind,
                        spacing, collisionRadius)
                    || !HasPathClearance(point, collisionRadius, paths,
                        builtPathWidth)) continue;

                acceptedCount++;
                if (solitaire) solitaires++;
                result.Add(new ParkDecorationPlacement
                {
                    Kind = kind,
                    Position = point,
                    Rotation = random.NextFloat(0f, math.PI * 2f),
                    Size = size,
                    Variant = variant,
                    AgeStage = kind == ParkDecorationKind.Tree
                        ? SelectTreeAge(ref random) : (byte)0,
                });
                grid.Add(result.Count - 1);
            }
        }

        /// <summary>
        /// Same rules as the former linear scan: the plant's own layer keeps
        /// <paramref name="spacing"/>, every other object its collision gap.
        /// </summary>
        private static bool TooCloseToPlaced(List<ParkDecorationPlacement> result,
            PlacementGrid grid, List<int> nearby, float2 point,
            ParkDecorationKind kind, float spacing, float collisionRadius)
        {
            grid.Query(point, math.max(spacing,
                math.max(collisionRadius + 1.75f, 6f)), nearby);
            for (var i = 0; i < nearby.Count; i++)
            {
                var other = result[nearby[i]];
                var distanceSquared = math.distancesq(point, other.Position);
                if (other.Kind == kind && distanceSquared < spacing * spacing)
                    return true;
                float combined;
                if (other.Kind == ParkDecorationKind.Bench)
                    combined = collisionRadius + 1.75f;
                else if (other.Kind == ParkDecorationKind.Lamp)
                    combined = collisionRadius + 0.85f;
                else if (other.Kind == ParkDecorationKind.TrashBin)
                    combined = collisionRadius + 0.75f;
                else if (other.Kind == ParkDecorationKind.Tree
                    || other.Kind == ParkDecorationKind.Bush)
                    combined = kind == ParkDecorationKind.Tree
                        || other.Kind == ParkDecorationKind.Tree ? 6f : 3.5f;
                else continue;
                if (distanceSquared < combined * combined) return true;
            }
            return false;
        }

        /// <summary>
        /// Uniform bucket grid over placement indices for neighbour queries.
        /// </summary>
        private sealed class PlacementGrid
        {
            private const float CellSize = 8f;
            private readonly List<ParkDecorationPlacement> _items;
            private readonly Dictionary<long, List<int>> _cells =
                new Dictionary<long, List<int>>();

            internal PlacementGrid(List<ParkDecorationPlacement> items)
            {
                _items = items;
                for (var i = 0; i < items.Count; i++) Add(i);
            }

            internal void Add(int index)
            {
                var cell = (int2)math.floor(_items[index].Position / CellSize);
                var key = Key(cell.x, cell.y);
                if (!_cells.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>();
                    _cells.Add(key, bucket);
                }
                bucket.Add(index);
            }

            internal void Query(float2 point, float radius, List<int> output)
            {
                output.Clear();
                var low = (int2)math.floor((point - radius) / CellSize);
                var high = (int2)math.floor((point + radius) / CellSize);
                for (var y = low.y; y <= high.y; y++)
                for (var x = low.x; x <= high.x; x++)
                    if (_cells.TryGetValue(Key(x, y), out var bucket))
                        output.AddRange(bucket);
            }

            private static long Key(int x, int y)
                => ((long)x << 32) | (uint)y;
        }

        /// <summary>
        /// Irregular planting group: a rotated ellipse whose outline wobbles
        /// with two low-frequency waves, so no two groves share size or shape.
        /// </summary>
        private sealed class Grove
        {
            internal float2 Center;
            internal float Radius;
            internal float Aspect;
            internal float Rotation;
            internal int Lobes;
            internal int FineLobes;
            internal float Phase;
            internal float FinePhase;
            /// <summary>Own-species spacing factor: dense thickets to open stands.</summary>
            internal float Spacing;

            internal float Area => math.PI * Radius * Radius * Aspect;

            /// <summary>Point at <paramref name="radial"/> times the outline distance.</summary>
            internal float2 Point(float angle, float radial)
            {
                var outline = Radius * (1f + 0.22f * math.sin(Lobes * angle + Phase)
                    + 0.12f * math.sin(FineLobes * angle + FinePhase));
                var local = new float2(math.cos(angle), math.sin(angle) * Aspect)
                    * outline * radial;
                math.sincos(Rotation, out var sin, out var cos);
                return Center + new float2(local.x * cos - local.y * sin,
                    local.x * sin + local.y * cos);
            }
        }

        /// <summary>
        /// Places groves of varied size until they cover
        /// <see cref="GroveCoverage"/> of the park. Small groves are common and
        /// large ones rare, which reads as grown rather than laid out.
        /// </summary>
        private static List<Grove> BuildGroves(IReadOnlyList<float2> polygon,
            IReadOnlyList<float2> lake, float2 min, float2 max, double area,
            uint seed)
        {
            var result = new List<Grove>();
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            var side = (float)Math.Sqrt(area);
            var baseRadius = math.clamp(side * 0.16f, 12f, 30f);
            var largestRadius = math.max(8f, side * 0.28f);
            var budget = (float)area * GroveCoverage;
            var covered = 0f;
            for (var attempt = 0; attempt < 4000 && result.Count < MaximumGroves
                && (covered < budget || result.Count < 2); attempt++)
            {
                var grove = new Grove
                {
                    Radius = math.clamp(baseRadius * (0.45f
                        + 1.25f * math.pow(random.NextFloat(), 1.8f)), 6f,
                        largestRadius),
                    Aspect = random.NextFloat(0.55f, 1f),
                    Rotation = random.NextFloat(0f, math.PI),
                    Lobes = random.NextInt(2, 4),
                    FineLobes = random.NextInt(4, 7),
                    Phase = random.NextFloat(0f, math.PI * 2f),
                    FinePhase = random.NextFloat(0f, math.PI * 2f),
                    Spacing = random.NextFloat(0.85f, 1.35f),
                    Center = random.NextFloat2(min, max),
                };
                if (!PolygonMath.PointInside(grove.Center, polygon)
                    || InLake(grove.Center, lake, grove.Radius * 0.5f)) continue;
                var valid = true;
                for (var i = 0; i < result.Count && valid; i++)
                {
                    // Slight overlap is allowed; the wobbling outlines merge
                    // into larger irregular woods.
                    var gap = math.max(10f, (grove.Radius + result[i].Radius) * 0.8f);
                    valid = math.distancesq(grove.Center, result[i].Center) >= gap * gap;
                }
                if (!valid) continue;
                result.Add(grove);
                covered += grove.Area;
            }
            return result;
        }

        /// <summary>Index of the cumulative weight bucket containing <paramref name="value"/>.</summary>
        private static int WeightedIndex(float[] cumulative, float value)
        {
            var low = 0;
            var high = cumulative.Length - 1;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (cumulative[middle] > value) high = middle;
                else low = middle + 1;
            }
            return low;
        }

        /// <summary>
        /// Planted band along the park boundary. Smooth periodic noise along
        /// the perimeter varies its coverage (leaving open views), its depth
        /// and its dominant species per section; trees and bushes share the
        /// same structure so bushes line the tree clumps.
        /// </summary>
        private sealed class BoundaryBelt
        {
            private readonly IReadOnlyList<float2> _polygon;
            private readonly float _orientation;
            private readonly uint _seed;
            private readonly int _coverageCells;
            private readonly int _depthCells;
            private readonly int _sectionCells;
            private readonly float _depthScale;

            internal float Perimeter { get; }

            internal BoundaryBelt(IReadOnlyList<float2> polygon, double area, uint seed)
            {
                _polygon = polygon;
                _seed = seed;
                _orientation = PolygonMath.SignedArea(polygon) >= 0d ? 1f : -1f;
                for (var i = 0; i < polygon.Count; i++)
                    Perimeter += math.distance(polygon[i],
                        polygon[(i + 1) % polygon.Count]);
                _coverageCells = Math.Max(1, (int)Math.Round(Perimeter / 70f));
                _depthCells = Math.Max(1, (int)Math.Round(Perimeter / 45f));
                _sectionCells = Math.Max(1, (int)Math.Round(Perimeter / 90f));
                // Larger parks carry a deeper planted edge.
                _depthScale = math.clamp((float)Math.Sqrt(area) / 250f, 1f, 2.2f);
            }

            /// <summary>
            /// Acceptance probability: 0 along open stretches, 1 in clumps.
            /// Roughly half of the boundary stays open so the planting reads
            /// as clumps rather than a continuous row.
            /// </summary>
            internal float Coverage(float along)
                => math.smoothstep(0.42f, 0.72f, Noise(along, _coverageCells, 0x1u));

            /// <summary>Clumps are deepest where they are densest.</summary>
            internal float DepthFactor(float along)
                => 0.25f + 0.75f * (0.6f * Noise(along, _coverageCells, 0x1u)
                    + 0.4f * Noise(along, _depthCells, 0x2u));

            internal float MaximumDepth(bool tree) => (tree ? 7f : 9f) * _depthScale;

            internal uint SectionVariant(float along, uint layerSeed)
                => Hash(layerSeed, (int)(along / Perimeter * _sectionCells)
                    % _sectionCells);

            internal float2 Point(float along, float inset)
            {
                for (var i = 0; i < _polygon.Count; i++)
                {
                    var a = _polygon[i];
                    var edge = _polygon[(i + 1) % _polygon.Count] - a;
                    var length = math.length(edge);
                    if (along > length && i + 1 < _polygon.Count)
                    {
                        along -= length;
                        continue;
                    }
                    if (length < 1e-4f) return a;
                    var tangent = edge / length;
                    var inward = new float2(-tangent.y, tangent.x) * _orientation;
                    return a + tangent * math.min(along, length) + inward * inset;
                }
                return _polygon[0];
            }

            /// <summary>Two octaves of value noise that wrap around the perimeter.</summary>
            private float Noise(float along, int cells, uint salt)
            {
                var t = along / Perimeter;
                return 0.7f * ValueNoise(t * cells, cells, _seed ^ salt)
                    + 0.3f * ValueNoise(t * cells * 3, cells * 3,
                        _seed ^ (salt * 0x9e3779b9u));
            }

            private static float ValueNoise(float x, int period, uint seed)
            {
                var cell = (int)math.floor(x);
                var f = x - cell;
                f = f * f * (3f - 2f * f);
                return math.lerp(Unit(seed, cell % period),
                    Unit(seed, (cell + 1) % period), f);
            }

            private static float Unit(uint seed, int index)
                => (Hash(seed, index) & 0xffffffu) / 16777215f;

            private static uint Hash(uint seed, int index)
                => Seeds.Mix(unchecked((int)(seed ^ (uint)index * 0x9e3779b9u)),
                    0x85ebca6bu);
        }

        private static byte SelectTreeAge(ref Unity.Mathematics.Random random)
        {
            // An established park: mostly mature trees with some replanting.
            var roll = random.NextFloat();
            if (roll < YoungTreeChance) return 1;
            if (roll < YoungTreeChance + AdultTreeChance) return 2;
            return 3;
        }

        private static void SampleFurniture(List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, ParkPathPlan paths,
            IReadOnlyList<float2> entrances, ParkDecorationKind kind,
            float interval, float footprintRadius, float builtPathWidth, uint seed)
        {
            if (paths == null) return;
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            var count = 0;
            for (var edgeIndex = 0; edgeIndex < paths.Edges.Count
                && count < MaximumFurniture; edgeIndex++)
            {
                var edge = paths.Edges[edgeIndex];
                var a = paths.Nodes[edge.A].Position;
                var b = paths.Nodes[edge.B].Position;
                var delta = b - a;
                var length = math.length(delta);
                if (length < interval * 0.55f) continue;
                var tangent = delta / length;
                var normal = new float2(-tangent.y, tangent.x);
                var pieces = Math.Max(1, (int)Math.Floor(length / interval));
                for (var i = 0; i < pieces && count < MaximumFurniture; i++)
                {
                    var t = (i + 1f) / (pieces + 1f);
                    t = math.clamp(t + random.NextFloat(-0.06f, 0.06f), 0.15f, 0.85f);
                    var sidePhase = kind == ParkDecorationKind.Lamp ? 1
                        : kind == ParkDecorationKind.TrashBin ? 2 : 0;
                    var side = ((i + edgeIndex + sidePhase) & 1) == 0
                        ? 1f : -1f;
                    // Put the object's footprint immediately beside the real
                    // rendered path. The ECS layer refines this fallback with
                    // the selected prefab's exact collision bounds.
                    var safeOffset = math.max(edge.Width, builtPathWidth) * 0.5f
                        + footprintRadius + 0.2f;
                    var point = math.lerp(a, b, t)
                        + normal * (safeOffset * side);
                    if (!PolygonMath.PointInside(point, polygon)
                        || PolygonMath.DistanceToBoundarySquared(point, polygon) < 2.25f
                        || PolygonMath.DistanceToPointsSquared(point, entrances) < 64f)
                        continue;
                    if (TooCloseToKind(result, point, kind,
                        FurnitureSpacing(kind))) continue;
                    if (TooCloseToFurniture(result, point, kind)) continue;
                    result.Add(new ParkDecorationPlacement
                    {
                        Kind = kind,
                        Position = point,
                        Rotation = math.atan2(tangent.x, tangent.y)
                            + (side < 0f ? math.PI : 0f),
                        Size = FurniturePreviewSize(kind),
                        Variant = unchecked((uint)random.NextInt()),
                    });
                    count++;
                }
            }
        }

        /// <summary>
        /// Places bins where they are useful instead of distributing them at a
        /// fixed interval: first near gates, then at real graph junctions and
        /// finally beside benches. Positions remain just outside the rendered
        /// path footprint and use the same deterministic seed as the rest of
        /// the furnishing plan.
        /// </summary>
        private static void SampleTrashBins(
            List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, ParkPathPlan paths,
            IReadOnlyList<float2> entrances, float builtPathWidth,
            float densityScale, uint seed)
        {
            if (paths == null || paths.Edges.Count == 0) return;
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            var incident = new List<int>[paths.Nodes.Count];
            for (var i = 0; i < incident.Length; i++) incident[i] = new List<int>();
            for (var i = 0; i < paths.Edges.Count; i++)
            {
                incident[paths.Edges[i].A].Add(i);
                incident[paths.Edges[i].B].Add(i);
            }

            var importantNodes = 0;
            for (var i = 0; i < paths.Nodes.Count; i++)
                if (paths.Nodes[i].Kind == ParkPathNodeKind.Gate
                    || incident[i].Count >= 3) importantNodes++;
            var target = math.clamp((int)Math.Round(Math.Max(importantNodes,
                paths.TotalLength / 90f) * densityScale), 1, MaximumTrashBins);
            var placed = 0;

            // Gate nodes have first priority. Moving a few metres into the park
            // keeps the bin clear of the actual entrance opening.
            for (var i = 0; i < paths.Nodes.Count && placed < target; i++)
                if (paths.Nodes[i].Kind == ParkPathNodeKind.Gate
                    && TryPlaceTrashAtNode(result, polygon, paths, incident,
                        i, 3.5f, 6f, builtPathWidth, ref random)) placed++;

            // A graph degree of three or more is a genuine crossing or fork.
            for (var i = 0; i < paths.Nodes.Count && placed < target; i++)
                if (paths.Nodes[i].Kind != ParkPathNodeKind.Gate
                    && incident[i].Count >= 3
                    && TryPlaceTrashAtNode(result, polygon, paths, incident,
                        i, 2.5f, 4.5f, builtPathWidth, ref random)) placed++;

            // Benches were generated first. Offset along their path tangent so
            // the bin is nearby without intersecting the seating footprint.
            var furnitureCount = result.Count;
            for (var i = 0; i < furnitureCount && placed < target; i++)
            {
                var bench = result[i];
                if (bench.Kind != ParkDecorationKind.Bench) continue;
                var tangent = math.normalizesafe(new float2(
                    math.sin(bench.Rotation), math.cos(bench.Rotation)));
                var firstSign = random.NextBool() ? 1f : -1f;
                var accepted = false;
                for (var attempt = 0; attempt < 2 && !accepted; attempt++)
                {
                    var sign = attempt == 0 ? firstSign : -firstSign;
                    var point = bench.Position + tangent * sign
                        * random.NextFloat(2.7f, 4.2f);
                    accepted = TryAddTrashBin(result, polygon, point,
                        tangent, ref random);
                }
                if (accepted) placed++;
            }

            // Very small or degenerate graphs may contain none of the preferred
            // anchors. Retain one useful path-side bin as a graceful fallback.
            if (placed == 0)
                SampleFurniture(result, polygon, paths, entrances,
                    ParkDecorationKind.TrashBin, 70f, 0.45f,
                    builtPathWidth, seed);
        }

        private static bool TryPlaceTrashAtNode(
            List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, ParkPathPlan paths,
            IReadOnlyList<int>[] incident, int nodeIndex, float minimumAlong,
            float maximumAlong, float builtPathWidth,
            ref Unity.Mathematics.Random random)
        {
            var edges = incident[nodeIndex];
            if (edges == null || edges.Count == 0) return false;
            var edgeStart = random.NextInt(0, edges.Count);
            var firstSide = random.NextBool() ? 1f : -1f;
            for (var edgeOffset = 0; edgeOffset < edges.Count; edgeOffset++)
            {
                var edge = paths.Edges[edges[(edgeStart + edgeOffset) % edges.Count]];
                var other = edge.A == nodeIndex ? edge.B : edge.A;
                var tangent = math.normalizesafe(paths.Nodes[other].Position
                    - paths.Nodes[nodeIndex].Position);
                if (math.lengthsq(tangent) < 0.5f) continue;
                var normal = new float2(-tangent.y, tangent.x);
                var along = random.NextFloat(minimumAlong, maximumAlong);
                var sideOffset = math.max(edge.Width, builtPathWidth) * 0.5f
                    + FurnitureCollisionRadius(ParkDecorationKind.TrashBin) + 0.2f;
                for (var sideAttempt = 0; sideAttempt < 2; sideAttempt++)
                {
                    var side = sideAttempt == 0 ? firstSide : -firstSide;
                    var point = paths.Nodes[nodeIndex].Position + tangent * along
                        + normal * sideOffset * side;
                    if (TryAddTrashBin(result, polygon, point, tangent,
                        ref random)) return true;
                }
            }
            return false;
        }

        private static bool TryAddTrashBin(List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, float2 point, float2 tangent,
            ref Unity.Mathematics.Random random)
        {
            if (!PolygonMath.PointInside(point, polygon)
                || PolygonMath.DistanceToBoundarySquared(point, polygon) < 1f
                || TooCloseToKind(result, point, ParkDecorationKind.TrashBin, 9f)
                || TooCloseToFurniture(result, point,
                    ParkDecorationKind.TrashBin)) return false;
            result.Add(new ParkDecorationPlacement
            {
                Kind = ParkDecorationKind.TrashBin,
                Position = point,
                Rotation = math.atan2(tangent.x, tangent.y),
                Size = FurniturePreviewSize(ParkDecorationKind.TrashBin),
                Variant = unchecked((uint)random.NextInt()),
            });
            return true;
        }

        private static void SampleFence(List<ParkDecorationPlacement> result,
            IReadOnlyList<float2> polygon, IReadOnlyList<float2> entrances,
            float builtPathWidth, uint seed)
        {
            var fenceBoundary = InsetFenceBoundary(polygon);
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            // One deterministic prefab variant is used for the complete fence.
            // Mixing pieces with different mesh lengths cannot form a continuous
            // line and differs from the game's object-line fence mode.
            var fenceVariant = unchecked((uint)random.NextInt());
            var count = 0;
            for (var edgeIndex = 0; edgeIndex < polygon.Count
                && count < MaximumFencePieces; edgeIndex++)
            {
                var a = fenceBoundary[edgeIndex];
                var b = fenceBoundary[(edgeIndex + 1) % polygon.Count];
                var delta = b - a;
                var length = math.length(delta);
                if (length < 1f) continue;
                var tangent = delta / length;
                var runs = new List<float2> { new float2(0f, length) };
                if (entrances != null)
                {
                    for (var gateIndex = 0; gateIndex < entrances.Count; gateIndex++)
                    {
                        var gate = entrances[gateIndex];
                        var projected = math.dot(gate - a, tangent);
                        var closest = a + tangent * math.clamp(projected, 0f, length);
                        if (math.distancesq(gate, closest) > 4f) continue;
                        var halfGap = math.max(3.75f,
                            builtPathWidth * 0.5f + 0.75f);
                        CutFenceGap(runs, projected - halfGap,
                            projected + halfGap, length);
                    }
                }
                runs.Sort((left, right) => left.x.CompareTo(right.x));
                for (var i = 0; i < runs.Count && count < MaximumFencePieces; i++)
                {
                    var runLength = runs[i].y - runs[i].x;
                    if (runLength < 0.5f) continue;
                    var point = a + tangent * ((runs[i].x + runs[i].y) * 0.5f);
                    result.Add(new ParkDecorationPlacement
                    {
                        Kind = ParkDecorationKind.Fence,
                        Position = point,
                        Rotation = math.atan2(tangent.x, tangent.y),
                        // Fence placements describe continuous runs. The ECS
                        // builder expands each run using the chosen prefab's
                        // real longitudinal mesh bounds.
                        Size = runLength,
                        Variant = fenceVariant,
                    });
                    count++;
                }
            }
        }

        /// <summary>
        /// Moves shared fence corners into the polygon. Offsetting individual
        /// edge centres would separate adjacent native fence-network nodes.
        /// </summary>
        private static float2[] InsetFenceBoundary(IReadOnlyList<float2> polygon)
        {
            var inset = new float2[polygon.Count];
            var orientation = PolygonMath.SignedArea(polygon) >= 0d ? 1f : -1f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                var corner = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var before = math.normalizesafe(corner - previous);
                var after = math.normalizesafe(next - corner);
                if (math.lengthsq(before) < 0.5f
                    || math.lengthsq(after) < 0.5f)
                {
                    inset[i] = corner;
                    continue;
                }
                var firstNormal = new float2(-before.y, before.x) * orientation;
                var secondNormal = new float2(-after.y, after.x) * orientation;
                var bisector = math.normalizesafe(firstNormal + secondNormal,
                    secondNormal);
                var distance = FenceInset / math.max(0.25f,
                    math.dot(bisector, secondNormal));
                var candidate = corner + bisector * math.min(distance,
                    FenceInset * 3f);
                if (!PolygonMath.PointInside(candidate, polygon))
                {
                    candidate = corner + secondNormal * FenceInset;
                    if (!PolygonMath.PointInside(candidate, polygon))
                        candidate = corner + firstNormal * FenceInset;
                }
                inset[i] = candidate;
            }
            return inset;
        }

        private static void CutFenceGap(List<float2> runs, float gapStart,
            float gapEnd, float edgeLength)
        {
            gapStart = math.clamp(gapStart, 0f, edgeLength);
            gapEnd = math.clamp(gapEnd, 0f, edgeLength);
            if (gapEnd <= gapStart) return;
            for (var i = runs.Count - 1; i >= 0; i--)
            {
                var run = runs[i];
                if (gapEnd <= run.x || gapStart >= run.y) continue;
                runs.RemoveAt(i);
                if (gapStart - run.x >= 0.5f)
                    runs.Add(new float2(run.x, gapStart));
                if (run.y - gapEnd >= 0.5f)
                    runs.Add(new float2(gapEnd, run.y));
            }
        }

        private static bool TooCloseToKind(List<ParkDecorationPlacement> items,
            float2 point, ParkDecorationKind kind, float distance)
        {
            var squared = distance * distance;
            for (var i = 0; i < items.Count; i++)
                if (items[i].Kind == kind
                    && math.distancesq(items[i].Position, point) < squared)
                    return true;
            return false;
        }

        private static bool TooCloseToFurniture(
            List<ParkDecorationPlacement> items, float2 point,
            ParkDecorationKind kind)
        {
            var ownRadius = FurnitureCollisionRadius(kind);
            for (var i = 0; i < items.Count; i++)
            {
                var other = items[i];
                if (other.Kind != ParkDecorationKind.Bench
                    && other.Kind != ParkDecorationKind.Lamp
                    && other.Kind != ParkDecorationKind.TrashBin) continue;
                var otherRadius = FurnitureCollisionRadius(other.Kind);
                var clearance = ownRadius + otherRadius + 0.35f;
                if (math.distancesq(point, other.Position)
                    < clearance * clearance) return true;
            }
            return false;
        }

        private static float FurnitureCollisionRadius(ParkDecorationKind kind)
        {
            if (kind == ParkDecorationKind.Bench) return 1.75f;
            if (kind == ParkDecorationKind.Lamp) return 0.85f;
            return 0.75f;
        }

        private static float FurnitureSpacing(ParkDecorationKind kind)
        {
            if (kind == ParkDecorationKind.Bench) return 14f;
            if (kind == ParkDecorationKind.Lamp) return 10f;
            return 12f;
        }

        private static float FurniturePreviewSize(ParkDecorationKind kind)
        {
            if (kind == ParkDecorationKind.Bench) return 3f;
            if (kind == ParkDecorationKind.Lamp) return 2.2f;
            return 1.2f;
        }

        private static bool HasPathClearance(float2 point, float objectRadius,
            ParkPathPlan paths, float builtPathWidth)
        {
            if (paths == null) return true;
            for (var i = 0; i < paths.Edges.Count; i++)
            {
                var edge = paths.Edges[i];
                var clearance = math.max(edge.Width, builtPathWidth) * 0.5f
                    + objectRadius + 0.35f;
                if (PolygonMath.DistanceToSegmentSquared(point,
                    paths.Nodes[edge.A].Position,
                    paths.Nodes[edge.B].Position) < clearance * clearance)
                    return false;
            }
            return true;
        }
    }
}

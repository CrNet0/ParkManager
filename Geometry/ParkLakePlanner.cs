using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ParkManager.Geometry
{
    /// <summary>
    /// Finds the largest open space between the paths of a park and shapes an
    /// organic lake outline there. The outline keeps a shore strip to paths,
    /// entrances and the park boundary; it grows into elongated open space by
    /// casting rays from the most open point. Without enough room no lake is
    /// planned. The result is a plain polygon for preview, excavation, water
    /// and a separate lake-bed surface.
    /// </summary>
    internal static class ParkLakePlanner
    {
        /// <summary>Free strip between the water edge and paths or boundary.</summary>
        internal const float ShoreMargin = 8f;
        private const float MinimumRadius = 10f;
        private const float MaximumRadius = 70f;
        private const int OutlineVertices = 48;
        private const float RayStep = 1f;

        internal static List<float2> Generate(IReadOnlyList<float2> polygon,
            ParkPathPlan paths, IReadOnlyList<float2> entrances,
            float builtPathWidth, uint seed)
        {
            var lake = new List<float2>();
            if (polygon == null || polygon.Count < 3) return lake;
            var area = Math.Abs(PolygonMath.SignedArea(polygon));
            PolygonMath.Bounds(polygon, out var min, out var max);

            // The most open point of the park is the lake centre.
            var step = math.max(3f, (float)Math.Sqrt(area) / 80f);
            var center = float2.zero;
            var bestClearance = 0f;
            for (var y = min.y + step * 0.5f; y < max.y; y += step)
            for (var x = min.x + step * 0.5f; x < max.x; x += step)
            {
                var point = new float2(x, y);
                if (!PolygonMath.PointInside(point, polygon)) continue;
                var clearance = Clearance(point, polygon, paths, entrances,
                    builtPathWidth);
                if (clearance <= bestClearance) continue;
                bestClearance = clearance;
                center = point;
            }
            var free = bestClearance - ShoreMargin;
            if (free < MinimumRadius) return lake;

            // Aim a little beyond the free radius so the outline follows
            // elongated open space; every ray stops at the shore strip.
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            var target = math.min(free * 1.35f,
                math.min(MaximumRadius, (float)Math.Sqrt(area) * 0.25f));
            var aspect = random.NextFloat(0.6f, 1f);
            var rotation = random.NextFloat(0f, math.PI);
            var lobes = random.NextInt(2, 4);
            var fineLobes = random.NextInt(4, 7);
            var phase = random.NextFloat(0f, math.PI * 2f);
            var finePhase = random.NextFloat(0f, math.PI * 2f);

            var limits = new float[OutlineVertices];
            var radii = new float[OutlineVertices];
            for (var i = 0; i < OutlineVertices; i++)
            {
                var angle = i * math.PI * 2f / OutlineVertices;
                var local = angle - rotation;
                var ellipse = 1f / math.sqrt(math.square(math.cos(local))
                    + math.square(math.sin(local) / aspect));
                var desired = target * ellipse * (1f + 0.18f * math.sin(lobes * angle + phase)
                    + 0.08f * math.sin(fineLobes * angle + finePhase));
                var direction = new float2(math.cos(angle), math.sin(angle));
                var reach = 0f;
                while (reach + RayStep <= desired
                    && Clearance(center + direction * (reach + RayStep), polygon,
                        paths, entrances, builtPathWidth) >= ShoreMargin)
                    reach += RayStep;
                limits[i] = reach;
                radii[i] = reach;
            }

            // Smooth the ray lengths so obstacles bend the shore instead of
            // notching it, without ever exceeding a ray's free length.
            for (var pass = 0; pass < 3; pass++)
            {
                var smoothed = new float[OutlineVertices];
                for (var i = 0; i < OutlineVertices; i++)
                {
                    var sum = 0f;
                    var weight = 0f;
                    for (var k = -2; k <= 2; k++)
                    {
                        var w = 3f - math.abs(k);
                        sum += radii[(i + k + OutlineVertices) % OutlineVertices] * w;
                        weight += w;
                    }
                    smoothed[i] = math.min(sum / weight, limits[i]);
                }
                radii = smoothed;
            }

            for (var i = 0; i < OutlineVertices; i++)
            {
                var angle = i * math.PI * 2f / OutlineVertices;
                lake.Add(center + new float2(math.cos(angle), math.sin(angle)) * radii[i]);
            }
            // Reject slivers where only a narrow corridor was free.
            if (Math.Abs(PolygonMath.SignedArea(lake))
                < math.PI * MinimumRadius * MinimumRadius * 0.6f)
                lake.Clear();
            return lake;
        }

        /// <summary>
        /// Centres of round levelling brushes that together excavate the lake.
        /// Centres stay <c>0.7 × radius</c> inside the outline, so the soft
        /// brush edge forms the bank without reaching shore planting or paths.
        /// Narrow lakes fall back to a smaller inset rather than no basin.
        /// </summary>
        internal static List<float2> ExcavationDabs(IReadOnlyList<float2> lake,
            float brushRadius)
        {
            var result = new List<float2>();
            if (lake == null || lake.Count < 3) return result;
            PolygonMath.Bounds(lake, out var min, out var max);
            var spacing = brushRadius * 0.6f;
            foreach (var inset in new[] { 0.7f, 0.4f })
            {
                var minimum = brushRadius * inset;
                for (var y = min.y + spacing * 0.5f; y < max.y; y += spacing)
                for (var x = min.x + spacing * 0.5f; x < max.x; x += spacing)
                {
                    var point = new float2(x, y);
                    if (PolygonMath.PointInside(point, lake)
                        && PolygonMath.DistanceToBoundarySquared(point, lake)
                            >= minimum * minimum)
                        result.Add(point);
                }
                if (result.Count > 0) break;
            }
            return result;
        }

        /// <summary>
        /// Deepest-inside point of the lake and its distance to the shore,
        /// used for the water source position and radius.
        /// </summary>
        internal static float2 WaterAnchor(IReadOnlyList<float2> lake,
            out float innerRadius)
        {
            innerRadius = 0f;
            var best = float2.zero;
            if (lake == null || lake.Count < 3) return best;
            PolygonMath.Bounds(lake, out var min, out var max);
            const int grid = 24;
            for (var y = 0; y <= grid; y++)
            for (var x = 0; x <= grid; x++)
            {
                var point = math.lerp(min, max, new float2(x, y) / grid);
                if (!PolygonMath.PointInside(point, lake)) continue;
                var distance = math.sqrt(PolygonMath.DistanceToBoundarySquared(point, lake));
                if (distance <= innerRadius) continue;
                innerRadius = distance;
                best = point;
            }
            return best;
        }

        /// <summary>Distance to the nearest path edge, entrance or boundary.</summary>
        private static float Clearance(float2 point, IReadOnlyList<float2> polygon,
            ParkPathPlan paths, IReadOnlyList<float2> entrances, float builtPathWidth)
        {
            if (!PolygonMath.PointInside(point, polygon)) return 0f;
            var clearance = math.sqrt(PolygonMath.DistanceToBoundarySquared(point, polygon));
            clearance = math.min(clearance,
                math.sqrt(PolygonMath.DistanceToPointsSquared(point, entrances)));
            if (paths == null) return clearance;
            for (var i = 0; i < paths.Edges.Count; i++)
            {
                var edge = paths.Edges[i];
                var halfWidth = math.max(edge.Width, builtPathWidth) * 0.5f;
                clearance = math.min(clearance, math.sqrt(PolygonMath.DistanceToSegmentSquared(
                    point, paths.Nodes[edge.A].Position, paths.Nodes[edge.B].Position))
                    - halfWidth);
            }
            return clearance;
        }
    }
}

using System.Collections.Generic;
using Unity.Mathematics;

namespace ParkManager.Geometry
{
    /// <summary>
    /// Shared single-precision polygon helpers for the planners and the world
    /// tool. Polygons are closed implicitly (last point connects to the first)
    /// and may use either winding. <see cref="ParkPathPlanner"/> keeps its own
    /// double-precision variants because its graph costs depend on them.
    /// </summary>
    internal static class PolygonMath
    {
        internal static double SignedArea(IReadOnlyList<float2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0.0;
            var area = 0.0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }
            return area * 0.5;
        }

        /// <summary>Even-odd test; points exactly on an edge are undefined.</summary>
        internal static bool PointInside(float2 point, IReadOnlyList<float2> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y)
                        / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Even-odd test that also accepts points closer to the boundary than
        /// the square root of <paramref name="toleranceSquared"/>.
        /// </summary>
        internal static bool PointInsideOrBoundary(float2 point,
            IReadOnlyList<float2> polygon, float toleranceSquared = 0.01f)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[j];
                var b = polygon[i];
                if (DistanceToSegmentSquared(point, a, b) < toleranceSquared)
                    return true;
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y)
                        / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        internal static float2 ClosestPointOnSegment(float2 point, float2 a, float2 b)
        {
            var ab = b - a;
            var length = math.lengthsq(ab);
            if (length < 0.0001f) return a;
            var t = math.clamp(math.dot(point - a, ab) / length, 0f, 1f);
            return a + ab * t;
        }

        internal static float DistanceToSegmentSquared(float2 point, float2 a, float2 b)
            => math.distancesq(point, ClosestPointOnSegment(point, a, b));

        internal static float DistanceToBoundarySquared(float2 point,
            IReadOnlyList<float2> polygon)
        {
            var nearest = float.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
                nearest = math.min(nearest, DistanceToSegmentSquared(point,
                    polygon[i], polygon[(i + 1) % polygon.Count]));
            return nearest;
        }

        internal static float DistanceToPointsSquared(float2 point,
            IReadOnlyList<float2> points)
        {
            var nearest = float.MaxValue;
            if (points == null) return nearest;
            for (var i = 0; i < points.Count; i++)
                nearest = math.min(nearest, math.distancesq(point, points[i]));
            return nearest;
        }

        internal static void Bounds(IReadOnlyList<float2> polygon,
            out float2 min, out float2 max)
        {
            min = polygon[0];
            max = polygon[0];
            for (var i = 1; i < polygon.Count; i++)
            {
                min = math.min(min, polygon[i]);
                max = math.max(max, polygon[i]);
            }
        }
    }
}

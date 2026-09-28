using Game.Common;
using Game.Tools;
using Game.Simulation;
using Unity.Mathematics;
using Unity.Entities;

namespace ParkManager.Tools
{
    public sealed partial class ParkToolSystem
    {
        private bool CreatePolygonArea(Entity prefab,
            ref TerrainHeightData heightData)
        {
            if (_points.Count < 3) return false;
            var definition = CreateBuildDefinition();
            EntityManager.AddComponentData(definition, new CreationDefinition
            {
                m_Prefab = prefab,
            });
            EntityManager.AddComponent<Updated>(definition);
            var nodes = EntityManager.AddBuffer<Game.Areas.Node>(definition);
            nodes.ResizeUninitialized(_points.Count + 1);
            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                var height = TerrainUtils.SampleHeight(ref heightData,
                    new float3(point.x, 0f, point.y));
                if (!math.isfinite(height)) return false;
                nodes[i] = new Game.Areas.Node(
                    new float3(point.x, height, point.y), float.MinValue);
            }
            nodes[_points.Count] = nodes[0];
            return true;
        }

    }
}

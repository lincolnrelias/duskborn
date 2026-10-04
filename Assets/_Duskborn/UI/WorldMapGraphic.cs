using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI
{
    // Two batched UI meshes: a circular terrain surface and its live marker overlay.
    public sealed class WorldMapGraphic : MaskableGraphic
    {
        public bool Markers;
        public Texture2D Atlas;
        public Rect AtlasBounds;
        public Vector2 Center;
        public float WorldRadius = 55f;
        public bool Square;
        public bool Frame;
        public bool ShowPlayers = true, ShowBuildings = true;
        public bool HasWaypoint;
        public Vector3 Waypoint;
        public override Texture mainTexture => !Markers && Atlas != null ? Atlas : base.mainTexture;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            Vector2 origin = rectTransform.rect.center;
            if (Frame)
            {
                Ring(mesh, origin, radius, radius - 3, new Color32(39, 30, 24, 255));
                Ring(mesh, origin, radius - 3, radius - 7, new Color32(180, 145, 82, 255));
                Ring(mesh, origin, radius - 7, radius - 15, new Color32(62, 57, 47, 255));
                Ring(mesh, origin, radius - 15, radius - 17, new Color32(227, 190, 113, 255));
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16;
                    Polygon(mesh, origin + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (radius - 11),
                        i % 4 == 0 ? 2.5f : 1f, 8, new Color32(204, 171, 103, 255));
                }
                return;
            }
            if (!Markers)
            {
                if (Square)
                {
                    Vector2 half = rectTransform.rect.size * .5f;
                    AddTerrainVertex(mesh, origin - half, -half / radius);
                    AddTerrainVertex(mesh, origin + new Vector2(-half.x, half.y), new Vector2(-half.x, half.y) / radius);
                    AddTerrainVertex(mesh, origin + half, half / radius);
                    AddTerrainVertex(mesh, origin + new Vector2(half.x, -half.y), new Vector2(half.x, -half.y) / radius);
                    mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
                    return;
                }
                const int segments = 96;
                AddTerrainVertex(mesh, origin, Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    AddTerrainVertex(mesh, origin + direction * radius, direction);
                    if (i > 0) mesh.AddTriangle(0, i, i + 1);
                }
                return;
            }

            var world = BuildingWorld.Instance;
            if (ShowBuildings && world != null)
                foreach (var building in world.Buildings.Values)
                {
                    if (building == null || !building.gameObject.activeInHierarchy || building.State == null) continue;
                    Vector2 point = WorldMapProjection.Project(building.State.position, Center, WorldRadius);
                    if (!Contains(point, radius, .94f)) continue;
                    Vector2 p = origin + point * radius;
                    // Diamond-shaped structures remain distinct from round teammate markers.
                    Polygon(mesh, p, 8f, 4, new Color32(43, 31, 22, 255));
                    Polygon(mesh, p, 6f, 4, new Color32(244, 190, 96, 255));
                    Polygon(mesh, p, 2f, 4, new Color32(67, 42, 24, 255));
                }

            var players = PlayerRegistry.All;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                bool local = player == LocalPlayerContext.Stats;
                if (!local && !ShowPlayers) continue;
                Vector2 point = WorldMapProjection.Project(player.transform.position, Center, WorldRadius);
                bool distant = !Contains(point, radius, .92f);
                Vector2 p = origin + Clamp(point, radius) * radius;
                Color32 tint = local ? new Color32(255, 250, 227, 255) :
                    player.IsAlive ? new Color32(98, 224, 211, 255) : new Color32(151, 155, 165, 255);
                Polygon(mesh, p, local ? 9f : 7f, 16, new Color32(12, 23, 27, 255));
                if (local || distant)
                {
                    Vector3 forward = player.transform.forward;
                    Vector2 heading = local ? new Vector2(forward.x, forward.z).normalized : point.normalized;
                    if (heading.sqrMagnitude < .01f) heading = Vector2.up;
                    Vector2 side = new Vector2(-heading.y, heading.x);
                    Triangle(mesh, p + heading * 8f, p - heading * 5f + side * 5f,
                        p - heading * 5f - side * 5f, tint);
                }
                else Polygon(mesh, p, 4.5f, 12, tint);
            }
            if (HasWaypoint)
            {
                Vector2 point = WorldMapProjection.Project(Waypoint, Center, WorldRadius);
                Polygon(mesh, origin + Clamp(point, radius) * radius, 9, 4, new Color32(45, 24, 40, 255));
                Polygon(mesh, origin + Clamp(point, radius) * radius, 6, 4, new Color32(235, 138, 238, 255));
            }
        }

        public bool Contains(Vector2 point, float radius, float margin) => Square ?
            Mathf.Abs(point.x) < rectTransform.rect.width * .5f / radius - .06f && Mathf.Abs(point.y) < margin :
            point.sqrMagnitude < margin * margin;

        private Vector2 Clamp(Vector2 point, float radius) => Square ? new Vector2(
            Mathf.Clamp(point.x, -rectTransform.rect.width * .5f / radius + .08f, rectTransform.rect.width * .5f / radius - .08f),
            Mathf.Clamp(point.y, -.92f, .92f)) : WorldMapProjection.ClampMarker(point);

        public Vector2 MarkerPosition(Vector3 position)
        {
            float radius = rectTransform.rect.height * .5f;
            return Clamp(WorldMapProjection.Project(position, Center, WorldRadius), radius) * radius;
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float outer, float inner, Color32 tint)
        {
            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
                Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Triangle(mesh, center + u * outer, center + v * outer, center + u * inner, tint);
                Triangle(mesh, center + v * outer, center + v * inner, center + u * inner, tint);
            }
        }

        private void AddTerrainVertex(VertexHelper mesh, Vector2 point, Vector2 direction)
        {
            Vector2 world = Center + direction * WorldRadius;
            Vector2 uv = Atlas == null ? Vector2.zero : new Vector2(
                (world.x - AtlasBounds.xMin) / AtlasBounds.width,
                (world.y - AtlasBounds.yMin) / AtlasBounds.height);
            mesh.AddVert(point, Atlas == null ? new Color(.07f, .13f, .16f) : color, uv);
        }

        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private static void Polygon(VertexHelper mesh, Vector2 center, float radius, int sides, Color32 tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                mesh.AddVert(center + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}

using UnityEngine;
namespace Duskborn.Gameplay.Building
{
    public static class PlacementValidator
    {
        private static readonly Collider[] BoxBuffer = new Collider[32];

        public static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        public static string Validate(BuildableDefinition d, Vector3 p, Quaternion rotation, Vector3 player, Transform ignored = null)
        {
            if (d == null || !Finite(p) || !Finite(rotation.eulerAngles)) return "Invalid position.";
            if (!BuildableBounds.TryGet(d, out var bounds)) return "The building model has no usable mesh.";
            if ((player - p).sqrMagnitude > d.reach * d.reach) return "Move closer to the location.";
            // Test center and all footprint corners. Reject supports which are props or stations.
            for (int i = 0; i < 5; i++)
            {
                var offset = i == 0 ? Vector3.zero : new Vector3(((i & 1) == 0 ? -1 : 1) * bounds.size.x * .48f, 0, ((i & 2) == 0 ? -1 : 1) * bounds.size.z * .48f);
                var sample = p + rotation * (new Vector3(bounds.center.x, 0, bounds.center.z) + offset);
                if (!Physics.Raycast(sample + Vector3.up * (d.groundTolerance + .05f), Vector3.down, out var hit,
                    d.groundTolerance * 2 + .1f, d.groundLayers, QueryTriggerInteraction.Ignore)) return "The entire base must touch the ground.";
                if (hit.collider.GetComponentInParent<PlacedBuilding>() != null || hit.collider.GetComponentInParent<Crafting.Workbench>() != null)
                    return "Cannot build on another station.";
                if (Mathf.Abs(hit.point.y - p.y) > d.groundTolerance) return "Uneven terrain beneath the base.";
                if (Vector3.Angle(hit.normal, Vector3.up) > d.maxSlope) return "Terrain is too steep.";
            }
            // The bottom is raised above the support tolerance; terrain intruding further is an obstacle.
            var extents = bounds.extents + new Vector3(d.clearance, 0, d.clearance);
            float bottomInset = Mathf.Min(d.groundTolerance + .02f, extents.y * .5f);
            extents.y -= bottomInset * .5f;
            var center = p + BuildableBounds.GroundOffset(bounds) + rotation * bounds.center + Vector3.up * bottomInset * .5f;
            int count = Physics.OverlapBoxNonAlloc(center, extents, BoxBuffer, rotation, d.blockingLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = BoxBuffer[i];
                BoxBuffer[i] = null;
                if (ignored != null && collider.transform.IsChildOf(ignored)) continue;
                // Clear remaining refs
                for (int j = i + 1; j < count; j++) BoxBuffer[j] = null;
                return "An obstacle is in the area or there is insufficient clearance.";
            }
            if (d.rules != null) foreach (var rule in d.rules)
            {
                if (rule == null) continue;
                string reason = rule.Validate(d, p, rotation);
                if (!string.IsNullOrEmpty(reason)) return reason;
            }
            return null;
        }
    }
}

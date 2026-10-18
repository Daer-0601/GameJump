using System.Collections.Generic;
using UnityEngine;

namespace Gamejam2026
{
    // Small collision grid for this one existing map; no NavMesh package or scene rebuild.
    internal sealed class RecintoShadowPath
    {
        const int Width = 51, Height = 86;
        const float Cell = 1.5f;
        readonly bool[] open = new bool[Width * Height];
        readonly int[] previous = new int[Width * Height];
        readonly Collider[] buffer = new Collider[32];
        readonly Queue<int> frontier = new Queue<int>();
        Vector3 Point(int cell) => new Vector3(-35.5f + cell % Width * Cell, 0, -9.5f + cell / Width * Cell);
        static bool Obstacle(Collider collider) => collider && !collider.GetComponentInParent<DemoPlayer>() && !collider.GetComponentInParent<CarryObject>()
            && !collider.GetComponentInParent<RecintoStation>() && !collider.GetComponentInParent<RecintoContamination>();
        public void Build()
        {
            Physics.SyncTransforms();
            for (int i = 0; i < open.Length; i++)
            {
                int count = Physics.OverlapSphereNonAlloc(Point(i) + Vector3.up * .4f, .27f, buffer, ~0, QueryTriggerInteraction.Ignore);
                open[i] = count < buffer.Length;
                for (int n = 0; n < count && open[i]; n++) if (Obstacle(buffer[n])) open[i] = false;
            }
        }
        int NearestIndex(Vector3 point)
        {
            int result = -1; float closest = float.MaxValue;
            for (int i = 0; i < open.Length; i++)
            {
                if (!open[i]) continue;
                float distance = (Point(i) - point).sqrMagnitude;
                if (distance < closest) { closest = distance; result = i; }
            }
            return result;
        }
        public Vector3 Nearest(Vector3 point) { int i = NearestIndex(point); return i < 0 ? point : Point(i); }

        // Celda abierta más cercana al punto pedido, fuera del radio de los jugadores.
        public bool TryStand(Vector3 preferred, Vector3[] avoid, float clearance, out Vector3 stand)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            float limit = clearance * clearance;
            for (int i = 0; i < open.Length; i++)
            {
                if (!open[i]) continue;
                Vector3 point = Point(i);
                bool crowded = false;
                if (avoid != null && limit > 0f)
                {
                    for (int a = 0; a < avoid.Length; a++)
                    {
                        if ((point - avoid[a]).sqrMagnitude < limit) { crowded = true; break; }
                    }
                }
                if (crowded) continue;
                float score = (point - preferred).sqrMagnitude;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            stand = preferred;
            if (best < 0) return false;
            stand = Point(best);
            return true;
        }
        bool Connected(int a, int b)
        {
            Vector3 from = Point(a) + Vector3.up * .4f, delta = Point(b) - Point(a);
            foreach (var hit in Physics.SphereCastAll(from, .24f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (Obstacle(hit.collider)) return false;
            return true;
        }

        public bool ClearSight(Vector3 a, Vector3 b, float radius = 0.26f)
        {
            Vector3 from = a + Vector3.up * 0.4f;
            Vector3 to = b + Vector3.up * 0.4f;
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 0.05f) return true;
            var hits = Physics.SphereCastAll(from, radius, delta.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (Obstacle(hits[i].collider)) return false;
            }
            return true;
        }

        public void Route(Vector3 from, Vector3 to, Queue<Vector3> result)
        {
            result.Clear(); frontier.Clear();
            int start = NearestIndex(from), end = NearestIndex(to);
            if (start < 0 || end < 0) return;
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            previous[start] = start; frontier.Enqueue(start);
            while (frontier.Count > 0 && previous[end] < 0)
            {
                int cell = frontier.Dequeue();
                for (int n = 0; n < 4; n++)
                {
                    int x = cell % Width + (n == 0 ? 1 : n == 1 ? -1 : 0);
                    int z = cell / Width + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (x < 0 || z < 0 || x >= Width || z >= Height) continue;
                    int next = z * Width + x;
                    if (!open[next] || previous[next] >= 0 || !Connected(cell, next)) continue;
                    previous[next] = cell; frontier.Enqueue(next);
                }
            }
            if (previous[end] < 0) return;
            var reverse = new List<Vector3>();
            for (int at = end; at != start; at = previous[at]) reverse.Add(Point(at));

            if (reverse.Count <= 2)
            {
                for (int i = reverse.Count - 1; i >= 0; i--) result.Enqueue(reverse[i]);
                return;
            }

            var direct = new List<Vector3>(reverse.Count);
            for (int i = reverse.Count - 1; i >= 0; i--) direct.Add(reverse[i]);

            int curr = 0;
            result.Enqueue(direct[0]);
            while (curr < direct.Count - 1)
            {
                int furthest = curr + 1;
                int maxLookAhead = Mathf.Min(direct.Count - 1, curr + 8);
                for (int check = maxLookAhead; check > curr + 1; check--)
                {
                    if (ClearSight(direct[curr], direct[check]))
                    {
                        furthest = check;
                        break;
                    }
                }
                result.Enqueue(direct[furthest]);
                curr = furthest;
            }
        }
    }
}

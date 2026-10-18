using System.Collections.Generic;
using UnityEngine;

namespace Gamejam2026
{
    // Cruces y roces que no suman peligro. El anfitrión decide; la presentación no toca la sombra.
    public class RecintoPresencia : MonoBehaviour
    {
        enum Phase { Hidden, Crossing, Startled }

        RecintoInvestigation owner;
        DemoGame game;
        RecintoShadowPath grid;
        AudioClip tick;
        AudioClip crawlClip;
        GameObject body;
        Animator animator;
        AudioSource crawl;
        readonly List<Material> materials = new List<Material>();
        readonly Queue<Vector3> route = new Queue<Vector3>();
        readonly Queue<Vector3> scratch = new Queue<Vector3>();
        Phase phase = Phase.Hidden;
        Vector3 goal;
        float nextAt = 26f;
        float hideAt;
        float startleUntil;
        float retryAt;
        int glimpses;
        int rustles;
        bool edgeTold;
        bool rustleTold;
        bool startled;
        bool bound;

        public bool Active => phase != Phase.Hidden;
        public int Glimpses => glimpses;
        public int Rustles => rustles;

        public void Bind(RecintoInvestigation investigation, DemoGame ownerGame, RecintoThreatPresentation threat)
        {
            owner = investigation;
            game = ownerGame;
            grid = investigation ? investigation.Grid : null;
            tick = threat ? threat.knock : null;
            crawlClip = Resources.Load<AudioClip>("Production/Crawl");
            if (bound) return;
            var source = Resources.Load<GameObject>("Production/Shadow");
            if (!source || !owner)
            {
                Debug.LogWarning("RecintoPresencia: no hay Production/Shadow. El cruce breve no se crea; la sombra que hace daño sigue igual.");
                return;
            }
            body = Instantiate(source, owner.transform);
            body.name = "Silueta - cruce breve";
            owner.Adopt(body);
            animator = body.GetComponentInChildren<Animator>();
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                if (!renderer.sharedMaterial) continue;
                var material = new Material(renderer.sharedMaterial);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.02f, 0.025f, 0.02f));
                renderer.sharedMaterial = material;
                materials.Add(material);
            }
            crawl = body.AddComponent<AudioSource>();
            crawl.playOnAwake = false;
            crawl.spatialBlend = 0f;
            crawl.loop = true;
            crawl.clip = crawlClip;
            Show(false);
            bound = true;
            nextAt = Mathf.Max(12f, owner.presenceFirstSeconds);
        }

        public void Present(Vector3 from, Vector3 to, byte step)
        {
            if (step == 0) Begin(from, to);
            else if (step == 1) Startle();
            else End();
        }

        void OnDestroy()
        {
            if (crawl && crawl.isPlaying) crawl.Stop();
            for (int i = 0; i < materials.Count; i++) if (materials[i]) Destroy(materials[i]);
            materials.Clear();
        }

        void Update()
        {
            if (!bound || !game) return;
            if (!game.Playing)
            {
                if (phase != Phase.Hidden) End();
                return;
            }
            if (LanAuthority.Simulates) Consider();
            if (phase != Phase.Hidden) Advance();
            MixCrawl();
        }

        void Consider()
        {
            if (phase != Phase.Hidden || grid == null || !owner) return;
            if (game.ElapsedSeconds < nextAt || !owner.ScareWindowOpen) return;
            if (Time.unscaledTime < retryAt) return;
            retryAt = Time.unscaledTime + 1.2f;
            int glimpseCap = Mathf.Max(1, owner.maxSilhouettes);
            int rustleCap = Mathf.Max(0, owner.maxRustles);
            if (glimpses >= glimpseCap && rustles >= rustleCap) return;
            bool preferGlimpse = owner.ShadowRevealed && glimpses < glimpseCap && glimpses <= rustles;
            if (preferGlimpse && TryGlimpse(out Vector3 from, out Vector3 to))
            {
                glimpses++;
                ArmCooldown();
                if (tick) owner.Sound(tick, from, 0.14f);
                Raise(0, from, to);
                return;
            }
            if (rustles < rustleCap && tick && TryRustle(out Vector3 point))
            {
                rustles++;
                ArmCooldown();
                owner.Sound(tick, point, 0.16f);
                if (!rustleTold)
                {
                    rustleTold = true;
                    game.WarnThreat("Un roce metálico, más adelante.");
                }
            }
        }

        void ArmCooldown()
        {
            float wait = Mathf.Max(20f, owner.presenceCooldown);
            nextAt = game.ElapsedSeconds + wait;
            owner.HoldQuiet(12f);
        }

        void Raise(byte step, Vector3 from, Vector3 to)
        {
            if (!LanEcho.Applying && LanAuthority.IsHost) LanSession.BroadcastGlimpse(from, to, step);
            Present(from, to, step);
        }

        void Begin(Vector3 from, Vector3 to)
        {
            if (!body || grid == null) return;
            goal = to;
            body.transform.position = from;
            grid.Route(from, to, route);
            if (route.Count == 0)
            {
                End();
                return;
            }
            phase = Phase.Crossing;
            startled = false;
            startleUntil = 0f;
            hideAt = game.ElapsedSeconds + 3.6f;
            Show(true);
            Play("Crawl");
            if (crawl && crawlClip && !crawl.isPlaying) crawl.Play();
        }

        void Startle()
        {
            if (!body || phase == Phase.Hidden) return;
            phase = Phase.Startled;
            startled = true;
            startleUntil = game.ElapsedSeconds + 0.62f;
            Play("Attack");
            if (LanAuthority.Simulates && !edgeTold)
            {
                edgeTold = true;
                game.WarnThreat("Una silueta cruzó al borde. No os ha tocado.");
            }
        }

        void End()
        {
            phase = Phase.Hidden;
            route.Clear();
            startled = false;
            if (crawl && crawl.isPlaying) crawl.Stop();
            Show(false);
        }

        void Advance()
        {
            if (!body) return;
            if (phase == Phase.Startled && game.ElapsedSeconds < startleUntil) return;
            if (phase == Phase.Startled)
            {
                phase = Phase.Crossing;
                Play("Crawl");
            }
            bool finished = game.ElapsedSeconds >= hideAt || route.Count == 0;
            if (!finished && route.Count > 0)
            {
                Vector3 delta = route.Peek() - body.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.002f)
                {
                    body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 7f);
                    float facing = Mathf.Clamp01(Vector3.Dot(body.transform.forward, delta.normalized));
                    float turnFactor = Mathf.Lerp(0.35f, 1f, facing * facing);
                    float speed = (startled ? 2.1f : 1.5f) * turnFactor;
                    float step = Mathf.Min(delta.magnitude, Time.deltaTime * speed);
                    body.transform.position += delta.normalized * step;
                }
                if (Physics.Raycast(body.transform.position + Vector3.up * 0.8f, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Vector3 pos = body.transform.position;
                    pos.y = Mathf.Lerp(pos.y, hit.point.y, Time.deltaTime * 12f);
                    body.transform.position = pos;
                }
                if (delta.magnitude < 0.22f) route.Dequeue();
                finished = route.Count == 0;
            }
            if (!LanAuthority.Simulates) return;
            Vector3 chest = body.transform.position + Vector3.up * 0.35f;
            if (TooClose(2.25f) || finished)
            {
                Raise(2, Vector3.zero, Vector3.zero);
                return;
            }
            if (!startled && LookingAt(chest)) Raise(1, body.transform.position, goal);
            else if (!edgeTold && AtEdge(chest))
            {
                edgeTold = true;
                game.WarnThreat("Una silueta cruzó al borde. No os ha tocado.");
            }
        }

        void Play(string state)
        {
            if (!animator) return;
            animator.speed = 1f;
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state))
                animator.CrossFade(state, 0.16f);
        }

        void Show(bool value)
        {
            if (!body) return;
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true)) renderer.enabled = value;
        }

        void MixCrawl()
        {
            if (!crawl || !game) return;
            if (phase == Phase.Hidden || !crawlClip)
            {
                crawl.volume = 0f;
                return;
            }
            DemoPlayer ear = null;
            float nearest = float.MaxValue;
            if (game.players != null)
            {
                foreach (var player in game.players)
                {
                    if (!player || !player.view) continue;
                    float distance = Vector3.Distance(player.view.transform.position, body.transform.position);
                    if (distance < nearest) { nearest = distance; ear = player; }
                }
            }
            if (!ear)
            {
                crawl.volume = 0f;
                return;
            }
            Vector3 delta = body.transform.position - ear.view.transform.position;
            crawl.panStereo = Mathf.Clamp(Vector3.Dot(ear.view.transform.right, delta.sqrMagnitude > 0.01f ? delta.normalized : Vector3.forward), -0.6f, 0.6f);
            crawl.volume = game.sfxVolume * 0.05f * Mathf.Clamp01(1f - nearest / 16f);
        }

        bool TryGlimpse(out Vector3 from, out Vector3 to)
        {
            from = to = default;
            DemoPlayer player = PickPlayer();
            if (!player) return false;
            Vector3 forward = Flat(player.transform.forward);
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3[] avoid = Feet();
            float[] sides = { 1f, -1f };
            float[] ahead = { 4.4f, 2.4f };
            float[] lateral = { 6.6f, 4.8f };
            for (int s = 0; s < sides.Length; s++)
            {
                for (int a = 0; a < ahead.Length; a++)
                {
                    for (int l = 0; l < lateral.Length; l++)
                    {
                        Vector3 preferred = player.transform.position + forward * ahead[a] + side * sides[s] * lateral[l];
                        if (!grid.TryStand(preferred, avoid, 3.3f, out Vector3 stand)) continue;
                        if ((stand - preferred).sqrMagnitude > 2.4f * 2.4f) continue;
                        float distance = Vector3.Distance(stand, player.transform.position);
                        if (distance < 5.8f || distance > 12f) continue;
                        if (shadowNear(stand) || owner.NearSetpiece(stand, 3.1f)) continue;
                        if (!Fringe(stand + Vector3.up * 0.35f)) continue;
                        Vector3 leave = stand + forward * 4.8f;
                        grid.Route(stand, leave, scratch);
                        if (scratch.Count < 2 || !RouteSafe(scratch)) continue;
                        from = stand;
                        to = leave;
                        return true;
                    }
                }
            }
            return false;
        }

        bool TryRustle(out Vector3 point)
        {
            point = default;
            DemoPlayer player = PickPlayer();
            if (!player || grid == null) return false;
            Vector3 forward = Flat(player.transform.forward);
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3[] avoid = Feet();
            Vector3[] offsets =
            {
                side * 6.5f, -side * 6.5f, forward * 8f, -forward * 5.5f, forward * 5f + side * 4f
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 preferred = player.transform.position + offsets[i];
                if (!grid.TryStand(preferred, avoid, 4f, out Vector3 stand)) continue;
                if ((stand - preferred).sqrMagnitude > 3.2f * 3.2f) continue;
                float distance = Vector3.Distance(stand, player.transform.position);
                if (distance < 5.2f || distance > 12.5f) continue;
                if (owner.NearSetpiece(stand, 1.8f) || shadowNear(stand)) continue;
                Vector3 chest = stand + Vector3.up * 0.4f;
                if (LookingAt(chest)) continue;
                point = stand;
                return true;
            }
            return false;
        }

        bool RouteSafe(Queue<Vector3> points)
        {
            foreach (var point in points)
            {
                if (TooClose(point, 2.5f) || owner.NearSetpiece(point, 1.6f)) return false;
            }
            return true;
        }

        bool Fringe(Vector3 point)
        {
            if (game.players == null) return false;
            bool fringe = false;
            foreach (var player in game.players)
            {
                if (!player || !player.view) continue;
                if (!owner.ClearBetween(player.view.transform.position, point)) continue;
                Vector3 screen = owner.Viewport(player.view, point);
                if (screen.z <= 0.3f) continue;
                bool center = screen.x > 0.3f && screen.x < 0.7f && screen.y > 0.24f && screen.y < 0.76f;
                if (center) return false;
                bool near = screen.x > -0.28f && screen.x < 1.28f && screen.y > -0.2f && screen.y < 1.2f;
                if (near) fringe = true;
            }
            return fringe;
        }

        bool LookingAt(Vector3 point)
        {
            if (game.players == null) return false;
            foreach (var player in game.players)
            {
                if (!player || !player.view) continue;
                if (Vector3.Distance(player.view.transform.position, point) > 8f) continue;
                Vector3 screen = owner.Viewport(player.view, point);
                if (screen.z <= 0.2f) continue;
                bool center = screen.x > 0.28f && screen.x < 0.72f && screen.y > 0.22f && screen.y < 0.78f;
                if (!center) continue;
                if (owner.ClearBetween(player.view.transform.position, point)) return true;
            }
            return false;
        }

        bool AtEdge(Vector3 point)
        {
            if (game.players == null) return false;
            foreach (var player in game.players)
            {
                if (!player || !player.view) continue;
                Vector3 screen = owner.Viewport(player.view, point);
                if (screen.z <= 0.2f) continue;
                bool onScreen = screen.x > 0.02f && screen.x < 0.98f && screen.y > 0.04f && screen.y < 0.96f;
                bool center = screen.x > 0.28f && screen.x < 0.72f && screen.y > 0.22f && screen.y < 0.78f;
                if (!onScreen || center) continue;
                if (owner.ClearBetween(player.view.transform.position, point)) return true;
            }
            return false;
        }

        bool TooClose(float radius) => body && TooClose(body.transform.position, radius);

        bool TooClose(Vector3 point, float radius)
        {
            if (game.players == null) return false;
            float limit = radius * radius;
            foreach (var player in game.players)
            {
                if (!player) continue;
                Vector3 delta = player.transform.position - point;
                delta.y = 0f;
                if (delta.sqrMagnitude < limit) return true;
            }
            return false;
        }

        bool shadowNear(Vector3 point)
        {
            Vector3 threat = owner.ThreatPoint;
            if (threat.y < -20f) return false;
            return Vector3.Distance(threat, point) < 8f;
        }

        DemoPlayer PickPlayer()
        {
            if (game.players == null) return null;
            DemoPlayer best = null;
            float score = -1f;
            foreach (var player in game.players)
            {
                if (!player) continue;
                float apart = 0f;
                foreach (var other in game.players)
                {
                    if (!other || other == player) continue;
                    apart = Mathf.Max(apart, Vector3.Distance(player.transform.position, other.transform.position));
                }
                if (apart >= score) { score = apart; best = player; }
            }
            return best;
        }

        Vector3[] Feet()
        {
            int count = game.players != null ? game.players.Length : 0;
            var avoid = new Vector3[count];
            for (int i = 0; i < count; i++)
                if (game.players[i]) avoid[i] = game.players[i].transform.position;
            return avoid;
        }

        static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        }
    }
}

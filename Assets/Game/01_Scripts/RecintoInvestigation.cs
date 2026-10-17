using System.Collections.Generic;
using UnityEngine;

namespace Gamejam2026
{
    // Run-level investigation. Locations and station roles are shuffled on every reload.
    public class RecintoInvestigation : MonoBehaviour
    {
        static readonly Vector3[] Sites =
        {
            new Vector3(-19f, 0f, 27f), new Vector3(-4f, 0f, 46f),
            new Vector3(15f, 0f, 64f), new Vector3(29f, 0f, 76f),
            new Vector3(5f, 0f, 84f), new Vector3(-25f, 0f, 90f),
            new Vector3(-23f, 0f, 110f), new Vector3(34f, 0f, 106f)
        };
        DemoGame game;
        readonly List<RecintoStation> stations = new List<RecintoStation>();
        readonly bool[] evidence = new bool[3];
        readonly List<TextMesh> signs = new List<TextMesh>();
        readonly List<string> signLabels = new List<string>();
        bool synchronized;
        Material metal;
        Material darkLamp;
        Material activeLamp;
        Material stain;
        bool ownsStain;
        Material insects;
        Material larva;
        Material shadowMaterial;
        Material bodyMaterial;
        readonly List<Material> eldritchMaterials = new List<Material>();
        GameObject eldritch;
        bool eldritchSoundPlayed;
        static readonly Vector3[] EldritchSites = { new Vector3(-19f, 0f, 104f), new Vector3(31f, 0f, 108f) };
        GameObject depotBody;
        readonly GameObject[] coveredBodies = new GameObject[2];
        readonly bool[] coveredBodySoundPlayed = new bool[2];
        Quaternion bodyModelRotation;
        Quaternion shadowModelRotation;
        bool depotBodySeen;
        bool depotBodyMoved;
        bool depotBodyMayMove;
        GameObject shadow;
        Vector3 shadowStart;
        Vector3 shadowEnd;
        float shadowGroundOffset;
        float shadowStarted;
        float shadowDuration;
        Quaternion shadowHeading;
        bool shadowCrossing;
        readonly bool[] usedCrossingSites = new bool[2];
        static readonly Vector3[] BodySites = { new Vector3(-15f, 0f, 24f), new Vector3(24f, 0f, 70f), new Vector3(-12f, 0f, 82f), new Vector3(31f, 0f, 105f) };
        static readonly Vector3[] CrossingStarts = { new Vector3(-10.5f, 0f, 32.8f), new Vector3(-8.5f, 0f, 90.8f) };
        static readonly Vector3[] CrossingEnds = { new Vector3(0.5f, 0f, 32.8f), new Vector3(0.5f, 0f, 90.8f) };
        static readonly Vector3[] CrossingWitnesses = { new Vector3(-5f, 1f, 32.8f), new Vector3(-4f, 1f, 90.8f) };
        AudioSource fright;
        float nextShadow;
        int shadowEvents;
        int mistakes;
        int alteredSign = -1;
        float restoreSignAt;
        public int EvidenceCount { get; private set; }
        public bool Contrasted => synchronized;
        public int RequiredPower { get; private set; }
        public int PoweredCount
        {
            get { int count = 0; foreach (var station in stations) if (station && station.IsPowerOn) count++; return count; }
        }
        public bool Ready => EvidenceCount == 3 && synchronized && PoweredCount >= RequiredPower;
        public string EvidenceSummary => "1:" + (evidence[0] ? game.SymbolAt(0) : "?") + "  2:" + (evidence[1] ? game.SymbolAt(1) : "?") + "  3:" + (evidence[2] ? game.SymbolAt(2) : "?");
        public Material Metal => metal;
        public Material DarkLamp => darkLamp;
        public Material ActiveLamp => activeLamp;

        public void Initialize(DemoGame owner)
        {
            game = owner;
            metal = MakeMaterial("Metal húmedo", new Color(0.12f, 0.13f, 0.12f));
            darkLamp = MakeMaterial("Indicador apagado", new Color(0.09f, 0.12f, 0.09f));
            activeLamp = MakeMaterial("Indicador activo", new Color(0.42f, 0.68f, 0.35f));
            // Match the surface treatment already used in the map.
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                if (renderer.name == "Humedad superficial" && renderer.sharedMaterial)
                { stain = renderer.sharedMaterial; break; }
            if (!stain) { stain = MakeMaterial("Humedad orgánica", new Color(0.045f, 0.065f, 0.035f)); ownsStain = true; }
            insects = MakeMaterial("Insectos", new Color(0.025f, 0.025f, 0.018f));
            larva = MakeMaterial("Larvas pálidas", new Color(0.43f, 0.38f, 0.27f));
            var shadowTexture = Resources.Load<Texture2D>("Horror/CreepingShadow_BaseColor");
            if (!shadowTexture) Debug.LogError("Falta la textura original de Creeping Shadow.");
            shadowMaterial = MakeMaterial("Piel original de Creeping Shadow", Color.white);
            if (shadowTexture) shadowMaterial.SetTexture("_BaseMap", shadowTexture);
            fright = gameObject.AddComponent<AudioSource>();
            fright.playOnAwake = false;
            fright.spatialBlend = 0f; // Both split-screen players hear the event.
            RequiredPower = Random.Range(0, 3);

            var slots = new List<int>();
            for (int i = 0; i < Sites.Length; i++) slots.Add(i);
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = slots[i]; slots[i] = slots[j]; slots[j] = swap;
            }
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("Puesto de investigación variable");
                root.transform.position = Sites[slots[i]];
                root.transform.rotation = Quaternion.Euler(0f, slots[i] % 2 == 0 ? 25f : -35f, 0f);
                root.transform.SetParent(transform, true);
                var station = root.AddComponent<RecintoStation>();
                var role = i < 3 ? RecintoStation.Role.Evidence : i == 3 ? RecintoStation.Role.Synchronize : RecintoStation.Role.Power;
                station.Configure(this, game, role, i, i % 2 == 0);
                stations.Add(station);
            }
            // Organic decay is confined to a few recognizable pockets, not tiled across every wall.
            CreateDecay(Sites[slots[0]] + new Vector3(1.2f, 0f, 0.8f), 6);
            CreateDecay(Sites[slots[3]] + new Vector3(-1f, 0f, 0.6f), 9);
            CreateDecay(Sites[slots[5]] + new Vector3(1f, 0f, -0.8f), 5);
            AddExistingClutter(slots);
            CreateCoveredBodies();
            CreateEldritchAnomaly();
            PrepareSigns();
            CreateShadow();
            nextShadow = 3f;
        }

        static Material MakeMaterial(string label, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = label, color = color };
            material.SetFloat("_Smoothness", 0.12f);
            return material;
        }

        void CreateDecay(Vector3 center, int count)
        {
            var patch = new GameObject("Brote de humedad e insectos");
            patch.transform.SetParent(transform, false);
            patch.transform.position = center;
            for (int i = 0; i < 4; i++)
            {
                var blotch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blotch.name = "Mancha orgánica";
                blotch.transform.SetParent(patch.transform, false);
                blotch.transform.localPosition = new Vector3(Random.Range(-0.85f, 0.85f), 0.02f, Random.Range(-0.6f, 0.6f));
                blotch.transform.localScale = new Vector3(Random.Range(0.5f, 1.2f), 0.025f, Random.Range(0.35f, 0.8f));
                blotch.GetComponent<Renderer>().sharedMaterial = stain;
                Destroy(blotch.GetComponent<Collider>());
            }
            for (int i = 0; i < count; i++)
            {
                var worm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                worm.name = "Larva";
                worm.transform.SetParent(patch.transform, false);
                worm.transform.localPosition = new Vector3(Random.Range(-0.8f, 0.8f), 0.045f, Random.Range(-0.55f, 0.55f));
                worm.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                worm.transform.localScale = new Vector3(0.035f, Random.Range(0.07f, 0.13f), 0.035f);
                worm.GetComponent<Renderer>().sharedMaterial = larva;
                Destroy(worm.GetComponent<Collider>());
                worm.AddComponent<RecintoWorm>().Initialize(Random.value * 6.28f);
            }
            var swarm = new GameObject("Moscas");
            swarm.transform.SetParent(patch.transform, false);
            swarm.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var particles = swarm.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
            main.startColor = new Color(0.025f, 0.025f, 0.02f);
            main.maxParticles = 24;
            var emission = particles.emission;
            emission.rateOverTime = 6f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.65f;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = insects;
            particles.Play();
        }

        void AddExistingClutter(List<int> slots)
        {
            GameObject barrel = null, pallet = null;
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!barrel && candidate.name == "Barrel") barrel = candidate.gameObject;
                if (!pallet && candidate.name == "Pallet") pallet = candidate.gameObject;
                if (barrel && pallet) break;
            }
            if (!barrel && !pallet) return;
            for (int i = 0; i < 4; i++)
            {
                var source = i % 2 == 0 ? barrel : pallet;
                if (!source) source = barrel ? barrel : pallet;
                var prop = Instantiate(source, transform);
                prop.name = i % 2 == 0 ? "Barril recuperado" : "Palé abandonado";
                prop.transform.SetPositionAndRotation(Sites[slots[i + 2]] + new Vector3(i % 2 == 0 ? 1.6f : -1.6f, 0f, 1.1f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                foreach (var collider in prop.GetComponentsInChildren<Collider>()) Destroy(collider);
                float bottom = float.MaxValue;
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (bottom < float.MaxValue) prop.transform.position += Vector3.up * (0.03f - bottom);
            }
        }

        void CreateShadow()
        {
            var source = Resources.Load<GameObject>("Horror/CreepingShadow");
            if (!source)
            {
                Debug.LogError("Falta Assets/Resources/Horror/CreepingShadow.fbx para las apariciones del recinto.");
                return;
            }
            shadow = Instantiate(source);
            shadowModelRotation = shadow.transform.rotation;
            shadow.name = "Ente - cruza una abertura";
            shadow.transform.SetParent(transform, true);
            foreach (var collider in shadow.GetComponentsInChildren<Collider>()) Destroy(collider);
            var renderers = shadow.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) Debug.LogError("CreepingShadow no contiene mallas visibles.");
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = shadowMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Normalize the downloaded model so encounter coordinates stay independent of export scale.
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f)
            {
                shadow.transform.localScale *= 1.9f / bounds.size.y;
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                shadowGroundOffset = -bounds.min.y;
            }
            shadow.SetActive(false);
        }

        void CreateCoveredBodies()
        {
            var source = Resources.Load<GameObject>("Horror/DeadBodyCovered");
            var texture = Resources.Load<Texture2D>("Horror/DeadBodyCovered_BaseColor");
            if (!source || !texture)
            {
                Debug.LogError("Faltan el modelo o la textura de DeadBodyCovered en Assets/Resources/Horror.");
                return;
            }
            bodyMaterial = MakeMaterial("Tela de cuerpo cubierto", Color.white);
            bodyMaterial.SetTexture("_BaseMap", texture);
            bodyModelRotation = source.transform.rotation;
            depotBodyMayMove = Random.value < 0.75f;
            int second = Random.Range(1, BodySites.Length);
            int[] selected = { 0, second };
            foreach (int index in selected)
            {
                var body = Instantiate(source);
                body.name = "Cuerpo cubierto - hallazgo " + index;
                if (index == 0) depotBody = body;
                coveredBodies[index == 0 ? 0 : 1] = body;
                body.transform.SetParent(transform, true);
                body.transform.SetPositionAndRotation(BodySites[index], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * bodyModelRotation);
                foreach (var collider in body.GetComponentsInChildren<Collider>()) Destroy(collider);
                var renderers = body.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { Debug.LogError("DeadBodyCovered no tiene malla visible."); continue; }
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = bodyMaterial;
                    renderer.sharedMaterials = materials;
                }
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                if (bounds.size.x > 0.001f && bounds.size.z > 0.001f)
                    body.transform.localScale *= 2.3f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                body.transform.position += Vector3.up * (0.03f - bounds.min.y);
            }
        }

        void CreateEldritchAnomaly()
        {
            var source = Resources.Load<GameObject>("Horror/LovecraftianHorror");
            var bodyTexture = Resources.Load<Texture2D>("Horror/LovecraftBody");
            var gumsTexture = Resources.Load<Texture2D>("Horror/LovecraftGums");
            var teethTexture = Resources.Load<Texture2D>("Horror/LovecraftTeeth");
            if (!source || !bodyTexture || !gumsTexture || !teethTexture)
            {
                Debug.LogError("Faltan recursos de LovecraftianHorror en Assets/Resources/Horror.");
                return;
            }
            var skin = MakeMaterial("Piel del ente", Color.white);
            skin.SetTexture("_BaseMap", bodyTexture);
            var gums = MakeMaterial("Encías del ente", Color.white);
            gums.SetTexture("_BaseMap", gumsTexture);
            var teeth = MakeMaterial("Dientes del ente", Color.white);
            teeth.SetTexture("_BaseMap", teethTexture);
            var eyes = MakeMaterial("Ojos del ente", new Color(0.14f, 0.035f, 0.035f));
            eldritchMaterials.Add(skin);
            eldritchMaterials.Add(gums);
            eldritchMaterials.Add(teeth);
            eldritchMaterials.Add(eyes);
            eldritch = Instantiate(source);
            eldritch.name = "Anomalía persistente - ente del recinto";
            eldritch.transform.SetParent(transform, true);
            int site = Random.Range(0, EldritchSites.Length);
            Quaternion modelRotation = eldritch.transform.rotation;
            eldritch.transform.SetPositionAndRotation(EldritchSites[site], Quaternion.Euler(0f, site == 0 ? 145f : -80f, 0f) * modelRotation);
            foreach (var collider in eldritch.GetComponentsInChildren<Collider>()) Destroy(collider);
            var renderers = eldritch.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogError("LovecraftianHorror no contiene mallas visibles."); return; }
            foreach (var renderer in renderers)
            {
                Material chosen = skin;
                if (renderer.name.Contains("29013")) chosen = eyes;
                else if (renderer.name.Contains("63675")) chosen = gums;
                else if (renderer.name.Contains("33359")) chosen = teeth;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = chosen;
                renderer.sharedMaterials = materials;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f) eldritch.transform.localScale *= 2.4f / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            eldritch.transform.position += Vector3.up * (0.03f - bounds.min.y);
            var glow = new GameObject("Reflejo tenue del ente");
            glow.transform.SetParent(eldritch.transform, false);
            glow.transform.localPosition = Vector3.up * 0.18f;
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 0.19f, 0.15f);
            light.intensity = 0.32f;
            light.range = 3f;
            light.shadows = LightShadows.None;
            Debug.Log("ANOMALIA_ENTE_CREADA zona=" + site);
        }

        void PrepareSigns()
        {
            string[] names = { "Cartel ENTRADA", "Cartel CARGA / DEPOSITO", "Cartel PATIO INTERIOR", "Cartel TALLER", "Cartel BOMBAS", "Cartel MANIVELA" };
            string[] labels = { "ACCESO 01", "ALMACÉN 02", "PATIO 03", "TALLER 04", "BOMBAS 05", "ÚTILES" };
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude))
                for (int i = 0; i < names.Length; i++)
                    if (text.name == names[i])
                    {
                        text.text = labels[i];
                        signs.Add(text);
                        signLabels.Add(labels[i]);
                        break;
                    }
        }

        void Update()
        {
            if (!game || !game.Playing)
            {
                if (shadow) shadow.SetActive(false);
                shadowCrossing = false;
                if (fright && fright.isPlaying) fright.Stop();
                return;
            }
            if (alteredSign >= 0 && Time.time >= restoreSignAt
                && (!signs[alteredSign] || BothPlayersAwayAndNotWatching(signs[alteredSign].transform.position, 9f)))
            {
                if (signs[alteredSign]) signs[alteredSign].text = signLabels[alteredSign];
                alteredSign = -1;
            }
            UpdateCoveredBody();
            UpdateCoveredBodySounds();
            UpdateEldritchAnomaly();
            if (shadowCrossing)
            {
                float t = Mathf.Clamp01((Time.time - shadowStarted) / shadowDuration);
                float stride = Mathf.Sin(t * 30f);
                shadow.transform.position = Vector3.Lerp(shadowStart, shadowEnd, t) + Vector3.up * (shadowGroundOffset + Mathf.Abs(stride) * 0.045f);
                shadow.transform.rotation = shadowHeading * Quaternion.Euler(0f, 0f, stride * 3f) * shadowModelRotation;
                if (t >= 1f) { shadow.SetActive(false); shadowCrossing = false; }
                return;
            }
            if (!shadow || shadowEvents >= CrossingStarts.Length || game.ElapsedSeconds < nextShadow) return;
            if (!TryStartCrossing()) nextShadow = game.ElapsedSeconds + 2f;
        }

        bool TryStartCrossing()
        {
            int first = Random.Range(0, CrossingStarts.Length);
            for (int attempt = 0; attempt < CrossingStarts.Length; attempt++)
            {
                int site = (first + attempt) % CrossingStarts.Length;
                if (usedCrossingSites[site] || !HasCrossingWitness(site)) continue;
                // The first route always offers a sighting; later routes vary by run.
                if (shadowEvents > 0 && Random.value < 0.4f)
                {
                    usedCrossingSites[site] = true;
                    nextShadow = game.ElapsedSeconds + 12f;
                    return true;
                }
                usedCrossingSites[site] = true;
                shadowStart = CrossingStarts[site];
                shadowEnd = CrossingEnds[site];
                if (Random.value < 0.5f) { var swap = shadowStart; shadowStart = shadowEnd; shadowEnd = swap; }
                shadow.transform.position = shadowStart + Vector3.up * shadowGroundOffset;
                shadowHeading = Quaternion.LookRotation((shadowEnd - shadowStart).normalized, Vector3.up);
                shadow.transform.rotation = shadowHeading * shadowModelRotation;
                shadowDuration = Random.Range(1.15f, 1.55f);
                shadowStarted = Time.time;
                shadowCrossing = true;
                shadow.SetActive(true);
                shadowEvents++;
                nextShadow = game.ElapsedSeconds + Random.Range(45f, 70f);
                if (game.jumpScareClip) fright.PlayOneShot(game.jumpScareClip, game.sfxVolume * 0.23f);
                Debug.Log("ANOMALIA_CRUCE zona=" + site + " tiempo=" + game.ElapsedSeconds.ToString("F1"));
                return true;
            }
            return false;
        }

        bool HasCrossingWitness(int site)
        {
            Vector3 opening = CrossingWitnesses[site];
            foreach (var player in game.players)
            {
                if (!player || !player.view) continue;
                Vector3 position = player.transform.position;
                if (site == 0 && (position.z < 21f || position.z > 31f || position.x < -16f || position.x > 1f)) continue;
                if (site == 1 && (position.z < 80f || position.z > 89f || position.x < -14f || position.x > 3f)) continue;
                if (Vector3.Distance(position, opening) > 10f) continue;
                if (!IsInView(player.view, opening)) continue;
                return true;
            }
            return false;
        }

        void UpdateCoveredBody()
        {
            if (!depotBody || depotBodyMoved || !depotBodyMayMove) return;
            bool nearby = false;
            Vector3 destination = new Vector3(-12.2f, depotBody.transform.position.y, 28f);
            foreach (var player in game.players)
            {
                if (!player) continue;
                float distance = Vector3.Distance(player.transform.position, depotBody.transform.position);
                if (distance < 7f) nearby = true;
            }
            if (nearby) depotBodySeen = true;
            if (!depotBodySeen || !BothPlayersAwayAndNotWatching(depotBody.transform.position + Vector3.up * 0.35f, 9f)
                || !BothPlayersAwayAndNotWatching(destination + Vector3.up * 0.35f, 9f)) return;
            depotBody.transform.SetPositionAndRotation(destination, Quaternion.Euler(0f, 105f, 0f) * bodyModelRotation);
            depotBodyMoved = true;
            coveredBodySoundPlayed[0] = false;
            Debug.Log("ANOMALIA_CUERPO_MOVIDO tiempo=" + game.ElapsedSeconds.ToString("F1"));
        }

        void UpdateCoveredBodySounds()
        {
            for (int i = 0; i < coveredBodies.Length; i++)
            {
                var body = coveredBodies[i];
                if (!body || coveredBodySoundPlayed[i]) continue;
                foreach (var player in game.players)
                {
                    if (!player || Vector3.Distance(player.transform.position, body.transform.position) > 4.5f) continue;
                    coveredBodySoundPlayed[i] = true;
                    if (game.jumpScareClip) fright.PlayOneShot(game.jumpScareClip, game.sfxVolume * 0.26f);
                    break;
                }
            }
        }

        void UpdateEldritchAnomaly()
        {
            if (!eldritch || eldritchSoundPlayed) return;
            foreach (var player in game.players)
            {
                if (!player || Vector3.Distance(player.transform.position, eldritch.transform.position) > 7f) continue;
                eldritchSoundPlayed = true;
                if (game.jumpScareClip) fright.PlayOneShot(game.jumpScareClip, game.sfxVolume * 0.3f);
                break;
            }
        }

        static bool IsInView(Camera view, Vector3 worldPoint)
        {
            if (!view) return false;
            Vector3 screen = view.WorldToViewportPoint(worldPoint);
            return screen.z > 0f && screen.x > 0.07f && screen.x < 0.93f && screen.y > 0.08f && screen.y < 0.92f;
        }

        bool BothPlayersAwayAndNotWatching(Vector3 point, float minDistance)
        {
            foreach (var player in game.players)
            {
                if (!player) continue;
                if (Vector3.Distance(player.transform.position, point) < minDistance || IsInView(player.view, point)) return false;
            }
            return true;
        }

        public void RecordEvidence(int index)
        {
            if (index < 0 || index >= 3 || evidence[index]) return;
            evidence[index] = true;
            EvidenceCount++;
            PlayWorkSound(game.lightClip);
            string message = "Registro " + (index + 1) + "/3: marca " + game.SymbolAt(index) + ". Compárenla con las otras.";
            foreach (var player in game.players) if (player) player.Tell(message, 8f);
            if (Ready) game.Pulse("Procedimiento deducido");
        }

        public void RecordSynchronization()
        {
            synchronized = true;
            PlayWorkSound(game.gearsClip);
            foreach (var player in game.players) if (player) player.Tell("Consola contrastada. Revisen registros y red auxiliar.", 6f);
            if (Ready) game.Pulse("Procedimiento deducido");
        }

        public void RecordPower()
        {
            PlayWorkSound(game.lightClip);
            game.Pulse(Ready ? "Procedimiento deducido" : "Luz auxiliar encendida");
        }

        void PlayWorkSound(AudioClip clip)
        {
            if (clip && fright) fright.PlayOneShot(clip, game.sfxVolume * 0.45f);
        }

        public void OnWrongProcedure()
        {
            mistakes++;
            nextShadow = Mathf.Min(nextShadow, game.ElapsedSeconds + Random.Range(7f, 14f));
            if (signs.Count > 1)
            {
                if (alteredSign >= 0 && signs[alteredSign]) signs[alteredSign].text = signLabels[alteredSign];
                alteredSign = Random.Range(0, signs.Count);
                int other = (alteredSign + Random.Range(1, signs.Count)) % signs.Count;
                if (signs[alteredSign]) signs[alteredSign].text = signLabels[other];
                restoreSignAt = Time.time + 22f;
            }
            var powered = new List<RecintoStation>();
            foreach (var station in stations) if (station && station.IsPowerOn) powered.Add(station);
            if (powered.Count > 0)
            {
                powered[Random.Range(0, powered.Count)].Corrupt();
                foreach (var player in game.players) if (player) player.Tell("Una luz auxiliar se apagó tras el error.");
            }
            else
            {
                CreateDecay(stations[Random.Range(0, stations.Count)].transform.position + Vector3.right, 4);
                foreach (var player in game.players) if (player) player.Tell("La contaminación avanzó hacia otro puesto.");
            }
        }

        void OnDestroy()
        {
            if (metal) Destroy(metal);
            if (darkLamp) Destroy(darkLamp);
            if (activeLamp) Destroy(activeLamp);
            if (ownsStain && stain) Destroy(stain);
            if (insects) Destroy(insects);
            if (larva) Destroy(larva);
            if (shadowMaterial) Destroy(shadowMaterial);
            if (bodyMaterial) Destroy(bodyMaterial);
            foreach (var material in eldritchMaterials) if (material) Destroy(material);
        }
    }
}

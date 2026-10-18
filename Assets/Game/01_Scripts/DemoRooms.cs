using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamejam2026
{
    // Candle-readable identity for the two final rooms. Uses the existing signs and mechanisms.
    [DefaultExecutionOrder(-40)]
    public class DemoRooms : MonoBehaviour
    {
        public Material tallerPlate;
        public Material bombasPlate;

        public struct RoomZone
        {
            public int id;
            public string name;
            public string title;
            public string subtitle;
            public Bounds bounds;
            public Color vignetteColor;
            public float vignetteIntensity;
        }

        readonly RoomZone[] zones = new[]
        {
            new RoomZone
            {
                id = 0,
                name = "ENTRADA",
                title = "ACCESO 01",
                subtitle = "GARITA Y UMBRAL AL RECINTO",
                bounds = new Bounds(new Vector3(0f, 2.5f, 3.5f), new Vector3(40f, 7f, 28f)),
                vignetteColor = new Color(0.04f, 0.035f, 0.03f),
                vignetteIntensity = 0.36f
            },
            new RoomZone
            {
                id = 1,
                name = "DEPOSITO",
                title = "ALMACÉN 02",
                subtitle = "DEPÓSITO DE CARGA Y SUMINISTROS",
                bounds = new Bounds(new Vector3(-11f, 2.5f, 32f), new Vector3(34f, 7f, 32f)),
                vignetteColor = new Color(0.08f, 0.045f, 0.025f),
                vignetteIntensity = 0.44f
            },
            new RoomZone
            {
                id = 2,
                name = "PATIO",
                title = "PATIO 03",
                subtitle = "ZONA EXPUESTA A LA PENUMBRA",
                bounds = new Bounds(new Vector3(18f, 2.5f, 71f), new Vector3(28f, 7f, 24f)),
                vignetteColor = new Color(0.02f, 0.035f, 0.055f),
                vignetteIntensity = 0.42f
            },
            new RoomZone
            {
                id = 3,
                name = "TALLER",
                title = "TALLER 04",
                subtitle = "MAQUINARIA PRINCIPAL Y PRENSAS",
                bounds = new Bounds(new Vector3(-11f, 2.5f, 92f), new Vector3(38f, 7f, 44f)),
                vignetteColor = new Color(0.10f, 0.04f, 0.02f),
                vignetteIntensity = 0.48f
            },
            new RoomZone
            {
                id = 4,
                name = "BOMBAS",
                title = "BOMBAS 05",
                subtitle = "RED DE BOMBEO Y PRESIÓN DE VAPOR",
                bounds = new Bounds(new Vector3(26f, 2.5f, 100f), new Vector3(32f, 7f, 36f)),
                vignetteColor = new Color(0.02f, 0.06f, 0.05f),
                vignetteIntensity = 0.52f
            }
        };

        static readonly Vector3 Workshop = new Vector3(-20f, 0f, 105f);
        static readonly Vector3 Pumps = new Vector3(34f, 0f, 109f);
        bool dressed;
        DemoGame game;
        readonly List<Lurker> lurkers = new List<Lurker>();
        readonly List<Transform> chains = new List<Transform>();
        readonly List<Transform> drips = new List<Transform>();
        readonly List<Transform> valves = new List<Transform>();
        readonly List<VolumeProfile> roomProfiles = new List<VolumeProfile>();
        readonly int[] playerZone = { -1, -1 };
        readonly bool[] visited = new bool[5];
        float candleDimUntil;
        float candleDraftUntil;
        AudioSource roomAudioSource;
        AudioClip windClip;
        AudioClip creakClip;

        struct Lurker
        {
            public Transform Body;
            public Quaternion Rest;
            public float Stir;
        }

        void Awake()
        {
            if (!GetComponent<DemoMarks>()) gameObject.AddComponent<DemoMarks>();
            roomAudioSource = gameObject.AddComponent<AudioSource>();
            roomAudioSource.playOnAwake = false;
            roomAudioSource.spatialBlend = 0f;
            windClip = Resources.Load<AudioClip>("Production/Crawl");
        }

        IEnumerator Start()
        {
            if (!tallerPlate) tallerPlate = Plate(new Color(0.22f, 0.12f, 0.07f), 0.4f, 0.14f);
            if (!bombasPlate) bombasPlate = Plate(new Color(0.07f, 0.13f, 0.12f), 0.66f, 0.46f);
            TintRoom(Workshop, 11f, new Color(0.34f, 0.18f, 0.1f), 0.16f);
            TintRoom(Pumps, 11f, new Color(0.1f, 0.2f, 0.18f), 0.42f);
            yield return null;
            if (dressed) yield break;
            dressed = true;
            PlaceLurkers();
            DressWorkshop();
            DressPumps();

            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                RoomPressure(z.bounds.center, z.bounds.size, z.vignetteColor, z.vignetteIntensity);
                RoomTone(z.bounds.center, Mathf.Max(z.bounds.size.x, z.bounds.size.z) * 0.35f);
            }

            FurnishRooms();
            var fxManager = GetComponent<RecintoSpecialEffects>();
            if (!fxManager) fxManager = gameObject.AddComponent<RecintoSpecialEffects>();
            fxManager.Initialize(game);

            GuideLamp("taller", new[]
            {
                Workshop + new Vector3(0f, 2.65f, 4.2f),
                Workshop + new Vector3(2.1f, 2.65f, 2.4f),
                Workshop + new Vector3(-2.1f, 2.65f, 2.4f)
            });
            GuideLamp("bombas", new[]
            {
                Pumps + new Vector3(0f, 2.65f, 4.2f),
                Pumps + new Vector3(2.1f, 2.65f, 2.4f),
                Pumps + new Vector3(-2.1f, 2.65f, 2.4f)
            });
        }

        void GuideLamp(string roomName, Vector3[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (Physics.CheckSphere(candidate, 0.16f, ~0, QueryTriggerInteraction.Ignore)) continue;
                bool already = false;
                foreach (var existing in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (!existing || existing.type != LightType.Point || !existing.enabled || existing.intensity < 0.35f) continue;
                    if (Vector3.Distance(existing.transform.position, candidate) < 4.5f) { already = true; break; }
                }
                if (already) return;
                var lamp = new GameObject("Farol " + roomName);
                lamp.transform.SetParent(transform, false);
                lamp.transform.position = candidate;
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.62f, 0.32f);
                light.intensity = 0.85f;
                light.range = 6.5f;
                light.shadows = LightShadows.None;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bombilla";
                Destroy(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(lamp.transform, false);
                bulb.transform.localScale = Vector3.one * 0.07f;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                var glass = new Material(shader ? shader : Shader.Find("Standard"));
                var glow = new Color(1f, 0.55f, 0.22f);
                glass.color = glow;
                if (glass.HasProperty("_BaseColor")) glass.SetColor("_BaseColor", glow);
                if (glass.HasProperty("_EmissionColor")) glass.SetColor("_EmissionColor", glow * 1.1f);
                glass.EnableKeyword("_EMISSION");
                bulb.GetComponent<Renderer>().sharedMaterial = glass;
                return;
            }
        }

        static Material Plate(Color color, float metal, float smooth)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader ? shader : Shader.Find("Standard"));
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            return material;
        }

        void TintRoom(Vector3 center, float radius, Color tint, float smoothness)
        {
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!renderer || renderer.GetComponent<TextMesh>()) continue;
                if (renderer.GetComponentInParent<DemoPlayer>() || renderer.GetComponentInParent<CarryObject>()) continue;
                if (renderer.GetComponentInParent<DemoMechanism>()) continue;
                string name = renderer.name;
                if (name.Contains("Sombra") || name.Contains("Ente") || name.Contains("Cuerpo") || name.Contains("Llama")) continue;
                Vector3 size = renderer.bounds.size;
                if (size.x > 10f || size.z > 10f || size.y > 8f) continue;
                Vector3 flat = renderer.bounds.center;
                flat.y = 0f;
                Vector3 room = center;
                room.y = 0f;
                if (Vector3.Distance(flat, room) > radius) continue;
                var material = renderer.material;
                if (!material) continue;
                Color current = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                Color next = Color.Lerp(current, tint, 0.34f);
                material.color = next;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", next);
                if (material.HasProperty("_Color")) material.SetColor("_Color", next);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            }
        }

        void DressWorkshop()
        {
            var oil = Plate(new Color(0.08f, 0.045f, 0.02f), 0.15f, 0.22f);
            var rust = Plate(new Color(0.28f, 0.1f, 0.05f), 0.35f, 0.08f);
            var iron = Plate(new Color(0.07f, 0.06f, 0.05f), 0.7f, 0.18f);
            FloorMark(Workshop + new Vector3(0f, 0f, -7.2f), "TALLER 04", rust, 18f);
            Slick(Workshop + new Vector3(1.4f, 0f, 0.6f), new Vector3(2.4f, 0.012f, 1.6f), oil);
            for (int i = 0; i < 5; i++)
                Handprint(Workshop + new Vector3(-1.2f + i * 0.55f, 0.02f, -2.4f + i * 0.7f), rust, 30f + i * 8f);
            Chain(Workshop + new Vector3(-2.2f, 3.1f, 1.1f), iron, 1.6f);
            Chain(Workshop + new Vector3(2.6f, 3.05f, -0.4f), iron, 2.1f);
            ScratchWall(Workshop + new Vector3(0.2f, 1.45f, 4.8f), rust);
            Gate(Workshop + new Vector3(0f, 0f, -7.2f), iron, 18f);
        }

        void DressPumps()
        {
            var wet = Plate(new Color(0.03f, 0.07f, 0.06f), 0.2f, 0.78f);
            var moss = Plate(new Color(0.16f, 0.28f, 0.22f), 0.05f, 0.4f);
            var iron = Plate(new Color(0.08f, 0.09f, 0.09f), 0.75f, 0.35f);
            FloorMark(Pumps + new Vector3(0f, 0f, -7.4f), "BOMBAS 05", moss, -12f);
            Slick(Pumps + new Vector3(-0.8f, 0f, 1.2f), new Vector3(2.8f, 0.01f, 2.1f), wet);
            Chain(Pumps + new Vector3(1.8f, 3.2f, 0.5f), iron, 1.8f);
            Chain(Pumps + new Vector3(-2.4f, 3.15f, -1.2f), iron, 2.4f);
            for (int i = 0; i < 3; i++)
                Drip(Pumps + new Vector3(-0.4f + i * 0.7f, 2.6f, 0.3f), wet);
            ScratchWall(Pumps + new Vector3(-5.2f, 1.35f, 0.4f), moss);
            Gate(Pumps + new Vector3(0f, 0f, -7.4f), iron, -12f);
        }

        void Gate(Vector3 position, Material material, float yaw)
        {
            var root = new GameObject("Pórtico");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Post(root.transform, material, new Vector3(-1.15f, 1.15f, 0f));
            Post(root.transform, material, new Vector3(1.15f, 1.15f, 0f));
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Dintel";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            beam.transform.localScale = new Vector3(2.5f, 0.12f, 0.12f);
            beam.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void Post(Transform parent, Material material, Vector3 local)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Jamba";
            Destroy(post.GetComponent<Collider>());
            post.transform.SetParent(parent, false);
            post.transform.localPosition = local;
            post.transform.localScale = new Vector3(0.12f, 2.3f, 0.12f);
            post.GetComponent<Renderer>().sharedMaterial = material;
        }

        void FloorMark(Vector3 position, string word, Material plate, float yaw)
        {
            var root = new GameObject("Umbral " + word);
            root.transform.SetPositionAndRotation(position + Vector3.up * 0.03f, Quaternion.Euler(0f, yaw, 0f));
            var band = GameObject.CreatePrimitive(PrimitiveType.Quad);
            band.name = "Chapa de umbral";
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(root.transform, false);
            band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            band.transform.localScale = new Vector3(2.4f, 0.7f, 1f);
            band.GetComponent<Renderer>().sharedMaterial = plate;
            Label(root.transform, word, new Color(0.86f, 0.72f, 0.48f), 0.07f, new Vector3(0f, 0.02f, 0f));
            var ink = root.GetComponentInChildren<TextMesh>();
            if (ink) ink.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void Slick(Vector3 position, Vector3 scale, Material material)
        {
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "Charco";
            Destroy(pool.GetComponent<Collider>());
            pool.transform.position = position + Vector3.up * 0.02f;
            pool.transform.localScale = scale;
            pool.GetComponent<Renderer>().sharedMaterial = material;
        }

        void Handprint(Vector3 position, Material material, float yaw)
        {
            var print = GameObject.CreatePrimitive(PrimitiveType.Quad);
            print.name = "Huella";
            Destroy(print.GetComponent<Collider>());
            print.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, yaw, 0f));
            print.transform.localScale = new Vector3(0.16f, 0.22f, 1f);
            print.GetComponent<Renderer>().sharedMaterial = material;
        }

        void Chain(Vector3 ceiling, Material material, float length)
        {
            var root = new GameObject("Cadena");
            root.transform.position = ceiling;
            for (int i = 0; i < 6; i++)
            {
                var link = GameObject.CreatePrimitive(PrimitiveType.Cube);
                link.name = "Eslabón";
                Destroy(link.GetComponent<Collider>());
                link.transform.SetParent(root.transform, false);
                link.transform.localPosition = Vector3.down * (0.12f + i * length / 6f);
                link.transform.localScale = new Vector3(0.035f, length / 7f, 0.035f);
                link.transform.localRotation = Quaternion.Euler(0f, i * 28f, i % 2 == 0 ? 18f : -18f);
                link.GetComponent<Renderer>().sharedMaterial = material;
            }
            chains.Add(root.transform);
        }

        void Drip(Vector3 ceiling, Material material)
        {
            var drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drop.name = "Goteo";
            Destroy(drop.GetComponent<Collider>());
            drop.transform.position = ceiling;
            drop.transform.localScale = new Vector3(0.02f, 0.35f, 0.02f);
            drop.GetComponent<Renderer>().sharedMaterial = material;
            drips.Add(drop.transform);
        }

        void ScratchWall(Vector3 position, Material material)
        {
            var board = new GameObject("Arañazos");
            board.transform.position = position;
            board.transform.rotation = Quaternion.LookRotation((position.x < 0 ? Vector3.right : Vector3.left));
            for (int i = 0; i < 4; i++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "Marca";
                Destroy(line.GetComponent<Collider>());
                line.transform.SetParent(board.transform, false);
                line.transform.localPosition = new Vector3(-0.35f + i * 0.18f, -0.05f * i, 0.01f);
                line.transform.localRotation = Quaternion.Euler(0f, 0f, -18f - i * 7f);
                line.transform.localScale = new Vector3(0.55f, 0.012f, 0.012f);
                line.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        void FurnishRooms()
        {
            var pipe = Template("Pipe Long");
            var corner = Template("Pipe Corner");
            var valve = Template("Pipe Valve");
            if (!valve) valve = Template("Valve Pipe");
            var handle = Template("Valve Handle");
            var barrel = Template("Barrel");
            var pallet = Template("Pallet");
            var rust = new Color(0.45f, 0.22f, 0.1f);
            var wet = new Color(0.18f, 0.32f, 0.28f);

            for (int i = 0; i < 4; i++)
                Drop(pipe, Workshop + new Vector3(-4.8f + i * 2.6f, 1.55f, 5.6f), Quaternion.Euler(0f, 90f, 90f), rust, false);
            Drop(corner, Workshop + new Vector3(5.4f, 1.55f, 5.6f), Quaternion.identity, rust, false);
            Drop(valve, Workshop + new Vector3(-5.2f, 0.9f, 2.4f), Quaternion.Euler(0f, 40f, 0f), rust, true);
            Drop(handle, Workshop + new Vector3(-5.2f, 1.35f, 2.4f), Quaternion.identity, rust, false, true);
            Drop(barrel, Workshop + new Vector3(-5.6f, 0f, -3.8f), Quaternion.Euler(0f, 18f, 0f), rust, true);
            Drop(barrel, Workshop + new Vector3(4.8f, 0f, 3.4f), Quaternion.Euler(8f, -30f, 4f), rust, true);
            Drop(pallet, Workshop + new Vector3(-4.4f, 0f, 1.6f), Quaternion.Euler(0f, 12f, 0f), rust, true);
            CoveredBody(Workshop + new Vector3(5.3f, 0f, -1.4f), 160f);

            for (int i = 0; i < 5; i++)
                Drop(pipe, Pumps + new Vector3(-5.2f, 0.85f + (i % 2) * 0.7f, -4.2f + i * 2.1f), Quaternion.Euler(0f, 0f, 90f), wet, false);
            Drop(corner, Pumps + new Vector3(-5.2f, 1.5f, 5.2f), Quaternion.Euler(0f, -90f, 0f), wet, false);
            Drop(valve, Pumps + new Vector3(4.6f, 1.05f, -2.2f), Quaternion.Euler(0f, -70f, 0f), wet, true);
            Drop(handle, Pumps + new Vector3(4.6f, 1.5f, -2.2f), Quaternion.identity, wet, false, true);
            Drop(barrel, Pumps + new Vector3(5.4f, 0f, 4.2f), Quaternion.Euler(0f, 25f, 0f), wet, true);
            Drop(pallet, Pumps + new Vector3(-3.8f, 0f, -5.1f), Quaternion.Euler(0f, -8f, 0f), wet, true);
            CoveredBody(Pumps + new Vector3(-5.6f, 0f, 3.8f), -20f);
        }

        static Transform Template(string exactName)
        {
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (candidate.name != exactName) continue;
                if (candidate.GetComponentInParent<DemoPlayer>() || candidate.GetComponentInParent<DemoMechanism>()) continue;
                return candidate;
            }
            return null;
        }

        void Drop(Transform source, Vector3 position, Quaternion rotation, Color tint, bool onFloor, bool valve = false)
        {
            if (!source) return;
            var copy = Instantiate(source.gameObject);
            copy.name = "Pieza de sala";
            foreach (var collider in copy.GetComponentsInChildren<Collider>()) Destroy(collider);
            copy.transform.SetParent(null, false);
            copy.transform.localScale = source.lossyScale;
            copy.transform.SetPositionAndRotation(position, rotation);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
            {
                if (!renderer) continue;
                var material = renderer.material;
                Color current = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                Color next = Color.Lerp(current, tint, 0.42f);
                material.color = next;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", next);
                if (material.HasProperty("_Color")) material.SetColor("_Color", next);
            }
            if (onFloor)
            {
                float bottom = float.MaxValue;
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
                    bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (bottom < float.MaxValue) copy.transform.position += Vector3.up * (0.02f - bottom);
            }
            if (valve) valves.Add(copy.transform);
        }

        void CoveredBody(Vector3 position, float yaw)
        {
            var source = Resources.Load<GameObject>("Horror/DeadBodyCovered");
            var texture = Resources.Load<Texture2D>("Horror/DeadBodyCovered_BaseColor");
            if (!source) return;
            var body = Instantiate(source);
            body.name = "Cuerpo del turno anterior";
            foreach (var collider in body.GetComponentsInChildren<Collider>()) Destroy(collider);
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(body); return; }
            if (texture)
            {
                var cloth = Plate(Color.white, 0f, 0.08f);
                if (cloth.HasProperty("_BaseMap")) cloth.SetTexture("_BaseMap", texture);
                foreach (var renderer in renderers) renderer.sharedMaterial = cloth;
            }
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * source.transform.rotation);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.x > 0.05f) body.transform.localScale *= 2.1f / Mathf.Max(bounds.size.x, bounds.size.z);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            body.transform.position += Vector3.up * (0.02f - bounds.min.y);
        }

        void PlaceLurkers()
        {
            var source = Resources.Load<GameObject>("Horror/CreepingShadow");
            var texture = Resources.Load<Texture2D>("Horror/CreepingShadow_BaseColor");
            if (!source) return;
            var skin = Plate(new Color(0.02f, 0.015f, 0.018f), 0f, 0.04f);
            if (texture && skin.HasProperty("_BaseMap")) skin.SetTexture("_BaseMap", texture);
            PlaceLurker(source, skin, Workshop + new Vector3(4.2f, 0f, -3.1f), 210f);
            PlaceLurker(source, skin, Workshop + new Vector3(-3.4f, 0f, 3.6f), 20f);
            PlaceLurker(source, skin, Pumps + new Vector3(-4.6f, 0f, 2.2f), 250f);
            PlaceLurker(source, skin, Pumps + new Vector3(3.1f, 0f, -4.4f), 70f);
        }

        void PlaceLurker(GameObject source, Material skin, Vector3 position, float yaw)
        {
            var body = Instantiate(source);
            body.name = "Presencia en la sala";
            foreach (var collider in body.GetComponentsInChildren<Collider>()) Destroy(collider);
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(body); return; }
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = skin;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.05f) body.transform.localScale *= 1.15f / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            body.transform.position += Vector3.up * (0.02f - bounds.min.y);
            lurkers.Add(new Lurker { Body = body.transform, Rest = body.transform.rotation, Stir = -1f });
        }

        int GetZoneIndex(Vector3 pos)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].bounds.Contains(pos)) return i;
            }
            return -1;
        }

        void OnPlayerEnterRoom(DemoPlayer player, RoomZone zone)
        {
            if (!player || !game) return;
            player.Tell("ENTRANDO A: " + zone.title + " — " + zone.subtitle, 4f);

            bool nearCandle = false;
            foreach (var candle in game.candles)
            {
                if (!candle || !candle.candleLight || !candle.candleLight.enabled) continue;
                if (Vector3.Distance(player.transform.position, candle.transform.position) < 6f)
                {
                    nearCandle = true;
                    break;
                }
            }

            if (nearCandle)
            {
                candleDraftUntil = Time.time + 1.5f;
                if (roomAudioSource && windClip)
                {
                    roomAudioSource.pitch = Random.Range(0.85f, 1.15f);
                    roomAudioSource.PlayOneShot(windClip, game.sfxVolume * 0.22f);
                }
            }

            if (!visited[zone.id])
            {
                visited[zone.id] = true;
                if (roomAudioSource && game.gearsClip && (zone.id == 3 || zone.id == 4))
                {
                    roomAudioSource.pitch = 0.75f;
                    roomAudioSource.PlayOneShot(game.gearsClip, game.sfxVolume * 0.18f);
                }
            }
        }

        void Update()
        {
            if (!game) game = FindAnyObjectByType<DemoGame>();
            if (game && game.Playing && game.players != null)
            {
                for (int i = 0; i < game.players.Length; i++)
                {
                    var p = game.players[i];
                    if (!p) continue;
                    int zIdx = GetZoneIndex(p.transform.position);
                    if (zIdx >= 0 && zIdx != playerZone[i])
                    {
                        int prev = playerZone[i];
                        playerZone[i] = zIdx;
                        if (prev >= 0) OnPlayerEnterRoom(p, zones[zIdx]);
                        else p.Tell("ZONA: " + zones[zIdx].title + " — " + zones[zIdx].subtitle, 4f);
                    }
                }
            }

            if (lurkers.Count == 0) return;
            if (!game || !game.Playing) return;
            float sway = Mathf.Sin(Time.time * 0.55f);
            for (int i = 0; i < chains.Count; i++)
                if (chains[i]) chains[i].localRotation = Quaternion.Euler(sway * 7f, 0f, sway * 3f);
            for (int i = 0; i < valves.Count; i++)
                if (valves[i]) valves[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.35f + i) * 18f, 0f);
            for (int i = 0; i < drips.Count; i++)
            {
                if (!drips[i]) continue;
                float fall = Mathf.Repeat(Time.time * 0.18f + i * 0.37f, 1f);
                var scale = drips[i].localScale;
                scale.y = Mathf.Lerp(0.08f, 0.85f, fall);
                drips[i].localScale = scale;
            }
            for (int i = 0; i < lurkers.Count; i++)
            {
                var lurker = lurkers[i];
                if (!lurker.Body) continue;
                if (lurker.Stir < 0f)
                {
                    foreach (var player in game.players)
                    {
                        if (!player || !player.Held || !player.Held.isCandle) continue;
                        if (Vector3.Distance(player.transform.position, lurker.Body.position) > 5.5f) continue;
                        lurker.Stir = 0f;
                        candleDimUntil = Time.time + 0.45f;
                        if (game.gearsClip)
                        {
                            var tick = lurker.Body.gameObject.AddComponent<AudioSource>();
                            tick.spatialBlend = 0f;
                            tick.volume = game.sfxVolume * 0.12f;
                            tick.PlayOneShot(game.gearsClip);
                        }
                        break;
                    }
                }
                else if (lurker.Stir < 1f)
                {
                    lurker.Stir = Mathf.MoveTowards(lurker.Stir, 1f, Time.deltaTime * 0.35f);
                    float yaw = Mathf.Sin(lurker.Stir * Mathf.PI) * 28f;
                    lurker.Body.rotation = lurker.Rest * Quaternion.Euler(0f, yaw, Mathf.Sin(lurker.Stir * Mathf.PI) * 4f);
                }
                lurkers[i] = lurker;
            }
        }

        static void Label(Transform parent, string text, Color ink, float size, Vector3 local)
        {
            var line = new GameObject("Rótulo");
            line.transform.SetParent(parent, false);
            line.transform.localPosition = local;
            line.transform.localRotation = Quaternion.identity;
            var mesh = line.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = size;
            mesh.fontSize = 52;
            mesh.color = ink;
            mesh.richText = false;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font) mesh.font = font;
            WorldMark.PaintFixed(mesh);
        }

        void RoomPressure(Vector3 center, Vector3 size, Color tint, float vignetteAmount)
        {
            var volumeObject = new GameObject("Presión de " + center);
            volumeObject.transform.SetParent(transform, false);
            volumeObject.transform.position = center;
            var box = volumeObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 60f;
            volume.blendDistance = 3.5f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            roomProfiles.Add(profile);
            volume.sharedProfile = profile;
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(vignetteAmount);
            vignette.color.Override(tint);
            vignette.smoothness.Override(0.7f);
        }

        void RoomTone(Vector3 center, float radius)
        {
            var clip = Resources.Load<AudioClip>("Production/Crawl");
            if (!clip) return;
            if (!game) game = FindAnyObjectByType<DemoGame>();
            var ear = new GameObject("Aliento de la sala");
            ear.transform.SetParent(transform, false);
            ear.transform.position = center + Vector3.up * 1.2f;
            var tone = ear.AddComponent<RecintoSound>();
            tone.game = game;
            tone.clips = new[] { clip };
            tone.loop = false;
            tone.volume = 0.045f;
            tone.radius = radius;
            tone.interval = new Vector2(14f, 28f);
            tone.startDelay = 6f;
        }

        void LateUpdate()
        {
            if (!game) return;
            bool isDraft = Time.time < candleDraftUntil;
            bool isDim = Time.time < candleDimUntil;
            if (!isDraft && !isDim) return;

            float flicker = 1f;
            if (isDraft)
            {
                float t = (candleDraftUntil - Time.time) / 1.5f;
                float flutter = Mathf.Sin(Time.time * 28f) * 0.14f;
                flicker = Mathf.Lerp(1f, 0.74f + flutter, t);
            }
            if (isDim)
            {
                float recover = 1f - (candleDimUntil - Time.time) / 0.45f;
                float dip = Mathf.Lerp(0.38f, 1f, recover);
                flicker = Mathf.Min(flicker, dip);
            }

            foreach (var candle in game.candles)
            {
                if (!candle || !candle.candleLight || !candle.candleLight.enabled) continue;
                candle.candleLight.intensity *= flicker;
            }
        }

        void OnDestroy()
        {
            foreach (var profile in roomProfiles)
                if (profile) Destroy(profile);
        }
    }
}

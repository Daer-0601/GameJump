using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamejam2026
{
    // El cuaderno en manos de un jugador: solo su cámara lo dibuja y solo él queda quieto leyendo.
    public class CuadernoVista : MonoBehaviour
    {
        enum State { Hidden, Opening, Open, Closing }

        const float RiseTime = 0.42f;
        const float CoverOpenStart = 0.47f;
        const float CoverOpenTime = 0.55f;
        const float RiffleTime = 0.26f;
        const float CoverCloseTime = 0.42f;
        const float DropTime = 0.32f;
        const float FlipTime = 0.5f;
        const int Segments = 22;
        const float Distance = 0.325f;

        // medidas del modelo importado (metros, ejes de Unity)
        static readonly Vector3 Hinge = new Vector3(0.012f, 0.0155f, 0f);
        static readonly Vector3 PageCenter = new Vector3(-0.068f, 0.0137f, 0f);
        const float SheetSpine = -0.009f;
        const float SheetWidth = 0.119f;
        const float SheetHalfHeight = 0.093f;
        const float RightStack = 0.0137f;
        const float LeftStack = 0.0186f;
        const float Leaf = 0.00035f;

        DemoPlayer player;
        DemoGame game;
        CuadernoContenido content;
        Camera overlay;
        Transform holder;
        Transform coverPivot;
        GameObject closedModel;
        GameObject openModel;
        Sheet[] sheets;
        Material leather;
        Material[] pageMaterials;
        Material backMaterial;
        AudioSource voice;
        AudioClip rustle, creak, snap;
        State state;
        float clock;
        int page;
        int flipping = -1;
        int flipDirection;
        float flipClock;
        bool wantClose;
        bool riffling;
        int seenNotes;
        int layer;

        sealed class Sheet
        {
            public Mesh mesh;
            public Transform transform;
            public float angle;
            public float bend;
            public int index;
            public bool dirty = true;
        }

        public bool Blocking => state != State.Hidden;
        public bool Unread => content && content.NotesVersion != seenNotes;
        public bool IsOpen => state == State.Open;
        public int Page => page;
        public Camera OverlayCamera => overlay;
        public string Describe() => state + " t=" + clock.ToString("0.00") + " page=" + page
            + (holder ? " holder=" + holder.localPosition.ToString("F3") + " active=" + holder.gameObject.activeInHierarchy : "")
            + (overlay ? " cam=" + overlay.enabled : "");

        // Mismo efecto que la tecla del cuaderno; lo usan las pruebas automáticas.
        public void Toggle()
        {
            if (state == State.Hidden) Begin();
            else if (state == State.Open) StartClose();
            else if (state == State.Opening) wantClose = true;
        }

        // Congela la animación en un instante para revisarla (pruebas).
        public void Preview(string phase, float seconds)
        {
            if (state == State.Hidden) Begin();
            if (phase == "flip")
            {
                state = State.Open;
                ShowClosed(false);
                SetCover(0f);
                float k = Ease.InOut(Mathf.Clamp01(seconds / FlipTime));
                SetSheet(sheets[0], 180f * k, 1);
                Rest(1f);
            }
            else if (phase == "close")
            {
                state = State.Closing;
                riffling = false;
                clock = seconds;
                Closing();
            }
            else
            {
                state = State.Opening;
                clock = seconds;
                Opening();
            }
            foreach (var sheet in sheets) if (sheet.dirty) BuildSheet(sheet);
            Light();
        }

        public void Turn(int direction)
        {
            if (state != State.Open || flipping >= 0) return;
            if (direction > 0 && page < sheets.Length - 1) StartFlip(page, 1);
            else if (direction < 0 && page > 0) StartFlip(page - 1, -1);
        }

        public void Bind(DemoPlayer owner, DemoGame session)
        {
            player = owner;
            game = session;
            content = CuadernoContenido.Ensure(session);
            seenNotes = content ? content.NotesVersion : 0;
            layer = 10 + Mathf.Clamp(owner.playerIndex, 0, 1);
            if (!owner.view) return;
            owner.view.cullingMask &= ~((1 << 10) | (1 << 11));
            BuildCamera(owner.view);
            BuildBook();
            BuildSounds();
            SetVisible(false);
        }

        void BuildCamera(Camera view)
        {
            var go = new GameObject("Cuaderno - cámara J" + (player.playerIndex + 1));
            go.transform.SetParent(view.transform, false);
            overlay = go.AddComponent<Camera>();
            overlay.cullingMask = 1 << layer;
            overlay.fieldOfView = 38f;
            overlay.nearClipPlane = 0.02f;
            overlay.farClipPlane = 3f;
            overlay.clearFlags = CameraClearFlags.Depth;
            var data = overlay.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            var baseData = view.GetUniversalAdditionalCameraData();
            if (baseData && !baseData.cameraStack.Contains(overlay)) baseData.cameraStack.Add(overlay);
            overlay.enabled = false;
        }

        void BuildBook()
        {
            var shader = Shader.Find("Gamejam/Cuaderno");
            if (!shader) { Debug.LogError("Falta el shader Gamejam/Cuaderno.", this); return; }
            leather = new Material(shader) { name = "Cuero del cuaderno" };
            leather.SetTexture("_BaseMap", Resources.Load<Texture2D>("Cuaderno/binder_notebook_diff_2k"));
            var normal = Resources.Load<Texture2D>("Cuaderno/binder_notebook_nor_gl_2k");
            if (normal) leather.SetTexture("_BumpMap", normal);
            leather.SetFloat("_Gloss", 0.35f);

            holder = new GameObject("Cuaderno en mano").transform;
            holder.SetParent(overlay.transform, false);
            var model = new GameObject("Modelo").transform;
            model.SetParent(holder, false);
            var rotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
            model.localRotation = rotation;
            model.localPosition = -(rotation * PageCenter);

            closedModel = Spawn("Cuaderno/binder_closed", model);
            openModel = new GameObject("Abierto");
            openModel.transform.SetParent(model, false);
            Spawn("Cuaderno/binder_body", openModel.transform);
            coverPivot = new GameObject("Bisagra de la tapa").transform;
            coverPivot.SetParent(openModel.transform, false);
            coverPivot.localPosition = Hinge;
            var cover = Spawn("Cuaderno/binder_cover", openModel.transform);
            if (cover) cover.transform.SetParent(coverPivot, true);

            backMaterial = new Material(shader) { name = "Dorso de hoja" };
            backMaterial.SetTexture("_BaseMap", content ? content.Paper : Texture2D.whiteTexture);
            backMaterial.SetFloat("_Gloss", 0.04f);
            int count = CuadernoContenido.PageCount;
            sheets = new Sheet[count];
            pageMaterials = new Material[count];
            for (int i = 0; i < count; i++)
            {
                pageMaterials[i] = new Material(shader) { name = "Hoja " + (i + 1) };
                if (content) pageMaterials[i].SetTexture("_BaseMap", content.Pages[i]);
                pageMaterials[i].SetFloat("_Gloss", 0.05f);
                var go = new GameObject("Hoja " + (i + 1));
                go.layer = layer;
                go.transform.SetParent(openModel.transform, false);
                var mesh = new Mesh { name = "Hoja curvada" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { pageMaterials[i], backMaterial };
                Quiet(renderer);
                sheets[i] = new Sheet { mesh = mesh, transform = go.transform, index = i };
                BuildSheet(sheets[i]);
            }
            SetLayer(holder.gameObject);
        }

        GameObject Spawn(string path, Transform parent)
        {
            var source = Resources.Load<GameObject>(path);
            if (!source) { Debug.LogError("Falta " + path + " en Resources.", this); return null; }
            var instance = Instantiate(source, parent, false);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = leather;
                renderer.sharedMaterials = materials;
                Quiet(renderer);
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            return instance;
        }

        static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void SetLayer(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void SetVisible(bool visible)
        {
            if (overlay) overlay.enabled = visible;
            if (holder) holder.gameObject.SetActive(visible);
        }

        void ShowClosed(bool closed)
        {
            if (closedModel) closedModel.SetActive(closed);
            if (openModel) openModel.SetActive(!closed);
        }

        void Update()
        {
            if (!player || !game || !holder) return;
            if (game.Finished)
            {
                if (state != State.Hidden) Hide();
                return;
            }
            if (!game.Playing) return;
            if (LanAuthority.LanMatch && player.playerIndex != LanAuthority.LocalSlot) return;
            float dt = Time.unscaledDeltaTime;
            bool toggle = game.PlayerInputEnabled && DemoPlayerInput.NotebookPressed(player.playerIndex);
            switch (state)
            {
                case State.Hidden:
                    if (toggle) Begin();
                    break;
                case State.Opening:
                    if (toggle) wantClose = true;
                    clock += dt;
                    Opening();
                    if (clock >= CoverOpenStart + CoverOpenTime)
                    {
                        state = State.Open;
                        clock = 0f;
                        if (wantClose) StartClose();
                    }
                    break;
                case State.Open:
                    if (content) seenNotes = content.NotesVersion;
                    if (toggle) { StartClose(); break; }
                    int turn = flipping < 0 ? DemoPlayerInput.PageTurn(player.playerIndex) : 0;
                    if (turn > 0 && page < sheets.Length - 1) StartFlip(page, 1);
                    else if (turn < 0 && page > 0) StartFlip(page - 1, -1);
                    Flip(dt);
                    Rest(1f);
                    break;
                case State.Closing:
                    clock += dt;
                    Closing();
                    break;
            }
            Light();
            foreach (var sheet in sheets) if (sheet.dirty) BuildSheet(sheet);
        }

        void Begin()
        {
            state = State.Opening;
            clock = 0f;
            page = 0;
            flipping = -1;
            wantClose = false;
            foreach (var sheet in sheets) { sheet.angle = 0f; sheet.bend = 0f; sheet.dirty = true; }
            if (content) content.Refresh();
            ShowClosed(true);
            SetCover(180f);
            SetVisible(true);
            Opening();
            Play(creak, 0.55f, 1.05f);
        }

        void StartClose()
        {
            state = State.Closing;
            clock = 0f;
            riffling = page > 0;
            flipping = -1;
            Play(riffling ? rustle : creak, 0.5f, riffling ? 1.25f : 0.95f);
        }

        void Hide()
        {
            state = State.Hidden;
            flipping = -1;
            SetVisible(false);
        }

        void Opening()
        {
            float rise = Ease.OutCubic(Mathf.Clamp01(clock / RiseTime));
            if (clock < CoverOpenStart)
            {
                ShowClosed(true);
                Pose(rise, 0f);
                return;
            }
            if (closedModel.activeSelf)
            {
                ShowClosed(false);
                Play(snap, 0.6f, 1f);
                Play(creak, 0.35f, 1.2f);
            }
            float t = Mathf.Clamp01((clock - CoverOpenStart) / CoverOpenTime);
            SetCover(180f * (1f - Ease.OutBack(t, 0.6f)));
            Pose(1f, Mathf.Sin(t * Mathf.PI) * 0.6f);
        }

        void Closing()
        {
            float t = clock;
            float riffle = riffling ? RiffleTime : 0f;
            if (t < riffle)
            {
                float k = Ease.InOut(t / riffle);
                for (int i = 0; i < sheets.Length; i++)
                {
                    if (i >= page) continue;
                    SetSheet(sheets[i], 180f * (1f - Mathf.Clamp01(k * 1.25f - i * 0.12f)), -1);
                }
                Rest(1f);
                return;
            }
            if (riffling)
            {
                foreach (var sheet in sheets) SetSheet(sheet, 0f, 0);
                riffling = false;
                page = 0;
                Play(creak, 0.45f, 0.95f);
            }
            t -= riffle;
            if (t < CoverCloseTime)
            {
                float k = Ease.InOut(t / CoverCloseTime);
                SetCover(180f * k);
                Pose(1f, -Mathf.Sin(k * Mathf.PI) * 0.5f);
                return;
            }
            if (openModel.activeSelf)
            {
                ShowClosed(true);
                Play(snap, 0.6f, 0.92f);
            }
            t -= CoverCloseTime;
            float drop = Mathf.Clamp01(t / DropTime);
            Pose(1f - Ease.InCubic(drop), 0f);
            if (drop >= 1f) Hide();
        }

        void Rest(float amount) => Pose(amount, 0f);

        // amount 0: fuera de cuadro, abajo; 1: en posición de lectura.
        void Pose(float amount, float nudge)
        {
            float time = Time.unscaledTime;
            float danger = game ? Mathf.Clamp01(game.Danger / 100f) : 0f;
            float tremor = 0.0005f + danger * danger * 0.0022f;
            Vector3 shake = new Vector3(
                Mathf.PerlinNoise(time * 2.3f, 1.7f + player.playerIndex) - 0.5f,
                Mathf.PerlinNoise(4.1f, time * 2.1f) - 0.5f,
                0f) * tremor * 2f;
            Vector3 breathe = new Vector3(0f, Mathf.Sin(time * 1.6f) * 0.0018f, 0f);
            Vector3 restPosition = new Vector3(0f, -0.004f, Distance) + breathe + shake;
            Quaternion restRotation = Quaternion.Euler(24f + nudge * 3f + Mathf.Sin(time * 1.1f) * 0.4f, -2.5f + shake.x * 300f, 1.5f + shake.y * 200f);
            Vector3 lowPosition = new Vector3(0.05f, -0.25f, Distance + 0.06f);
            Quaternion lowRotation = Quaternion.Euler(72f, 12f, -16f);
            holder.localPosition = Vector3.LerpUnclamped(lowPosition, restPosition, amount);
            holder.localRotation = Quaternion.SlerpUnclamped(lowRotation, restRotation, amount);
        }

        void SetCover(float degrees)
        {
            if (coverPivot) coverPivot.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        void StartFlip(int sheet, int direction)
        {
            flipping = sheet;
            flipDirection = direction;
            flipClock = 0f;
            Play(rustle, 0.55f, Random.Range(0.9f, 1.1f));
        }

        void Flip(float dt)
        {
            if (flipping < 0) return;
            flipClock += dt;
            float t = Mathf.Clamp01(flipClock / FlipTime);
            float k = Ease.InOut(t);
            float angle = flipDirection > 0 ? 180f * k : 180f * (1f - k);
            SetSheet(sheets[flipping], angle, flipDirection);
            if (t < 1f) return;
            page += flipDirection;
            SetSheet(sheets[flipping], flipDirection > 0 ? 180f : 0f, 0);
            flipping = -1;
        }

        void SetSheet(Sheet sheet, float angle, int direction)
        {
            float bend = direction == 0 ? 0f : -direction * 0.85f * Mathf.Sin(angle * Mathf.Deg2Rad);
            if (Mathf.Approximately(sheet.angle, angle) && Mathf.Approximately(sheet.bend, bend)) return;
            sheet.angle = angle;
            sheet.bend = bend;
            sheet.dirty = true;
        }

        // Hoja como tira que gira en las anillas; el borde se retrasa y la curva.
        void BuildSheet(Sheet sheet)
        {
            sheet.dirty = false;
            int columns = Segments + 1;
            var vertices = new Vector3[columns * 4];
            var normals = new Vector3[columns * 4];
            var uvs = new Vector2[columns * 4];
            float theta = sheet.angle * Mathf.Deg2Rad;
            int count = sheets != null ? sheets.Length : CuadernoContenido.PageCount;
            float right = RightStack + (count - 1 - sheet.index) * Leaf;
            float left = LeftStack + sheet.index * Leaf;
            float lift = Mathf.SmoothStep(0f, 1f, theta / Mathf.PI);
            var point = new Vector3(SheetSpine, Mathf.Lerp(right, left, lift), 0f);
            float step = SheetWidth / Segments;
            for (int k = 0; k < columns; k++)
            {
                float s = k / (float)Segments;
                float phi = Mathf.Clamp(theta + sheet.bend * s * s, 0f, Mathf.PI);
                var normal = new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0f);
                for (int row = 0; row < 2; row++)
                {
                    float z = row == 0 ? -SheetHalfHeight : SheetHalfHeight;
                    float v = row == 0 ? 1f : 0f;
                    int front = k * 2 + row;
                    int back = columns * 2 + front;
                    vertices[front] = new Vector3(point.x, point.y, z);
                    vertices[back] = vertices[front] - normal * 0.00012f;
                    normals[front] = normal;
                    normals[back] = -normal;
                    uvs[front] = new Vector2(s, v);
                    uvs[back] = new Vector2(1f - s, v);
                }
                if (k < Segments)
                {
                    float mid = Mathf.Clamp(theta + sheet.bend * (s + 0.5f / Segments) * (s + 0.5f / Segments), 0f, Mathf.PI);
                    point += new Vector3(-Mathf.Cos(mid), Mathf.Sin(mid), 0f) * step;
                }
            }
            var frontTris = new int[Segments * 6];
            var backTris = new int[Segments * 6];
            for (int k = 0; k < Segments; k++)
            {
                int a = k * 2, b = k * 2 + 1, c = k * 2 + 2, d = k * 2 + 3;
                int o = k * 6;
                frontTris[o] = a; frontTris[o + 1] = c; frontTris[o + 2] = b;
                frontTris[o + 3] = b; frontTris[o + 4] = c; frontTris[o + 5] = d;
                int shift = columns * 2;
                backTris[o] = a + shift; backTris[o + 1] = b + shift; backTris[o + 2] = c + shift;
                backTris[o + 3] = b + shift; backTris[o + 4] = d + shift; backTris[o + 5] = c + shift;
            }
            var mesh = sheet.mesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(frontTris, 0);
            mesh.SetTriangles(backTris, 1);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // Luz de lectura: la vela si la lleva este jugador; si no, lo poco que llega del recinto.
        void Light()
        {
            if (!holder) return;
            bool candle = player.Held && player.Held.isCandle;
            float time = Time.unscaledTime;
            float flicker = candle ? 0.9f + Mathf.PerlinNoise(time * 7.3f, 0.3f) * 0.18f : 1f;
            Color lamp = candle ? new Color(1.25f, 0.92f, 0.62f) * flicker : new Color(0.66f, 0.68f, 0.74f);
            Color ambient = candle ? new Color(0.17f, 0.14f, 0.11f) : new Color(0.15f, 0.15f, 0.17f);
            Vector3 position = holder.TransformPoint(candle ? new Vector3(-0.2f, 0.16f, -0.16f) : new Vector3(0.12f, 0.3f, -0.1f));
            var lampVector = new Vector4(position.x, position.y, position.z, 5f);
            Apply(leather, lampVector, lamp, ambient);
            Apply(backMaterial, lampVector, lamp, ambient);
            foreach (var material in pageMaterials) Apply(material, lampVector, lamp, ambient);
        }

        static void Apply(Material material, Vector4 position, Color lamp, Color ambient)
        {
            if (!material) return;
            material.SetVector("_LampPos", position);
            material.SetColor("_LampColor", lamp);
            material.SetColor("_Ambient", ambient);
        }

        void Play(AudioClip clip, float volume, float pitch)
        {
            if (!clip || !voice) return;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * (game ? game.sfxVolume : 0.8f));
        }

        void BuildSounds()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.panStereo = player.playerIndex == 0 ? -0.35f : 0.35f;
            rustle = Synth("Hoja", 0.36f, 1337, (t, n, state) =>
            {
                float envelope = Mathf.Sin(Mathf.Clamp01(t / 0.36f) * Mathf.PI) * (0.55f + 0.45f * Mathf.PerlinNoise(t * 60f, 0.5f));
                return n * envelope * 0.55f;
            }, 0.55f);
            creak = Synth("Cuero", 0.5f, 4242, (t, n, state) =>
            {
                float envelope = Mathf.Exp(-t * 6f) * 0.8f + Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI) * 0.25f;
                float grain = Mathf.PerlinNoise(t * 140f, 3.3f) > 0.62f ? 1f : 0.35f;
                return n * envelope * grain * 0.7f;
            }, 0.12f);
            snap = Synth("Broche", 0.09f, 99, (t, n, state) =>
            {
                float envelope = Mathf.Exp(-t * 70f);
                return (Mathf.Sin(t * 2f * Mathf.PI * 1250f) * 0.5f + n * 0.6f) * envelope;
            }, 0.8f);
        }

        delegate float Sample(float t, float noise, float state);

        static AudioClip Synth(string label, float seconds, int seed, Sample sample, float brightness)
        {
            const int rate = 44100;
            int length = Mathf.CeilToInt(seconds * rate);
            var data = new float[length];
            var random = new System.Random(seed);
            float low = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * brightness;
                data[i] = Mathf.Clamp(sample(i / (float)rate, low, 0f), -1f, 1f);
            }
            var clip = AudioClip.Create("Cuaderno - " + label, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (player && player.view && overlay)
            {
                var baseData = player.view.GetUniversalAdditionalCameraData();
                if (baseData) baseData.cameraStack.Remove(overlay);
            }
            if (leather) Destroy(leather);
            if (backMaterial) Destroy(backMaterial);
            if (pageMaterials != null) foreach (var m in pageMaterials) if (m) Destroy(m);
            if (sheets != null) foreach (var s in sheets) if (s != null && s.mesh) Destroy(s.mesh);
            foreach (var clip in new[] { rustle, creak, snap }) if (clip) Destroy(clip);
        }

        static class Ease
        {
            public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
            public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
            public static float InOut(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
            public static float OutBack(float t, float s)
            {
                t = Mathf.Clamp01(t) - 1f;
                return t * t * ((s + 1f) * t + s) + 1f;
            }
        }
    }
}

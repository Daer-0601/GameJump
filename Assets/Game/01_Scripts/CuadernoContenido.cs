using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Gamejam2026
{
    // Lo escrito en el cuaderno es común a los dos operarios; cada uno lo abre por su cuenta (CuadernoVista).
    // Las hojas se pintan a mano alzada en texturas que comparten ambas vistas.
    public class CuadernoContenido : MonoBehaviour
    {
        public const int PageCount = 3;
        public const int Width = 800;
        public const int Height = 1264;
        public static CuadernoContenido Instance { get; private set; }
        public RenderTexture[] Pages { get; private set; }
        public Texture2D Paper { get; private set; }
        public int NotesVersion { get; private set; }
        public int ProcedureVersion { get; private set; }

        enum Face { Marker, Hand, Pencil }

        sealed class Item
        {
            public string text;
            public Face face = Face.Hand;
            public int size = 36;
            public Color color = Ink;
            public int box;
            public bool struck;
            public float indent;
            public int gap;
            public bool title;
        }

        struct Note
        {
            public float time;
            public string text;
        }

        struct Quad
        {
            public Vector2 a, b, c, d;
            public Vector2 ua, ub, uc, ud;
            public Color color;
        }

        static readonly Color Ink = new Color(0.08f, 0.12f, 0.34f, 0.94f);
        static readonly Color MarkerInk = new Color(0.06f, 0.05f, 0.045f, 0.96f);
        static readonly Color PencilInk = new Color(0.2f, 0.2f, 0.22f, 0.78f);
        static readonly Color RedInk = new Color(0.64f, 0.07f, 0.05f, 0.92f);
        static readonly Color RuleInk = new Color(0.32f, 0.44f, 0.62f, 0.32f);
        static readonly Color MarginInk = new Color(0.7f, 0.22f, 0.2f, 0.38f);

        const float Left = 116f;
        const float Right = Width - 44f;
        const float FirstRule = 188f;
        const float Rule = 47f;
        const float Bottom = Height - 52f;

        DemoGame game;
        readonly List<Note> notes = new List<Note>();
        readonly string[] signatures = new string[PageCount];
        readonly List<Quad> under = new List<Quad>();
        readonly List<Quad> over = new List<Quad>();
        readonly Dictionary<Font, List<Quad>> text = new Dictionary<Font, List<Quad>>();
        string placa;
        int growthsSeen;
        Font marker, hand, pencil;
        Material ink, glyphs;
        Texture2D blot;
        bool fontRebuilt;
        float nextCheck;

        public static CuadernoContenido Ensure(DemoGame owner)
        {
            if (Instance) return Instance;
            if (!owner) return null;
            Instance = owner.gameObject.AddComponent<CuadernoContenido>();
            Instance.game = owner;
            return Instance;
        }

        public static void Anotar(DemoGame owner, string message)
        {
            var content = Ensure(owner);
            if (content) content.AddNote(message);
        }

        public static void PlacaLeida(DemoGame owner, string message)
        {
            var content = Ensure(owner);
            if (!content || content.placa == message) return;
            content.placa = message;
            content.AddNote("Placa de la entrada, leída con la vela: " + message);
        }

        void AddNote(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            message = message.Replace("\n", " ").Trim();
            float now = game ? game.ElapsedSeconds : 0f;
            if (notes.Count > 0 && notes[notes.Count - 1].text == message && now - notes[notes.Count - 1].time < 20f) return;
            notes.Add(new Note { time = now, text = message });
            if (notes.Count > 40) notes.RemoveAt(0);
            NotesVersion++;
            nextCheck = 0f;
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            marker = Resources.Load<Font>("Cuaderno/Fuentes/PermanentMarker-Regular");
            hand = Resources.Load<Font>("Cuaderno/Fuentes/Kalam-Regular");
            pencil = Resources.Load<Font>("Cuaderno/Fuentes/PatrickHand-Regular");
            if (!marker || !hand || !pencil) Debug.LogError("Faltan las fuentes del cuaderno en Resources/Cuaderno/Fuentes.", this);
            var shader = Shader.Find("Hidden/Gamejam/CuadernoTinta");
            if (!shader) Debug.LogError("Falta el shader Hidden/Gamejam/CuadernoTinta.", this);
            ink = new Material(shader) { hideFlags = HideFlags.DontSave };
            glyphs = new Material(shader) { hideFlags = HideFlags.DontSave };
            glyphs.SetFloat("_AlphaOnly", 1f);
            Paper = MakePaper();
            blot = MakeBlot();
            Pages = new RenderTexture[PageCount];
            for (int i = 0; i < PageCount; i++)
            {
                var rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Hoja del cuaderno " + (i + 1),
                    useMipMap = true,
                    autoGenerateMips = false,
                    anisoLevel = 8,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                rt.Create();
                Pages[i] = rt;
            }
            Font.textureRebuilt += OnFontRebuilt;
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            if (Instance == this) Instance = null;
            if (Pages != null) foreach (var rt in Pages) if (rt) { rt.Release(); Destroy(rt); }
            if (Paper) Destroy(Paper);
            if (blot) Destroy(blot);
            if (ink) Destroy(ink);
            if (glyphs) Destroy(glyphs);
        }

        void OnFontRebuilt(Font font)
        {
            if (font == marker || font == hand || font == pencil)
            {
                fontRebuilt = true;
                for (int i = 0; i < signatures.Length; i++) signatures[i] = null;
            }
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.25f;
            Refresh();
        }

        public void Refresh()
        {
            if (!game) game = GetComponent<DemoGame>();
            if (!game || Pages == null) return;
            for (int page = 0; page < PageCount; page++)
            {
                var items = page == 0 ? Procedure() : page == 1 ? Notes() : Manual();
                string signature = Signature(items);
                if (signature == signatures[page] && Pages[page].IsCreated()) continue;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    fontRebuilt = false;
                    Draw(page, items);
                    if (!fontRebuilt) break;
                }
                signatures[page] = signature;
                if (page == 0) ProcedureVersion++;
            }
        }

        static string Signature(List<Item> items)
        {
            var sb = new StringBuilder();
            foreach (var item in items) sb.Append(item.text).Append(item.struck ? '#' : '|').Append(item.box).Append('\n');
            return sb.ToString();
        }

        // ---------- contenido ----------

        List<Item> Procedure()
        {
            var list = new List<Item>();
            var inv = game.Investigation;
            list.Add(new Item { text = "ORDEN DE TRABAJO", face = Face.Marker, size = 60, color = MarkerInk, title = true });
            if (!inv)
            {
                list.Add(Line("Esperando el parte del turno...", Face.Pencil, PencilInk));
                return list;
            }
            int evidence = inv.EvidenceCount;
            list.Add(Check("Recuperar los 3 registros de los paneles (" + evidence + "/3)", evidence >= 3));
            var marks = new StringBuilder("Marcas:");
            for (int i = 0; i < 3; i++)
            {
                marks.Append("   ").Append(i + 1).Append(" = ");
                if (!inv.HasEvidence(i)) marks.Append("?");
                else if (inv.FalseRevealed && i == inv.FalseIndex) marks.Append('~').Append(inv.FalseMark).Append("~ ").Append(game.SymbolAt(i));
                else marks.Append(inv.DisplayedSymbol(i));
            }
            list.Add(Sub(marks.ToString(), Ink));

            int growths = 0;
            foreach (var station in inv.Stations) if (station && station.Contaminated) growths++;
            growthsSeen = Mathf.Max(growthsSeen, growths);
            if (growthsSeen > 0)
                list.Add(Check(growths > 0 ? "Quemar los brotes de los paneles con la vela en la mano (quedan " + growths + ")"
                    : "Quemar los brotes de los paneles con la vela en la mano", growths == 0));

            list.Add(Check("Contrastar los registros en la consola: los dos a la vez", inv.Contrasted));
            if (inv.Contrasted) list.Add(Sub("El registro " + (inv.FalseIndex + 1) + " mentía.", PencilInk));
            else if (evidence >= 3) list.Add(Sub("Uno de los tres miente.", PencilInk));

            if (inv.RequiredPower <= 0) list.Add(Check("Red: esta vez no hace falta encender luces", true));
            else
            {
                int surveyed = Mathf.Min(inv.SurveyedPower, inv.RequiredPower);
                string lights = inv.RequiredPower == 1 ? "1 luz auxiliar" : inv.RequiredPower + " luces auxiliares";
                list.Add(Check("Encender " + lights + " (" + surveyed + "/" + inv.RequiredPower + ") y " + (inv.RequiredPower == 1 ? "apagarla" : "apagarlas") + " antes del aviso", inv.SurveyedPower >= inv.RequiredPower));
            }
            foreach (var station in inv.Stations)
            {
                if (!station || !station.IsPowerOn) continue;
                list.Add(Sub(station.Status == "DELATANDO" ? "¡" + station.name + " delata! Apagarla ya."
                    : station.name + " encendida: apagarla en " + Mathf.CeilToInt(station.SecondsLeft / 5f) * 5 + " s", RedInk));
            }

            int done = game.CompletedCount;
            if (inv.Contrasted)
            {
                var order = new StringBuilder("Soportes en orden: ");
                for (int i = 0; i < 3; i++)
                {
                    if (i > 0) order.Append(" > ");
                    string symbol = game.SymbolAt(i);
                    order.Append(i < done ? "~" + symbol + "~" : symbol);
                }
                list.Add(Check(order.ToString(), done >= 3));
            }
            else list.Add(Check("Soportes A, B y C en el orden verificado", done >= 3));
            list.Add(Sub("Uno sujeta el asa; el otro gira la manivela.", PencilInk));
            if (game.mechanisms != null)
                foreach (var mechanism in game.mechanisms)
                    if (mechanism && mechanism.unstable)
                        list.Add(Sub("¡" + mechanism.symbol + " desajustado! Mantener 1,5 s, quieto y sin manivela.", RedInk));

            list.Add(Check("Contener 25 s: uno en TALLER 04 y otro en BOMBAS 05", game.Finished && game.Won));
            if (game.Containing && !game.Finished)
                list.Add(Sub("Contención " + Mathf.RoundToInt(game.Containment01 * 20f) * 5 + "%. " + (game.SealsOccupied ? "Aguantar." : "Una persona en cada sala."), RedInk));
            return list;
        }

        List<Item> Notes()
        {
            var list = new List<Item>();
            list.Add(new Item { text = "NOTAS", face = Face.Marker, size = 60, color = MarkerInk, title = true });
            if (notes.Count == 0)
            {
                list.Add(Line("Nada todavía. Lo que pase, lo apunto aquí.", Face.Pencil, PencilInk));
                return list;
            }
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                int total = Mathf.Max(0, Mathf.FloorToInt(notes[i].time));
                string clock = (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
                list.Add(new Item { text = clock + "  " + notes[i].text, face = Face.Pencil, size = 34, color = i == notes.Count - 1 ? Ink : PencilInk });
            }
            return list;
        }

        List<Item> Manual()
        {
            var list = new List<Item>();
            list.Add(new Item { text = "CÓMO", face = Face.Marker, size = 60, color = MarkerInk, title = true });
            list.Add(Line("J1: WASD mover, ratón o F H T G mirar, E usar, Q soltar, Shift izq. correr, Tab cuaderno.", Face.Hand, Ink, 31));
            list.Add(Line("J2: flechas mover, J L I K mirar, U usar, O soltar, Shift der. correr, P cuaderno.", Face.Hand, Ink, 31));
            list.Add(Line("Hojas: A / D o flechas izq. / der. Mientras lees, no te mueves.", Face.Pencil, PencilInk, 31));
            list.Add(Line("Una cosa por mano. Lejos de la vela, el peligro sube.", Face.Hand, Ink, 31, 1));
            list.Add(Line("Paneles: mantener usar. Si hay brote, quemarlo con la vela en la mano.", Face.Hand, Ink, 31));
            list.Add(Line("Consola: los dos mantienen usar a la vez.", Face.Hand, Ink, 31));
            list.Add(Line("Soporte: uno en el asa lateral, otro en la manivela puesta. Toque corto con las manos libres: sacar la manivela.", Face.Hand, Ink, 31));
            list.Add(Line("Desajuste: mantener 1,5 s, quieto y sin manivela.", Face.Hand, Ink, 31));
            list.Add(Line("Dos roces de la sombra fuera de la vela y se acabó.", Face.Hand, RedInk, 31));
            return list;
        }

        static Item Check(string message, bool done) => new Item { text = message, box = done ? 2 : 1, struck = done, gap = 0 };
        static Item Sub(string message, Color color) => new Item { text = message, face = Face.Pencil, size = 32, color = color, indent = 44f };
        static Item Line(string message, Face face, Color color, int size = 34, int gap = 0) => new Item { text = message, face = face, size = size, color = color, gap = gap };

        // ---------- dibujo ----------

        Font FontOf(Face face) => face == Face.Marker ? marker : face == Face.Pencil ? pencil : hand;

        void Draw(int page, List<Item> items)
        {
            under.Clear();
            over.Clear();
            foreach (var list in text.Values) list.Clear();
            foreach (var item in items)
            {
                var font = FontOf(item.face);
                if (font) font.RequestCharactersInTexture(Plain(item.text), item.size, FontStyle.Normal);
            }
            if (page == 1) RequestDigits();

            Rules(page);
            int line = 0;
            uint seed = (uint)(page * 7919 + 17);
            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var font = FontOf(item.face);
                if (!font) continue;
                ParseStrike(item.text, out string plain, out bool[] mask);
                if (item.title)
                {
                    float width = Measure(font, plain, item.size);
                    float x = Left - 6f + Hash(seed, 3) * 10f;
                    DrawString(font, plain, item.size, x, FirstRule - 52f, item.color, seed + 11, null, null, 0.004f);
                    Stroke(over, new Vector2(x - 4f, FirstRule - 40f), new Vector2(x + width + 10f, FirstRule - 43f), 3.2f, MarkerInk * new Color(1, 1, 1, 0.8f), seed + 5, 1.6f);
                    continue;
                }
                line += item.gap;
                float x0 = Left + item.indent + (item.box > 0 ? 44f : 0f);
                var wrapped = Wrap(font, plain, item.size, Right - x0);
                int start = 0;
                for (int w = 0; w < wrapped.Count; w++)
                {
                    float baseline = FirstRule + line * Rule - 10f;
                    if (baseline > Bottom)
                    {
                        Flush(page);
                        return;
                    }
                    string segment = wrapped[w];
                    var xs = new List<float>();
                    var ends = new List<float>();
                    uint lineSeed = seed + (uint)(index * 131 + w * 17);
                    if (w == 0 && item.box > 0) Box(Left + item.indent, baseline, item.box == 2, lineSeed);
                    float slope = (Hash(lineSeed, 1) - 0.5f) * 0.01f;
                    DrawString(font, segment, item.size, x0, baseline, item.color, lineSeed, xs, ends, slope);
                    if (item.struck)
                    {
                        if (xs.Count > 0) Strike(xs[0], ends[ends.Count - 1], baseline, item.size, slope, x0, lineSeed);
                    }
                    else
                    {
                        int run = -1;
                        for (int c = 0; c <= segment.Length; c++)
                        {
                            bool on = c < segment.Length && start + c < mask.Length && mask[start + c] && segment[c] != ' ';
                            if (on && run < 0) run = c;
                            if (!on && run >= 0)
                            {
                                Strike(xs[run], ends[c - 1], baseline, item.size, slope, x0, lineSeed + (uint)c);
                                run = -1;
                            }
                        }
                    }
                    start += segment.Length;
                    while (start < plain.Length && plain[start] == ' ') start++;
                    line++;
                }
            }
            Flush(page);
        }

        void RequestDigits()
        {
            if (pencil) pencil.RequestCharactersInTexture("0123456789:", 34, FontStyle.Normal);
        }

        void Flush(int page)
        {
            if (fontRebuilt) return;
            var rt = Pages[page];
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Width, Height, 0);
            GL.Clear(false, true, new Color(0.8f, 0.76f, 0.66f, 1f));
            ink.mainTexture = Paper;
            float shift = page * 0.31f;
            DrawQuads(ink, new List<Quad> { Rect(new Rect(0, 0, Width, Height), new Rect(shift % 1f * 0.25f, 0f, 0.75f, 1f), Color.white) });
            ink.mainTexture = blot;
            DrawQuads(ink, Stains(page));
            ink.mainTexture = Texture2D.whiteTexture;
            DrawQuads(ink, under);
            foreach (var pair in text)
            {
                if (pair.Value.Count == 0 || !pair.Key) continue;
                glyphs.mainTexture = pair.Key.material.mainTexture;
                DrawQuads(glyphs, pair.Value);
            }
            ink.mainTexture = Texture2D.whiteTexture;
            DrawQuads(ink, over);
            GL.PopMatrix();
            RenderTexture.active = previous;
            rt.GenerateMips();
            under.Clear();
            over.Clear();
        }

        List<Quad> Stains(int page)
        {
            var list = new List<Quad>();
            uint seed = (uint)(page * 101 + 7);
            int count = 3 + page;
            for (int i = 0; i < count; i++)
            {
                float size = Mathf.Lerp(90f, 260f, Hash(seed, (uint)i * 3));
                float x = Hash(seed, (uint)i * 3 + 1) * Width;
                float y = Hash(seed, (uint)i * 3 + 2) * Height;
                var color = new Color(0.35f, 0.25f, 0.12f, Mathf.Lerp(0.05f, 0.12f, Hash(seed, (uint)i + 40)));
                list.Add(Rect(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), new Rect(0, 0, 1, 1), color));
            }
            if (page == 1)
            {
                // huella de dedos con algo oscuro, seco
                for (int i = 0; i < 3; i++)
                {
                    float size = 46f + i * 10f;
                    list.Add(Rect(new Rect(Width - 150f + i * 26f, Height - 190f + i * 34f, size, size * 1.3f), new Rect(0, 0, 1, 1), new Color(0.3f, 0.06f, 0.04f, 0.28f)));
                }
            }
            return list;
        }

        void Rules(int page)
        {
            var color = Linear(RuleInk);
            for (float y = FirstRule; y < Height - 30f; y += Rule)
                under.Add(Bar(new Vector2(18f, y), new Vector2(Width - 10f, y), 1.6f, color));
            under.Add(Bar(new Vector2(Left - 18f, 30f), new Vector2(Left - 18f, Height - 10f), 1.8f, Linear(MarginInk)));
            under.Add(Bar(new Vector2(Left - 13f, 30f), new Vector2(Left - 13f, Height - 10f), 1.2f, Linear(MarginInk)));
            string number = (page + 1).ToString();
            if (pencil)
            {
                pencil.RequestCharactersInTexture(number, 34, FontStyle.Normal);
                DrawString(pencil, number, 34, Width - 70f, Height - 22f, PencilInk, (uint)(page + 900), null, null, 0f);
            }
        }

        void Box(float x, float baseline, bool done, uint seed)
        {
            float s = 27f;
            float top = baseline - s - 3f;
            var color = Ink;
            var a = new Vector2(x, top);
            var b = new Vector2(x + s, top + 1f);
            var c = new Vector2(x + s + 1f, top + s);
            var d = new Vector2(x - 1f, top + s - 1f);
            Stroke(over, a, b, 2.4f, color, seed + 1, 0.8f);
            Stroke(over, b, c, 2.4f, color, seed + 2, 0.8f);
            Stroke(over, c, d, 2.4f, color, seed + 3, 0.8f);
            Stroke(over, d, a + new Vector2(0f, -2f), 2.4f, color, seed + 4, 0.8f);
            if (!done) return;
            var p0 = new Vector2(x + 4f, top + s * 0.5f);
            var p1 = new Vector2(x + s * 0.42f, top + s - 2f);
            var p2 = new Vector2(x + s + 8f, top - 10f);
            Stroke(over, p0, p1, 4.2f, RedInk, seed + 5, 0.6f);
            Stroke(over, p1, p2, 4.2f, RedInk, seed + 6, 1.2f);
        }

        void Strike(float from, float to, float baseline, int size, float slope, float x0, uint seed)
        {
            float y = baseline - size * 0.3f;
            var a = new Vector2(from - 6f, y + (from - x0) * slope + (Hash(seed, 9) - 0.5f) * 3f);
            var b = new Vector2(to + 6f, y + (to - x0) * slope + (Hash(seed, 10) - 0.5f) * 3f);
            Stroke(over, a, b, 4.6f, RedInk, seed + 21, 2.2f);
        }

        // Trazo de rotulador: segmentos cortos con ondulación y grosor irregular.
        void Stroke(List<Quad> target, Vector2 a, Vector2 b, float width, Color color, uint seed, float wobble)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / 14f), 2, 80);
            Vector2 dir = (b - a) / Mathf.Max(length, 0.001f);
            Vector2 normal = new Vector2(-dir.y, dir.x);
            float phase = Hash(seed, 1) * 6.28f;
            Vector2 previous = a;
            var linear = Linear(color);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(a, b, t) + normal * (Mathf.Sin(t * 9.4f + phase) * wobble + (Hash(seed, (uint)i + 3) - 0.5f) * wobble * 0.6f);
                float w = width * Mathf.Lerp(0.8f, 1.1f, Hash(seed, (uint)i + 60));
                var tint = linear;
                tint.a *= Mathf.Lerp(0.82f, 1f, Hash(seed, (uint)i + 90));
                target.Add(Bar(previous, p, w, tint));
                previous = p;
            }
        }

        void DrawString(Font font, string value, int size, float x, float baseline, Color color, uint seed, List<float> starts, List<float> ends, float slope)
        {
            if (!text.TryGetValue(font, out var list)) { list = new List<Quad>(); text[font] = list; }
            float originX = x;
            var linear = Linear(color);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (!font.GetCharacterInfo(ch, out var info, size, FontStyle.Normal))
                {
                    starts?.Add(x);
                    x += size * 0.3f;
                    ends?.Add(x);
                    continue;
                }
                uint h = seed * 31u + (uint)i * 7u;
                float jitterY = (Hash(h, 1) - 0.5f) * 2.4f + Mathf.Sin((x + seed) * 0.012f) * 1.4f + (x - originX) * slope;
                float angle = (Hash(h, 2) - 0.5f) * 5f * Mathf.Deg2Rad;
                float scale = Mathf.Lerp(0.95f, 1.05f, Hash(h, 3));
                float left = x + info.minX * scale;
                float right = x + info.maxX * scale;
                float top = baseline + jitterY - info.maxY * scale;
                float bottom = baseline + jitterY - info.minY * scale;
                starts?.Add(left);
                ends?.Add(right);
                if (ch != ' ')
                {
                    var center = new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
                    var tint = linear;
                    tint.a *= Mathf.Lerp(0.82f, 1f, Hash(h, 4));
                    list.Add(new Quad
                    {
                        a = Rotate(new Vector2(left, top), center, angle),
                        b = Rotate(new Vector2(right, top), center, angle),
                        c = Rotate(new Vector2(right, bottom), center, angle),
                        d = Rotate(new Vector2(left, bottom), center, angle),
                        ua = info.uvTopLeft,
                        ub = info.uvTopRight,
                        uc = info.uvBottomRight,
                        ud = info.uvBottomLeft,
                        color = tint
                    });
                }
                x += info.advance * scale + (Hash(h, 5) - 0.5f) * 1.2f;
            }
        }

        static float Measure(Font font, string value, int size)
        {
            float width = 0f;
            foreach (char ch in value)
                width += font.GetCharacterInfo(ch, out var info, size, FontStyle.Normal) ? info.advance : size * 0.3f;
            return width;
        }

        static List<string> Wrap(Font font, string value, int size, float width)
        {
            var lines = new List<string>();
            var words = value.Split(' ');
            var current = new StringBuilder();
            foreach (var word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && Measure(font, candidate, size) > width)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
                else
                {
                    current.Clear();
                    current.Append(candidate);
                }
            }
            if (current.Length > 0) lines.Add(current.ToString());
            return lines;
        }

        static string Plain(string value) => value.Replace("~", string.Empty);

        static void ParseStrike(string value, out string plain, out bool[] mask)
        {
            var sb = new StringBuilder();
            var flags = new List<bool>();
            bool on = false;
            foreach (char ch in value)
            {
                if (ch == '~') { on = !on; continue; }
                sb.Append(ch);
                flags.Add(on);
            }
            plain = sb.ToString();
            mask = flags.ToArray();
        }

        static Vector2 Rotate(Vector2 p, Vector2 center, float angle)
        {
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            Vector2 d = p - center;
            return center + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        static Quad Bar(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            Vector2 extend = dir * (width * 0.35f);
            return new Quad
            {
                a = a - extend + n, b = b + extend + n, c = b + extend - n, d = a - extend - n,
                ua = new Vector2(0, 1), ub = new Vector2(1, 1), uc = new Vector2(1, 0), ud = new Vector2(0, 0),
                color = color
            };
        }

        static Quad Rect(Rect r, Rect uv, Color color) => new Quad
        {
            a = new Vector2(r.xMin, r.yMin), b = new Vector2(r.xMax, r.yMin), c = new Vector2(r.xMax, r.yMax), d = new Vector2(r.xMin, r.yMax),
            ua = new Vector2(uv.xMin, uv.yMax), ub = new Vector2(uv.xMax, uv.yMax), uc = new Vector2(uv.xMax, uv.yMin), ud = new Vector2(uv.xMin, uv.yMin),
            color = Linear(color)
        };

        static void DrawQuads(Material material, List<Quad> quads)
        {
            if (quads.Count == 0) return;
            material.SetPass(0);
            GL.Begin(GL.QUADS);
            foreach (var q in quads)
            {
                GL.Color(q.color);
                GL.TexCoord(q.ua); GL.Vertex3(q.a.x, q.a.y, 0f);
                GL.TexCoord(q.ub); GL.Vertex3(q.b.x, q.b.y, 0f);
                GL.TexCoord(q.uc); GL.Vertex3(q.c.x, q.c.y, 0f);
                GL.TexCoord(q.ud); GL.Vertex3(q.d.x, q.d.y, 0f);
            }
            GL.End();
        }

        static Color Linear(Color color)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return color;
            var linear = color.linear;
            linear.a = color.a;
            return linear;
        }

        static float Hash(uint seed, uint salt)
        {
            uint h = seed * 747796405u + salt * 2891336453u + 1442695041u;
            h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
            h = (h >> 22) ^ h;
            return (h & 0xFFFFFF) / 16777215f;
        }

        // ---------- papel ----------

        static Texture2D MakePaper()
        {
            const int w = 512, h = 809;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Papel del cuaderno", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var pixels = new Color32[w * h];
            var base0 = new Color(0.84f, 0.8f, 0.69f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float cloud = Mathf.PerlinNoise(u * 3.1f + 11f, v * 4.7f + 3f) * 0.6f + Mathf.PerlinNoise(u * 9f + 5f, v * 13f + 9f) * 0.4f;
                    float fiber = Mathf.PerlinNoise(u * 180f, v * 23f) * 0.5f + Mathf.PerlinNoise(u * 41f + 2f, v * 260f) * 0.5f;
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float age = 1f - Mathf.Clamp01(edge * 9f);
                    float shade = 0.9f + cloud * 0.12f + (fiber - 0.5f) * 0.05f - age * age * 0.16f;
                    var c = base0 * shade;
                    c = Color.Lerp(c, new Color(0.7f, 0.6f, 0.42f), age * age * 0.35f + Mathf.Max(0f, cloud - 0.62f) * 0.5f);
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        static Texture2D MakeBlot()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Mancha del cuaderno", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x / (s - 1f) * 2f - 1f, dy = y / (s - 1f) * 2f - 1f;
                    float angle = Mathf.Atan2(dy, dx);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (0.75f + Mathf.PerlinNoise(Mathf.Cos(angle) * 2f + 4f, Mathf.Sin(angle) * 2f + 4f) * 0.25f);
                    float body = Mathf.Clamp01(1f - r) * 0.55f;
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.92f) / 0.07f, 2f));
                    float a = Mathf.Clamp01(body + ring) * Mathf.Lerp(0.7f, 1f, Mathf.PerlinNoise(x * 0.08f, y * 0.08f));
                    pixels[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }
    }
}

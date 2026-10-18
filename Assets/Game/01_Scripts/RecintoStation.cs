using UnityEngine;
using UnityEngine.Rendering;
namespace Gamejam2026
{
    public class RecintoStation : MonoBehaviour
    {
        public enum Role { Evidence, Power, Synchronize }
        RecintoInvestigation director;
        DemoGame game;
        Role role;
        int evidenceIndex, taps;
        bool holdEvidence, complete, surveyed, betrayed;
        float progress, tapDeadline, expires, lampDuration;
        readonly int[] heldFrame = { -1, -1 };
        readonly bool[] contrastHolding = new bool[2];
        Light workLight;
        Renderer lamp;
        TextMesh label;
        RecintoContamination contamination;
        public bool IsPowerOn => role == Role.Power && complete;
        public bool Surveyed => role == Role.Power && surveyed;
        public bool IsContrastStation => role == Role.Synchronize;
        public bool Contaminated => contamination && !contamination.Consumed;
        public float Progress01 => complete || Contaminated ? 0f
            : role == Role.Evidence && !holdEvidence ? taps / 3f
            : progress / (role == Role.Evidence ? 2f : 2.5f);
        public bool AcceptsWork => !Contaminated && !complete && (role != Role.Synchronize || director.EvidenceCount == 3);
        public float SecondsLeft => IsPowerOn ? Mathf.Max(0, expires - game.ElapsedSeconds) : 0;
        public string Status => IsPowerOn ? (betrayed ? "DELATANDO" : Mathf.CeilToInt(SecondsLeft) + " s") : "apagada";
        public void Configure(RecintoInvestigation owner, DemoGame session, Role selected, int index, bool hold)
        {
            director = owner; game = session; role = selected; evidenceIndex = index; holdEvidence = hold;
            name = selected == Role.Evidence ? "Registro " + (index + 1) : selected == Role.Power ? "Luz " + (index == 4 ? "X" : "Y") : "Consola de contraste";
            lampDuration = Random.Range(45f, 70f);
            BuildAssembly();
            var target = gameObject.AddComponent<BoxCollider>();
            if (selected == Role.Evidence) { target.center = new Vector3(0f, 0.52f, 0.05f); target.size = new Vector3(1.18f, 1.05f, 0.78f); }
            else if (selected == Role.Power) { target.center = new Vector3(0f, 0.42f, 0f); target.size = new Vector3(0.95f, 0.85f, 0.72f); }
            else { target.center = new Vector3(0f, 0.85f, 0.08f); target.size = new Vector3(0.9f, 1.55f, 0.55f); }
            var pilot = GameObject.CreatePrimitive(PrimitiveType.Sphere); pilot.name = "Piloto de estado"; pilot.transform.SetParent(transform, false);
            if (selected == Role.Evidence) pilot.transform.localPosition = new Vector3(0.42f, 1.18f, 0.22f);
            else if (selected == Role.Power) pilot.transform.localPosition = new Vector3(0.28f, 0.72f, -0.22f);
            else pilot.transform.localPosition = new Vector3(0.32f, 1.28f, -0.12f);
            pilot.transform.localScale = Vector3.one * 0.07f;
            Destroy(pilot.GetComponent<Collider>()); lamp = pilot.GetComponent<Renderer>(); lamp.sharedMaterial = owner.DarkLamp;
            lamp.shadowCastingMode = ShadowCastingMode.Off;
            var plate = new GameObject("Placa de procedimiento"); plate.transform.SetParent(transform, false);
            plate.transform.localPosition = new Vector3(0, 1.05f, -0.28f); plate.transform.localRotation = Quaternion.identity;
            label = plate.AddComponent<TextMesh>(); label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.characterSize = .018f; label.fontSize = 48; label.color = new Color(.83f,.79f,.64f);
            UpdateLabel();
            if (selected != Role.Power) return;
            var fixture = new GameObject("Farol de trabajo"); fixture.transform.SetParent(transform, false);
            fixture.transform.localPosition = new Vector3(0.02f, 0.82f, -0.22f);
            workLight = fixture.AddComponent<Light>(); workLight.type = LightType.Point; workLight.color = new Color(.8f,.9f,.78f);
            workLight.range = 8; workLight.intensity = 2.7f; workLight.enabled = false;
        }
        void BuildAssembly()
        {
            if (role == Role.Evidence) BuildRegistro();
            else if (role == Role.Synchronize) BuildConsola();
            else BuildLuz();
        }
        void BuildRegistro()
        {
            var cart = Kit("cart", Vector3.zero, Quaternion.identity);
            Sit(cart, 0f);
            var box = Kit("toolbox", new Vector3(-0.18f, 0f, 0.04f), Quaternion.Euler(0f, 18f, 0f));
            Sit(box, 0.97f);
            var binder = Resources.Load<GameObject>("Cuaderno/binder_closed");
            if (binder)
            {
                var note = Instantiate(binder, transform);
                note.name = "Cuaderno del registro";
                note.transform.localPosition = new Vector3(0.26f, 0f, 0.02f);
                note.transform.localRotation = Quaternion.Euler(0f, 22f, 0f);
                note.transform.localScale = Vector3.one * 1.4f;
                Strip(note);
                var leather = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var diff = Resources.Load<Texture2D>("Cuaderno/binder_notebook_diff_2k");
                if (diff) leather.SetTexture("_BaseMap", diff);
                leather.color = Color.white;
                if (leather.HasProperty("_BaseColor")) leather.SetColor("_BaseColor", Color.white);
                leather.SetFloat("_Smoothness", 0.2f);
                foreach (var renderer in note.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = leather;
                Sit(note, 0.97f);
            }
        }
        void BuildConsola()
        {
            HangPanel();
            var stool = Kit("stool", new Vector3(0.58f, 0f, -0.12f), Quaternion.Euler(0f, -25f, 0f));
            Sit(stool, 0f);
            var radio = Kit("boombox", new Vector3(-0.48f, 0f, 0.18f), Quaternion.Euler(0f, 180f, 0f));
            Sit(radio, 0f);
        }
        void BuildLuz()
        {
            var gen = Kit("generator", new Vector3(0f, 0f, 0.04f), Quaternion.Euler(0f, 180f, 0f));
            Sit(gen, 0f);
            var box = Kit("toolbox", new Vector3(0.52f, 0f, 0.12f), Quaternion.Euler(0f, -12f, 0f));
            Sit(box, 0f);
        }
        void HangPanel()
        {
            var source = Resources.Load<GameObject>("Production/Station");
            if (!source)
            {
                Debug.LogError("Falta Production/Station. Aplica Gamejam/Producción/Aplicar arte sin reconstruir mapa.", this);
                return;
            }
            var panel = Instantiate(source, transform, false);
            panel.name = "Panel de muro";
            Strip(panel);
            panel.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            panel.transform.localScale = Vector3.one * 0.52f;
            panel.transform.localPosition = Vector3.zero;
            var bounds = BoundsOf(panel);
            Vector3 localMax = transform.InverseTransformPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.max.z));
            Vector3 localMin = transform.InverseTransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            panel.transform.localPosition += new Vector3(-localCenter.x, 1.15f - bounds.center.y, 0.34f - localMax.z);
            foreach (var renderer in panel.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
        }
        GameObject Kit(string key, Vector3 localPos, Quaternion extra)
        {
            var source = Resources.Load<GameObject>("Atrezzo/" + key);
            if (!source)
            {
                Debug.LogWarning("Falta Resources/Atrezzo/" + key + ". Ejecuta Gamejam/Mapa definitivo/Colocar atrezzo.", this);
                return null;
            }
            var obj = Instantiate(source, transform);
            obj.name = key;
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = extra * obj.transform.localRotation;
            Strip(obj);
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = key == "cart" || key == "generator" ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            return obj;
        }
        static void Strip(GameObject obj)
        {
            if (!obj) return;
            foreach (var collider in obj.GetComponentsInChildren<Collider>()) Destroy(collider);
        }
        static void Sit(GameObject obj, float y)
        {
            if (!obj) return;
            var bounds = BoundsOf(obj);
            obj.transform.position += Vector3.up * (y - bounds.min.y);
        }
        static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 0.1f);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        void UpdateLabel()
        {
            if (!label) return;
            label.text = role == Role.Evidence ? name.ToUpperInvariant() + "\n" + director.DisplayedSymbol(evidenceIndex) + (director.FalseRevealed && director.FalseIndex == evidenceIndex ? " (CORREGIDO)" : "")
                : role == Role.Power ? name.ToUpperInvariant() + "\n" + Status : "CONTRASTE\n" + (complete ? "VERIFICADO" : "DOS OPERARIOS");
        }
        public string Hint(DemoPlayer player)
        {
            if (player == null) return string.Empty;
            string key = "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] ";
            if (Contaminated) return key + "Con la vela en la mano, mantener 2 s sobre el brote de " + name;
            if (IsPowerOn) return key + "Apagar " + name + (betrayed ? " — está delatando vuestra posición" : " — delata en " + Status);
            if (complete)
            {
                if (role == Role.Evidence) return name + " — Marca " + director.DisplayedSymbol(evidenceIndex) + (director.FalseRevealed && director.FalseIndex == evidenceIndex ? " (CORREGIDO Y VERIFICADO)" : "");
                if (role == Role.Synchronize) return "Consola de contraste: VERIFICADA. Se identificó el registro falso; seguid el orden verificado";
                return name + " inspeccionada (red registrada)";
            }
            if (role == Role.Synchronize)
            {
                if (director.EvidenceCount < 3) return "Consola de contraste: recuperad primero los 3 registros (" + director.EvidenceCount + "/3)";
                int myIdx = player.playerIndex;
                int otherIdx = 1 - myIdx;
                bool myHeld = game.PlayerInputEnabled && myIdx >= 0 && myIdx < 2 && contrastHolding[myIdx];
                bool otherHeld = game.PlayerInputEnabled && otherIdx >= 0 && otherIdx < 2 && contrastHolding[otherIdx];
                string myKey = DemoPlayerInput.UseLabel(myIdx);
                string otherKey = DemoPlayerInput.UseLabel(otherIdx);
                int percent = Mathf.RoundToInt(progress / 2.5f * 100f);
                if (myHeld && otherHeld)
                    return "[" + myKey + "] Contrastando " + percent + "%. Mantened ambos hasta el 100%.";
                if (myHeld)
                    return "[" + myKey + "] Esperando al compañero (" + percent + "%). J" + (otherIdx + 1) + " debe mantener [" + otherKey + "] a la vez.";
                if (otherHeld)
                    return "[" + myKey + "] Tu compañero está esperando (" + percent + "%). Mantén pulsado para contrastar.";
                return "[" + myKey + "] Mantener para contrastar. Cooperativo: J" + (myIdx + 1) + " [" + myKey + "] y J" + (otherIdx + 1) + " [" + otherKey + "] a la vez.";
            }
            if (role == Role.Power)
            {
                int percent = Mathf.RoundToInt(progress / 2.5f * 100f);
                return key + "Mantener para encender " + name + " (" + percent + "%). Alumbra la zona " + Mathf.RoundToInt(lampDuration) + " s.";
            }
            if (holdEvidence)
            {
                int percent = Mathf.RoundToInt(progress / 2f * 100f);
                return key + "Mantener para registrar datos (" + percent + "%).";
            }
            return key + "Pulsar para registrar datos (" + taps + "/3 pulsaciones).";
        }
        public void Interact(DemoPlayer player, bool pressed, bool held)
        {
            if (!game || !game.PlayerInputEnabled) return;
            if (Contaminated) { if (pressed) player.Tell("El brote bloquea el panel. Sostén la vela y quémalo durante 2 s."); return; }
            if (IsPowerOn) { if (pressed) SwitchOff(); return; }
            if (!AcceptsWork) return;
            if (role == Role.Evidence && !holdEvidence)
            {
                if (!pressed) return;
                if (game.ElapsedSeconds > tapDeadline) taps = 0;
                taps++; tapDeadline = game.ElapsedSeconds + 4;
                if (taps >= 3) Complete(); return;
            }
            if (held && player.playerIndex >= 0 && player.playerIndex < 2) heldFrame[player.playerIndex] = Time.frameCount;
        }
        void Update()
        {
            if (!game || !game.Playing) return;
            if (IsPowerOn)
            {
                if (!betrayed && SecondsLeft <= 0) { betrayed = true; director.LightBetrayed(this); }
                workLight.intensity = betrayed || SecondsLeft < 8 ? (Mathf.Sin(game.ElapsedSeconds * 27) > -.3f ? 2.7f : .08f) : 2.7f;
            }
            UpdateLabel();
        }
        void LateUpdate()
        {
            for (int i = 0; i < contrastHolding.Length; i++)
                contrastHolding[i] = game && game.PlayerInputEnabled && heldFrame[i] == Time.frameCount;
            if (!AcceptsWork || !game || !game.Playing || (role == Role.Evidence && !holdEvidence)) return;
            bool active = role == Role.Synchronize ? heldFrame[0] == Time.frameCount && heldFrame[1] == Time.frameCount : heldFrame[0] == Time.frameCount || heldFrame[1] == Time.frameCount;
            progress = Mathf.Clamp(progress + (active ? 1 : -.7f) * Time.deltaTime, 0, 2.5f);
            if (progress >= (role == Role.Evidence ? 2 : 2.5f)) Complete();
        }
        void Complete()
        {
            complete = true; progress = 0;
            if (lamp) lamp.sharedMaterial = director.ActiveLamp;
            if (role == Role.Evidence) director.RecordEvidence(evidenceIndex);
            else if (role == Role.Synchronize)
            {
                director.RecordSynchronization();
                for (int i = 0; i < contrastHolding.Length; i++)
                    if (contrastHolding[i]) game.NotifyContrastCompleted(i);
            }
            else
            {
                surveyed = true; betrayed = false; expires = game.ElapsedSeconds + lampDuration;
                workLight.enabled = true; director.RecordPower();
                game.Notify(name + " encendida: " + Mathf.RoundToInt(lampDuration) + " s para apagarla antes de que delate. Red inspeccionada.", 6);
            }
            UpdateLabel();
        }
        void SwitchOff()
        {
            complete = false; progress = 0; betrayed = false; expires = 0;
            if (workLight) workLight.enabled = false;
            if (lamp) lamp.sharedMaterial = director.DarkLamp;
            game.Notify(name + " apagada. Aviso cancelado; la inspección de red se conserva.", 4);
        }
        public void SetContamination(RecintoContamination growth) { contamination = growth; }
        public void ClearContamination(RecintoContamination growth) { if (contamination == growth) contamination = null; }
        public void Corrupt()
        {
            if (Contaminated) return;
            if (role == Role.Power) SwitchOff();
            else { complete = false; progress = 0; taps = 0; director.Invalidate(role == Role.Evidence ? evidenceIndex : -1); if (lamp) lamp.sharedMaterial = director.DarkLamp; }
            director.Contaminate(this);
        }
    }
}

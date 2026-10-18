using UnityEngine;
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
        public bool AcceptsWork => !Contaminated && !complete && (role != Role.Synchronize || director.EvidenceCount == 3);
        public float SecondsLeft => IsPowerOn ? Mathf.Max(0, expires - game.ElapsedSeconds) : 0;
        public string Status => IsPowerOn ? (betrayed ? "DELATANDO" : Mathf.CeilToInt(SecondsLeft) + " s") : "apagada";
        public void Configure(RecintoInvestigation owner, DemoGame session, Role selected, int index, bool hold)
        {
            director = owner; game = session; role = selected; evidenceIndex = index; holdEvidence = hold;
            name = selected == Role.Evidence ? "Registro " + (index + 1) : selected == Role.Power ? "Luz " + (index == 4 ? "X" : "Y") : "Consola de contraste";
            lampDuration = Random.Range(45f, 70f);
            var source = Resources.Load<GameObject>("Production/Station");
            if (source) Instantiate(source, transform, false);
            else Debug.LogError("Falta Production/Station. Aplica Gamejam/Producción/Aplicar arte sin reconstruir mapa.", this);
            var target = gameObject.AddComponent<BoxCollider>(); target.center = new Vector3(0, .85f, 0); target.size = new Vector3(.95f, 1.65f, .5f);
            var pilot = GameObject.CreatePrimitive(PrimitiveType.Sphere); pilot.name = "Piloto de estado"; pilot.transform.SetParent(transform, false);
            pilot.transform.localPosition = new Vector3(.29f, 1.34f, -.28f); pilot.transform.localScale = Vector3.one * .075f;
            Destroy(pilot.GetComponent<Collider>()); lamp = pilot.GetComponent<Renderer>(); lamp.sharedMaterial = owner.DarkLamp;
            var plate = new GameObject("Placa de procedimiento"); plate.transform.SetParent(transform, false);
            plate.transform.localPosition = new Vector3(0, 1.05f, -.28f); plate.transform.localRotation = Quaternion.identity;
            label = plate.AddComponent<TextMesh>(); label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.characterSize = .018f; label.fontSize = 48; label.color = new Color(.83f,.79f,.64f);
            UpdateLabel();
            if (selected != Role.Power) return;
            var fixture = new GameObject("Farol de trabajo"); fixture.transform.SetParent(transform, false); fixture.transform.localPosition = new Vector3(0, 2.4f, 0);
            for (int i = 0; i < 6; i++)
            {
                var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rib.name = "Jaula del farol"; rib.transform.SetParent(fixture.transform, false);
                float angle = i * Mathf.PI / 3; rib.transform.localPosition = new Vector3(Mathf.Cos(angle) * .13f, 0, Mathf.Sin(angle) * .13f);
                rib.transform.localScale = new Vector3(.014f, .19f, .014f); rib.GetComponent<Renderer>().sharedMaterial = director.Metal; Destroy(rib.GetComponent<Collider>());
            }
            var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere); hood.name = "Reflector de metal"; hood.transform.SetParent(fixture.transform, false);
            hood.transform.localPosition = Vector3.up * .22f; hood.transform.localScale = new Vector3(.44f,.08f,.44f);
            hood.GetComponent<Renderer>().sharedMaterial = director.Metal; Destroy(hood.GetComponent<Collider>());
            workLight = fixture.AddComponent<Light>(); workLight.type = LightType.Point; workLight.color = new Color(.8f,.9f,.78f);
            workLight.range = 8; workLight.intensity = 2.7f; workLight.enabled = false;
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
        public void RefreshLabel() { UpdateLabel(); }
        public void CaptureReplica(out float prog, out float seconds, out byte flags)
        {
            prog = progress;
            seconds = SecondsLeft;
            flags = 0;
            if (complete) flags |= 1;
            if (surveyed) flags |= 2;
            if (betrayed) flags |= 4;
            if (workLight && workLight.enabled) flags |= 8;
        }
        public void ApplyReplica(float prog, float seconds, byte flags)
        {
            progress = prog;
            complete = (flags & 1) != 0;
            surveyed = (flags & 2) != 0;
            betrayed = (flags & 4) != 0;
            if (lamp && director) lamp.sharedMaterial = complete ? director.ActiveLamp : director.DarkLamp;
            if (workLight) workLight.enabled = (flags & 8) != 0;
            if (IsPowerOn) expires = game.ElapsedSeconds + seconds;
            UpdateLabel();
        }
        void Update()
        {
            if (!game || !game.Playing) return;
            if (LanAuthority.Simulates && IsPowerOn && !betrayed && SecondsLeft <= 0)
            {
                betrayed = true;
                director.LightBetrayed(this);
            }
            if (IsPowerOn && workLight)
                workLight.intensity = betrayed || SecondsLeft < 8 ? (Mathf.Sin(game.ElapsedSeconds * 27) > -.3f ? 2.7f : .08f) : 2.7f;
            UpdateLabel();
        }
        void LateUpdate()
        {
            if (!LanAuthority.Simulates) return;
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

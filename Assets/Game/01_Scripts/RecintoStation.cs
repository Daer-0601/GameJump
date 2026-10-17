using UnityEngine;

namespace Gamejam2026
{
    // A physical point of work. The director chooses its role anew for each expedition.
    public class RecintoStation : MonoBehaviour
    {
        public enum Role { Evidence, Power, Synchronize }
        RecintoInvestigation director;
        DemoGame game;
        Role role;
        int evidenceIndex;
        bool holdEvidence;
        bool complete;
        float progress;
        float tapDeadline;
        int taps;
        readonly int[] heldFrame = { -1, -1 };
        Light workLight;
        Renderer lamp;

        public bool IsPowerOn => role == Role.Power && complete;

        public void Configure(RecintoInvestigation owner, DemoGame session, Role selected, int index, bool hold)
        {
            director = owner;
            game = session;
            role = selected;
            evidenceIndex = index;
            holdEvidence = hold;
            name = selected == Role.Evidence ? "Registro de campo " + (index + 1) :
                selected == Role.Power ? "Interruptor de luz auxiliar" : "Consola de contraste";

            var casing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            casing.name = "Caja de control";
            casing.transform.SetParent(transform, false);
            casing.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            casing.transform.localScale = new Vector3(0.82f, 1.45f, 0.24f);
            casing.GetComponent<Renderer>().sharedMaterial = owner.Metal;
            // The casing itself is what the players aim at.
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Indicador";
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, 1.24f, -0.137f);
            face.transform.localScale = new Vector3(0.38f, 0.22f, 0.015f);
            face.GetComponent<Collider>().enabled = false;
            lamp = face.GetComponent<Renderer>();
            lamp.sharedMaterial = owner.DarkLamp;

            var label = new GameObject("Placa técnica");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 0.85f, -0.14f);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.text = selected == Role.Evidence ? "REGISTRO" : selected == Role.Power ? "RED AUX." : "CONTRASTE";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.09f;
            text.fontSize = 32;
            text.color = new Color(0.7f, 0.67f, 0.55f);

            if (selected == Role.Power)
            {
                var lightObject = new GameObject("Luz auxiliar");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                workLight = lightObject.AddComponent<Light>();
                workLight.type = LightType.Point;
                workLight.color = new Color(0.8f, 0.9f, 0.78f);
                workLight.range = 8f;
                workLight.intensity = 2.7f;
                workLight.enabled = false;
            }
        }

        public string Hint(DemoPlayer player)
        {
            if (complete) return role == Role.Power ? "Red auxiliar encendida" : "Trabajo completado";
            string key = "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] ";
            if (role == Role.Synchronize) return key + "Ambos: mantener para contrastar " + Mathf.RoundToInt(progress / 2.5f * 100f) + "%";
            if (role == Role.Power) return key + "Mantener para encender luz auxiliar " + Mathf.RoundToInt(progress / 2.5f * 100f) + "%";
            return holdEvidence ? key + "Mantener para recuperar registro " + Mathf.RoundToInt(progress / 2f * 100f) + "%"
                : key + "Pulsar tres veces para recuperar registro " + taps + "/3";
        }

        public void Interact(DemoPlayer player, bool pressed, bool held)
        {
            if (complete || !game || !game.PlayerInputEnabled) return;
            if (role == Role.Evidence && !holdEvidence)
            {
                if (!pressed) return;
                if (Time.time > tapDeadline) taps = 0;
                taps++;
                tapDeadline = Time.time + 4f;
                if (taps >= 3) Complete();
                return;
            }
            if (held && player.playerIndex >= 0 && player.playerIndex < 2)
                heldFrame[player.playerIndex] = Time.frameCount;
        }

        void LateUpdate()
        {
            if (complete || !game || !game.Playing || (role == Role.Evidence && !holdEvidence)) return;
            bool active = role == Role.Synchronize
                ? heldFrame[0] == Time.frameCount && heldFrame[1] == Time.frameCount
                : heldFrame[0] == Time.frameCount || heldFrame[1] == Time.frameCount;
            progress = Mathf.Clamp(progress + (active ? 1f : -0.7f) * Time.deltaTime, 0f, 2.5f);
            if (progress >= (role == Role.Evidence ? 2f : 2.5f)) Complete();
        }

        void Complete()
        {
            complete = true;
            if (lamp) lamp.sharedMaterial = director.ActiveLamp;
            if (workLight) workLight.enabled = true;
            if (role == Role.Evidence) director.RecordEvidence(evidenceIndex);
            else if (role == Role.Synchronize) director.RecordSynchronization();
            else director.RecordPower();
        }

        public void Corrupt()
        {
            if (!IsPowerOn) return;
            complete = false;
            progress = 0f;
            if (workLight) workLight.enabled = false;
            if (lamp) lamp.sharedMaterial = director.DarkLamp;
        }
    }
}

using UnityEngine;

namespace Gamejam2026
{
    public class DemoMechanism : MonoBehaviour
    {
        public int id;
        public string symbol;
        public DemoGame game;
        public Light indicator;
        public Transform wheel;
        public Transform crankSocket;
        public CarryObject mountedCrank;
        public bool done;
        public bool unstable;
        public bool HasCrank => mountedCrank != null;
        public float Hold01 => stabilizeTime <= 0f ? 0f : Mathf.Clamp01(stabilizeProgress / stabilizeTime);
        const float stabilizeTime = 1.5f;
        Quaternion startRotation;
        Quaternion socketStartRotation;
        float stabilizeProgress;
        bool heldThisFrame;

        void Awake()
        {
            if (!crankSocket)
            {
                var existing = transform.Find("Soporte manivela");
                if (existing)
                {
                    crankSocket = existing;
                }
                else
                {
                    var socketObj = new GameObject("Soporte manivela");
                    socketObj.transform.SetParent(transform, false);
                    socketObj.transform.localPosition = new Vector3(0f, 1.2f, -0.52f);
                    socketObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    crankSocket = socketObj.transform;
                }
            }
            socketStartRotation = crankSocket.localRotation;
        }

        void Start()
        {
            if (wheel) startRotation = wheel.localRotation;
        }

        public string GetHint(DemoPlayer player)
        {
            if (player == null) return string.Empty;

            if (HasCrank)
            {
                if (player.Held == null)
                    return "Retirar manivela";
                return "Solta lo que llevas para retirar la manivela";
            }

            if (player.Held != null && !player.Held.isCandle)
                return "Colocar manivela";

            if (player.Held != null && player.Held.isCandle)
                return "Necesitas la manivela (deja la vela con " + DemoPlayerInput.DropLabel(player.playerIndex) + ")";

            return done ? "Mecanismo completado" : "Necesitas la manivela";
        }

        public bool AttachCrank(CarryObject crank, DemoPlayer player)
        {
            if (HasCrank || crank == null || crank.isCandle) return false;
            mountedCrank = crank;
            crank.AttachTo(crankSocket);
            if (player != null && player.Held == crank)
            {
                player.Held = null;
            }
            if (!done)
            {
                game.Activate(this, player);
            }
            return true;
        }

        public bool DetachCrank(DemoPlayer player)
        {
            if (!HasCrank || player == null || player.Held != null) return false;
            var crank = mountedCrank;
            mountedCrank = null;
            crank.PickUp(player);
            return true;
        }

        public void Interact(DemoPlayer player)
        {
            if (player == null) return;

            if (HasCrank)
            {
                if (player.Held == null)
                {
                    DetachCrank(player);
                }
                else
                {
                    player.Tell("Solta lo que llevas para retirar la manivela.");
                }
            }
            else
            {
                if (player.Held != null && !player.Held.isCandle)
                {
                    AttachCrank(player.Held, player);
                }
                else if (player.Held != null && player.Held.isCandle)
                {
                    player.Tell("Necesitas la manivela. Deja la vela con " + DemoPlayerInput.DropLabel(player.playerIndex) + ".");
                }
                else
                {
                    player.Tell(done ? "Mecanismo completado." : "Necesitas la manivela para este soporte.");
                }
            }
        }

        public void Use(DemoPlayer player)
        {
            Interact(player);
        }

        public void Hold(DemoPlayer player, bool holding)
        {
            if (!done || !unstable || !holding || player == null) return;
            heldThisFrame = true;
            stabilizeProgress += Time.deltaTime;
            if (stabilizeProgress < stabilizeTime) return;
            stabilizeProgress = 0f;
            unstable = false;
            game.OnStabilized(this, player);
        }

        void LateUpdate()
        {
            if (!heldThisFrame) stabilizeProgress = 0f;
            heldThisFrame = false;
        }

        void Update()
        {
            if (wheel && done && !unstable)
                wheel.localRotation = Quaternion.Slerp(wheel.localRotation, startRotation * Quaternion.Euler(0f, 0f, 90f), Time.deltaTime * 3f);
            else if (wheel && unstable)
                wheel.localRotation = startRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 8f) * 25f);

            if (crankSocket && wheel)
            {
                Quaternion delta = wheel.localRotation * Quaternion.Inverse(startRotation);
                crankSocket.localRotation = socketStartRotation * delta;
            }

            if (!indicator) return;
            if (unstable) indicator.color = Color.Lerp(new Color(0.95f, 0.45f, 0.05f), new Color(1f, 0.15f, 0.05f), Mathf.PingPong(Time.time * 4f, 1f));
            else indicator.color = done ? new Color(0.3f, 0.85f, 0.65f) : new Color(0.9f, 0.3f, 0.1f);
        }
    }
}

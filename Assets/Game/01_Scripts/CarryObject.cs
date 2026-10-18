using System.Collections;
using UnityEngine;

namespace Gamejam2026
{
    [DefaultExecutionOrder(50)]
    public class CarryObject : MonoBehaviour
    {
        public bool isCandle;
        public string displayName;
        public DemoPlayer owner;
        public Light candleLight;
        Collider[] colliders;
        Coroutine useAnimation;
        public bool Inserting => useAnimation != null;
        Vector3 homePosition;
        Quaternion homeRotation;
        bool homeReady;
        byte appliedAnchor = 255;

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeReady = true;
        }

        public void RestoreHome()
        {
            StopUseAnimation();
            appliedAnchor = 255;
            if (owner)
            {
                owner.Held = null;
                owner = null;
            }
            transform.SetParent(null, true);
            if (homeReady) transform.SetPositionAndRotation(homePosition, homeRotation);
            if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
            if (colliders != null)
                foreach (var col in colliders) if (col) col.enabled = true;
            gameObject.SetActive(true);
        }

        public byte CaptureAnchor(DemoMechanism[] mechanisms, out Vector3 position, out float yaw)
        {
            position = transform.position;
            yaw = transform.eulerAngles.y;
            if (owner) return (byte)(owner.playerIndex == 0 ? 1 : 2);
            if (mechanisms != null)
                for (int i = 0; i < mechanisms.Length; i++)
                    if (mechanisms[i] && mechanisms[i].mountedCrank == this) return (byte)(10 + i);
            return 0;
        }

        public void ApplyAnchor(byte code, Vector3 position, float yaw, DemoPlayer[] players, DemoMechanism[] mechanisms)
        {
            if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
            if (code == 0)
            {
                if (appliedAnchor != 0) ReleaseAnchor(mechanisms);
                appliedAnchor = 0;
                transform.SetParent(null, true);
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                if (colliders != null) foreach (var col in colliders) if (col) col.enabled = true;
                return;
            }
            if (code == appliedAnchor) return;
            ReleaseAnchor(mechanisms);
            appliedAnchor = code;
            if (code == 1 || code == 2)
            {
                int index = code - 1;
                if (players == null || index >= players.Length || !players[index] || !players[index].hand) return;
                owner = players[index];
                owner.Held = this;
                if (colliders != null) foreach (var col in colliders) if (col) col.enabled = false;
                transform.SetParent(owner.hand, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }
            if (code >= 10 && mechanisms != null)
            {
                int index = code - 10;
                if (index < 0 || index >= mechanisms.Length || !mechanisms[index] || !mechanisms[index].crankSocket) return;
                mechanisms[index].SetMountedCrank(this);
                if (colliders != null) foreach (var col in colliders) if (col) col.enabled = false;
                transform.SetParent(mechanisms[index].crankSocket, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        void ReleaseAnchor(DemoMechanism[] mechanisms)
        {
            if (owner)
            {
                if (owner.Held == this) owner.Held = null;
                owner = null;
            }
            if (mechanisms == null) return;
            for (int i = 0; i < mechanisms.Length; i++)
                if (mechanisms[i] && mechanisms[i].mountedCrank == this) mechanisms[i].SetMountedCrank(null);
        }
        void LateUpdate()
        {
            // The imported hand has a different bone basis; wax and flame stay upright.
            if (isCandle && owner && !Inserting)
                transform.rotation = Quaternion.Euler(0, owner.transform.eulerAngles.y, 0);
        }

        public void PickUp(DemoPlayer player)
        {
            if (owner != null || player.Held != null) return;
            StopUseAnimation();
            owner = player;
            player.Held = this;
            foreach (var col in colliders) col.enabled = false;
            transform.SetParent(player.hand, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void AttachTo(Transform socket)
        {
            if (!socket) return;
            StopUseAnimation();
            Vector3 start = Vector3.ClampMagnitude(socket.InverseTransformPoint(transform.position), 0.8f);
            Quaternion rotation = Quaternion.Inverse(socket.rotation) * transform.rotation;
            if (owner != null)
            {
                owner.Held = null;
                owner = null;
            }
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (colliders == null) colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;
            transform.localPosition = start;
            transform.localRotation = rotation;
            useAnimation = StartCoroutine(Mount(socket, start, rotation));
        }

        void StopUseAnimation()
        {
            if (useAnimation == null) return;
            StopCoroutine(useAnimation);
            useAnimation = null;
        }

        IEnumerator Mount(Transform socket, Vector3 start, Quaternion rotation)
        {
            var mechanism = socket.GetComponentInParent<DemoMechanism>();
            float elapsed = 0f;
            while (elapsed < .36f && !owner && socket && transform.parent == socket)
            {
                elapsed += mechanism && mechanism.game.Finished ? Time.unscaledDeltaTime : Time.deltaTime;
                float amount = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .36f));
                transform.localPosition = Vector3.Lerp(start, Vector3.zero, amount);
                transform.localRotation = Quaternion.Slerp(rotation, Quaternion.identity, amount);
                yield return null;
            }
            if (!owner && socket && transform.parent == socket)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            useAnimation = null;
        }

        public void Drop()
        {
            if (owner == null) return;
            StopUseAnimation();
            var player = owner;
            var origin = player.transform.position + Vector3.up * 0.7f;
            var direction = Vector3.ProjectOnPlane(player.view.transform.forward, Vector3.up).normalized;

            // no deja el objeto detras de una pared
            float distance = Physics.Raycast(origin, direction, out var wall, 1.2f) ? Mathf.Max(0.25f, wall.distance - 0.25f) : 1.1f;
            var position = origin + direction * distance;
            if (Physics.Raycast(position, Vector3.down, out var floor, 3f)) position = floor.point + Vector3.up * 0.08f;
            player.Held = null;
            owner = null;
            transform.SetParent(null, true);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            foreach (var col in colliders) col.enabled = true;
        }

        public void AnimateUse(Transform socket)
        {
            if (isCandle || !owner || !socket) return;
            if (useAnimation != null) StopCoroutine(useAnimation);
            useAnimation = StartCoroutine(Insert(socket));
        }

        IEnumerator Insert(Transform socket)
        {
            Vector3 localStart = transform.localPosition;
            Quaternion rotationStart = transform.localRotation;
            float elapsed = 0f;
            while (elapsed < 0.42f && owner && socket)
            {
                elapsed += Time.deltaTime;
                float amount = Mathf.Sin(Mathf.Clamp01(elapsed / 0.42f) * Mathf.PI);
                Vector3 target = owner.hand.InverseTransformPoint(socket.position + socket.forward * 0.04f);
                transform.localPosition = Vector3.Lerp(localStart, target, amount * 0.9f);
                transform.localRotation = rotationStart * Quaternion.Euler(0f, 0f, amount * 85f);
                yield return null;
            }
            if (owner)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            useAnimation = null;
        }
    }
}

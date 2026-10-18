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
            if (!isCandle || !candleLight) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith("Llama")) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            candleLight.shadows = LightShadows.Soft;
            candleLight.shadowNearPlane = 0.05f;
            candleLight.useViewFrustumForShadowCasterCull = false;
            FitCandle();
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

        // El modelo importado viene acostado y a escala de utilería. Lo paro y lo dejo de mano.
        void FitCandle()
        {
            var wax = transform.Find("Vela importada");
            if (!wax) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                if (name.StartsWith("Cera ") || name.StartsWith("Llama provisional") || name == "Llama nucleo" || name == "Llama")
                    renderer.enabled = false;
            }
            wax.localRotation = Quaternion.identity;
            wax.localPosition = Vector3.zero;
            var renderers = wax.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 size = bounds.size;
            if (size.z >= size.x && size.z >= size.y) wax.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            else if (size.x >= size.y) wax.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.001f) wax.localScale *= 0.2f / longest;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 basePoint = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            wax.localPosition -= basePoint;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var flame = transform.Find("Llama animada");
            if (flame)
            {
                Vector3 wick = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                flame.localPosition = wick + Vector3.up * 0.004f;
                flame.localRotation = Quaternion.identity;
                flame.localScale = Vector3.one * 0.42f;
            }
            if (candleLight)
                candleLight.transform.localPosition = (flame ? flame.localPosition : Vector3.up * 0.2f) + new Vector3(0f, 0.015f, 0.04f);
        }
        static readonly Vector3 CandleGrip = new Vector3(0.01f, 0.045f, 0.02f);
        static readonly Vector3 CrankGrip = new Vector3(0.02f, 0.02f, 0.05f);

        void LateUpdate()
        {
            if (isCandle && candleLight) candleLight.transform.rotation = Quaternion.identity;
            if (!owner || Inserting) return;
            float settle = 1f - Mathf.Exp(-Time.deltaTime * 16f);
            if (isCandle)
            {
                float stride = Mathf.Clamp01(owner.ActualSpeed / Mathf.Max(0.2f, owner.moveSpeed));
                float step = Mathf.Sin(Time.time * 6.4f);
                float carry = Mathf.Lerp(0.15f, 1f, stride);
                Vector3 grip = CandleGrip + new Vector3(step * 0.006f * carry, Mathf.Abs(step) * 0.008f * stride, 0f);
                transform.localPosition = Vector3.Lerp(transform.localPosition, grip, settle);
                var upright = Quaternion.Euler(step * 1.4f * carry, owner.transform.eulerAngles.y, -step * 3.2f * carry);
                transform.rotation = Quaternion.Slerp(transform.rotation, upright, settle);
                return;
            }
            transform.localPosition = Vector3.Lerp(transform.localPosition, CrankGrip, settle);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, settle);
        }

        public void PickUp(DemoPlayer player)
        {
            if (owner != null || player.Held != null) return;
            StopUseAnimation();
            owner = player;
            player.Held = this;
            foreach (var col in colliders) col.enabled = false;
            Vector3 worldScale = isCandle ? transform.lossyScale : Vector3.one;
            transform.SetParent(player.hand, false);
            if (isCandle)
            {
                Vector3 boneScale = player.hand.lossyScale;
                transform.localScale = new Vector3(
                    worldScale.x / Mathf.Max(0.0001f, boneScale.x),
                    worldScale.y / Mathf.Max(0.0001f, boneScale.y),
                    worldScale.z / Mathf.Max(0.0001f, boneScale.z));
            }
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

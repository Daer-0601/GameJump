using System.Collections;
using UnityEngine;

namespace Gamejam2026
{
    public class CarryObject : MonoBehaviour
    {
        public bool isCandle;
        public string displayName;
        public DemoPlayer owner;
        public Light candleLight;
        Collider[] colliders;
        Coroutine useAnimation;

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>();
        }

        public void PickUp(DemoPlayer player)
        {
            if (owner != null || player.Held != null) return;
            owner = player;
            player.Held = this;
            foreach (var col in colliders) col.enabled = false;
            transform.SetParent(player.hand, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void AttachTo(Transform socket)
        {
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
        }

        public void Drop()
        {
            if (owner == null) return;
            if (useAnimation != null)
            {
                StopCoroutine(useAnimation);
                useAnimation = null;
            }
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
                elapsed += Time.unscaledDeltaTime;
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

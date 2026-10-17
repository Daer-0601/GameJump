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
    }
}

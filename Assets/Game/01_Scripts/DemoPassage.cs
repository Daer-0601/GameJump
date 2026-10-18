using UnityEngine;
using UnityEngine.Events;

namespace Gamejam2026
{
    // A single opening tied to a real first success, independent of the shuffled symbol.
    public class DemoPassage : MonoBehaviour
    {
        public DemoGame game;
        public Transform panel;
        public Collider blocker;
        [Min(0f)] public float openingSeconds = 1.5f;
        public AudioClip openingClip;
        [Tooltip("Eduardo puede conectar aquí el sonido de apertura; se dispara una sola vez.")]
        public UnityEvent onOpening = new UnityEvent();
        AudioSource openingSource;
        bool openingSoundPlayed;
        public bool OpenRequested { get; private set; }
        public bool IsOpen { get; private set; }
        public float Open01 { get; private set; }
        Vector3 closedPosition;
        Vector3 closedScale;

        void Awake()
        {
            if (!game) game = FindAnyObjectByType<DemoGame>();
            if (panel)
            {
                closedPosition = panel.localPosition;
                closedScale = panel.localScale;
            }
            openingSource = gameObject.AddComponent<AudioSource>();
            openingSource.playOnAwake = false;
            openingSource.spatialBlend = 0f;
            openingSource.loop = false;
        }

        void OnEnable()
        {
            if (game) game.FirstMechanismCompleted += FirstCompleted;
            if (game && game.CompletedCount > 0) Open();
        }

        void OnDisable()
        {
            if (game) game.FirstMechanismCompleted -= FirstCompleted;
        }

        void FirstCompleted(DemoMechanism mechanism) { Open(); }

        public void Open()
        {
            if (OpenRequested) return;
            OpenRequested = true;
            if (!openingSoundPlayed && openingClip && openingSource)
            {
                openingSoundPlayed = true;
                float effects = game ? game.sfxVolume : 0.8f;
                float distance = float.MaxValue;
                if (game) foreach (var p in game.players) if (p) distance = Mathf.Min(distance, Vector3.Distance(p.transform.position, transform.position));
                openingSource.PlayOneShot(openingClip, effects * .4f * Mathf.Clamp01(1f - distance / 50f));
            }
            onOpening?.Invoke();
            if (openingSeconds <= 0f) Apply(1f);
        }

        void Update()
        {
            if (!OpenRequested || IsOpen || !game || !game.Playing) return;
            Apply(Mathf.MoveTowards(Open01, 1f, Time.deltaTime / Mathf.Max(0.01f, openingSeconds)));
        }

        void Apply(float amount)
        {
            Open01 = amount;
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            if (panel)
            {
                // Retracts into its lintel, keeping its top fixed rather than clipping the roof.
                panel.localPosition = closedPosition + Vector3.up * closedScale.y * 0.48f * eased;
                panel.localScale = new Vector3(closedScale.x, Mathf.Lerp(closedScale.y, closedScale.y * 0.04f, eased), closedScale.z);
            }
            IsOpen = amount >= 1f;
            if (IsOpen && blocker) blocker.enabled = false;
        }
    }
}

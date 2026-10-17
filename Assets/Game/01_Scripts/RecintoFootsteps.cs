using UnityEngine;

namespace Gamejam2026
{
    public class RecintoFootsteps : MonoBehaviour
    {
        public AudioClip[] concrete;
        public AudioClip[] metal;
        readonly int[] previous = { -1, -1 };

        public void Play(AudioSource source, float volume)
        {
            var player = source.GetComponent<DemoPlayer>();
            if (!player) return;
            Vector3 p = player.transform.position;
            var bank = p.x >= 12 && p.x <= 40 && p.z >= 90 && p.z <= 116 ? metal : concrete;
            if (bank == null || bank.Length == 0) return;
            int i = Random.Range(0, bank.Length);
            if (bank.Length > 1 && previous[player.playerIndex] == i) i = (i + 1) % bank.Length;
            previous[player.playerIndex] = i;
            source.pitch = Random.Range(0.94f, 1.06f);
            source.PlayOneShot(bank[i], volume * 0.55f);
        }
    }
}

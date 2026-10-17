using UnityEngine;

namespace Gamejam2026
{
    public class RecintoFootsteps : MonoBehaviour
    {
        public AudioClip[] concrete;
        public AudioClip[] metal;
        readonly int[] previous = { -1, -1 };

        public bool Play(AudioSource source, float volume)
        {
            if (!source) return false;
            var player = source.GetComponent<DemoPlayer>();
            if (!player) return false;
            Vector3 p = player.transform.position;
            var bank = p.x >= 12 && p.x <= 40 && p.z >= 90 && p.z <= 116 ? metal : concrete;
            if (bank == null || bank.Length == 0) return false;
            int i = Random.Range(0, bank.Length);
            if (bank.Length > 1 && previous[player.playerIndex] == i) i = (i + 1) % bank.Length;
            int attempts = 0;
            while (!bank[i] && attempts++ < bank.Length) i = (i + 1) % bank.Length;
            if (!bank[i]) return false;
            previous[player.playerIndex] = i;
            source.pitch = Random.Range(0.94f, 1.06f);
            source.PlayOneShot(bank[i], volume * 0.7f);
            return true;
        }
    }
}

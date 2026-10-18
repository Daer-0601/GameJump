using UnityEngine;

namespace Gamejam2026
{
    // La rata corre por su zona cuando alguien se acerca durante la expedición
    // y desaparece cuando los dos jugadores se alejan, o si la partida no está en juego.
    public class RataSusto : MonoBehaviour
    {
        public float distanciaAparece = 7.5f;
        public float distanciaDesaparece = 12f;
        public float velocidad = 1.7f;
        public float velocidadHuye = 3.1f;
        public AudioClip chirrido;

        Transform cuerpo;
        Transform[] puntos;
        DemoPlayer[] jugadores;
        DemoGame partida;
        AudioSource voz;
        int indice;
        float espera;
        bool activa;

        void Awake()
        {
            cuerpo = transform.Find("Cuerpo");
            var camino = transform.Find("Camino");
            puntos = camino ? new Transform[camino.childCount] : System.Array.Empty<Transform>();
            for (int i = 0; i < puntos.Length; i++) puntos[i] = camino.GetChild(i);
            if (cuerpo && puntos.Length > 0) cuerpo.position = puntos[0].position;
            voz = gameObject.AddComponent<AudioSource>();
            voz.playOnAwake = false;
            voz.spatialBlend = 0f;
            Visible(false);
        }

        void Update()
        {
            if (!cuerpo || puntos == null || puntos.Length < 2) return;
            if (!partida) partida = FindAnyObjectByType<DemoGame>();
            if (!partida || !partida.Playing)
            {
                if (activa)
                {
                    Visible(false);
                    activa = false;
                }
                return;
            }
            float cerca = DistanciaMasCercana(transform.position);
            if (!activa)
            {
                if (cerca <= distanciaAparece) Aparecer();
                return;
            }
            if (cerca >= distanciaDesaparece)
            {
                Visible(false);
                activa = false;
                return;
            }
            if (espera > 0f)
            {
                espera -= Time.deltaTime;
                return;
            }
            Correr();
        }

        void Aparecer()
        {
            int lejos = 0;
            float mejor = -1f;
            for (int i = 0; i < puntos.Length; i++)
            {
                float distancia = DistanciaMasCercana(puntos[i].position);
                if (distancia > mejor)
                {
                    mejor = distancia;
                    lejos = i;
                }
            }
            cuerpo.position = puntos[lejos].position;
            indice = (lejos + 1) % puntos.Length;
            espera = 0.15f;
            Visible(true);
            activa = true;
            if (chirrido && voz && partida)
            {
                Orientar(cuerpo.position);
                voz.PlayOneShot(chirrido, partida.sfxVolume * 0.2f);
            }
        }

        void Correr()
        {
            float cercaDelCuerpo = DistanciaMasCercana(cuerpo.position);
            if (cercaDelCuerpo < 3.4f)
            {
                int mejor = indice;
                float lejos = -1f;
                for (int i = 0; i < puntos.Length; i++)
                {
                    float distancia = DistanciaMasCercana(puntos[i].position);
                    if (distancia > lejos)
                    {
                        lejos = distancia;
                        mejor = i;
                    }
                }
                indice = mejor;
            }
            var destino = puntos[indice].position;
            float rapidez = cercaDelCuerpo < 3.4f ? velocidadHuye : velocidad;
            cuerpo.position = Vector3.MoveTowards(cuerpo.position, destino, rapidez * Time.deltaTime);
            var paso = destino - cuerpo.position;
            paso.y = 0f;
            if (paso.sqrMagnitude > 0.0004f)
                cuerpo.rotation = Quaternion.Slerp(cuerpo.rotation, Quaternion.LookRotation(paso.normalized, Vector3.up), 10f * Time.deltaTime);
            if (Vector3.Distance(cuerpo.position, destino) < 0.05f)
            {
                indice = (indice + 1) % puntos.Length;
                espera = 0.28f;
            }
        }

        float DistanciaMasCercana(Vector3 punto)
        {
            if (jugadores == null || jugadores.Length == 0 || jugadores[0] == null)
                jugadores = FindObjectsByType<DemoPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            float distancia = 999f;
            if (jugadores == null) return distancia;
            foreach (var jugador in jugadores)
                if (jugador) distancia = Mathf.Min(distancia, Vector3.Distance(punto, jugador.transform.position));
            return distancia;
        }

        void Orientar(Vector3 punto)
        {
            DemoPlayer oido = null;
            float cerca = float.MaxValue;
            if (partida.players != null)
            {
                foreach (var jugador in partida.players)
                {
                    if (!jugador || !jugador.view) continue;
                    float distancia = Vector3.Distance(jugador.view.transform.position, punto);
                    if (distancia < cerca) { cerca = distancia; oido = jugador; }
                }
            }
            if (!oido) return;
            Vector3 delta = punto - oido.view.transform.position;
            voz.panStereo = Mathf.Clamp(Vector3.Dot(oido.view.transform.right, delta.sqrMagnitude > 0.01f ? delta.normalized : Vector3.forward), -0.55f, 0.55f);
        }

        void Visible(bool valor)
        {
            if (!cuerpo) return;
            foreach (var renderer in cuerpo.GetComponentsInChildren<Renderer>()) renderer.enabled = valor;
        }
    }
}

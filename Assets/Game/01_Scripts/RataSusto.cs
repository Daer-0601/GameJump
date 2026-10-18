using UnityEngine;

namespace Gamejam2026
{
    // La rata corre por su zona cuando alguien se acerca y desaparece
    // cuando los dos jugadores se alejan lo suficiente.
    public class RataSusto : MonoBehaviour
    {
        public float distanciaAparece = 7.5f;
        public float distanciaDesaparece = 12f;
        public float velocidad = 1.7f;
        public float velocidadHuye = 3.1f;

        Transform cuerpo;
        Transform[] puntos;
        DemoPlayer[] jugadores;
        int indice;
        float espera;
        bool activa;

        void Awake()
        {
            cuerpo = transform.Find("Cuerpo");
            var camino = transform.Find("Camino");
            puntos = new Transform[camino.childCount];
            for (int i = 0; i < puntos.Length; i++) puntos[i] = camino.GetChild(i);
            if (cuerpo && puntos.Length > 0) cuerpo.position = puntos[0].position;
            Visible(false);
        }

        void Update()
        {
            if (!cuerpo || puntos == null || puntos.Length < 2) return;
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

        void Visible(bool valor)
        {
            foreach (var renderer in cuerpo.GetComponentsInChildren<Renderer>()) renderer.enabled = valor;
        }
    }
}

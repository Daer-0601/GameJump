using System.Collections.Generic;
using UnityEngine;

namespace Gamejam2026
{
    // Sistema integral de efectos especiales (Vapor, Niebla, Chispas, Polvo, Goteos, Miasma y Ascuas).
    // Diseñado acorde a la estética industrial oscura y retro-terror del juego.
    public class RecintoSpecialEffects : MonoBehaviour
    {
        static Texture2D softCircleTex;
        static Texture2D sparkGlowTex;
        static Texture2D rippleRingTex;
        static Material softParticleMat;
        static Material additiveSparkMat;
        static Material miasmaSmokeMat;
        static Material waterRippleMat;

        static AudioClip steamHissClip;
        static AudioClip electricSnapClip;
        static AudioClip waterDripClip;

        DemoGame game;
        GameObject fxRoot;
        readonly List<ElectricArcEffect> electricArcs = new List<ElectricArcEffect>();
        readonly List<SteamJetEffect> steamJets = new List<SteamJetEffect>();
        readonly List<WaterDripEffect> waterDrips = new List<WaterDripEffect>();

        class ElectricArcEffect
        {
            public Vector3 position;
            public ParticleSystem sparks;
            public ParticleSystem smoke;
            public Light flashLight;
            public AudioSource audioSource;
            public float nextTriggerTime;
            public float flashUntil;
        }

        class SteamJetEffect
        {
            public ParticleSystem steam;
            public AudioSource audioSource;
            public float nextSurgeTime;
            public float surgeUntil;
            public float baseVolume;
        }

        class WaterDripEffect
        {
            public Vector3 ceilingPos;
            public Vector3 floorPos;
            public ParticleSystem fallingDrip;
            public ParticleSystem floorRipple;
            public AudioSource audioSource;
            public float nextDripTime;
        }

        public void Initialize(DemoGame session)
        {
            game = session ? session : FindAnyObjectByType<DemoGame>();
            EnsureAssets();
            BuildWorldEffects();
        }

        void OnDestroy()
        {
            Cleanup();
        }

        static void EnsureAssets()
        {
            if (softCircleTex == null) softCircleTex = BuildSoftCircleTexture(64);
            if (sparkGlowTex == null) sparkGlowTex = BuildSparkTexture(64);
            if (rippleRingTex == null) rippleRingTex = BuildRingTexture(64);

            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!particleShader) particleShader = Shader.Find("Particles/Standard Unlit");
            if (!particleShader) particleShader = Shader.Find("Universal Render Pipeline/Unlit");

            if (softParticleMat == null && particleShader)
            {
                softParticleMat = new Material(particleShader) { name = "FX_SoftParticle" };
                softParticleMat.mainTexture = softCircleTex;
                ConfigureBlend(softParticleMat, false);
            }

            if (additiveSparkMat == null && particleShader)
            {
                additiveSparkMat = new Material(particleShader) { name = "FX_AdditiveSpark" };
                additiveSparkMat.mainTexture = sparkGlowTex;
                ConfigureBlend(additiveSparkMat, true);
            }

            if (miasmaSmokeMat == null && particleShader)
            {
                miasmaSmokeMat = new Material(particleShader) { name = "FX_MiasmaSmoke" };
                miasmaSmokeMat.mainTexture = softCircleTex;
                ConfigureBlend(miasmaSmokeMat, false);
            }

            if (waterRippleMat == null && particleShader)
            {
                waterRippleMat = new Material(particleShader) { name = "FX_WaterRipple" };
                waterRippleMat.mainTexture = rippleRingTex;
                ConfigureBlend(waterRippleMat, true);
            }

            if (steamHissClip == null) steamHissClip = BuildSteamHissClip();
            if (electricSnapClip == null) electricSnapClip = BuildElectricSnapClip();
            if (waterDripClip == null) waterDripClip = BuildWaterDripClip();
        }

        static void ConfigureBlend(Material mat, bool additive)
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", additive ? 1f : 0f);
            mat.SetInt("_SrcBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.SrcAlpha));
            mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        void BuildWorldEffects()
        {
            if (fxRoot) Destroy(fxRoot);
            fxRoot = new GameObject("Efectos Especiales del Recinto");
            fxRoot.transform.SetParent(transform, false);

            // -------------------------------------------------------------
            // 1. BOMBAS 05: Fugas de Vapor a Presión y Goteo de Tuberías
            // -------------------------------------------------------------
            // Fuga principal de tubería elevada
            CreateSteamJet(new Vector3(16.2f, 2.1f, 96.4f), new Vector3(0.85f, -0.3f, 0.45f), 1.8f);
            // Fuga en caseta de bombas junto a válvulas
            CreateSteamJet(new Vector3(33.4f, 1.6f, 103.2f), new Vector3(-0.9f, -0.1f, 0.35f), 1.5f);
            // Fuga en pasaje oscuro de conexión
            CreateSteamJet(new Vector3(36.8f, 1.4f, 82.5f), new Vector3(-0.85f, 0.15f, -0.5f), 1.2f);
            // Goteo con ondas de agua en el suelo húmedo de bombas
            CreateWaterDrip(new Vector3(22f, 3.3f, 107f), new Vector3(22f, 0.03f, 107f));

            // -------------------------------------------------------------
            // 2. TALLER 04: Cortocircuitos Eléctricos y Polvillo Metálico
            // -------------------------------------------------------------
            // Caja de fusibles / disyuntor averiado
            CreateElectricArc(new Vector3(-24.5f, 2.3f, 88.2f), new Vector3(0.7f, -0.7f, 0f));
            // Cuadro eléctrico sobre maquinaria central
            CreateElectricArc(new Vector3(-12.4f, 2.85f, 97.4f), new Vector3(0f, -1f, 0.3f));
            // Polvillo suspendido sobre el banco de herramientas
            CreateSuspendedDust(new Vector3(-20f, 1.6f, 78f), new Vector3(8f, 2.5f, 5f), new Color(0.38f, 0.32f, 0.22f, 0.08f), 12);

            // -------------------------------------------------------------
            // 3. PATIO 03: Manto de Niebla Baja y Esporas Heladas
            // -------------------------------------------------------------
            // Niebla rastrera en el empedrado
            CreateGroundFog(new Vector3(18f, 0.22f, 71f), new Vector3(22f, 0.6f, 15f), new Color(0.12f, 0.16f, 0.18f, 0.085f), 28);
            // Esporas y cenizas que caen del cielo abierto
            CreateFallingSpores(new Vector3(18f, 3.8f, 71f), new Vector3(20f, 1f, 14f));

            // -------------------------------------------------------------
            // 4. ALMACÉN 02: Motas de Polvo en Suspensión y Goteo de Viga
            // -------------------------------------------------------------
            // Motas de polvo flotantes entre palés y estanterías
            CreateSuspendedDust(new Vector3(-11f, 2.2f, 32f), new Vector3(26f, 3.6f, 24f), new Color(0.42f, 0.38f, 0.3f, 0.075f), 35);
            // Goteo desde viga metálica sobre charco
            CreateWaterDrip(new Vector3(-14f, 3.4f, 28f), new Vector3(-14f, 0.03f, 28f));

            // -------------------------------------------------------------
            // 5. ACCESO 01: Polillas de la Farola de Entrada
            // -------------------------------------------------------------
            CreateLampMoths(new Vector3(0f, 3.85f, 1f));
        }

        void CreateSteamJet(Vector3 position, Vector3 direction, float intensity)
        {
            var go = new GameObject("FX_Vapor_Presion");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction.normalized);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f * intensity, 5.4f * intensity);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new Color(0.72f, 0.76f, 0.8f, 0.38f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 45;

            var emission = ps.emission;
            emission.rateOverTime = 18f * intensity;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.03f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.65f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.38f, 0.15f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.drag = 1.6f;
            limitVel.multiplyDragByParticleSize = false;
            limitVel.multiplyDragByParticleVelocity = false;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = softParticleMat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;

            // Audio siseante 3D
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1.4f;
            audio.maxDistance = 13f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.loop = true;
            audio.clip = steamHissClip;
            audio.volume = (game ? game.sfxVolume : 0.8f) * 0.14f * intensity;
            audio.Play();

            steamJets.Add(new SteamJetEffect
            {
                steam = ps,
                audioSource = audio,
                baseVolume = audio.volume,
                nextSurgeTime = Time.time + Random.Range(5f, 12f),
                surgeUntil = 0f
            });
        }

        void CreateElectricArc(Vector3 position, Vector3 sparkDirection)
        {
            var go = new GameObject("FX_Arco_Electrico");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(sparkDirection.normalized);

            // Chispas incandescentes
            var sparksPS = go.AddComponent<ParticleSystem>();
            var main = sparksPS.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(1f, 0.82f, 0.45f, 1f);
            main.gravityModifier = 1.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;

            var emission = sparksPS.emission;
            emission.rateOverTime = 0f;

            var shape = sparksPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.04f;

            var rend = sparksPS.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = additiveSparkMat;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.08f;
            rend.lengthScale = 1.4f;

            // Humo tenue del chispazo
            var smokeObj = new GameObject("Humo");
            smokeObj.transform.SetParent(go.transform, false);
            var smokePS = smokeObj.AddComponent<ParticleSystem>();
            var sMain = smokePS.main;
            sMain.playOnAwake = false;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            sMain.startColor = new Color(0.18f, 0.17f, 0.16f, 0.35f);
            sMain.gravityModifier = -0.05f;
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var sEmission = smokePS.emission;
            sEmission.rateOverTime = 0f;

            var sRend = smokePS.GetComponent<ParticleSystemRenderer>();
            sRend.sharedMaterial = miasmaSmokeMat;

            // Luz de flash momentáneo
            var lightObj = new GameObject("Flash");
            lightObj.transform.SetParent(go.transform, false);
            var flash = lightObj.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(0.75f, 0.88f, 1f);
            flash.intensity = 0f;
            flash.range = 5.5f;
            flash.shadows = LightShadows.None;

            // Audio 3D del chispazo
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 2f;
            audio.maxDistance = 16f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.clip = electricSnapClip;

            electricArcs.Add(new ElectricArcEffect
            {
                position = position,
                sparks = sparksPS,
                smoke = smokePS,
                flashLight = flash,
                audioSource = audio,
                nextTriggerTime = Time.time + Random.Range(3f, 9f),
                flashUntil = 0f
            });
        }

        void CreateGroundFog(Vector3 center, Vector3 size, Color tint, int count)
        {
            var go = new GameObject("FX_Niebla_Rastrera_Patio");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = center;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 9.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.8f, 4.4f);
            main.startColor = tint;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;

            var emission = ps.emission;
            emission.rateOverTime = count / 3.5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(tint.a, 0.25f), new GradientAlphaKey(tint.a, 0.75f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.25f));

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = softParticleMat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }

        void CreateSuspendedDust(Vector3 center, Vector3 size, Color tint, int count)
        {
            var go = new GameObject("FX_Motas_Polvo");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = center;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = tint;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;

            var emission = ps.emission;
            emission.rateOverTime = count / 3f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(tint.a, 0.3f), new GradientAlphaKey(tint.a, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = softParticleMat;
        }

        void CreateFallingSpores(Vector3 center, Vector3 size)
        {
            var go = new GameObject("FX_Esporas_Patio");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = center;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = new Color(0.25f, 0.28f, 0.3f, 0.45f);
            main.gravityModifier = 0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = softParticleMat;
        }

        void CreateWaterDrip(Vector3 ceilingPos, Vector3 floorPos)
        {
            var go = new GameObject("FX_Goteo_Agua");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = ceilingPos;

            // Gota cayendo
            var dripPS = go.AddComponent<ParticleSystem>();
            var main = dripPS.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
            main.startColor = new Color(0.7f, 0.8f, 0.85f, 0.6f);
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8;

            var emission = dripPS.emission;
            emission.rateOverTime = 0f;

            var rend = dripPS.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = softParticleMat;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.04f;
            rend.lengthScale = 1.2f;

            // Onda concéntrica al caer en el suelo
            var rippleObj = new GameObject("Onda_Charco");
            rippleObj.transform.SetParent(go.transform, false);
            rippleObj.transform.position = floorPos;
            rippleObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var ripplePS = rippleObj.AddComponent<ParticleSystem>();
            var rMain = ripplePS.main;
            rMain.playOnAwake = false;
            rMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            rMain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.01f);
            rMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            rMain.startColor = new Color(0.7f, 0.85f, 0.9f, 0.35f);
            rMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var rEmission = ripplePS.emission;
            rEmission.rateOverTime = 0f;

            var rSize = ripplePS.sizeOverLifetime;
            rSize.enabled = true;
            rSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 3.2f));

            var rCol = ripplePS.colorOverLifetime;
            rCol.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            rCol.color = grad;

            var rRend = ripplePS.GetComponent<ParticleSystemRenderer>();
            rRend.sharedMaterial = waterRippleMat;

            // Audio 3D del goteo
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1.2f;
            audio.maxDistance = 10f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.clip = waterDripClip;

            waterDrips.Add(new WaterDripEffect
            {
                ceilingPos = ceilingPos,
                floorPos = floorPos,
                fallingDrip = dripPS,
                floorRipple = ripplePS,
                audioSource = audio,
                nextDripTime = Time.time + Random.Range(1.8f, 3.6f)
            });
        }

        void CreateLampMoths(Vector3 lampPos)
        {
            var go = new GameObject("FX_Polillas_Farola");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = lampPos;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            main.startColor = new Color(0.85f, 0.8f, 0.65f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 14;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.55f;

            var velOverLife = ps.velocityOverLifetime;
            velOverLife.enabled = true;
            velOverLife.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velOverLife.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velOverLife.orbitalZ = new ParticleSystem.MinMaxCurve(1.5f, 3f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = softParticleMat;
        }

        void Update()
        {
            if (!game || !game.Playing) return;

            float now = Time.time;

            // 1. Actualización de Arcos Eléctricos
            for (int i = 0; i < electricArcs.Count; i++)
            {
                var arc = electricArcs[i];
                if (now >= arc.nextTriggerTime)
                {
                    arc.nextTriggerTime = now + Random.Range(5f, 12f);
                    arc.flashUntil = now + 0.07f;
                    if (arc.flashLight) arc.flashLight.intensity = 2.8f;
                    if (arc.sparks) arc.sparks.Emit(Random.Range(14, 26));
                    if (arc.smoke) arc.smoke.Emit(Random.Range(3, 6));
                    if (arc.audioSource)
                    {
                        arc.audioSource.pitch = Random.Range(0.88f, 1.18f);
                        arc.audioSource.PlayOneShot(electricSnapClip, (game ? game.sfxVolume : 0.8f) * 0.35f);
                    }
                }
                if (arc.flashLight && now > arc.flashUntil && arc.flashLight.intensity > 0f)
                {
                    arc.flashLight.intensity = 0f;
                }
            }

            // 2. Actualización de Fugas de Vapor
            for (int i = 0; i < steamJets.Count; i++)
            {
                var jet = steamJets[i];
                if (now >= jet.nextSurgeTime)
                {
                    jet.nextSurgeTime = now + Random.Range(8f, 16f);
                    jet.surgeUntil = now + Random.Range(1.8f, 3.2f);
                }
                if (jet.audioSource)
                {
                    bool surging = now < jet.surgeUntil;
                    float targetVol = surging ? jet.baseVolume * 1.8f : jet.baseVolume;
                    jet.audioSource.volume = Mathf.MoveTowards(jet.audioSource.volume, targetVol, Time.deltaTime * 0.6f);
                }
            }

            // 3. Actualización de Goteos de Agua
            for (int i = 0; i < waterDrips.Count; i++)
            {
                var drip = waterDrips[i];
                if (now >= drip.nextDripTime)
                {
                    drip.nextDripTime = now + Random.Range(2.2f, 4.4f);
                    if (drip.fallingDrip) drip.fallingDrip.Emit(1);
                    StartCoroutine(TriggerSplashAfter(drip, 0.58f));
                }
            }
        }

        System.Collections.IEnumerator TriggerSplashAfter(WaterDripEffect drip, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (drip.floorRipple) drip.floorRipple.Emit(1);
            if (drip.audioSource)
            {
                drip.audioSource.pitch = Random.Range(0.9f, 1.15f);
                drip.audioSource.PlayOneShot(waterDripClip, (game ? game.sfxVolume : 0.8f) * 0.16f);
            }
        }

        // =====================================================================
        // MÉTODOS PÚBLICOS PARA ACOPLAR A VELA Y SOMBRA
        // =====================================================================

        public static void AttachCandleEmbers(Transform flameTransform)
        {
            if (!flameTransform) return;
            EnsureAssets();

            var existing = flameTransform.Find("FX_Ascuas_Vela");
            if (existing) return;

            var go = new GameObject("FX_Ascuas_Vela");
            go.transform.SetParent(flameTransform, false);
            go.transform.localPosition = new Vector3(0f, 0.04f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.024f);
            main.startColor = new Color(1f, 0.72f, 0.28f, 0.85f);
            main.gravityModifier = -0.12f; // Ascienden suavemente
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.02f;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.35f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = additiveSparkMat;
        }

        public static void AttachShadowMiasma(GameObject shadowObj)
        {
            if (!shadowObj) return;
            EnsureAssets();

            var existing = shadowObj.transform.Find("FX_Miasma_Sombra");
            if (existing) return;

            var go = new GameObject("FX_Miasma_Sombra");
            go.transform.SetParent(shadowObj.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startColor = new Color(0.02f, 0.015f, 0.025f, 0.45f);
            main.gravityModifier = -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 35;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.02f, 0.015f, 0.025f), 0f), new GradientColorKey(new Color(0.01f, 0.01f, 0.015f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.25f));

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = miasmaSmokeMat;
        }

        // =====================================================================
        // GENERADORES PROCEDIMENTALES DE TEXTURAS Y AUDIO
        // =====================================================================

        static Texture2D BuildSoftCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.SmoothStep(1f, 0f, d);
                    byte alphaByte = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alphaByte);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D BuildSparkTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    byte alphaByte = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alphaByte);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D BuildRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.72f) * 6.5f, 2f));
                    byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(ring) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alphaByte);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static AudioClip BuildSteamHissClip()
        {
            const int sampleRate = 22050;
            int length = Mathf.RoundToInt(sampleRate * 1.4f);
            var samples = new float[length];
            var rng = new System.Random(107);
            float lowFilter = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowFilter = Mathf.Lerp(lowFilter, white, 0.32f);
                float fade = Mathf.Min(1f, Mathf.Min(i, length - i) / (sampleRate * 0.08f));
                samples[i] = lowFilter * 0.2f * fade;
            }
            var clip = AudioClip.Create("FX_SteamHissLoop", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip BuildElectricSnapClip()
        {
            const int sampleRate = 22050;
            int length = Mathf.RoundToInt(sampleRate * 0.065f);
            var samples = new float[length];
            var rng = new System.Random(211);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float env = Mathf.Exp(-t * 26f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = Mathf.Sin(2f * Mathf.PI * 1350f * (float)i / sampleRate);
                samples[i] = (noise * 0.65f + tone * 0.35f) * env * 0.45f;
            }
            var clip = AudioClip.Create("FX_ElectricSnap", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip BuildWaterDripClip()
        {
            const int sampleRate = 22050;
            int length = Mathf.RoundToInt(sampleRate * 0.045f);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 14f);
                float freq = Mathf.Lerp(1120f, 680f, t);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate);
                samples[i] = tone * env * 0.38f;
            }
            var clip = AudioClip.Create("FX_WaterDrip", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void Cleanup()
        {
            if (fxRoot) Destroy(fxRoot);
            electricArcs.Clear();
            steamJets.Clear();
            waterDrips.Clear();
        }
    }
}

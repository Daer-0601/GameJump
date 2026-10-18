using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Gamejam2026
{
    public class RecintoInvestigation : MonoBehaviour
    {
        static readonly Vector3[] Sites = {
            new Vector3(-19,0,27), new Vector3(-4,0,46), new Vector3(15,0,64), new Vector3(29,0,76),
            new Vector3(5,0,84), new Vector3(-25,0,88.5f), new Vector3(-23,0,110), new Vector3(34,0,106) };
        DemoGame game;
        readonly List<RecintoStation> stations = new List<RecintoStation>();
        readonly bool[] evidence = new bool[3];
        readonly List<TextMesh> signs = new List<TextMesh>();
        readonly List<string> signLabels = new List<string>();
        readonly List<Material> eldritchMaterials = new List<Material>();
        bool synchronized, ownsStain, eldritchSoundPlayed, bodyClueGiven;
        Material metal, darkLamp, activeLamp, stain, insects, larva, bodyMaterial;
        GameObject eldritch, depotBody, shadow;
        readonly GameObject[] coveredBodies = new GameObject[2];
        readonly bool[] coveredBodySoundPlayed = new bool[2];
        Quaternion bodyModelRotation;
        bool depotBodySeen, depotBodyMoved, depotBodyMayMove;
        static readonly Vector3[] EldritchSites = { new Vector3(-19,0,104), new Vector3(31,0,108) };
        static readonly Vector3[] BodySites = { new Vector3(-15,0,24), new Vector3(24,0,70), new Vector3(-12,0,82), new Vector3(31,0,105) };
        AudioSource fright;
        AudioClip burnClip;
        RecintoThreatPresentation threat;
        Animator shadowAnimator;
        RecintoShadowPath path;
        readonly Queue<Vector3> route = new Queue<Vector3>();
        RecintoStation shadowTarget;
        float abandonedTime, nextAbandonedStep, travelBudget, moveAfter, contactAfter, nextAppearance;
        Vector3 retreatPoint;
        bool retreating, navigating;
        readonly List<Material> shadowMaterials = new List<Material>();
        [Range(20f, 90f)] public float firstAppearanceSeconds = 36f;
        [Range(22f, 90f)] public float calmAppearanceSeconds = 50f;
        [Range(16f, 60f)] public float dangerAppearanceSeconds = 28f;
        [Range(6f, 14f)] public float seenDistance = 8f;
        int alteredSign = -1, pendingSign = -1;
        float restoreSignAt;
        string falseMark;
        public int EvidenceCount { get; private set; }
        public int FalseIndex { get; private set; }
        public bool FalseRevealed { get; private set; }
        public bool Contrasted => synchronized;
        public int RequiredPower { get; private set; }
        public int PoweredCount => stations.Count(s => s && s.IsPowerOn);
        public int SurveyedPower => stations.Count(s => s && s.Surveyed);
        public bool CanAttempt => EvidenceCount == 3;
        public bool Ready => CanAttempt && synchronized && SurveyedPower >= RequiredPower;
        public string EvidenceSummary => string.Join("   ", Enumerable.Range(0,3).Select(i => (i+1) + ":" + (evidence[i] ? DisplayedSymbol(i) : "?")));
        public string LightSummary => string.Join("   ", stations.Where(s => s && s.name.StartsWith("Luz ")).Select(s => s.name + " " + s.Status));
        public IReadOnlyList<RecintoStation> Stations => stations;
        public GameObject Shadow => shadow;
        public bool ShadowMoving => navigating;
        public Material Metal => metal;
        public Material DarkLamp => darkLamp;
        public Material ActiveLamp => activeLamp;
        public string DisplayedSymbol(int index) => !FalseRevealed && index == FalseIndex ? falseMark : game.SymbolAt(index);
        public string NextTask => EvidenceCount < 3 ? "Recuperen los registros que faltan (" + EvidenceCount + "/3); quemen los brotes que bloquean los paneles"
            : !synchronized ? "Contrasten los tres registros: ambos mantener E / U en la consola; uno es falso"
            : SurveyedPower < RequiredPower ? "Inspeccionen la red: activen " + (RequiredPower - SurveyedPower) + " luces auxiliares; después pueden apagarlas"
            : game.Containing ? game.ContainmentHint : "Lleven la manivela al mecanismo " + game.NextSymbol + " y sigan el orden verificado";

        public void Initialize(DemoGame owner)
        {
            game = owner;
            metal = MakeMaterial("Metal húmedo", new Color(.12f,.13f,.12f)); metal.SetFloat("_Metallic", .65f); metal.SetFloat("_Smoothness", .38f);
            darkLamp = MakeMaterial("Indicador apagado", new Color(.09f,.12f,.09f));
            activeLamp = MakeMaterial("Indicador activo", new Color(.42f,.68f,.35f));
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                if (renderer.name == "Humedad superficial" && renderer.sharedMaterial) { stain = renderer.sharedMaterial; break; }
            if (!stain) { stain = MakeMaterial("Humedad orgánica", new Color(.045f,.065f,.035f)); ownsStain = true; }
            insects = MakeMaterial("Insectos", new Color(.025f,.025f,.018f)); larva = MakeMaterial("Larvas", new Color(.43f,.38f,.27f));
            fright = gameObject.AddComponent<AudioSource>(); fright.playOnAwake = false; fright.spatialBlend = 0;
            threat = GetComponent<RecintoThreatPresentation>();
            if (!threat) threat = gameObject.AddComponent<RecintoThreatPresentation>();
            threat.Bind(game);
            burnClip = Resources.Load<AudioClip>("Production/Burn");
            var fire = game.candles[0].gameObject.AddComponent<RecintoSound>(); fire.game = game; fire.clips = new[] { burnClip }; fire.loop = true; fire.volume = .035f; fire.radius = 9;
            RequiredPower = Random.Range(0,3); FalseIndex = Random.Range(0,3); falseMark = game.SymbolAt((FalseIndex + Random.Range(1,3)) % 3);
            var slots = Enumerable.Range(0,Sites.Length).ToList();
            for (int i = slots.Count - 1; i > 0; i--) { int j = Random.Range(0,i+1); int k = slots[i]; slots[i] = slots[j]; slots[j] = k; }
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("Puesto variable"); root.transform.SetParent(transform, false); root.transform.position = Sites[slots[i]];
                root.transform.rotation = Quaternion.Euler(0, slots[i] % 2 == 0 ? 25 : -35, 0);
                var station = root.AddComponent<RecintoStation>();
                station.Configure(this, game, i < 3 ? RecintoStation.Role.Evidence : i == 3 ? RecintoStation.Role.Synchronize : RecintoStation.Role.Power, i, i % 2 == 0);
                stations.Add(station);
            }
            Contaminate(stations[0]); Contaminate(stations[3]);
            CreateDecay(Sites[slots[1]] + Vector3.right * 1.2f, 6, null);
            CreateDecay(Sites[slots[4]] + Vector3.left * 1.2f, 5, null);
            AddExistingClutter(slots); CreateCoveredBodies(); CreateEldritchAnomaly(); PrepareSigns(); CreateShadow(); CreateSeals();
            game.FirstMechanismCompleted += PassageChanged;
        }
        void PassageChanged(DemoMechanism mechanism) { Invoke(nameof(RebuildShadowPath), 1.7f); }
        void RebuildShadowPath() { if (path != null) { path.Build(); route.Clear(); } }
        void CreateSeals()
        {
            foreach (var point in new[] { DemoGame.WorkshopSeal, DemoGame.PumpSeal })
            {
                var root = new GameObject(point == DemoGame.WorkshopSeal ? "Zona de contención TALLER 04" : "Zona de contención BOMBAS 05"); root.transform.SetParent(transform,false); root.transform.position = point;
                var line = root.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 5; line.startWidth = line.endWidth = .05f;
                Bounds room = DemoGame.SealRoom(point == DemoGame.WorkshopSeal);
                Vector3 a = room.min - point, b = room.max - point;
                line.sharedMaterial = activeLamp; line.SetPositions(new[] { new Vector3(a.x,0,a.z),new Vector3(b.x,0,a.z),new Vector3(b.x,0,b.z),new Vector3(a.x,0,b.z),new Vector3(a.x,0,a.z) });
                var text = new GameObject("Placa de contención").AddComponent<TextMesh>(); text.transform.SetParent(root.transform,false); text.transform.localPosition = new Vector3(0,1.7f,2.8f); text.transform.localRotation = Quaternion.identity;
                text.text = "CONTENCIÓN\nPERMANECER EN LA SALA"; text.characterSize = .025f; text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            }
        }
        public void Contaminate(RecintoStation station)
        {
            if (!station || station.Contaminated) return;
            var patch = CreateDecay(station.transform.position + station.transform.forward * -.58f, 5, station);
            station.SetContamination(patch);
        }
        RecintoContamination CreateDecay(Vector3 center, int count, RecintoStation station)
        {
            var patch = new GameObject("Brote quemable"); patch.transform.SetParent(transform,false); patch.transform.position = center;
            var growth = patch.AddComponent<RecintoContamination>(); growth.game = game; growth.blockedStation = station;
            var collider = patch.AddComponent<BoxCollider>(); collider.center = new Vector3(0,.28f,0); collider.size = new Vector3(.85f,.65f,.75f);
            for (int i = 0; i < 4; i++)
            {
                var blotch = GameObject.CreatePrimitive(PrimitiveType.Sphere); blotch.name = "Mancha orgánica"; blotch.transform.SetParent(patch.transform,false);
                blotch.transform.localPosition = new Vector3(Random.Range(-.3f,.3f),.07f,Random.Range(-.25f,.25f)); blotch.transform.localScale = new Vector3(.5f,.11f,.45f);
                blotch.GetComponent<Renderer>().sharedMaterial = stain; Destroy(blotch.GetComponent<Collider>());
            }
            for (int i = 0; i < count; i++)
            {
                var worm = GameObject.CreatePrimitive(PrimitiveType.Capsule); worm.name = "Brote vivo"; worm.transform.SetParent(patch.transform,false);
                worm.transform.localPosition = new Vector3(Random.Range(-.28f,.28f),Random.Range(.1f,.35f),Random.Range(-.2f,.2f));
                worm.transform.localRotation = Quaternion.Euler(45,Random.Range(0,360),35); worm.transform.localScale = new Vector3(.05f,.18f,.05f);
                worm.GetComponent<Renderer>().sharedMaterial = larva; Destroy(worm.GetComponent<Collider>()); worm.AddComponent<RecintoWorm>().Initialize(Random.value * 6.28f);
            }
            return growth;
        }
        void CreateShadow()
        {
            var source = Resources.Load<GameObject>("Production/Shadow");
            if (!source) { Debug.LogError("Falta Production/Shadow; aplica arte desde Gamejam/Producción."); return; }
            shadow = Instantiate(source,transform); shadow.name = "Sombra - amenaza avisada";
            shadow.transform.position = new Vector3(-13,0,27); shadowAnimator = shadow.GetComponentInChildren<Animator>();
            var crawl = shadow.AddComponent<RecintoSound>(); crawl.game = game;
            var crawlClip = threat && threat.Movement ? threat.Movement : Resources.Load<AudioClip>("Production/Crawl");
            crawl.clips = new[] { crawlClip };
            crawl.loop = true; crawl.onlyWhileShadowMoves = true; crawl.volume = .07f; crawl.radius = 16;
            foreach (var renderer in shadow.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                if (!renderer.sharedMaterial) continue;
                var skinMaterial = new Material(renderer.sharedMaterial);
                skinMaterial.EnableKeyword("_EMISSION");
                skinMaterial.SetColor("_EmissionColor", new Color(.04f, .05f, .035f));
                renderer.sharedMaterial = skinMaterial;
                shadowMaterials.Add(skinMaterial);
            }
            path = new RecintoShadowPath();
            path.Build(); shadow.transform.position = path.Nearest(shadow.transform.position);
            retreatPoint = shadow.transform.position;
            nextAppearance = Mathf.Max(12f, firstAppearanceSeconds);
        }
        void Update()
        {
            if (!game || !game.Playing)
            {
                if (fright && fright.isPlaying) fright.Stop();
                if (shadowAnimator) shadowAnimator.speed = 0;
                return;
            }
            UpdateSigns(); UpdateCoveredBody(); UpdateBodySounds(); UpdateAnomaly(); UpdateShadow();
        }
        void UpdateSigns()
        {
            if (pendingSign >= 0 && BothPlayersAwayAndNotWatching(signs[pendingSign].transform.position,9))
            {
                if (alteredSign >= 0 && signs[alteredSign] && !BothPlayersAwayAndNotWatching(signs[alteredSign].transform.position,9)) return;
                if (alteredSign >= 0 && signs[alteredSign]) signs[alteredSign].text = signLabels[alteredSign];
                alteredSign = pendingSign; pendingSign = -1; signs[alteredSign].text = signLabels[(alteredSign + 1) % signs.Count]; restoreSignAt = game.ElapsedSeconds + 22;
            }
            if (alteredSign >= 0 && game.ElapsedSeconds >= restoreSignAt && BothPlayersAwayAndNotWatching(signs[alteredSign].transform.position,9))
            { signs[alteredSign].text = signLabels[alteredSign]; alteredSign = -1; }
        }
        void UpdateBodySounds()
        {
            for (int i = 0; i < coveredBodies.Length; i++)
            {
                var body = coveredBodies[i]; if (!body || coveredBodySoundPlayed[i]) continue;
                foreach (var player in game.players)
                {
                    if (!player || Vector3.Distance(player.transform.position,body.transform.position) > 4.5f || !Visible(player.view,body.transform.position + Vector3.up * .25f)) continue;
                    coveredBodySoundPlayed[i] = true; Sound(game.lightClip,body.transform.position,.12f);
                    if (!bodyClueGiven)
                    {
                        bodyClueGiven = true; int mark = Random.Range(0, 3);
                        game.Notify("Bajo la tela: registro " + (mark+1) + " dice «" + DisplayedSymbol(mark) + "». " + (FalseRevealed ? "El dato está contrastado." : "Puede mentir; verificadlo en la consola."),7);
                    }
                    else game.Notify("La tela se ha movido. Aquí ya no hay otra letra.",4);
                    break;
                }
            }
        }
        void UpdateAnomaly()
        {
            if (!eldritch || eldritchSoundPlayed) return;
            foreach (var player in game.players)
            {
                if (!player || Vector3.Distance(player.transform.position,eldritch.transform.position) > 7) continue;
                Vector3 position = player.transform.position;
                bool insideRoom = eldritch.transform.position.x < 0 ? position.x < -10 && position.z > 90 : position.x > 26 && position.z > 100;
                if (!insideRoom) continue;
                eldritchSoundPlayed = true; Sound(game.gearsClip,eldritch.transform.position,.08f); game.Notify("No os separéis aquí",6); break;
            }
        }
        public bool HasCandleLight(Vector3 point)
        {
            foreach (var candle in game.candles)
            {
                if (!candle || !candle.isActiveAndEnabled || !candle.candleLight || !candle.candleLight.enabled) continue;
                Vector3 delta = point - candle.candleLight.transform.position;
                if (delta.magnitude <= candle.candleLight.range && ClearRay(candle.candleLight.transform.position,point)) return true;
            }
            return false;
        }
        static bool ClearRay(Vector3 from, Vector3 to)
        {
            Vector3 delta = to-from;
            foreach (var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<DemoPlayer>() || hit.collider.GetComponentInParent<CarryObject>() || hit.collider.GetComponentInParent<RecintoStation>() || hit.collider.GetComponentInParent<RecintoContamination>()) continue;
                return false;
            }
            return true;
        }
        static bool IsInView(Camera camera, Vector3 point)
        {
            if (!camera) return false; Vector3 screen = camera.WorldToViewportPoint(point);
            return screen.z > 0 && screen.x > -.08f && screen.x < 1.08f && screen.y > -.08f && screen.y < 1.08f;
        }
        static bool Visible(Camera camera, Vector3 point) => IsInView(camera,point) && ClearRay(camera.transform.position,point);
        bool BothPlayersAwayAndNotWatching(Vector3 point, float distance)
        {
            foreach (var p in game.players) if (p && (Vector3.Distance(p.transform.position,point) < distance || IsInView(p.view,point))) return false;
            return true;
        }
        public void AdvanceShadow(string reason)
        {
            if (!game.Playing || !shadow) return;
            game.WarnThreat("Algo cruza fuera de la vela. " + reason);
            if (threat && shadow) threat.Warn(shadow.transform.position);
            if (travelBudget <= 0) { moveAfter = game.ElapsedSeconds + 3; route.Clear(); shadowTarget = null; }
            travelBudget = Mathf.Min(18,travelBudget + 7);
        }
        void UpdateShadow()
        {
            if (!shadow || path == null) return;
            bool candleClose = false;
            foreach (var p in game.players) if (p && HasCandleLight(p.transform.position + Vector3.up)) candleClose = true;
            abandonedTime = candleClose ? 0 : abandonedTime + Time.deltaTime;
            if (abandonedTime >= 7 && game.ElapsedSeconds >= nextAbandonedStep)
            { nextAbandonedStep = game.ElapsedSeconds + 9; AdvanceShadow("La luz está lejos de ambos."); }
            bool watched = ShadowWatched();
            navigating = (retreating || travelBudget > 0) && game.ElapsedSeconds >= moveAfter && (!watched || retreating);
            if (shadowAnimator)
            {
                shadowAnimator.speed = navigating ? 1 : 0;
                if (navigating && !shadowAnimator.GetCurrentAnimatorStateInfo(0).IsName("Crawl")) shadowAnimator.Play("Crawl");
            }
            if (threat) threat.NoteMotion(navigating, shadow.transform.position);
            if (navigating)
            {
                if (route.Count == 0)
                {
                    Vector3 target;
                    if (retreating) target = retreatPoint;
                    else
                    {
                        var nearest = game.players.OrderBy(p => Vector3.Distance(p.transform.position,shadow.transform.position)).First();
                        var candle = game.candles.FirstOrDefault(c => c && c.isActiveAndEnabled && c.candleLight && c.candleLight.enabled);
                        Vector3 lure = candle ? candle.candleLight.transform.position : nearest.transform.position;
                        shadowTarget = stations.Where(s => s && !s.Contaminated).OrderBy(s => Vector3.Distance(s.transform.position,lure)).FirstOrDefault();
                        target = Vector3.Distance(lure,shadow.transform.position) < 9 || !shadowTarget ? lure : shadowTarget.transform.position;
                    }
                    path.Route(shadow.transform.position,target,route);
                }
                if (route.Count > 0)
                {
                    Vector3 delta = route.Peek() - shadow.transform.position; delta.y = 0;
                    float step = Mathf.Min(delta.magnitude,Time.deltaTime * 1.1f);
                    if (delta.sqrMagnitude > .002f) shadow.transform.rotation = Quaternion.Slerp(shadow.transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime * 5);
                    shadow.transform.position += delta.normalized * step;
                    if (!retreating) travelBudget = Mathf.Max(0,travelBudget-step);
                    if (delta.magnitude < .15f) route.Dequeue();
                    if (retreating && route.Count == 0) retreating = false;
                }
                else { travelBudget = 0; retreating = false; }
                if (shadowTarget && !retreating && !shadowTarget.Contaminated && Vector3.Distance(shadow.transform.position,shadowTarget.transform.position) < 2)
                {
                    shadowTarget.Corrupt(); game.Notify("Metal y roce húmedo: la sombra ha corrompido " + shadowTarget.name + ". Quemen el brote.",7);
                    Sound(game.gearsClip,shadow.transform.position,.14f); shadowTarget = null; travelBudget = 0; route.Clear();
                }
            }
            bool canTouch = !retreating && game.ElapsedSeconds >= contactAfter && game.ElapsedSeconds >= moveAfter && travelBudget > 0f;
            if (canTouch)
            {
                foreach (var p in game.players)
                {
                    if (!p || Vector3.Distance(p.transform.position,shadow.transform.position) > 1.6f || HasCandleLight(p.transform.position + Vector3.up) || watched) continue;
                    contactAfter = game.ElapsedSeconds + 15;
                    bool alreadyTouched = game.ShadowContacts > 0;
                    game.ShadowContact();
                    if (!alreadyTouched && !game.Finished && threat) threat.Contact(shadow.transform.position);
                    retreating = !game.Finished; travelBudget = 0; route.Clear();
                    Vector3 away = shadow.transform.position - p.transform.position; away.y = 0;
                    retreatPoint = path.Nearest(shadow.transform.position + away.normalized * 9); moveAfter = game.ElapsedSeconds;
                    break;
                }
            }
            ConsiderAppearance();
        }
        float AppearanceGap()
        {
            float danger = game ? Mathf.Clamp01(game.Danger / 100f) : 0f;
            return Mathf.Lerp(Mathf.Max(12f, calmAppearanceSeconds), Mathf.Max(12f, dangerAppearanceSeconds), danger);
        }
        void ConsiderAppearance()
        {
            bool traveling = retreating || travelBudget > 0f || game.ElapsedSeconds < moveAfter;
            if (traveling) nextAppearance = Mathf.Max(nextAppearance, game.ElapsedSeconds + AppearanceGap());
            if (traveling || game.ElapsedSeconds < contactAfter || game.ElapsedSeconds < nextAppearance) return;
            AdvanceShadow("Se oye un gateo en la oscuridad.");
        }
        bool ShadowWatched()
        {
            Vector3 low = shadow.transform.position + Vector3.up * .25f;
            Vector3 side = shadow.transform.position + Vector3.right * .5f + Vector3.up * .3f;
            foreach (var player in game.players)
            {
                if (!player) continue;
                if (Perceivable(player.view, low) || Perceivable(player.view, side)) return true;
            }
            return false;
        }
        bool Perceivable(Camera camera, Vector3 point)
        {
            if (!Visible(camera, point)) return false;
            return Vector3.Distance(camera.transform.position, point) <= Mathf.Max(4f, seenDistance);
        }
        public void RecordEvidence(int index)
        {
            if (index < 0 || index >= 3 || evidence[index]) return;
            evidence[index] = true; EvidenceCount++;
            Sound(game.lightClip,stations[index].transform.position,.22f);
            game.Notify("Registro " + (index+1) + ": marca «" + DisplayedSymbol(index) + "». " + (FalseRevealed ? "Dato verificado." : "Uno de los tres miente: contrastad juntos."),8);
        }
        public void RecordSynchronization()
        {
            if (EvidenceCount < 3 || synchronized) return;
            synchronized = true; FalseRevealed = true;
            Sound(game.gearsClip,stations[3].transform.position,.22f);
            game.Notify("Registro " + (FalseIndex+1) + " FALSO: «" + falseMark + "» se corrige a «" + game.SymbolAt(FalseIndex) + "». " + game.SequenceText,10);
        }
        public void RecordPower() { game.Pulse("Red inspeccionada " + SurveyedPower + "/" + RequiredPower); }
        public void Invalidate(int index)
        {
            if (index >= 0 && index < 3 && evidence[index]) { evidence[index] = false; EvidenceCount--; }
            else if (index < 0) synchronized = false;
        }
        public void LightBetrayed(RecintoStation station)
        {
            game.Notify(station.name + " DELATA: parpadea y suena metal. La sombra avanza incluso con la vela cerca.",7);
            Sound(game.gearsClip,station.transform.position,.22f); AdvanceShadow("Una luz auxiliar ha delatado la zona.");
        }
        public void BurnSound(Vector3 point) { Sound(burnClip,point,.19f); }
        public void Sound(AudioClip clip, Vector3 point, float volume)
        {
            if (!clip || !fright || !game.Playing) return;
            var p = game.players.OrderBy(x => Vector3.Distance(x.view.transform.position,point)).First();
            Vector3 delta = point-p.view.transform.position;
            fright.panStereo = Mathf.Clamp(Vector3.Dot(p.view.transform.right,delta.normalized),-.6f,.6f);
            fright.PlayOneShot(clip,game.sfxVolume * volume * Mathf.Clamp01(1-delta.magnitude/28));
        }
        public string OnWrongProcedure()
        {
            AdvanceShadow("El error ha atraído un roce húmedo.");
            if (signs.Count > 1) pendingSign = Random.Range(0,signs.Count);
            var light = stations.FirstOrDefault(s => s.IsPowerOn);
            if (light) { light.Corrupt(); return "El error apagó " + light.name + " y dejó contaminación."; }
            var station = stations.FirstOrDefault(s => !s.Contaminated);
            if (!station) return "";
            Contaminate(station); return "Hay un brote en " + station.name + ".";
        }
        void OnDestroy()
        {
            if (game) game.FirstMechanismCompleted -= PassageChanged;
            foreach (var m in new[] { metal,darkLamp,activeLamp,insects,larva,bodyMaterial }) if (m) Destroy(m);
            if (ownsStain && stain) Destroy(stain);
            foreach (var m in eldritchMaterials) if (m) Destroy(m);
            foreach (var m in shadowMaterials) if (m) Destroy(m);
        }

        static Material MakeMaterial(string label, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = label, color = color };
            material.SetFloat("_Smoothness", 0.12f);
            return material;
        }

        void AddExistingClutter(List<int> slots)
        {
            GameObject barrel = null, pallet = null;
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!barrel && candidate.name == "Barrel") barrel = candidate.gameObject;
                if (!pallet && candidate.name == "Pallet") pallet = candidate.gameObject;
                if (barrel && pallet) break;
            }
            if (!barrel && !pallet) return;
            for (int i = 0; i < 4; i++)
            {
                var source = i % 2 == 0 ? barrel : pallet;
                if (!source) source = barrel ? barrel : pallet;
                var prop = Instantiate(source, transform);
                prop.name = i % 2 == 0 ? "Barril recuperado" : "Palé abandonado";
                prop.transform.SetPositionAndRotation(Sites[slots[i + 2]] + new Vector3(i % 2 == 0 ? 1.6f : -1.6f, 0f, 1.1f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                foreach (var collider in prop.GetComponentsInChildren<Collider>()) Destroy(collider);
                float bottom = float.MaxValue;
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (bottom < float.MaxValue) prop.transform.position += Vector3.up * (0.03f - bottom);
            }
        }

        void CreateCoveredBodies()
        {
            var source = Resources.Load<GameObject>("Horror/DeadBodyCovered");
            var texture = Resources.Load<Texture2D>("Horror/DeadBodyCovered_BaseColor");
            if (!source || !texture)
            {
                Debug.LogError("Faltan el modelo o la textura de DeadBodyCovered en Assets/Resources/Horror.");
                return;
            }
            bodyMaterial = MakeMaterial("Tela de cuerpo cubierto", Color.white);
            bodyMaterial.SetTexture("_BaseMap", texture);
            bodyModelRotation = source.transform.rotation;
            depotBodyMayMove = Random.value < 0.75f;
            int second = Random.Range(1, BodySites.Length);
            int[] selected = { 0, second };
            foreach (int index in selected)
            {
                var body = Instantiate(source);
                body.name = "Cuerpo cubierto - hallazgo " + index;
                if (index == 0) depotBody = body;
                coveredBodies[index == 0 ? 0 : 1] = body;
                body.transform.SetParent(transform, true);
                body.transform.SetPositionAndRotation(BodySites[index], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * bodyModelRotation);
                foreach (var collider in body.GetComponentsInChildren<Collider>()) Destroy(collider);
                var renderers = body.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { Debug.LogError("DeadBodyCovered no tiene malla visible."); continue; }
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = bodyMaterial;
                    renderer.sharedMaterials = materials;
                }
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                if (bounds.size.x > 0.001f && bounds.size.z > 0.001f)
                    body.transform.localScale *= 2.3f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                body.transform.position += Vector3.up * (0.03f - bounds.min.y);
            }
        }

        void CreateEldritchAnomaly()
        {
            var source = Resources.Load<GameObject>("Horror/LovecraftianHorror");
            var bodyTexture = Resources.Load<Texture2D>("Horror/LovecraftBody");
            var gumsTexture = Resources.Load<Texture2D>("Horror/LovecraftGums");
            var teethTexture = Resources.Load<Texture2D>("Horror/LovecraftTeeth");
            if (!source || !bodyTexture || !gumsTexture || !teethTexture)
            {
                Debug.LogError("Faltan recursos de LovecraftianHorror en Assets/Resources/Horror.");
                return;
            }
            var skin = MakeMaterial("Piel del ente", Color.white);
            skin.SetTexture("_BaseMap", bodyTexture);
            var gums = MakeMaterial("Encías del ente", Color.white);
            gums.SetTexture("_BaseMap", gumsTexture);
            var teeth = MakeMaterial("Dientes del ente", Color.white);
            teeth.SetTexture("_BaseMap", teethTexture);
            var eyes = MakeMaterial("Ojos del ente", new Color(0.14f, 0.035f, 0.035f));
            eldritchMaterials.Add(skin);
            eldritchMaterials.Add(gums);
            eldritchMaterials.Add(teeth);
            eldritchMaterials.Add(eyes);
            eldritch = Instantiate(source);
            eldritch.name = "Anomalía persistente - ente del recinto";
            eldritch.transform.SetParent(transform, true);
            int site = Random.Range(0, EldritchSites.Length);
            Quaternion modelRotation = eldritch.transform.rotation;
            eldritch.transform.SetPositionAndRotation(EldritchSites[site], Quaternion.Euler(0f, site == 0 ? 145f : -80f, 0f) * modelRotation);
            foreach (var collider in eldritch.GetComponentsInChildren<Collider>()) Destroy(collider);
            var renderers = eldritch.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogError("LovecraftianHorror no contiene mallas visibles."); return; }
            foreach (var renderer in renderers)
            {
                Material chosen = skin;
                if (renderer.name.Contains("29013")) chosen = eyes;
                else if (renderer.name.Contains("63675")) chosen = gums;
                else if (renderer.name.Contains("33359")) chosen = teeth;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = chosen;
                renderer.sharedMaterials = materials;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f) eldritch.transform.localScale *= 2.4f / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            eldritch.transform.position += Vector3.up * (0.03f - bounds.min.y);
            var glow = new GameObject("Reflejo tenue del ente");
            glow.transform.SetParent(eldritch.transform, false);
            glow.transform.localPosition = Vector3.up * 0.18f;
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 0.19f, 0.15f);
            light.intensity = 0.32f;
            light.range = 3f;
            light.shadows = LightShadows.None;
            Debug.Log("ANOMALIA_ENTE_CREADA zona=" + site);
        }

        void PrepareSigns()
        {
            string[] names = { "Cartel ENTRADA", "Cartel CARGA / DEPOSITO", "Cartel PATIO INTERIOR", "Cartel TALLER", "Cartel BOMBAS", "Cartel MANIVELA" };
            string[] labels = { "ACCESO 01", "ALMACÉN 02", "PATIO 03", "TALLER 04", "BOMBAS 05", "ÚTILES" };
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude))
                for (int i = 0; i < names.Length; i++)
                    if (text.name == names[i])
                    {
                        text.text = labels[i];
                        signs.Add(text);
                        signLabels.Add(labels[i]);
                        break;
                    }
        }

        void UpdateCoveredBody()
        {
            if (!depotBody || depotBodyMoved || !depotBodyMayMove) return;
            bool nearby = false;
            Vector3 destination = new Vector3(-12.2f, depotBody.transform.position.y, 28f);
            foreach (var player in game.players)
            {
                if (!player) continue;
                float distance = Vector3.Distance(player.transform.position, depotBody.transform.position);
                if (distance < 7f) nearby = true;
            }
            if (nearby) depotBodySeen = true;
            if (!depotBodySeen || !BothPlayersAwayAndNotWatching(depotBody.transform.position + Vector3.up * 0.35f, 9f)
                || !BothPlayersAwayAndNotWatching(destination + Vector3.up * 0.35f, 9f)) return;
            depotBody.transform.SetPositionAndRotation(destination, Quaternion.Euler(0f, 105f, 0f) * bodyModelRotation);
            depotBodyMoved = true;
            coveredBodySoundPlayed[0] = false;
            Debug.Log("ANOMALIA_CUERPO_MOVIDO tiempo=" + game.ElapsedSeconds.ToString("F1"));
        }
    }
}

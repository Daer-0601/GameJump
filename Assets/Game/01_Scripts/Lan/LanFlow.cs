using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Gamejam2026
{
    // Ciclo de vida de la sala. La simulación de la expedición sigue en DemoGame, solo en el anfitrión.
    public static class LanFlow
    {
        public const ushort Port = 7777;
        public const float ConnectTimeout = 8f;

        public enum Phase { Idle, Starting, Waiting, Connecting, Lobby, Preparing, Playing, Error }

        public static Phase Current { get; private set; } = Phase.Idle;
        public static string Banner { get; private set; } = "";
        public static string Detail { get; private set; } = "";
        public static string FormError { get; private set; } = "";
        public static string PeerMessage { get; private set; } = "";
        public static string Addresses { get; private set; } = "";
        public static bool LocalReady { get; private set; }
        public static bool PeerReady { get; private set; }
        public static bool PeerJoined { get; private set; }
        public static bool PeerLost { get; private set; }
        public static bool LanMatch { get; private set; }
        public static bool InSession => manager != null && (manager.IsListening || manager.IsClient || manager.IsServer);
        public static bool IsHost => manager != null && manager.IsServer;
        public static bool IsClientView => manager != null && manager.IsClient && !manager.IsServer;
        public static bool Simulates => !IsClientView && !remoteView;
        public static int LocalSlot => remoteView || IsClientView ? 1 : stickySlot;
        public static DemoGame Game { get; private set; }

        static NetworkManager manager;
        static UnityTransport transport;
        static bool remoteView;
        static int stickySlot;
        static bool quiet;
        static bool shuttingDown;
        static bool storedBackground;
        static bool previousBackground;
        static float connectUntil;
        static float prepareUntil;
        static bool remoteConnected;
        static string joinAddress = "";

        public static Vector2 RemoteMove { get; private set; }
        public static Vector2 RemoteLook { get; private set; }
        public static bool RemoteRun { get; private set; }
        public static bool RemoteUseHeld { get; private set; }
        public static bool RemoteReading { get; private set; }
        static bool remoteUse;
        static bool remoteDrop;
        static float remoteAt = -100f;
        static bool hasSequence;
        static ushort lastSequence;

        public static void Bind(DemoGame game)
        {
            if (game) Game = game;
        }

        public static void Detach(DemoGame game)
        {
            if (Game == game) Game = null;
        }

        public static bool ValidateIPv4(string text, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Escribe la IP local del anfitrión.";
                return false;
            }
            string trimmed = text.Trim();
            string[] parts = trimmed.Split('.');
            if (parts.Length != 4)
            {
                error = "IP inválida. Hacen falta cuatro números separados por puntos, por ejemplo 192.168.1.20.";
                return false;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 3 || !byte.TryParse(parts[i], out _))
                {
                    error = "IP inválida. Cada parte tiene que ser un número entre 0 y 255.";
                    return false;
                }
            }
            if (!IPAddress.TryParse(trimmed, out IPAddress address) || address.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "IP inválida. Usa una IPv4, por ejemplo 192.168.1.20.";
                return false;
            }
            return true;
        }

        public static void ArmJoin()
        {
            if (InSession) Cancel();
            Current = Phase.Idle;
            FormError = "";
            Banner = "Unirse a una partida en la misma red";
            Detail = "Escribe la IPv4 que muestra quien creó la sala. Tú serás el operario 2 y empezarás con la manivela. El anfitrión es el operario 1 y empieza con la vela. Cada PC usa su propio teclado: WASD, ratón, E, Q y Shift.";
            Publish();
        }

        public static void StartHost(DemoGame game)
        {
            Bind(game);
            FormError = "";
            PeerLost = false;
            PeerMessage = "";
            if (!EnsureManager())
            {
                Fail("No se pudo preparar la red local.", false);
                return;
            }
            Addresses = DescribeAddresses();
            quiet = false;
            Current = Phase.Starting;
            Banner = "Iniciando sala";
            Detail = "Abriendo el puerto " + Port + ".";
            transport.SetConnectionData("0.0.0.0", Port, "0.0.0.0");
            if (!manager.StartHost())
            {
                Fail("No se pudo abrir el puerto " + Port + ". Ciérralo si otra sala sigue activa en este PC.", false);
                return;
            }
            if (!SpawnSession())
            {
                Fail("No se pudo crear la sala. Falta el prefab LanSession.", false);
                return;
            }
            Current = Phase.Waiting;
            Publish();
        }

        public static void Connect(string address, DemoGame game)
        {
            Bind(game);
            if (!ValidateIPv4(address, out string error))
            {
                FormError = error;
                Current = Phase.Idle;
                Banner = "Unirse a una partida en la misma red";
                Detail = error;
                return;
            }
            FormError = "";
            if (!EnsureManager())
            {
                Fail("No se pudo preparar la red local.", false);
                return;
            }
            joinAddress = address.Trim();
            quiet = false;
            Current = Phase.Connecting;
            connectUntil = Time.unscaledTime + ConnectTimeout;
            Banner = "Conectando";
            Detail = "Buscando al anfitrión en " + joinAddress + ":" + Port + ".";
            transport.SetConnectionData(joinAddress, Port, "0.0.0.0");
            if (!manager.StartClient())
            {
                Fail("No se pudo iniciar la conexión. Revisa la IP y que el puerto " + Port + " esté libre.", false);
            }
        }

        public static void Cancel()
        {
            quiet = true;
            FormError = "";
            PeerJoined = false;
            LocalReady = false;
            PeerReady = false;
            remoteConnected = false;
            Current = Phase.Idle;
            Banner = "";
            Detail = "";
            ReleaseImmediate();
        }

        public static void ToggleReady()
        {
            if (Current != Phase.Lobby || !PeerJoined) return;
            LocalReady = !LocalReady;
            if (LanSession.Instance) LanSession.Instance.PushReady(LocalReady);
            Publish();
        }

        public static void RequestPause(bool pause)
        {
            if (PeerLost && !pause) return;
            if (LanSession.Instance) LanSession.Instance.PushPause(pause);
        }

        public static void MarkPlaying()
        {
            LanMatch = true;
            remoteView = IsClientView;
            stickySlot = remoteView ? 1 : 0;
            Current = Phase.Playing;
            LocalReady = false;
            PeerReady = false;
        }

        public static void NotePeer(bool joined)
        {
            remoteConnected = joined;
            PeerJoined = joined;
            if (!joined)
            {
                PeerReady = false;
                LocalReady = false;
                if (Current == Phase.Lobby || Current == Phase.Preparing) Current = Phase.Waiting;
            }
            else if (Current == Phase.Waiting || Current == Phase.Starting) Current = Phase.Lobby;
            Publish();
        }

        public static void NoteReady(bool hostReady, bool clientReady)
        {
            if (IsHost)
            {
                LocalReady = hostReady;
                PeerReady = clientReady;
            }
            else
            {
                LocalReady = clientReady;
                PeerReady = hostReady;
            }
            Publish();
        }

        public static void NotePreparing()
        {
            Current = Phase.Preparing;
            prepareUntil = Time.unscaledTime + 20f;
            Banner = "Preparando la expedición";
            Detail = "El anfitrión genera una sola distribución. Los dos veréis los mismos puestos, pistas y anomalías. Aún no empieces a caminar.";
        }

        public static void Tick()
        {
            if (Current == Phase.Connecting && Time.unscaledTime >= connectUntil && !PeerJoined)
                Fail("Tiempo de espera agotado. No hubo respuesta de " + joinAddress + ". Comprueba la IP, el puerto " + Port + " y que ambos PCs estén en la misma red.", false);
            if (Current == Phase.Preparing && IsHost && Time.unscaledTime >= prepareUntil && !LanMatch)
                Fail("La otra persona no confirmó la expedición a tiempo. Cierra la sala e inténtalo de nuevo.", true);
        }

        public static void StoreInput(LanInput input)
        {
            if (hasSequence && (ushort)(input.sequence - lastSequence) >= 32768) return;
            hasSequence = true;
            lastSequence = input.sequence;
            remoteAt = Time.unscaledTime;
            RemoteReading = input.reading;
            if (RemoteReading)
            {
                RemoteMove = Vector2.zero;
                RemoteLook = Vector2.zero;
                RemoteRun = false;
                RemoteUseHeld = false;
                remoteUse = false;
                remoteDrop = false;
                return;
            }
            RemoteMove = new Vector2(input.moveX, input.moveY);
            RemoteLook += new Vector2(input.lookX, input.lookY);
            RemoteRun = input.run;
            RemoteUseHeld = input.useHeld;
            remoteAt = Time.unscaledTime;
        }

        public static void StoreEdge(bool use, bool drop)
        {
            if (RemoteReading) return;
            if (use) remoteUse = true;
            if (drop) remoteDrop = true;
            remoteAt = Time.unscaledTime;
        }

        public static DemoPlayerInput.State ConsumeRemote()
        {
            if (RemoteReading) return default;
            if (Time.unscaledTime - remoteAt > 0.45f)
            {
                RemoteMove = Vector2.zero;
                RemoteLook = Vector2.zero;
                RemoteRun = false;
                RemoteUseHeld = false;
                remoteUse = false;
                remoteDrop = false;
                return default;
            }
            var state = new DemoPlayerInput.State(RemoteMove, RemoteLook, RemoteRun, remoteUse, RemoteUseHeld, remoteDrop);
            RemoteLook = Vector2.zero;
            remoteUse = false;
            remoteDrop = false;
            return state;
        }

        public static void ClearRemote()
        {
            RemoteReading = false;
            RemoteMove = Vector2.zero;
            RemoteLook = Vector2.zero;
            RemoteRun = false;
            RemoteUseHeld = false;
            remoteUse = false;
            remoteDrop = false;
            remoteAt = -100f;
            hasSequence = false;
        }

        public static void HostLostPeer()
        {
            if (LanMatch)
            {
                PeerLost = true;
                PeerMessage = "El otro jugador se desconectó. La expedición queda en pausa y no seguirá avanzando.";
                ClearRemote();
                if (Game) Game.PauseForPeer();
                return;
            }
            NotePeer(false);
            Detail = "El otro jugador se desconectó. La sala sigue abierta en el puerto " + Port + ".";
        }

        public static void ClientLost(string reason)
        {
            bool during = LanMatch;
            string message = MapReason(reason, during);
            if (during)
            {
                PeerLost = true;
                PeerMessage = message;
                remoteView = true;
                if (Game) Game.PauseForPeer();
            }
            Fail(message, during);
        }

        public static IEnumerator ShutdownAndWait()
        {
            quiet = true;
            ForgetMatch();
            if (manager == null)
            {
                DropSession();
                ClearManager();
                yield break;
            }
            shuttingDown = true;
            bool listening = manager.IsListening || manager.IsClient || manager.IsServer;
            if (!listening)
            {
                Object.Destroy(manager.gameObject);
                ClearManager();
                yield break;
            }
            bool done = false;
            void Stopped(bool _) { done = true; }
            manager.OnClientStopped += Stopped;
            manager.OnServerStopped += Stopped;
            manager.Shutdown();
            float waited = 0f;
            while (!done && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (manager != null)
            {
                manager.OnClientStopped -= Stopped;
                manager.OnServerStopped -= Stopped;
                Object.Destroy(manager.gameObject);
            }
            DropSession();
            ClearManager();
        }

        public static void ShutdownImmediate()
        {
            quiet = true;
            ForgetMatch();
            ReleaseImmediate();
            DropSession();
        }

        static void Fail(string message, bool keepMatch)
        {
            if (quiet) return;
            FormError = message;
            PeerMessage = message;
            Current = Phase.Error;
            Banner = keepMatch ? "Conexión perdida" : "No se pudo conectar";
            Detail = message;
            if (!keepMatch)
            {
                PeerJoined = false;
                LocalReady = false;
                PeerReady = false;
            }
            ReleaseImmediate();
        }

        static string MapReason(string reason, bool duringMatch)
        {
            if (reason == "full") return "Partida llena. Solo pueden jugar dos personas.";
            if (duringMatch) return "El anfitrión cerró la sala. La expedición no puede continuar.";
            if (Current == Phase.Connecting) return "Conexión rechazada. Comprueba que el anfitrión tenga la sala abierta y que la IP sea la de su PC.";
            return "Se perdió la conexión con la sala.";
        }

        static void Publish()
        {
            Addresses = string.IsNullOrEmpty(Addresses) ? DescribeAddresses() : Addresses;
            if (Current == Phase.Waiting)
            {
                Banner = "Sala abierta. Eres el anfitrión, operario 1 (vela).";
                Detail = "La otra persona elige Unirse a partida y escribe una de estas IP, puerto " + Port + ":\n" + Addresses
                    + "\nEsa persona será el operario 2 y empezará con la manivela. La expedición no arranca hasta que los dos estéis dentro y pulséis Estoy listo.";
            }
            else if (Current == Phase.Lobby && IsHost)
            {
                Banner = "Compañero conectado. Tú: operario 1, vela. El otro: operario 2, manivela.";
                Detail = ReadyLine();
            }
            else if (Current == Phase.Lobby)
            {
                Banner = "Conectado. Tú eres el operario 2 y empiezas con la manivela.";
                Detail = "El anfitrión es el operario 1, con la vela. En tu PC: WASD, ratón, E, Q y Shift.\n" + ReadyLine();
            }
            else if (Current == Phase.Error && string.IsNullOrEmpty(Detail))
            {
                Detail = FormError;
            }
        }

        static string ReadyLine()
        {
            string mine = LocalReady ? "listo" : "en espera";
            string other = PeerReady ? "listo" : "en espera";
            return "Tú: " + mine + ". Compañero: " + other + ". Cuando los dos estéis listos empieza la expedición, con la misma generación en ambos PCs.";
        }

        static bool EnsureManager()
        {
            if (manager != null) ReleaseImmediate();
            var prefab = Resources.Load<GameObject>("Lan/LanSession");
            if (!prefab || !prefab.GetComponent<NetworkObject>() || !prefab.GetComponent<LanSession>())
            {
                Debug.LogError("LanFlow: falta Assets/Resources/Lan/LanSession.prefab con NetworkObject y LanSession.");
                return false;
            }
            var host = new GameObject("LanNetwork");
            Object.DontDestroyOnLoad(host);
            transport = host.AddComponent<UnityTransport>();
            transport.ConnectTimeoutMS = (int)(ConnectTimeout * 1000f);
            transport.MaxConnectAttempts = 8;
            transport.DisconnectTimeoutMS = 2500;
            transport.HeartbeatTimeoutMS = 500;
            manager = host.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                ProtocolVersion = 1, // LanInput incluye el estado de lectura del cuaderno.
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                PlayerPrefab = null,
                TickRate = 30
            };
            if (manager.NetworkConfig.Prefabs == null) manager.NetworkConfig.Prefabs = new NetworkPrefabs();
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
            manager.OnTransportFailure += OnTransportFailed;
            if (!storedBackground)
            {
                previousBackground = Application.runInBackground;
                storedBackground = true;
            }
            Application.runInBackground = true;
            shuttingDown = false;
            quiet = false;
            return true;
        }

        static bool SpawnSession()
        {
            var prefab = Resources.Load<GameObject>("Lan/LanSession");
            if (!prefab || manager == null || !manager.IsServer) return false;
            var instance = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(instance);
            var networkObject = instance.GetComponent<NetworkObject>();
            if (!networkObject) return false;
            networkObject.Spawn();
            return true;
        }

        static void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int occupants = manager != null ? manager.ConnectedClientsIds.Count : 0;
            bool full = occupants >= 2;
            response.Approved = !full;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = full ? "full" : "";
        }

        static void OnConnected(ulong clientId)
        {
            if (manager == null) return;
            if (manager.IsServer && clientId != manager.LocalClientId)
            {
                NotePeer(true);
                if (LanSession.Instance && Game) LanSession.Instance.PublishNames(Game);
            }
            if (!manager.IsServer)
            {
                Current = Phase.Lobby;
                PeerJoined = true;
                Publish();
            }
        }

        static void OnDisconnected(ulong clientId)
        {
            if (quiet || shuttingDown || manager == null) return;
            if (manager.IsServer)
            {
                if (clientId != manager.LocalClientId) HostLostPeer();
                return;
            }
            string reason = manager.DisconnectReason;
            ClientLost(reason);
        }

        static void OnTransportFailed()
        {
            if (quiet || shuttingDown) return;
            Fail("La red local falló. Revisa el puerto " + Port + " y que los dos PCs estén en la misma red.", LanMatch);
        }

        static void ReleaseImmediate()
        {
            shuttingDown = true;
            if (manager != null)
            {
                manager.ConnectionApprovalCallback -= Approve;
                manager.OnClientConnectedCallback -= OnConnected;
                manager.OnClientDisconnectCallback -= OnDisconnected;
                manager.OnTransportFailure -= OnTransportFailed;
                if (manager.IsListening || manager.IsClient || manager.IsServer) manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }
            ClearManager();
        }

        static void ForgetMatch()
        {
            LanMatch = false;
            remoteView = false;
            stickySlot = 0;
            PeerLost = false;
            PeerJoined = false;
            LocalReady = false;
            PeerReady = false;
            if (Current != Phase.Idle) Current = Phase.Idle;
        }

        static void DropSession()
        {
            if (!LanSession.Instance) return;
            Object.Destroy(LanSession.Instance.gameObject);
        }

        static void ClearManager()
        {
            manager = null;
            transport = null;
            shuttingDown = false;
            if (!LanMatch)
            {
                remoteView = false;
                stickySlot = 0;
            }
            if (storedBackground && !LanMatch)
            {
                Application.runInBackground = previousBackground;
                storedBackground = false;
            }
            ClearRemote();
        }

        public static string DescribeAddresses()
        {
            var found = new List<string>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string text = address.Address.ToString();
                    if (text.StartsWith("169.254.")) continue;
                    if (!found.Contains(text)) found.Add(text);
                }
            }
            if (found.Count == 0) return "127.0.0.1 (no se detectó otra IPv4; sirve si el otro jugador está en este mismo PC)";
            return string.Join("\n", found);
        }
    }
}

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Gamejam2026
{
    public class LanSession : NetworkBehaviour
    {
        public static LanSession Instance { get; private set; }

        readonly NetworkVariable<bool> hostReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> clientReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        float nextInput;
        float nextSnapshot;
        ushort inputSequence;
        ushort snapshotSequence;
        Vector2 pendingLook;
        bool launching;
        bool built;
        bool seedApplied;
        int pendingSeed;
        float inputInterval = 1f / 30f;
        float snapshotInterval = 1f / 30f;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            hostReady.OnValueChanged += OnReadyChanged;
            clientReady.OnValueChanged += OnReadyChanged;
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += ServerPeerLeft;
                LanFlow.NotePeer(NetworkManager.ConnectedClientsIds.Count >= 2);
            }
            else if (LanFlow.Game) PushNameRpc(new FixedString64Bytes(CrewName(LanFlow.Game, true)));
        }

        public override void OnNetworkDespawn()
        {
            hostReady.OnValueChanged -= OnReadyChanged;
            clientReady.OnValueChanged -= OnReadyChanged;
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= ServerPeerLeft;
            if (Instance == this) Instance = null;
        }

        void OnReadyChanged(bool previous, bool current)
        {
            LanFlow.NoteReady(hostReady.Value, clientReady.Value);
            if (IsServer && hostReady.Value && clientReady.Value && !launching && !LanFlow.LanMatch)
                BeginPrepare();
        }

        void ServerPeerLeft(ulong clientId)
        {
            if (!IsServer || clientId == NetworkManager.ServerClientId) return;
            hostReady.Value = false;
            clientReady.Value = false;
            launching = false;
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsServer || LanFlow.PeerLost || !LanFlow.LanMatch) return;
            if (Time.unscaledTime < nextSnapshot) return;
            var game = LanFlow.Game;
            if (!game) return;
            nextSnapshot = Time.unscaledTime + snapshotInterval;
            snapshotSequence++;
            SnapshotRpc(LanPresentation.Capture(game, snapshotSequence));
        }

        void Update()
        {
            if (!IsSpawned || LanFlow.PeerLost) return;
            var game = LanFlow.Game;
            if (!game) return;
            if (IsServer) return;
            if (!LanFlow.LanMatch || !game.PlayerInputEnabled) return;
            int slot = LanAuthority.LocalSlot;
            if (game.players == null || slot < 0 || slot >= game.players.Length || !game.players[slot]) return;
            var avatar = game.players[slot];
            var sample = DemoPlayerInput.Read(0, avatar.keyboardLookSpeed, avatar.mouseSensitivity, Time.deltaTime, avatar.preciseLookMultiplier);
            if (sample.Use) EdgeRpc(1);
            if (sample.Drop) EdgeRpc(2);
            pendingLook += sample.Look;
            if (Time.unscaledTime < nextInput) return;
            nextInput = Time.unscaledTime + inputInterval;
            inputSequence++;
            InputRpc(new LanInput
            {
                moveX = sample.Move.x,
                moveY = sample.Move.y,
                lookX = pendingLook.x,
                lookY = pendingLook.y,
                run = sample.Run,
                useHeld = sample.UseHeld,
                sequence = inputSequence
            });
            pendingLook = Vector2.zero;
        }

        public void PushReady(bool ready)
        {
            if (!IsSpawned) return;
            ReadyRpc(ready);
        }

        public void PushPause(bool pause)
        {
            if (!IsSpawned) return;
            PauseRpc(pause);
        }

        public void PublishNames(DemoGame game)
        {
            if (!IsServer || !game) return;
            NamesRpc(new FixedString64Bytes(game.CrewName(0)), new FixedString64Bytes(game.CrewName(1)));
        }

        public static void BroadcastGrowth(int station, int visualSeed, int id)
        {
            if (Instance && Instance.IsServer) Instance.GrowthRpc(station, visualSeed, id);
        }

        public static void BroadcastTell(int player, string text, float seconds)
        {
            if (Instance && Instance.IsServer) Instance.TellRpc((byte)Mathf.Clamp(player, 0, 2), text ?? "", seconds);
        }

        public static void BroadcastWarn(string text)
        {
            if (Instance && Instance.IsServer) Instance.WarnRpc(text ?? "");
        }

        public static void BroadcastPulse(string text, int priority)
        {
            if (Instance && Instance.IsServer) Instance.PulseRpc(text ?? "", priority);
        }

        public static void BroadcastShock()
        {
            if (Instance && Instance.IsServer) Instance.ShockRpc();
        }

        public static void BroadcastSfx(byte id)
        {
            if (id == 255 || Instance == null || !Instance.IsServer) return;
            Instance.SfxRpc(id);
        }

        public static void BroadcastWorld(byte id, Vector3 point, float volume)
        {
            if (id == 255 || Instance == null || !Instance.IsServer) return;
            Instance.WorldRpc(id, point, volume);
        }

        public static void BroadcastThreat(byte kind, Vector3 point, float amount)
        {
            if (kind > 2 || Instance == null || !Instance.IsServer) return;
            Instance.ThreatRpc(kind, point, amount);
        }

        void BeginPrepare()
        {
            launching = true;
            pendingSeed = Random.Range(1, int.MaxValue);
            LanFlow.NotePreparing();
            LanPresentation.ResetSequence();
            if (LanFlow.Game) LanFlow.Game.RebuildFromSeed(pendingSeed);
            built = true;
            SeedRpc(pendingSeed);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            if (!IsServer || launching || LanFlow.LanMatch) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender == NetworkManager.ServerClientId) hostReady.Value = ready;
            else clientReady.Value = ready;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void InputRpc(LanInput input, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender == NetworkManager.ServerClientId) return;
            if (!LanFlow.Game || !LanFlow.Game.PlayerInputEnabled) return;
            LanFlow.StoreInput(input);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void EdgeRpc(byte edges, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) return;
            if (!LanFlow.Game || !LanFlow.Game.PlayerInputEnabled) return;
            LanFlow.StoreEdge((edges & 1) != 0, (edges & 2) != 0);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void PauseRpc(bool pause, RpcParams rpcParams = default)
        {
            if (!IsServer || !LanFlow.Game) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender == NetworkManager.ServerClientId) return;
            if (LanFlow.PeerLost) return;
            if (pause) LanFlow.Game.PauseNow();
            else LanFlow.Game.ResumeNow();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void PushNameRpc(FixedString64Bytes name, RpcParams rpcParams = default)
        {
            if (!IsServer || !LanFlow.Game) return;
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) return;
            LanFlow.Game.SetCrewNames(CrewName(LanFlow.Game, true), name.ToString());
            NamesRpc(new FixedString64Bytes(CrewName(LanFlow.Game, true)), name);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void AckRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || !launching || LanFlow.LanMatch) return;
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) return;
            if (!built || !LanFlow.Game) return;
            LanFlow.Game.EnterLanPlay();
            GoRpc();
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void SeedRpc(int seed)
        {
            if (IsServer || seedApplied || !LanFlow.Game) return;
            seedApplied = true;
            LanFlow.NotePreparing();
            LanPresentation.ResetSequence();
            LanFlow.Game.RebuildFromSeed(seed);
            AckRpc();
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void GoRpc()
        {
            if (IsServer || !LanFlow.Game) return;
            LanFlow.Game.EnterLanPlay();
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void NamesRpc(FixedString64Bytes hostName, FixedString64Bytes clientName)
        {
            if (IsServer || !LanFlow.Game) return;
            LanFlow.Game.SetCrewNames(hostName.ToString(), clientName.ToString());
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        void SnapshotRpc(LanSnapshot snapshot)
        {
            if (!LanFlow.LanMatch || !LanFlow.Game) return;
            LanPresentation.Apply(LanFlow.Game, snapshot);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void GrowthRpc(int station, int visualSeed, int id)
        {
            if (!LanFlow.Game || !LanFlow.Game.Investigation) return;
            LanFlow.Game.Investigation.ContaminateFromNet(station, visualSeed, id);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void TellRpc(byte player, string text, float seconds)
        {
            var game = LanFlow.Game;
            if (!game || game.players == null || player >= game.players.Length || !game.players[player]) return;
            LanEcho.Apply(() => game.players[player].Tell(text, seconds));
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void WarnRpc(string text)
        {
            if (!LanFlow.Game) return;
            LanEcho.Apply(() => LanFlow.Game.WarnThreat(text));
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void PulseRpc(string text, int priority)
        {
            if (!LanFlow.Game) return;
            LanEcho.Apply(() => LanFlow.Game.Pulse(text, priority));
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void ShockRpc()
        {
            if (!LanFlow.Game) return;
            LanEcho.Apply(() => LanFlow.Game.PresentShock());
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void SfxRpc(byte id)
        {
            if (!LanFlow.Game) return;
            LanFlow.Game.PlayRelayedSfx(id);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void WorldRpc(byte id, Vector3 point, float volume)
        {
            if (!LanFlow.Game || !LanFlow.Game.Investigation) return;
            LanFlow.Game.Investigation.PlayRelayed(id, point, volume);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void ThreatRpc(byte kind, Vector3 point, float amount)
        {
            if (!LanFlow.Game) return;
            var threat = LanFlow.Game.GetComponent<RecintoThreatPresentation>();
            if (threat) threat.Present(kind, point, amount);
        }

        static string CrewName(DemoGame game, bool hostSide)
        {
            return hostSide ? game.CrewName(0) : game.CrewName(1);
        }
    }
}

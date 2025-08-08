using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

public partial class NetworkManager : Node3D
{
    public static NetworkManager Instance { get; private set; }

    public PackedScene PlayerScene;
    public PackedScene TransitionScreenScene => (PackedScene)GD.Load("res://Scenes/TransitionScreen.tscn");
    public PackedScene ChatScene => (PackedScene)GD.Load("res://Scenes/Chat.tscn");
    public PackedScene PauseMenuScene => (PackedScene)GD.Load("res://Scenes/PauseMenu.tscn");
    public PackedScene GameScene => (PackedScene)GD.Load("res://Scenes/Game.tscn");

    public MultiplayerSpawner PlayerSpawner;
    public Timer connectionTimer;

    public Dictionary<int, string> PlayerNames = new();
    public Dictionary<int, Color> PlayerColors = new();
    public HashSet<int> InGamePlayers = new();

    public bool IsServer => Multiplayer.IsServer();

    public const int port = 7777;

    public const string GAMEVERSION = "v0.2.0";

    public TransitionScreen transitionScreen = null;

    public override void _Ready()
    {
        //ResourceDebugger.ListAllFiles();

        Instance = this;

        AddChild(new Node { Name = "Players" });
        PlayerSpawner = new MultiplayerSpawner();

        PlayerSpawner.SpawnFunction = new Callable(this, nameof(SpawnPlayerCallback));

        AddChild(PlayerSpawner);

        connectionTimer = new Timer { Name = "ConnectionTimer", WaitTime = 14.5 };
        connectionTimer.Timeout += () => { connectionTimer.QueueFree(); };
        AddChild(connectionTimer);
        PlayerSpawner.SpawnPath = "../Players";
        PlayerSpawner.AddSpawnableScene("res://Scenes/Player.tscn");

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
    }


    private Node SpawnPlayerCallback(Variant args)
    {
        int id = (int)args;
        if (GetNodeOrNull($"Players/Player_{id}") != null)
        {
            GD.PrintErr($"[SpawnPlayerCallback] Player_{id} already exists. Skipping spawn.");
            return null;
        }

        GD.Print($"[Spawner] Spawning player for {id}");

        PlayerScene = (PackedScene)GD.Load("res://Scenes/Player.tscn");
        if (PlayerScene == null)
        {
            GD.PrintErr("[NetworkManager] PlayerScene failed to load!");
            return null;
        }

        Player player = PlayerScene.Instantiate<Player>();
        player.Name = $"Player_{id}";
        player.SetMultiplayerAuthority(id);
        player.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(5 * GD.Randf(), 0, 5 * GD.Randf()));
        player.AddToGroup("players");

        if (PlayerNames.TryGetValue(id, out var playerName))
        {
            player.SetDisplayName(playerName);
            GD.Print($"[NetworkManager] Set name on spawn: {playerName}");
        }

        return player;
    }

    public void StartServer()
    {
        var peer = new ENetMultiplayerPeer();
        peer.CreateServer(port, maxClients: 10);
        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"[NetworkManager] Server started on port {port}");
    }

    public async void JoinServer(string ip = "127.0.0.1")
    {
        transitionScreen = TransitionScreenScene.Instantiate() as TransitionScreen;
        transitionScreen.MustTriggerOut = true;
        AddChild(transitionScreen);

        Multiplayer.ConnectedToServer += OnClientConnectedToServer;
        Multiplayer.ConnectionFailed += OnClientConnectionFailed;

        var peer = new ENetMultiplayerPeer();
        var result = peer.CreateClient(ip, port);
        Multiplayer.MultiplayerPeer = peer;

        await ToSignal(GetTree().CreateTimer(1), "timeout");
        connectionTimer.Start();
        connectionTimer.Timeout += () =>
        {
            if (IsInstanceValid(transitionScreen)) transitionScreen.PlayAnim("out");
        };

    }
    // 104.184.113.183

    private void OnPeerConnected(long id)
    {
        if (!IsServer) return;

        GD.Print($"[NetworkManager] Peer connected: {id}");
    }


    private void OnPeerDisconnected(long id)
    {
        if (id == 1)
        {
            PlayerNames.Clear();
            PlayerColors.Clear();
            GoBackToMenu();
            return;
        }
        GD.Print($"[Client {Multiplayer.GetUniqueId()}] Peer disconnected: {id}");

        Player player = GetNodeOrNull<Player>($"Players/Player_{id}");
        if (player != null)
        {
            player.QueueFree();
            GD.Print($"[Client {Multiplayer.GetUniqueId()}] Removed Player_{id} from scene");
        }
        if (PlayerNames.TryGetValue((int)id, out var name))
        {
            SendSystemMessage($"{name} left the game.");
        }
        else
        {
            GD.Print($"[NetworkManager] Tried to disconnect unknown player ID: {id}");
        }

        PlayerNames.Remove((int)id);
        PlayerColors.Remove((int)id);
        InGamePlayers.Remove((int)id);
    }

    public async void DisconnectFromServer()
    {
        GD.Print("[Client] Disconnecting...");


        CanvasLayer transitionScreen = TransitionScreenScene.Instantiate() as CanvasLayer;
        AddChild(transitionScreen);

        await ToSignal(GetTree().CreateTimer(0.5), "timeout");

        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }

        Multiplayer.ConnectedToServer -= OnClientConnectedToServer;
        Multiplayer.ConnectionFailed -= OnClientConnectionFailed;

        PlayerNames.Clear();
        PlayerColors.Clear();

        GoBackToMenu();
    }

    public void GoBackToMenu()
    {
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);

        GetTree().ChangeSceneToFile("res://Scenes/Main Menu.tscn");

    }

    private async void OnClientConnectedToServer()
    {
        await ToSignal(GetTree().CreateTimer(0.2), "timeout");

        GetTree().ChangeSceneToPacked(GameScene);

        await WaitForSceneReady();

        transitionScreen.PlayAnim("out");

        RpcId(1, nameof(RegisterName), SettingsManager.CurrentSettings.Username, false);
        var s = SettingsManager.CurrentSettings;
        RpcId(1, nameof(RegisterColor), new Color(s.ColorR, s.ColorG, s.ColorB));

        connectionTimer.Stop();
        GD.Print("[Client] Connected to server, registering name.");
    }

    private async Task WaitForSceneReady()
    {
        while (GetTree().CurrentScene == null || GetTree().CurrentScene.GetNodeOrNull("ChatLayer") == null)
            await ToSignal(GetTree().CreateTimer(0.1), "timeout");
    }

    private void OnClientConnectionFailed()
    {
        StatusMessageManager.Instance.ShowMessage("Error: Failed to Connect to Server.", StatusMessageManager.MessageType.Error);
        transitionScreen.PlayAnim("out");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public async void RegisterName(string name, bool replacingName = false)
    {
        if (!IsServer) return;

        if (name == "_ping_")
        {
            GD.Print("[Server] Skipping spawn for ping client.");
            return;
        }

        int peerId = Multiplayer.GetRemoteSenderId();

        if (!replacingName && PlayerNames.ContainsKey(peerId))
        {
            GD.Print($"[Server] Duplicate RegisterName from {peerId}, skipping...");
            return;
        }

        GD.Print($"[Server] Registered new player {peerId} as {name}");

        PlayerNames[peerId] = name;
        InGamePlayers.Add(peerId);

        if (!replacingName && GetTree().CurrentScene.SceneFilePath == "res://Scenes/Game.tscn")
        {
            if (GetNodeOrNull($"Players/Player_{peerId}") == null)
            {
                PlayerSpawner.Spawn(peerId);
                GD.Print($"[Debug] Player node: {GetNodeOrNull($"Players/Player_{peerId}")}");
            }
        }

        await WaitForPlayerNode(peerId, 2.0f);

        Rpc(nameof(AnnounceName), peerId, name);
        AnnounceName(peerId, name);

        foreach (var kvp in PlayerNames)
        {
            if (kvp.Key == peerId) continue;
            RpcId(peerId, nameof(AnnounceName), kvp.Key, kvp.Value);
        }

        if (!replacingName)
        {
            Rpc(nameof(SendSystemMessage), $"{name} joined the game.");
            SendSystemMessage($"{name} joined the game.");
        }
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public async void RegisterColor(Color color)
    {
        if (!IsServer) return;

        int peerId = Multiplayer.GetRemoteSenderId();
        PlayerColors[peerId] = color;

        GD.Print($"[Server] Registered color for {peerId}: {color}");

        await WaitForPlayerNode(peerId, 2.0f);

        Rpc(nameof(AnnounceColor), peerId, color);
        AnnounceColor(peerId, color);

        foreach (var kvp in PlayerColors)
        {
            if (kvp.Key == peerId) continue;
            RpcId(peerId, nameof(AnnounceColor), kvp.Key, kvp.Value);
        }
    }



    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AnnounceColor(int peerId, Color color)
    {
        PlayerColors[peerId] = color;

        var player = GetNodeOrNull<Player>($"Players/Player_{peerId}");
        if (player != null)
        {
            player.SetPlayerColor(color);
            GD.Print($"[NetworkManager] Applied color for Player_{peerId}: {color}");
        }
        else
        {
            GD.PrintErr($"[NetworkManager] Player_{peerId} not found to assign color '{color}'");
        }
    }




    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AnnounceName(int peerId, string name)
    {
        PlayerNames[peerId] = name;

        var player = GetNodeOrNull<Player>($"Players/Player_{peerId}");
        if (player != null)
        {
            player.SetDisplayName(name);
            GD.Print($"[NetworkManager] Applied name for Player_{peerId}: {name}");
        }
        else
        {
            GD.PrintErr($"[NetworkManager] Player_{peerId} not found to assign name '{name}'");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SendSystemMessage(string msg)
    {
        ChatManager chat = GetNode<ChatManager>("/root/Game/ChatLayer");
        chat.DisplaySystemMessage($"[color=yellow]{msg}[/color]");
    }

    private async Task WaitForPlayerNode(int peerId, float timeoutSeconds = 2.0f)
    {
        var timer = 0f;
        while (GetNodeOrNull($"Players/Player_{peerId}") == null && timer < timeoutSeconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            timer += (float)GetProcessDeltaTime();
        }

        if (GetNodeOrNull($"Players/Player_{peerId}") == null)
        {
            GD.PrintErr($"[NetworkManager] Timeout: Player_{peerId} not found after {timeoutSeconds}s.");
        }
    }

    //public PackedScene LandParticles = GD.Load<PackedScene>("res://Scenes/Particles/hitting_ground_particles.tscn");

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void PlayerLanded(Vector3 landPos)
    {
        if (!IsServer) return;

        var playerCont = GetNode<Node>("Players");
        foreach (var kvp in PlayerNames)
        {
            Player player = GetNode<Player>($"Players/Player_{kvp.Key}");

            float dist = player.GlobalTransform.Origin.DistanceTo(landPos);
            if (dist <= 30f)
            {
                //RpcId(kvp.Key, nameof(SpawnLandingParticlesAt), landPos - new Vector3(0, 0.6f, 0));
            }
        }
    }


    //
    //
    //
    // Pinging Server Data
    //
    //
    //

    public static Action<bool> VersionCheckCallback;

    public async Task<(float? ping, bool versionMatch)> PingServerAsync(string ip, float timeoutSec = 1f)
    {
        var peer = new ENetMultiplayerPeer();
        peer.CreateClient(ip, port);
        Multiplayer.MultiplayerPeer = peer;

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var pingTcs = new TaskCompletionSource<float?>();
        var matchTcs = new TaskCompletionSource<bool>();

        void OnConnected()
        {
            stopwatch.Stop();
            pingTcs.TrySetResult(stopwatch.ElapsedMilliseconds);
            RpcId(1, nameof(SendClientVersion), NetworkManager.GAMEVERSION);
            RpcId(1, nameof(RegisterName), "_ping_", false);

        }

        void OnFailed()
        {
            pingTcs.TrySetResult(null);
            matchTcs.TrySetResult(false);
        }

        Multiplayer.ConnectedToServer += OnConnected;
        Multiplayer.ConnectionFailed += OnFailed;

        VersionCheckCallback = (match) =>
        {
            matchTcs.TrySetResult(match);
            peer.Close(); // Close after check
        };

        var delay = Task.Delay((int)(timeoutSec * 1000));
        var finished = await Task.WhenAny(matchTcs.Task, delay);

        Multiplayer.ConnectedToServer -= OnConnected;
        Multiplayer.ConnectionFailed -= OnFailed;
        VersionCheckCallback = null;

        return finished == delay
            ? ((float?)null, false)
            : (await pingTcs.Task, await matchTcs.Task);
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SendClientVersion(string clientVersion)
    {
        bool matches = clientVersion == GAMEVERSION;
        int peerId = Multiplayer.GetRemoteSenderId();

        RpcId(peerId, nameof(ReceiveVersionMatch), matches);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ReceiveVersionMatch(bool matches)
    {
        VersionCheckCallback?.Invoke(matches);
    }

    public Player GetLocalPlayer()
    {
        return GetNodeOrNull<Player>($"Players/Player_{Multiplayer.GetUniqueId()}");
    }

    public Player GetPlayerFromID(int peerId)
    {
        return GetNodeOrNull<Player>($"Players/Player_{peerId}");
    }
}
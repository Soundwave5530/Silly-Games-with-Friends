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
    public bool IsDedicatedServer { get; private set; }
    public bool IsPlayerHost => IsServer && !IsDedicatedServer;

    public const int PORT = 7777;
    public const int MAXPLAYERS = 15;
    public const string GAMEVERSION = "v0.2.1 dev3";

    public TransitionScreen transitionScreen = null;

    public override void _Ready()
    {
        //ResourceDebugger.ListAllFiles();

        Instance = this;
        
        // Clear any stale network state
        PlayerNames.Clear();
        PlayerColors.Clear();
        InGamePlayers.Clear();

        // Reset multiplayer state
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }

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

        // Set initial player properties from cached data
        if (PlayerNames.TryGetValue(id, out var playerName))
        {
            player.SetDisplayName(playerName);
            GD.Print($"[NetworkManager] Set name on spawn: {playerName}");
        }
        
        // Apply color if we already have it
        if (PlayerColors.TryGetValue(id, out var playerColor))
        {
            player.SetPlayerColor(playerColor);
            GD.Print($"[NetworkManager] Set color on spawn: {playerColor}");
        }

        // Schedule the ready check for the next frame to ensure the node is in the scene tree
        GetTree().CreateTimer(0).Timeout += () => 
        {
            if (player.IsInsideTree())
            {
                GD.Print($"[NetworkManager] Player_{id} is ready in scene tree");
                // Re-apply color and name just to be safe
                if (PlayerColors.TryGetValue(id, out var color))
                    player.SetPlayerColor(color);
                if (PlayerNames.TryGetValue(id, out var name))
                    player.SetDisplayName(name);
            }
        };

        return player;
    }

    public void StartServer(bool isDedicated = false)
    {
        var peer = new ENetMultiplayerPeer();
        peer.CreateServer(PORT, maxClients: MAXPLAYERS);
        Multiplayer.MultiplayerPeer = peer;
        IsDedicatedServer = isDedicated;
        
        GD.Print($"[NetworkManager] {(isDedicated ? "Dedicated" : "Player-hosted")} server started on port {PORT}");

        // If this is a player-host, register them immediately
        if (!isDedicated)
        {
            // Register server player (ID 1) as host
            PlayerNames[1] = SettingsManager.CurrentSettings.Username;
            InGamePlayers.Add(1);

            // We'll register the color and spawn the player after changing to the game scene
            GetTree().CreateTimer(0.1).Timeout += () =>
            {
                if (GetTree().CurrentScene.SceneFilePath == "res://Scenes/Game.tscn")
                {
                    var s = SettingsManager.CurrentSettings;
                    PlayerColors[1] = new Color(s.ColorR, s.ColorG, s.ColorB);
                    PlayerSpawner.Spawn(1);
                }
            };
        }
    }

    public async void JoinServer(string ip = "127.0.0.1")
    {
        transitionScreen = TransitionScreenScene.Instantiate() as TransitionScreen;
        transitionScreen.MustTriggerOut = true;
        AddChild(transitionScreen);

        Multiplayer.ConnectedToServer += OnClientConnectedToServer;
        Multiplayer.ConnectionFailed += OnClientConnectionFailed;

        var peer = new ENetMultiplayerPeer();
        var result = peer.CreateClient(ip, PORT);
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
        
        // If we're a player host, send our info to the new client
        if (IsPlayerHost)
        {
            // Send host's info to new client
            RpcId((int)id, nameof(AnnounceName), 1, PlayerNames[1]);
            RpcId((int)id, nameof(AnnounceColor), 1, PlayerColors[1]);
        }
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

    public void ShutdownServer()
    {
        GD.Print("[Server] Shutting down...");

        // Notify all clients
        if (PlayerNames.Count > 1) // If there are other players
        {
            Rpc(nameof(SendSystemMessage), "Server is shutting down...");
        }

        // Create transition screen
        CanvasLayer transitionScreen = TransitionScreenScene.Instantiate() as CanvasLayer;
        AddChild(transitionScreen);

        GetTree().CreateTimer(0.5).Timeout += () =>
        {
            // Close the server
            if (Multiplayer.MultiplayerPeer != null)
            {
                Multiplayer.MultiplayerPeer.Close();
                GetNode<Node>("Players").QueueFreeChildren();

                Multiplayer.MultiplayerPeer = null;
            }

            // Clear all game state
            PlayerNames.Clear();
            PlayerColors.Clear();
            InGamePlayers.Clear();
            IsDedicatedServer = false;

            // Return to menu
            GoBackToMenu();
        };
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
        
        // Validate peer ID
        if (peerId <= 0)
        {
            GD.Print($"[NetworkManager] Invalid peer ID: {peerId}, ignoring color registration");
            return;
        }

        PlayerColors[peerId] = color;
        GD.Print($"[Server] Registered color for {peerId}: {color}");

        // Only wait for and announce to players if we're in game
        if (GetTree().CurrentScene?.SceneFilePath == "res://Scenes/Game.tscn")
        {
            await WaitForPlayerNode(peerId, 3.5f);

            Rpc(nameof(AnnounceColor), peerId, color);
            AnnounceColor(peerId, color);

            foreach (var kvp in PlayerColors)
            {
                if (kvp.Key == peerId) continue;
                RpcId(peerId, nameof(AnnounceColor), kvp.Key, kvp.Value);
            }
        }
    }



    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AnnounceColor(int peerId, Color color)
    {
        // Validate peer ID
        if (peerId <= 0)
        {
            GD.PrintErr($"[NetworkManager] Invalid peer ID in AnnounceColor: {peerId}, ignoring");
            return;
        }

        // Only process color announcements if we're in the game scene
        if (GetTree().CurrentScene?.SceneFilePath != "res://Scenes/Game.tscn")
        {
            GD.Print($"[NetworkManager] Skipping color announcement for Player_{peerId} - not in game scene");
            return;
        }

        PlayerColors[peerId] = color;

        var player = GetNodeOrNull<Player>($"Players/Player_{peerId}");
        if (player != null)
        {
            player.SetPlayerColor(color);
            GD.Print($"[NetworkManager] Applied color for Player_{peerId}: {color}");
        }
        else if (IsServer || Multiplayer.GetUniqueId() > 1)
        {
            // Only log the error if we're the server or a connected client
            GD.Print($"[NetworkManager] Player_{peerId} not found to assign color '{color}' - will be applied when spawned");
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

    private async Task WaitForPlayerNode(int peerId, float timeoutSeconds = 3.5f)
    {
        var timer = 0f;
        Player player = null;
        
        while (timer < timeoutSeconds)
        {
            player = GetNodeOrNull<Player>($"Players/Player_{peerId}");
            if (player != null && player.IsInsideTree())
            {
                GD.Print($"[NetworkManager] Found Player_{peerId} after {timer:F1}s");
                return;
            }
            
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            timer += (float)GetProcessDeltaTime();
        }

        // If we reached here, we timed out
        if (player == null)
        {
            GD.PrintErr($"[NetworkManager] Timeout: Player_{peerId} not found after {timeoutSeconds}s");
            // Try spawning the player if they're not found
            if (IsServer && GetTree().CurrentScene.SceneFilePath == "res://Scenes/Game.tscn")
            {
                GD.Print($"[NetworkManager] Attempting to respawn Player_{peerId}");
                PlayerSpawner.Spawn(peerId);
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
        peer.CreateClient(ip, PORT);
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
        if (Multiplayer.MultiplayerPeer == null) return null;
        return GetNodeOrNull<Player>($"Players/Player_{Multiplayer.GetUniqueId()}");
    }

    public Player GetPlayerFromID(int peerId)
    {
        return GetNodeOrNull<Player>($"Players/Player_{peerId}");
    }
}
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node3D
{
    public static GameManager Instance { get; private set; }

    [Export] public PackedScene TagLevelScene;
    [Export] public PackedScene LobbyScene;
    [Export] public PackedScene CountdownUIScene; // Add this export
    public PackedScene VotingUIScene;

    // Used to track if color changes are coming from the game system

    public bool IsColorChangeFromGame { get; private set; }
    
    [Signal] public delegate void GameSceneLoadedEventHandler();

    public enum GameState
    {
        Lobby,
        Voting,
        Starting,
        Countdown,  // Add new countdown state
        Playing,
        GameOver
    }

    public enum GameType
    {
        None,
        Tag,
        HideAndSeek,
        MurderMystery,
        Climbing,
        Race
    }

    [Signal] public delegate void GameStateChangedEventHandler(GameState newState);
    [Signal] public delegate void GameStartedEventHandler(GameType gameType);
    [Signal] public delegate void GameEndedEventHandler();
    [Signal] public delegate void VoteSubmittedEventHandler(int playerId, GameType gameType, string playerName, Color playerColor);

    private GameState currentState = GameState.Lobby;
    private GameType currentGameType = GameType.None;
    
    // Voting system
    private Dictionary<int, GameType> playerVotes = new();
    private Timer votingTimer;
    private const float VOTING_TIME = 30f;
    
    // Game state
    private int currentTagger = -1;
    private HashSet<int> alivePlayers = new();
    private Timer gameTimer;
    private float gameTimeRemaining = 0f;
    private const float TAG_GAME_DURATION = 180f; // 3 minutes
    private const float HIDE_AND_SEEK_DURATION = 300f; // 5 minutes
    private const float MURDER_MYSTERY_DURATION = 420f; // 7 minutes
    private const float TAG_DISTANCE = 2f;
    
    // Tagging delays and cooldowns
    private const float TAG_COOLDOWN = 0.5f; // 0.5 second between tags to prevent accidental double-tags
    private const float NEW_TAGGER_STUN_DURATION = 3f; // 3 seconds stun for newly tagged player
    private double lastTagTime = 0.0;
    private Dictionary<int, float> playerTagProtection = new(); // Players who are stunned/protected
    private bool debugTags = true; // Set to true to enable tag debugging
    
    // Murder Mystery roles
    private int currentMurderer = -1;
    private int currentSheriff = -1;
    private HashSet<int> innocentPlayers = new();
    
    // Hide and Seek roles
    private HashSet<int> seekers = new();
    private HashSet<int> hiders = new();
    
    // Color override system
    private Dictionary<int, Color> originalPlayerColors = new();
    private bool colorsOverridden = false;

    private Node WorldNode;

    public override void _Ready()
    {
        Instance = this;

        // Load CountdownUI scene if not set in editor
        if (CountdownUIScene == null)
        {
            CountdownUIScene = GD.Load<PackedScene>("res://Scenes/CountdownUI.tscn");
        }

        EmitSignal(SignalName.GameSceneLoaded);

        var hostCamera = GetNode<Camera3D>("HostCamera");
        if (NetworkManager.Instance?.IsDedicatedServer == true)
        {
            MultiplayerSpawner worldSpawner = GetNode<MultiplayerSpawner>("WorldSpawner");
            hostCamera.Current = true;
        }
        else
        {
            hostCamera.Current = false;
        }

        votingTimer = new Timer { WaitTime = VOTING_TIME, OneShot = true };
        AddChild(votingTimer);
        votingTimer.Timeout += OnVotingFinished;

        gameTimer = new Timer { WaitTime = TAG_GAME_DURATION, OneShot = false };
        AddChild(gameTimer);
        gameTimer.Timeout += OnGameTimeUp;

        if (NetworkManager.Instance != null)
        {
            Multiplayer.PeerConnected += OnPlayerConnected;
            Multiplayer.PeerDisconnected += OnPlayerDisconnected;
        }

        WorldNode = GetNode<Node>("World");

        if (currentState == GameState.Lobby)
        {
            LoadLobby();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (currentState == GameState.Playing && currentGameType == GameType.Tag)
        {
            CheckTagDistance();
        }

        if (currentState == GameState.Playing && gameTimeRemaining > 0)
        {
            gameTimeRemaining -= (float)delta;

            if ((int)gameTimeRemaining % 5 == 0)
            {
                if (IsMultiplayerActive())
                {
                    Rpc(nameof(SyncGameTimeRemaining), gameTimeRemaining);
                }
            }

            if (gameTimeRemaining <= 0)
            {
                gameTimeRemaining = 0;
                OnGameTimeUp();
            }
        }
        
        // Update tag protection timers
        UpdateTagProtection((float)delta);
        
        // Only try to connect game signals if multiplayer is active
        if (IsMultiplayerActive())
        {
            PlayerHUD.Instance?.ConnectGameSignals();
        }
    }

    // Helper method to safely check if multiplayer is active
    private bool IsMultiplayerActive()
    {
        try
        {
            return Multiplayer?.MultiplayerPeer != null && 
                   Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
        }
        catch
        {
            return false;
        }
    }

    // Helper method to safely get unique ID
    private int GetSafeUniqueId()
    {
        try
        {
            if (IsMultiplayerActive())
            {
                return Multiplayer.GetUniqueId();
            }
            else
            {
                return 1; // Default for single player
            }
        }
        catch
        {
            return 1; // Fallback
        }
    }

    // Helper method to safely check if server
    private bool IsSafeServer()
    {
        try
        {
            if (IsMultiplayerActive())
            {
                return Multiplayer.IsServer();
            }
            else
            {
                return true; // In single player, we're always the "server"
            }
        }
        catch
        {
            return true; // Fallback
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void CleanupVotingUI()
    {
        // Find and remove any voting UI instances
        foreach (var node in GetChildren())
        {
            if (node is VotingUI votingUI)
            {
                votingUI.QueueFree();
            }
        }
    }

    #region New Player Syncing
    
    private void OnPlayerConnected(long peerId)
    {
        if (!IsSafeServer()) return;
        
        GD.Print($"[GameManager] Player {peerId} connected, syncing game state...");
        
        // Wait a moment for the player to be fully connected
        GetTree().CreateTimer(1f).Timeout += () => {
            // Check if player is still connected before syncing
            if (IsPlayerStillConnected((int)peerId))
            {
                SyncNewPlayer((int)peerId);
            }
            else
            {
                GD.Print($"[GameManager] Player {peerId} disconnected before sync could complete");
            }
        };
    }
    
    private void SyncNewPlayer(int peerId)
    {
        if (!IsSafeServer()) return;
        
        if (!IsPlayerStillConnected(peerId))
        {
            GD.Print($"[GameManager] Cannot sync player {peerId} - no longer connected");
            return;
        }
        
        try
        {
            if (IsMultiplayerActive())
            {
                RpcId(peerId, nameof(ReceiveGameStateSync), (int)currentState, (int)currentGameType, 
                    gameTimeRemaining, currentTagger, currentMurderer, currentSheriff);
                
                switch (currentGameType)
                {
                    case GameType.Tag:
                        break;
                    case GameType.HideAndSeek:
                        RpcId(peerId, nameof(SyncHideAndSeekRoles), seekers.ToArray(), hiders.ToArray());
                        break;
                    case GameType.MurderMystery:
                        RpcId(peerId, nameof(SyncMurderMysteryRoles), currentMurderer, currentSheriff, innocentPlayers.ToArray());
                        break;
                }
            }
            
            GD.Print($"[GameManager] Successfully synced game state with player {peerId}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[GameManager] Error syncing player {peerId}: {e.Message}");
        }
    }

    private bool IsPlayerStillConnected(int peerId)
    {
        try
        {
            // Check if the peer still exists in the multiplayer session
            if (!IsMultiplayerActive()) return false;
            
            // Get list of connected peers
            var connectedPeers = Multiplayer.GetPeers();
            
            // Check if our peer is in the list
            bool isConnected = connectedPeers.Contains(peerId);
            
            // Also check if NetworkManager knows about this player
            bool isInNetworkManager = NetworkManager.Instance?.PlayerNames.ContainsKey(peerId) == true;
            
            return isConnected && isInNetworkManager;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[GameManager] Error checking if player {peerId} is connected: {e.Message}");
            return false;
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ReceiveGameStateSync(int stateInt, int gameTypeInt, float timeRemaining, 
                                   int tagger, int murderer, int sheriff)
    {
        currentState = (GameState)stateInt;
        currentGameType = (GameType)gameTypeInt;
        gameTimeRemaining = timeRemaining;
        currentTagger = tagger;
        currentMurderer = murderer;
        currentSheriff = sheriff;
        
        // Update HUD if it exists
        if (PlayerHUD.Instance != null)
        {
            string role = GetPlayerRole(GetSafeUniqueId());
            PlayerHUD.Instance.SyncGameState(currentState == GameState.Playing, role, timeRemaining);
        }
        
        GD.Print($"[GameManager] Received game state sync: {currentState}, {currentGameType}, {timeRemaining}s");
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncHideAndSeekRoles(int[] seekerIds, int[] hiderIds)
    {
        seekers = new HashSet<int>(seekerIds);
        hiders = new HashSet<int>(hiderIds);
        
        if (PlayerHUD.Instance != null)
        {
            string role = GetPlayerRole(GetSafeUniqueId());
            PlayerHUD.Instance.UpdatePlayerRole();
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncMurderMysteryRoles(int murderer, int sheriff, int[] innocents)
    {
        currentMurderer = murderer;
        currentSheriff = sheriff;
        innocentPlayers = new HashSet<int>(innocents);
        
        if (PlayerHUD.Instance != null)
        {
            string role = GetPlayerRole(GetSafeUniqueId());
            PlayerHUD.Instance.UpdatePlayerRole();
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncGameTimeRemaining(float timeRemaining)
    {
        gameTimeRemaining = timeRemaining;
        
        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.UpdateGameTimeRemaining(timeRemaining);
        }
    }
    
    private string GetPlayerRole(int playerId)
    {
        switch (currentGameType)
        {
            case GameType.Tag:
                return IsPlayerTagger(playerId) ? "IT" : "Runner";
            case GameType.HideAndSeek:
                return IsPlayerSeeker(playerId) ? "Seeker" : "Hider";
            case GameType.MurderMystery:
                if (IsPlayerMurderer(playerId)) return "Murderer";
                if (IsPlayerSheriff(playerId)) return "Sheriff";
                return "Innocent";
            default:
                return "";
        }
    }
    
    #endregion

    #region Original Methods

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SetAttacker(string name)
    {
        var player = GetNode<Node3D>("/root/Game/" + name);
        if (player == null) return;

        // Turn on attack mode
        GD.Print("Attacker selected: " + name);
    }
    
    #endregion

    #region Voting System
    
    public async void StartVoting()
    {
        if (!IsSafeServer() || currentState != GameState.Lobby) return;

        await ToSignal(GetTree().CreateTimer(0.1), "timeout");
        
        // Close settings menu if open and force mouse mode
        if (NewPauseMenu.IsOpen)
        {
            NewPauseMenu.Instance.CloseMenu();
        }
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible, true); // Force visible mode

        UpdateGameState(GameState.Voting, currentGameType);
        if (IsMultiplayerActive())
        {
            Rpc(nameof(UpdateClientState), (int)GameState.Voting, (int)currentGameType, currentTagger);
        }
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        playerVotes.Clear();
        votingTimer.Start();
        
        if (IsMultiplayerActive())
        {
            Rpc(nameof(ShowVotingUI), VOTING_TIME);
        }
        ShowVotingUI(VOTING_TIME);
        
        NetworkManager.Instance?.SendSystemMessage("Game voting started! Vote for your favorite game!");
    }

    public void EndVotingEarly()
    {
        votingTimer.Stop();
        OnVotingFinished();
    }
    
    // Called by VotingUI when player clicks to vote
    public void RegisterVote(GameManager.GameType gameType)
    {
        try
        {
            if (currentState != GameState.Voting)
            {
                GD.PrintErr("[GameManager] Cannot vote: Not in voting state");
                return;
            }

            if (NetworkManager.Instance == null)
            {
                GD.PrintErr("[GameManager] Cannot vote: NetworkManager not available");
                return;
            }

            if (!IsMultiplayerActive())
            {
                GD.PrintErr("[GameManager] Cannot vote: No multiplayer peer");
                return;
            }

            if (!IsSafeServer())
            {
                // Get player ID and validate it
                int playerId = GetSafeUniqueId();
                if (playerId <= 0)
                {
                    GD.PrintErr("[GameManager] Cannot vote: Invalid player ID");
                    return;
                }

                GD.Print($"[GameManager] Client {playerId} sending vote to server: {gameType}");
                RpcId(1, nameof(ProcessVote), playerId, (int)gameType);
            }
            else
            {
                // Server processes vote directly
                GD.Print($"[GameManager] Server processing direct vote for game type {gameType}");
                ProcessVote(1, (int)gameType);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[GameManager] Error registering vote: {e.Message}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void ProcessVote(int voterId, int gameTypeInt)
    {
        try
        {
            // Ensure only the server processes votes
            if (!IsSafeServer())
            {
                GD.PrintErr("[GameManager] Non-server tried to process vote");
                return;
            }

            GD.Print($"[GameManager] Server processing vote from {voterId} for game type {gameTypeInt}");
            
            if (currentState != GameState.Voting)
            {
                GD.PrintErr("[GameManager] Cannot process vote: Not in voting state");
                return;
            }

            GameType gameType = (GameType)gameTypeInt;
            
            // Get player information
            var player = NetworkManager.Instance.GetPlayerFromID(voterId);
            if (player == null)
            {
                GD.PrintErr($"[GameManager] Cannot find player with ID {voterId}");
                return;
            }

            string playerName = NetworkManager.Instance.PlayerNames.TryGetValue(voterId, out var name) ? name : "Unknown";
            Color playerColor = player.playerColor;
            string expressionId = player.SyncExpressionId;
            string hatId = player.SyncHatId;

            // Update server's vote tracking
            playerVotes[voterId] = gameType;
            
            // Broadcast to all clients including the original sender
            if (IsMultiplayerActive())
            {
                Rpc(nameof(BroadcastVote), voterId, gameTypeInt, playerName, 
                    new Color(playerColor.R, playerColor.G, playerColor.B, playerColor.A).ToHtml(), 
                    expressionId, hatId);
            }
            
            // Update server's own UI
            EmitSignal(SignalName.VoteSubmitted, voterId, gameTypeInt, playerName, playerColor);
            
            // Announce the vote
            // NetworkManager.Instance.SendSystemMessage($"{playerName} voted for {gameType}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[GameManager] Error processing vote: {e.Message}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void BroadcastVote(int voterId, int gameTypeInt, string playerName, string colorHtml, string expressionId, string hatId)
    {
        try
        {
            if (NetworkManager.Instance == null)
            {
                GD.PrintErr("[GameManager] Cannot broadcast vote: NetworkManager not available");
                return;
            }

            if (currentState != GameState.Voting)
            {
                GD.PrintErr("[GameManager] Cannot broadcast vote: Not in voting state");
                return;
            }

            GD.Print($"[GameManager] Received vote broadcast for player {playerName}");
            
            GameType gameType = (GameType)gameTypeInt;
            Color playerColor = new(colorHtml);
            
            // Update local vote tracking
            playerVotes[voterId] = gameType;
            
            // Update UI through signal
            EmitSignal(SignalName.VoteSubmitted, voterId, gameTypeInt, playerName, playerColor);

            // Verify the vote was recorded
            if (!playerVotes.ContainsKey(voterId) || playerVotes[voterId] != gameType)
            {
                GD.PrintErr($"[GameManager] Vote for player {voterId} was not properly recorded");
                return;
            }

            GD.Print($"[GameManager] Successfully processed vote from {playerName} for {gameType}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[GameManager] Error processing vote broadcast: {e.Message}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AnnounceVote(string playerName, string gameType)
    {
        if (NetworkManager.Instance == null) return;
        NetworkManager.Instance.SendSystemMessage($"{playerName} voted for {gameType}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ShowVotingUI(float timeLeft)
    {
        VotingUIScene = GD.Load<PackedScene>("res://Scenes/VotingUI.tscn");

        if (VotingUIScene == null)
        {
            GD.PrintErr("[GameManager] VotingUIScene not set!");
            return;
        }

        var votingUI = VotingUIScene.Instantiate<VotingUI>();
        AddChild(votingUI);

        votingUI.StartVoting(timeLeft);
    }

    private void OnVotingFinished()
    {
        if (!IsSafeServer()) return;

        // Count votes
        var voteCounts = new Dictionary<GameType, int>();
        foreach (var vote in playerVotes.Values)
        {
            voteCounts[vote] = voteCounts.GetValueOrDefault(vote, 0) + 1;
        }

        // Find winner (or random if tie)
        GameType winningGame = GameType.Tag; // Default fallback
        if (voteCounts.Count > 0)
        {
            int maxVotes = voteCounts.Values.Max();
            var topGames = voteCounts.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();
            winningGame = topGames[Math.Abs((int)GD.Randi()) % topGames.Count];
        }

        // Clean up voting UI on all clients
        if (IsMultiplayerActive())
        {
            Rpc(nameof(CleanupVotingUI));
        }
        CleanupVotingUI();

        NetworkManager.Instance?.SendSystemMessage($"{winningGame} wins the vote! Starting game...");
        StartGame(winningGame);
        
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);
    }
    
    #endregion
    
    #region Game Management
    
    public void StartGame(GameType gameType)
    {
        if (!IsSafeServer()) return;
        
        currentGameType = gameType;
        UpdateGameState(GameState.Starting, gameType);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        // Store original colors before overriding
        StoreOriginalColors();
        
        // Reset tag protection and cooldowns
        playerTagProtection.Clear();
        lastTagTime = 0f;
        
        // Set game duration based on type
        switch (gameType)
        {
            case GameType.Tag:
                gameTimeRemaining = TAG_GAME_DURATION;
                gameTimer.WaitTime = TAG_GAME_DURATION;
                StartTagGame();
                break;
            case GameType.HideAndSeek:
                gameTimeRemaining = HIDE_AND_SEEK_DURATION;
                gameTimer.WaitTime = HIDE_AND_SEEK_DURATION;
                StartHideAndSeekGame();
                break;
            case GameType.MurderMystery:
                gameTimeRemaining = MURDER_MYSTERY_DURATION;
                gameTimer.WaitTime = MURDER_MYSTERY_DURATION;
                StartMurderMysteryGame();
                break;
            // Add other game types here
        }
    }
    
    private void StartTagGame()
    {
        // Load tag level
        if (IsMultiplayerActive())
        {
            Rpc(nameof(LoadGameLevel), (int)GameType.Tag);
        }
        LoadGameLevel((int)GameType.Tag);
        NetworkManager.Instance?.GetLocalPlayer()?.SetGlobalPosition(new Vector3(2 * GD.Randf(), 1, 2 * GD.Randf()));
        
        // Wait for level to load, then start countdown
        GetTree().CreateTimer(2f).Timeout += () =>
        {
            if (IsSafeServer())
            {
                StartGameCountdown();
            }
        };
    }
    
    private void StartHideAndSeekGame()
    {
        // Load appropriate level for hide and seek
        if (IsMultiplayerActive())
        {
            Rpc(nameof(LoadGameLevel), (int)GameType.HideAndSeek);
        }
        LoadGameLevel((int)GameType.HideAndSeek);
        
        // Assign roles
        var playerIds = NetworkManager.Instance.PlayerNames.Keys.ToList();
        int seekerCount = Math.Max(1, playerIds.Count / 4); // 1 seeker per 4 players, minimum 1
        
        seekers.Clear();
        hiders.Clear();
        
        // Randomly select seekers
        for (int i = 0; i < seekerCount && i < playerIds.Count; i++)
        {
            int randomIndex = Math.Abs((int)GD.Randi()) % playerIds.Count;
            int seekerId = playerIds[randomIndex];
            seekers.Add(seekerId);
            playerIds.RemoveAt(randomIndex);
        }
        
        // Remaining players are hiders
        foreach (int playerId in playerIds)
        {
            hiders.Add(playerId);
        }
        
        GetTree().CreateTimer(2f).Timeout += () =>
        {
            if (IsSafeServer())
            {
                StartGameCountdown();
            }
        };
    }
    
    private void StartMurderMysteryGame()
    {
        // Load appropriate level for murder mystery
        if (IsMultiplayerActive())
        {
            Rpc(nameof(LoadGameLevel), (int)GameType.MurderMystery);
        }
        LoadGameLevel((int)GameType.MurderMystery);
        
        // Assign roles
        var playerIds = NetworkManager.Instance.PlayerNames.Keys.ToList();
        if (playerIds.Count < 3)
        {
            NetworkManager.Instance?.SendSystemMessage("Need at least 3 players for Murder Mystery!");
            ReturnToLobby();
            return;
        }
        
        // Randomly assign murderer and sheriff
        currentMurderer = playerIds[Math.Abs((int)GD.Randi()) % playerIds.Count];
        playerIds.Remove(currentMurderer);
        
        currentSheriff = playerIds[Math.Abs((int)GD.Randi()) % playerIds.Count];
        playerIds.Remove(currentSheriff);
        
        // Remaining players are innocent
        innocentPlayers = new HashSet<int>(playerIds);
        
        GetTree().CreateTimer(2f).Timeout += () =>
        {
            if (IsSafeServer())
            {
                StartGameCountdown();
            }
        };
    }

    // NEW: Start countdown for all players
    private void StartGameCountdown()
    {
        if (!IsSafeServer()) return;

        // Change to countdown state
        UpdateGameState(GameState.Countdown, currentGameType);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);

        // Show countdown on all clients
        if (IsMultiplayerActive())
        {
            Rpc(nameof(ShowCountdownUI));
        }
        ShowCountdownUI();

        GD.Print("[GameManager] Starting game countdown...");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ShowCountdownUI()
    {
        if (CountdownUIScene == null)
        {
            GD.PrintErr("[GameManager] CountdownUIScene not set!");
            return;
        }

        var countdownUI = CountdownUIScene.Instantiate<CountdownUI>();
        GetTree().Root.AddChild(countdownUI); // Add to root so it appears over everything
        
        // Start countdown with callback
        countdownUI.StartCountdown(() => OnCountdownComplete());
        
        // Also connect to the signal as backup
        countdownUI.CountdownFinished += OnCountdownComplete;
        
        GD.Print("[GameManager] Countdown UI shown");
    }

    private void OnCountdownComplete()
    {
        GD.Print("[GameManager] Countdown complete, starting gameplay!");
        
        if (IsSafeServer())
        {
            // Server initiates the actual game start
            switch (currentGameType)
            {
                case GameType.Tag:
                    SelectRandomTagger();
                    StartTagGameplay();
                    break;
                case GameType.HideAndSeek:
                    StartHideAndSeekGameplay();
                    break;
                case GameType.MurderMystery:
                    StartMurderMysteryGameplay();
                    break;
                // Add other game types here
            }
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void LoadGameLevel(int gameTypeInt)
    {
        GameType gameType = (GameType)gameTypeInt;
        
        switch (gameType)
        {
            case GameType.Tag:
                WorldNode.QueueFreeChildren();
                GD.Print("[GameManager] Loading Tag level...");
                WorldNode.AddChild(TagLevelScene.Instantiate());
                NetworkManager.Instance?.GetLocalPlayer()?.SetGlobalPosition(new Vector3(2 * GD.Randf(), 1, 2 * GD.Randf()));
                break;
            case GameType.MurderMystery:
                WorldNode.QueueFreeChildren();
                GD.Print("[GameManager] Loading Murder Mystery level...");
                // Use the same level for now, but you can create a specific MurderMystery level
                WorldNode.AddChild(TagLevelScene.Instantiate());
                NetworkManager.Instance?.GetLocalPlayer()?.SetGlobalPosition(new Vector3(2 * GD.Randf(), 1, 2 * GD.Randf()));
                break;
            // Add other levels here
        }
    }

    private void StartTagGameplay()
    {
        gameTimer.Start();
        NetworkManager.Instance?.SendSystemMessage($"Tag game started! {(int)gameTimeRemaining} seconds to play!");
        // Sync state with all clients
        UpdateGameState(GameState.Playing, currentGameType, currentTagger);
        if (IsMultiplayerActive())
        {
            Rpc(nameof(SyncGameTimeRemaining), gameTimeRemaining);
        }
    }
    
    // Server-side method to update and sync game state
    private void UpdateGameState(GameState newState, GameType gameType = GameType.None, int tagger = -1)
    {
        // Update server state
        currentState = newState;
        if (gameType != GameType.None) currentGameType = gameType;
        if (tagger != -1) currentTagger = tagger;
        
        // Signal the state change locally
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        if (IsMultiplayerActive())
        {
            // Tell all clients about the new state
            Rpc(nameof(UpdateClientState), (int)currentState, (int)currentGameType, currentTagger);
        }
        else
        {
            // Update local state for single player
            UpdateClientState((int)currentState, (int)currentGameType, currentTagger);
        }

        // Update roles
        if (IsMultiplayerActive())
        {
            Rpc(nameof(UpdateAllPlayerRoles));
        }
        UpdateAllPlayerRoles();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void UpdateClientState(int state, int gameType, int tagger)
    {
        currentState = (GameState)state;
        currentGameType = (GameType)gameType;
        currentTagger = tagger;

        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.UpdatePlayerRole();
            if (currentState == GameState.Playing)
            {
                string role = GetPlayerRole(GetSafeUniqueId());
                PlayerHUD.Instance.SyncGameState(true, role, gameTimeRemaining);
            }
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncGameState(int state, int gameType, int tagger)
    {
        currentState = (GameState)state;
        currentGameType = (GameType)gameType;
        currentTagger = tagger;

        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.UpdatePlayerRole();
            if (currentState == GameState.Playing)
            {
                string role = GetPlayerRole(GetSafeUniqueId());
                PlayerHUD.Instance.SyncGameState(true, role, gameTimeRemaining);
            }
        }
    }
    
    private void StartHideAndSeekGameplay()
    {
        UpdateGameState(GameState.Playing);
        EmitSignal(SignalName.GameStarted, (int)currentGameType);
        
        // Apply color overrides for seekers/hiders
        OverrideColorsHideAndSeek();
        
        // Announce roles
        string seekerNames = string.Join(", ", seekers.Select(id => 
            NetworkManager.Instance.PlayerNames.TryGetValue(id, out var name) ? name : "Unknown"));
        
        NetworkManager.Instance?.SendSystemMessage($"Hide and Seek started! Seekers: {seekerNames}");
        
        gameTimer.Start();
        
        // Sync time with all clients
        if (IsMultiplayerActive())
        {
            Rpc(nameof(SyncGameTimeRemaining), gameTimeRemaining);
        }
    }
    
    private void StartMurderMysteryGameplay()
    {
        UpdateGameState(GameState.Playing);
        EmitSignal(SignalName.GameStarted, (int)currentGameType);
        
        // Apply color overrides for murder mystery
        OverrideColorsMurderMystery();
        
        string murdererName = NetworkManager.Instance.PlayerNames.TryGetValue(currentMurderer, out var mName) ? mName : "Unknown";
        string sheriffName = NetworkManager.Instance.PlayerNames.TryGetValue(currentSheriff, out var sName) ? sName : "Unknown";
        
        NetworkManager.Instance?.SendSystemMessage($"Murder Mystery started! Find the murderer before time runs out!");
        
        gameTimer.Start();
        
        // Sync time with all clients
        if (IsMultiplayerActive())
        {
            Rpc(nameof(SyncGameTimeRemaining), gameTimeRemaining);
        }
    }
    
    #endregion

    #region Tag Game Logic

    // Replace the CheckTagDistance method and related tag handling methods with these fixed versions:

    private void CheckTagDistance()
    {
        // Only the server should perform tag checks
        if (!IsSafeServer() || currentTagger == -1)
        {
            if (debugTags) GD.Print($"[TagDebug] Tag check skipped - Not server or no tagger. Server: {IsSafeServer()}, Tagger: {currentTagger}");
            return;
        }

        // Check if we're in cooldown period
        double currentTime = Time.GetTicksMsec() / 1000.0; // Convert to seconds
        double timeSinceLastTag = currentTime - lastTagTime;

        if (timeSinceLastTag < TAG_COOLDOWN)
        {
            if (debugTags) GD.Print($"[TagDebug] Tag cooldown active - Time since last tag: {timeSinceLastTag:F2}s < {TAG_COOLDOWN}s");
            return;
        }

        Player taggerPlayer = NetworkManager.Instance?.GetPlayerFromID(currentTagger);
        if (taggerPlayer == null)
        {
            if (debugTags) GD.Print($"[TagDebug] Tagger player not found - ID: {currentTagger}");
            return;
        }

        // If the tagger is stunned (just got tagged), they cannot tag others
        if (playerTagProtection.ContainsKey(currentTagger))
        {
            if (debugTags) GD.Print($"[TagDebug] Tagger is stunned - Time remaining: {playerTagProtection[currentTagger]:F2}s");
            return;
        }

        if (debugTags) GD.Print($"[TagDebug] Checking for tag - Tagger: {currentTagger}, Alive players: {string.Join(", ", alivePlayers)}");

        foreach (int playerId in alivePlayers.ToList())
        {
            // Skip if trying to tag self or target is already stunned
            if (playerId == currentTagger)
            {
                if (debugTags) GD.Print($"[TagDebug] Skipping self-tag check for player {playerId}");
                continue;
            }

            if (playerTagProtection.ContainsKey(playerId))
            {
                if (debugTags) GD.Print($"[TagDebug] Player {playerId} is protected for {playerTagProtection[playerId]:F2}s");
                continue;
            }

            Player targetPlayer = NetworkManager.Instance?.GetPlayerFromID(playerId);
            if (targetPlayer == null)
            {
                if (debugTags) GD.Print($"[TagDebug] Target player {playerId} not found");
                continue;
            }

            float distance = taggerPlayer.GlobalPosition.DistanceTo(targetPlayer.GlobalPosition);
            if (debugTags) GD.Print($"[TagDebug] Distance to player {playerId}: {distance:F2} units");

            if (distance <= TAG_DISTANCE)
            {
                if (debugTags) GD.Print($"[TagDebug] Tag successful! Distance: {distance:F2} <= {TAG_DISTANCE}");
                
                // Update tag timing immediately to prevent rapid re-tags
                lastTagTime = currentTime;

                // Server handles the authoritative tag change
                HandlePlayerTagServer(playerId);

                // Notify all clients of the new tagger and current time remaining
                if (IsMultiplayerActive())
                {
                    Rpc(nameof(SyncTagState), currentTagger, gameTimeRemaining);
                }

                return; // Only tag one player at a time
            }
        }
    }

    // Server-only authoritative tag handler. Not exposed as an RPC.
    private void HandlePlayerTagServer(int newTaggerId)
    {
        if (debugTags) GD.Print($"[TagDebug] Server handling tag: {currentTagger} -> {newTaggerId}");

        // Store the old tagger before updating
        int oldTagger = currentTagger;
        currentTagger = newTaggerId;

        // Notify all clients of the tag change first
        if (IsMultiplayerActive())
        {
            Rpc(nameof(SyncTagStateAndModifiers), oldTagger, newTaggerId, gameTimeRemaining);
        }
        else
        {
            // In single player, apply directly
            ApplyTagStateAndModifiers(oldTagger, newTaggerId);
        }

        // Update HUD for all players
        UpdateAllPlayerRoles();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void SyncTagStateAndModifiers(int oldTaggerId, int newTaggerId, float timeRemaining)
    {
        if (debugTags) GD.Print($"[TagDebug] Received tag state sync: {oldTaggerId} -> {newTaggerId}");
        
        currentTagger = newTaggerId;
        gameTimeRemaining = timeRemaining;
        
        ApplyTagStateAndModifiers(oldTaggerId, newTaggerId);
        
        // Update colors and HUD
        ApplyTagColors();
        if (PlayerHUD.Instance != null)
        {
            string role = GetPlayerRole(GetSafeUniqueId());
            PlayerHUD.Instance.SyncGameState(currentState == GameState.Playing, role, timeRemaining);
            PlayerHUD.Instance.UpdatePlayerRole();
        }
    }

    private void ApplyTagStateAndModifiers(int oldTaggerId, int newTaggerId)
    {
        // Remove all speed modifiers from old tagger
        if (oldTaggerId != -1 && oldTaggerId != newTaggerId)
        {
            if (debugTags) GD.Print($"[TagDebug] Cleaning up old tagger {oldTaggerId}");
            
            Player oldTaggerPlayer = NetworkManager.Instance?.GetPlayerFromID(oldTaggerId);
            if (oldTaggerPlayer != null)
            {
                bool hadSpeedBoost = oldTaggerPlayer.HasSpeedModifier(TAGGER_SPEED_ID);
                bool hadStun = oldTaggerPlayer.HasSpeedModifier(NEW_TAGGER_STUN_ID);
                
                oldTaggerPlayer.RemoveSpeedModifier(TAGGER_SPEED_ID);
                oldTaggerPlayer.RemoveSpeedModifier(NEW_TAGGER_STUN_ID);
                
                if (debugTags) GD.Print($"[TagDebug] Removed modifiers from old tagger {oldTaggerId} - Had speed boost: {hadSpeedBoost}, Had stun: {hadStun}");
            }
            
            // Clear any existing protection on the old tagger
            bool hadProtection = playerTagProtection.Remove(oldTaggerId);
            if (debugTags) GD.Print($"[TagDebug] Removed protection from old tagger {oldTaggerId} - Had protection: {hadProtection}");
        }

        // Handle new tagger effects
        if (debugTags) GD.Print($"[TagDebug] Setting up new tagger {newTaggerId}");
        
        Player newTaggerPlayer = NetworkManager.Instance?.GetPlayerFromID(newTaggerId);
        if (newTaggerPlayer != null)
        {
            // Clear any existing modifiers first
            bool hadSpeedBoost = newTaggerPlayer.HasSpeedModifier(TAGGER_SPEED_ID);
            bool hadStun = newTaggerPlayer.HasSpeedModifier(NEW_TAGGER_STUN_ID);
            
            newTaggerPlayer.RemoveSpeedModifier(TAGGER_SPEED_ID);
            newTaggerPlayer.RemoveSpeedModifier(NEW_TAGGER_STUN_ID);
            
            if (debugTags) GD.Print($"[TagDebug] Cleared existing modifiers from new tagger - Had speed boost: {hadSpeedBoost}, Had stun: {hadStun}");

            // First apply the stun effect (makes them unable to move)
            newTaggerPlayer.AddSpeedModifier(NEW_TAGGER_STUN_ID, 0f, 0f, NEW_TAGGER_STUN_DURATION, false);
            if (debugTags) GD.Print($"[TagDebug] Applied stun to new tagger {newTaggerId}");

            // Then apply the permanent tagger speed buff (will take effect after stun)
            newTaggerPlayer.AddSpeedModifier(TAGGER_SPEED_ID, TAGGER_SPEED_MULTIPLIER, 0f, 0f, true);
            if (debugTags) GD.Print($"[TagDebug] Applied speed boost to new tagger {newTaggerId} (will activate after stun)");

            // Add protection for the stun duration
            playerTagProtection[newTaggerId] = NEW_TAGGER_STUN_DURATION;
            if (debugTags) GD.Print($"[TagDebug] Added protection for new tagger {newTaggerId} for {NEW_TAGGER_STUN_DURATION}s");
        }
        else
        {
            if (debugTags) GD.Print($"[TagDebug] ERROR: Could not find new tagger player {newTaggerId}");
        }

        // Announce the tag if we have NetworkManager
        if (NetworkManager.Instance != null)
        {
            string oldTaggerName = NetworkManager.Instance.PlayerNames.TryGetValue(oldTaggerId, out var oname) ? oname : "Someone";
            string newTaggerName = NetworkManager.Instance.PlayerNames.TryGetValue(newTaggerId, out var nname) ? nname : "Someone";
            NetworkManager.Instance.SendSystemMessage($"{oldTaggerName} tagged {newTaggerName}! {newTaggerName} is now IT!");
        }

        // Update colors for everyone (visual)
        ApplyTagColors();

        // Update HUD for all players
        UpdateAllPlayerRoles();

        // Server broadcasts authoritative tag state to clients
        if (IsMultiplayerActive())
        {
            Rpc(nameof(SyncTagState), currentTagger, gameTimeRemaining);
        }
    }

    // New method to apply tag colors more reliably
    private void ApplyTagColors()
    {
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor = (playerId == currentTagger) ? Colors.Red : Colors.Blue;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        IsColorChangeFromGame = false;
    }

    // New method to sync tag state
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncTagState(int taggerId, float timeRemaining)
    {
        if (debugTags) GD.Print($"[TagDebug] Syncing tag state to all clients - New Tagger: {taggerId}");
        
        // Store old tagger for modifier cleanup
        int oldTagger = currentTagger;
        currentTagger = taggerId;
        gameTimeRemaining = timeRemaining;
        
        // Apply speed modifiers on all clients
        ApplySpeedModifiersToPlayers(oldTagger, taggerId);
        
        // Apply colors
        ApplyTagColors();
        
        // Update HUD
        if (PlayerHUD.Instance != null)
        {
            string role = GetPlayerRole(GetSafeUniqueId());
            PlayerHUD.Instance.SyncGameState(currentState == GameState.Playing, role, timeRemaining);
            PlayerHUD.Instance.UpdatePlayerRole();
        }
        
        if (debugTags) GD.Print($"[TagDebug] Tag state sync complete");
    }

    private void ApplySpeedModifiersToPlayers(int oldTaggerId, int newTaggerId)
    {
        if (debugTags) GD.Print($"[TagDebug] Applying speed modifiers - Old: {oldTaggerId}, New: {newTaggerId}");

        // Remove modifiers from old tagger
        if (oldTaggerId != -1 && oldTaggerId != newTaggerId)
        {
            Player oldTaggerPlayer = NetworkManager.Instance?.GetPlayerFromID(oldTaggerId);
            if (oldTaggerPlayer != null)
            {
                oldTaggerPlayer.RemoveSpeedModifier(TAGGER_SPEED_ID);
                oldTaggerPlayer.RemoveSpeedModifier(NEW_TAGGER_STUN_ID);
                if (debugTags) GD.Print($"[TagDebug] Removed speed modifiers from old tagger {oldTaggerId}");
            }
        }

        // Apply modifiers to new tagger
        Player newTaggerPlayer = NetworkManager.Instance?.GetPlayerFromID(newTaggerId);
        if (newTaggerPlayer != null)
        {
            // Clear existing modifiers
            newTaggerPlayer.RemoveSpeedModifier(TAGGER_SPEED_ID);
            newTaggerPlayer.RemoveSpeedModifier(NEW_TAGGER_STUN_ID);

            // Apply stun first
            newTaggerPlayer.AddSpeedModifier(NEW_TAGGER_STUN_ID, 0f, 0f, NEW_TAGGER_STUN_DURATION, false);
            if (debugTags) GD.Print($"[TagDebug] Applied stun to new tagger {newTaggerId}");

            // Apply speed boost (will take effect after stun)
            newTaggerPlayer.AddSpeedModifier(TAGGER_SPEED_ID, TAGGER_SPEED_MULTIPLIER, 0f, 0f, true);
            if (debugTags) GD.Print($"[TagDebug] Applied speed boost to new tagger {newTaggerId}");

            // Add protection
            playerTagProtection[newTaggerId] = NEW_TAGGER_STUN_DURATION;
            if (debugTags) GD.Print($"[TagDebug] Added protection for new tagger {newTaggerId}");
        }
    }

    // Enhanced SelectRandomTagger method
    private void SelectRandomTagger()
    {
        var playerIds = NetworkManager.Instance.PlayerNames.Keys.ToList();
        if (playerIds.Count == 0) return;
        
        currentTagger = playerIds[Math.Abs((int)GD.Randi()) % playerIds.Count];
        alivePlayers = new HashSet<int>(playerIds);
        
        // Clear all protections at game start
        playerTagProtection.Clear();
        lastTagTime = 0f;
        
        GD.Print($"[GameManager] Selected tagger: {currentTagger}");
        
        // Apply initial colors
        StoreOriginalColors();
        ApplyTagColors();
        
        string taggerName = NetworkManager.Instance.PlayerNames[currentTagger];
        NetworkManager.Instance?.SendSystemMessage($"{taggerName} is IT! Run!");
        
        // Apply speed buff to tagger
        Player taggerPlayer = NetworkManager.Instance.GetPlayerFromID(currentTagger);
        if (taggerPlayer != null)
        {
            taggerPlayer.AddSpeedModifier(TAGGER_SPEED_ID, TAGGER_SPEED_MULTIPLIER, 0f, 0f, true);
            GD.Print($"[GameManager] Applied initial speed buff to tagger {currentTagger}");
        }
        
        UpdateGameState(GameState.Playing);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        EmitSignal(SignalName.GameStarted, (int)currentGameType);
        
        // Sync initial state to all clients
        if (IsMultiplayerActive())
        {
            Rpc(nameof(SyncTagState), currentTagger, gameTimeRemaining);
        }
    }

    // Updated UpdateTagProtection method with better logging and stun handling
    private void UpdateTagProtection(float delta)
    {
        if (debugTags) GD.Print($"[TagDebug] Updating tag protection - Current protections: {string.Join(", ", playerTagProtection.Select(kvp => $"{kvp.Key}:{kvp.Value:F2}s"))}");
        
        var keysToRemove = new List<int>();
        
        foreach (var kvp in playerTagProtection.ToList())
        {
            float oldValue = playerTagProtection[kvp.Key];
            playerTagProtection[kvp.Key] -= delta;
            
            if (debugTags) GD.Print($"[TagDebug] Updated protection for player {kvp.Key}: {oldValue:F2}s -> {playerTagProtection[kvp.Key]:F2}s");
            
            // Check if protection/stun has expired
            if (playerTagProtection[kvp.Key] <= 0)
            {
                keysToRemove.Add(kvp.Key);
                
                // If this was the tagger's stun wearing off, make sure they get their speed boost
                if (kvp.Key == currentTagger)
                {
                    if (debugTags) GD.Print($"[TagDebug] Tagger {kvp.Key} stun expired - Applying speed boost");
                    
                    Player taggerPlayer = NetworkManager.Instance?.GetPlayerFromID(kvp.Key);
                    if (taggerPlayer != null)
                    {
                        // Remove stun effect
                        taggerPlayer.RemoveSpeedModifier(NEW_TAGGER_STUN_ID);
                        if (debugTags) GD.Print($"[TagDebug] Removed stun effect from tagger {kvp.Key}");
                        
                        // Make sure they have their speed boost
                        if (!taggerPlayer.HasSpeedModifier(TAGGER_SPEED_ID))
                        {
                            taggerPlayer.AddSpeedModifier(TAGGER_SPEED_ID, TAGGER_SPEED_MULTIPLIER, 0f, 0f, true);
                            if (debugTags) GD.Print($"[TagDebug] Applied speed boost to tagger {kvp.Key}");
                        }
                        else
                        {
                            if (debugTags) GD.Print($"[TagDebug] Tagger {kvp.Key} already has speed boost");
                        }
                    }
                    else
                    {
                        if (debugTags) GD.Print($"[TagDebug] Could not find tagger player {kvp.Key} to apply speed boost");
                    }
                }
            }
        }
        
        foreach (var key in keysToRemove)
        {
            playerTagProtection.Remove(key);
            if (debugTags) GD.Print($"[TagDebug] Removed protection for player {key}");
        }

        if (debugTags && playerTagProtection.Count > 0)
        {
            GD.Print($"[TagDebug] Protection status after update: {string.Join(", ", playerTagProtection.Select(kvp => $"{kvp.Key}:{kvp.Value:F2}s"))}");
        }
    }
    
    private const string TAGGER_SPEED_ID = "tagger_speed";
    private const string NEW_TAGGER_STUN_ID = "new_tagger_stun";
    private const float TAGGER_SPEED_MULTIPLIER = 1.3f;
    

    
    #endregion
    
    #region Color Override System
    
    private void StoreOriginalColors()
    {
        originalPlayerColors.Clear();
        foreach (var kvp in NetworkManager.Instance.PlayerColors)
        {
            originalPlayerColors[kvp.Key] = kvp.Value;
        }
    }
    
    private void OverrideColors()
    {
        if (currentGameType != GameType.Tag) return;
        
        colorsOverridden = true;
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor = (playerId == currentTagger) ? Colors.Red : Colors.Blue;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        if (IsMultiplayerActive())
        {
            Rpc(nameof(ApplyColorOverrides), currentTagger);
        }
        IsColorChangeFromGame = false;
    }
    
    private void OverrideColorsHideAndSeek()
    {
        colorsOverridden = true;
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor = seekers.Contains(playerId) ? Colors.Red : Colors.Green;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        if (IsMultiplayerActive())
        {
            Rpc(nameof(ApplyColorOverridesHideAndSeek), seekers.ToArray());
        }
        IsColorChangeFromGame = false;
    }
    
    private void OverrideColorsMurderMystery()
    {
        colorsOverridden = true;
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor;
            if (playerId == currentMurderer)
                newColor = Colors.Red;
            else if (playerId == currentSheriff)
                newColor = Colors.Blue;
            else
                newColor = Colors.Yellow;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        if (IsMultiplayerActive())
        {
            Rpc(nameof(ApplyColorOverridesMurderMystery), currentMurderer, currentSheriff);
        }
        IsColorChangeFromGame = false;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ApplyColorOverrides(int taggerId)
    {
        currentTagger = taggerId;
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor = (playerId == currentTagger) ? Colors.Red : Colors.Blue;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        IsColorChangeFromGame = false;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ApplyColorOverridesHideAndSeek(int[] seekerIds)
    {
        seekers = new HashSet<int>(seekerIds);
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor = seekers.Contains(playerId) ? Colors.Red : Colors.Green;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        IsColorChangeFromGame = false;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ApplyColorOverridesMurderMystery(int murderer, int sheriff)
    {
        currentMurderer = murderer;
        currentSheriff = sheriff;
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor;
            if (playerId == currentMurderer)
                newColor = Colors.Red;
            else if (playerId == currentSheriff)
                newColor = Colors.Blue;
            else
                newColor = Colors.Yellow;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        IsColorChangeFromGame = false;
    }
    
    private void RestoreOriginalColors()
    {
        if (!colorsOverridden) return;
        
        IsColorChangeFromGame = true;
        
        foreach (var kvp in originalPlayerColors)
        {
            Player player = NetworkManager.Instance.GetPlayerFromID(kvp.Key);
            if (player != null)
            {
                player.SetPlayerColor(kvp.Value);
            }
        }
        
        if (IsMultiplayerActive())
        {
            Rpc(nameof(ApplyOriginalColors));
        }
        colorsOverridden = false;
        IsColorChangeFromGame = false;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ApplyOriginalColors()
    {
        IsColorChangeFromGame = true;
        
        foreach (var kvp in NetworkManager.Instance.PlayerColors)
        {
            Player player = NetworkManager.Instance.GetPlayerFromID(kvp.Key);
            if (player != null)
            {
                player.SetPlayerColor(kvp.Value);
            }
        }
        
        colorsOverridden = false;
        IsColorChangeFromGame = false;
    }
    
    #endregion
    
    #region Game End
    
    private void OnGameTimeUp()
    {
        if (!IsSafeServer()) return;
        
        switch (currentGameType)
        {
            case GameType.Tag:
                string taggerName = NetworkManager.Instance.PlayerNames.TryGetValue(currentTagger, out var name) ? name : "The tagger";
                NetworkManager.Instance?.SendSystemMessage($"Time's up! {taggerName} stays IT!");
                break;
            case GameType.HideAndSeek:
                NetworkManager.Instance?.SendSystemMessage("Time's up! Seekers win!");
                break;
            case GameType.MurderMystery:
                string murdererName = NetworkManager.Instance.PlayerNames.TryGetValue(currentMurderer, out var mName) ? mName : "The murderer";
                NetworkManager.Instance?.SendSystemMessage($"Time's up! {murdererName} (the murderer) wins!");
                break;
        }
        
        EndGame();
    }
    
    public void EndGame()
    {
        if (!IsSafeServer()) return;
        
        UpdateGameState(GameState.GameOver, currentGameType);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        EmitSignal(SignalName.GameEnded);
        
        gameTimer.Stop();
        gameTimeRemaining = 0f;
        
        // Restore original colors
        RestoreOriginalColors();
        
        // Clear game-specific data
        currentTagger = -1;
        currentMurderer = -1;
        currentSheriff = -1;
        seekers.Clear();
        hiders.Clear();
        innocentPlayers.Clear();
        alivePlayers.Clear();
        playerTagProtection.Clear();
        lastTagTime = 0f;
        
        // Update HUD
        if (IsMultiplayerActive())
        {
            // Role updates are handled by UpdateGameState
        }
        
        // Show game over screen briefly, then return to lobby
        GetTree().CreateTimer(3f).Timeout += () =>
        {
            ReturnToLobby();
        };
    }
    
    private void ReturnToLobby()
    {
        UpdateGameState(GameState.Lobby, GameType.None);
        currentGameType = GameType.None;
        currentTagger = -1;
        currentMurderer = -1;
        currentSheriff = -1;
        seekers.Clear();
        hiders.Clear();
        innocentPlayers.Clear();
        alivePlayers.Clear();
        playerTagProtection.Clear();
        lastTagTime = 0f;
        gameTimeRemaining = 0f;
        
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        if (IsMultiplayerActive())
        {
            Rpc(nameof(LoadLobby));
        }
        LoadLobby();
        
        // Update HUD
        if (IsMultiplayerActive())
        {
            Rpc(nameof(UpdateAllPlayerRoles));
        }
        UpdateAllPlayerRoles();
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void LoadLobby()
    {
        WorldNode.QueueFreeChildren();
        GD.Print("[GameManager] Loading Lobby...");
        WorldNode.AddChild(LobbyScene.Instantiate());
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnPlayerDisconnected(long peerId)
    {
        if (currentState == GameState.Playing)
        {
            if (currentGameType == GameType.Tag && currentTagger == peerId)
            {
                // If tagger disconnects, pick a new random tagger
                var remainingPlayers = alivePlayers.Where(id => id != peerId).ToList();
                if (remainingPlayers.Count > 0)
                {
                    int newTagger = remainingPlayers[Math.Abs((int)GD.Randi()) % remainingPlayers.Count];
                    // Server initiates the tag handling
                    if (IsSafeServer())
                    {
                        HandlePlayerTagServer(newTagger);
                        if (IsMultiplayerActive()) Rpc(nameof(SyncTagState), currentTagger, gameTimeRemaining);
                    }
                }
                else
                {
                    EndGame(); // No players left
                }
            }
            else if (currentGameType == GameType.MurderMystery)
            {
                if (currentMurderer == peerId || currentSheriff == peerId)
                {
                    // End game if key players disconnect
                    NetworkManager.Instance?.SendSystemMessage("A key player disconnected. Game ended.");
                    EndGame();
                }
            }
        }
        
        // Clean up player from all game data
        alivePlayers.Remove((int)peerId);
        seekers.Remove((int)peerId);
        hiders.Remove((int)peerId);
        innocentPlayers.Remove((int)peerId);
        playerVotes.Remove((int)peerId);
        playerTagProtection.Remove((int)peerId);
    }

    #endregion

    #region Public API
    
    // Main Methods
    public GameState GetCurrentState() => currentState;
    public GameType GetCurrentGameType() => currentGameType;
    public int GetCurrentTagger() => currentTagger;
    public bool IsPlayerTagger(int peerId) => currentTagger == peerId;
    
    // Hide and Seek methods
    public bool IsPlayerSeeker(int playerId) => seekers.Contains(playerId);
    public bool IsPlayerHider(int playerId) => hiders.Contains(playerId);
    
    // Murder Mystery methods
    public bool IsPlayerMurderer(int playerId) => currentMurderer == playerId;
    public bool IsPlayerSheriff(int playerId) => currentSheriff == playerId;
    public bool IsPlayerInnocent(int playerId) => innocentPlayers.Contains(playerId);
    
    public float GetGameTimeRemaining() => gameTimeRemaining;

    // Update roles for all players after a game state change
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void UpdateAllPlayerRoles()
    {
        if (PlayerHUD.Instance != null)
        {
            string role = GetPlayerRole(GetSafeUniqueId());
            PlayerHUD.Instance.SyncGameState(currentState == GameState.Playing, role, gameTimeRemaining);
            PlayerHUD.Instance.UpdatePlayerRole();
        }
    }
    
    #endregion
}
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node3D
{
    public static GameManager Instance { get; private set; }

    [Export] public PackedScene TagLevelScene;
    [Export] public PackedScene LobbyScene;
    public PackedScene VotingUIScene;
    
    // Used to track if color changes are coming from the game system
    public bool IsColorChangeFromGame { get; private set; }
    
    [Signal] public delegate void GameSceneLoadedEventHandler();

    public enum GameState
    {
        Lobby,
        Voting,
        Starting,
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
    private const float TAG_GAME_DURATION = 180f; // 3 minutes
    private const float TAG_DISTANCE = 3f;
    
    // Color override system
    private Dictionary<int, Color> originalPlayerColors = new();
    private bool colorsOverridden = false;

    public override void _Ready()
    {
        Instance = this;
        
        // Original GameController logic
        EmitSignal(SignalName.GameSceneLoaded);
        
        // Only enable host camera for dedicated servers
        var hostCamera = GetNode<Camera3D>("HostCamera");
        if (NetworkManager.Instance.IsDedicatedServer)
        {
            MultiplayerSpawner worldSpawner = GetNode<MultiplayerSpawner>("WorldSpawner");
            hostCamera.Current = true;
        }
        else
        {
            hostCamera.Current = false;
        }
        
        // Setup timers
        votingTimer = new Timer { WaitTime = VOTING_TIME, OneShot = true };
        AddChild(votingTimer);
        votingTimer.Timeout += OnVotingFinished;
        
        gameTimer = new Timer { WaitTime = TAG_GAME_DURATION, OneShot = true };
        AddChild(gameTimer);
        gameTimer.Timeout += OnGameTimeUp;
        
        // Connect to network events
        if (NetworkManager.Instance != null)
        {
            Multiplayer.PeerDisconnected += OnPlayerDisconnected;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (currentState == GameState.Playing && currentGameType == GameType.Tag)
        {
            CheckTagDistance();
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

    #region Original GameController Methods
    

    public void AssignAttacker()
    {
        if (!Multiplayer.IsServer()) return;

        var players = GetTree().GetNodesInGroup("Players");
        if (players.Count == 0) return;
        
        var attacker = players[Math.Abs((int)GD.Randi()) % players.Count];
        Rpc(nameof(SetAttacker), attacker.Name);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SetAttacker(string name)
    {
        var player = GetNode<Node3D>("/root/Game/" + name);
        if (player == null) return;

        // Turn on attack mode
        GD.Print("Attacker selected: " + name);
    }
    
    #endregion

# region Voting System
    
    public async void StartVoting()
    {
        if (!Multiplayer.IsServer() || currentState != GameState.Lobby) return;

        await ToSignal(GetTree().CreateTimer(0.1), "timeout");
        
        // Close settings menu if open and force mouse mode
        if (NewPauseMenu.IsOpen)
        {
            NewPauseMenu.Instance.CloseMenu();
        }
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible, true); // Force visible mode

        UpdateClientGameState(GameState.Voting);
        Rpc(nameof(UpdateClientGameState), (int)GameState.Voting);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        playerVotes.Clear();
        votingTimer.Start();
        
        Rpc(nameof(ShowVotingUI), VOTING_TIME);
        ShowVotingUI(VOTING_TIME);
        
        NetworkManager.Instance.SendSystemMessage("Game voting started! Vote for your favorite game!");
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

            if (Multiplayer.MultiplayerPeer == null)
            {
                GD.PrintErr("[GameManager] Cannot vote: No multiplayer peer");
                return;
            }

            if (!Multiplayer.IsServer())
            {
                // Get player ID and validate it
                int playerId = Multiplayer.GetUniqueId();
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
            if (!Multiplayer.IsServer())
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
            Rpc(nameof(BroadcastVote), voterId, gameTypeInt, playerName, 
                new Color(playerColor.R, playerColor.G, playerColor.B, playerColor.A).ToHtml(), 
                expressionId, hatId);
            
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
    }    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
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
        if (!Multiplayer.IsServer()) return;

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
        Rpc(nameof(CleanupVotingUI));
        CleanupVotingUI();

        NetworkManager.Instance.SendSystemMessage($"{winningGame} wins the vote! Starting game...");
        StartGame(winningGame);
        
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);
    }
    
    #endregion
    
    #region Game Management
    
    public void StartGame(GameType gameType)
    {
        if (!Multiplayer.IsServer()) return;
        
        currentGameType = gameType;
        UpdateClientGameState(GameState.Starting);
        Rpc(nameof(UpdateClientGameState), (int)GameState.Starting);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        // Store original colors before overriding
        StoreOriginalColors();
        
        switch (gameType)
        {
            case GameType.Tag:
                StartTagGame();
                break;
            // Add other game types here
        }
    }
    
    private void StartTagGame()
    {
        // Load tag level
        Rpc(nameof(LoadGameLevel), (int)GameType.Tag);
        LoadGameLevel((int)GameType.Tag);
        
        // Wait for level to load, then start
        GetTree().CreateTimer(2f).Timeout += () =>
        {
            if (Multiplayer.IsServer())
            {
                SelectRandomTagger();
                StartTagGameplay();
            }
        };
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void LoadGameLevel(int gameTypeInt)
    {
        GameType gameType = (GameType)gameTypeInt;
        
        switch (gameType)
        {
            case GameType.Tag:
                GetTree().ChangeSceneToPacked(TagLevelScene);
                NetworkManager.Instance.GetLocalPlayer().GlobalPosition = new Vector3(5 * GD.Randf(), 0, 5 * GD.Randf());
                break;
            // Add other levels here
        }
    }
    
    private void SelectRandomTagger()
    {
        var playerIds = NetworkManager.Instance.PlayerNames.Keys.ToList();
        if (playerIds.Count == 0) return;
        
        currentTagger = playerIds[Math.Abs((int)GD.Randi()) % playerIds.Count];
        alivePlayers = new HashSet<int>(playerIds);
        
        // Apply color overrides
        OverrideColors();
        
        string taggerName = NetworkManager.Instance.PlayerNames[currentTagger];
        Rpc(nameof(AnnounceTagger), currentTagger, taggerName);
        AnnounceTagger(currentTagger, taggerName);
        
        // Also use the old SetAttacker system for compatibility
        var players = GetTree().GetNodesInGroup("Players");
        var taggerPlayer = players.FirstOrDefault(p => p.Name.ToString().Contains($"Player_{currentTagger}"));
        if (taggerPlayer != null)
        {
            Rpc(nameof(SetAttacker), taggerPlayer.Name);
            SetAttacker(taggerPlayer.Name);
        }
        
        UpdateClientGameState(GameState.Playing);
        Rpc(nameof(UpdateClientGameState), (int)GameState.Playing);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        EmitSignal(SignalName.GameStarted, (int)currentGameType);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void UpdateClientGameState(GameState newState)
    {
        currentState = newState;
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AnnounceTagger(int taggerId, string taggerName)
    {
        currentTagger = taggerId;
        NetworkManager.Instance.SendSystemMessage($"{taggerName} is IT! Run!");
    }
    
    private void StartTagGameplay()
    {
        gameTimer.Start();
        NetworkManager.Instance.SendSystemMessage($"Tag game started! {TAG_GAME_DURATION} seconds to play!");
    }
    
    #endregion

    #region Tag Game Logic

    private void CheckTagDistance()
    {
        if (currentTagger == -1) return;

        Player taggerPlayer = NetworkManager.Instance.GetPlayerFromID(currentTagger);
        if (taggerPlayer == null) return;

        foreach (int playerId in alivePlayers.ToList())
        {
            if (playerId == currentTagger) continue;

            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player == null) continue;

            float distance = taggerPlayer.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (distance <= TAG_DISTANCE)
            {
                TagPlayer(playerId);
                return; // Only tag one player at a time
            }
        }
    }
    
    private void TagPlayer(int newTaggerId)
    {
        if (!Multiplayer.IsServer()) return;
        
        int oldTagger = currentTagger;
        currentTagger = newTaggerId;
        
        // Update colors immediately
        OverrideColors();
        
        string oldTaggerName = NetworkManager.Instance.PlayerNames.TryGetValue(oldTagger, out var name1) ? name1 : "Unknown";
        string newTaggerName = NetworkManager.Instance.PlayerNames.TryGetValue(newTaggerId, out var name2) ? name2 : "Unknown";
        
        // Use both new and old systems
        var players = GetTree().GetNodesInGroup("Players");
        var newTaggerPlayer = players.FirstOrDefault(p => p.Name.ToString().Contains($"Player_{newTaggerId}"));
        if (newTaggerPlayer != null)
        {
            Rpc(nameof(SetAttacker), newTaggerPlayer.Name);
            SetAttacker(newTaggerPlayer.Name);
        }
        
        Rpc(nameof(AnnounceTagChange), oldTagger, currentTagger, oldTaggerName, newTaggerName);
        AnnounceTagChange(oldTagger, currentTagger, oldTaggerName, newTaggerName);
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void AnnounceTagChange(int oldTagger, int newTagger, string oldTaggerName, string newTaggerName)
    {
        currentTagger = newTagger;
        NetworkManager.Instance.SendSystemMessage($"{newTaggerName} is now IT!");
        
        // Apply color changes on all clients
        RestoreOriginalColors();
        OverrideColors();
    }
    
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
            Color newColor = (playerId == currentTagger) ? Colors.Red : Colors.White;
            
            Player player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                player.SetPlayerColor(newColor);
            }
        }
        
        Rpc(nameof(ApplyColorOverrides), currentTagger);
        IsColorChangeFromGame = false;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ApplyColorOverrides(int taggerId)
    {
        currentTagger = taggerId;
        IsColorChangeFromGame = true;
        
        foreach (int playerId in NetworkManager.Instance.PlayerNames.Keys)
        {
            Color newColor = (playerId == currentTagger) ? Colors.Red : Colors.White;
            
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
        
        Rpc(nameof(ApplyOriginalColors));
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
        if (!Multiplayer.IsServer()) return;
        
        string taggerName = NetworkManager.Instance.PlayerNames.TryGetValue(currentTagger, out var name) ? name : "The tagger";
        NetworkManager.Instance.SendSystemMessage($"Time's up! {taggerName} stays IT!");
        
        EndGame();
    }
    
    public void EndGame()
    {
        if (!Multiplayer.IsServer()) return;
        
        UpdateClientGameState(GameState.GameOver);
        Rpc(nameof(UpdateClientGameState), (int)GameState.GameOver);
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        EmitSignal(SignalName.GameEnded);
        
        gameTimer.Stop();
        
        // Restore original colors
        RestoreOriginalColors();
        
        // Show game over screen briefly, then return to lobby
        GetTree().CreateTimer(3f).Timeout += () =>
        {
            ReturnToLobby();
        };
    }
    
    private void ReturnToLobby()
    {
        UpdateClientGameState(GameState.Lobby);
        Rpc(nameof(UpdateClientGameState), (int)GameState.Lobby);
        currentGameType = GameType.None;
        currentTagger = -1;
        alivePlayers.Clear();
        
        EmitSignal(SignalName.GameStateChanged, (int)currentState);
        
        Rpc(nameof(LoadLobby));
        LoadLobby();
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void LoadLobby()
    {
        GetTree().ChangeSceneToPacked(LobbyScene);
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnPlayerDisconnected(long peerId)
    {
        if (currentState == GameState.Playing && currentTagger == peerId)
        {
            // If tagger disconnects, pick a new random tagger
            var remainingPlayers = alivePlayers.Where(id => id != peerId).ToList();
            if (remainingPlayers.Count > 0)
            {
                int newTagger = remainingPlayers[Math.Abs((int)GD.Randi()) % remainingPlayers.Count];
                TagPlayer(newTagger);
            }
            else
            {
                EndGame(); // No players left
            }
        }
        
        alivePlayers.Remove((int)peerId);
        playerVotes.Remove((int)peerId);
    }
    
    #endregion
    
    #region Public API
    
    public GameState GetCurrentState() => currentState;
    public GameType GetCurrentGameType() => currentGameType;
    public int GetCurrentTagger() => currentTagger;
    public bool IsPlayerTagger(int playerId) => currentTagger == playerId;
    
    #endregion
}
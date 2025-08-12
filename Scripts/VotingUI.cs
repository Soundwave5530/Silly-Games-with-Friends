using Godot;
using System;
using System.Collections.Generic;

public partial class VotingUI : CanvasLayer
{
    public PackedScene VotingTabScene = GD.Load<PackedScene>("res://Scenes/VotingTab.tscn");

    [Export] public Label TimeLabel;
    [Export] public Label StatusLabel;
    [Export] public HBoxContainer VotingTabContainer;

    private Timer countdownTimer;
    private float timeLeft;
    private bool hasVoted = false;
    private Dictionary<int, GameManager.GameType> playerVotes = new();
    private VotingTab lastVotedTab = null;
    private List<VotingTab> votingTabs = new();

    public override void _Ready()
    {
        Hide();

        // Setup countdown timer
        countdownTimer = new Timer { WaitTime = 60f };
        AddChild(countdownTimer);

        // Connect to GameManager if it exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameStateChanged += OnGameStateChanged;
            GameManager.Instance.VoteSubmitted += OnVoteSubmitted;
        }
    }

    public void StartVoting(float duration)
    {
        timeLeft = duration;
        hasVoted = false;
        playerVotes.Clear();

        Show();

        // Make sure pause menu is closed and mouse is visible
        if (NewPauseMenu.IsOpen)
        {
            NewPauseMenu.Instance.CloseMenu();
        }
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);

        // Clear old tabs
        foreach (var tab in votingTabs)
        {
            tab.QueueFree();
        }
        votingTabs.Clear();

        StatusLabel.Text = "Vote for your favorite game!";

        // Initialize tabs with random game data
        var gameOptions = GameDataProvider.Instance.GetRandomGameOptions(3);
        float spacing = 10; // Spacing between tabs
        float currentY = 0;

        for (int i = 0; i < gameOptions.Count; i++)
        {
            var tab = VotingTabScene.Instantiate<VotingTab>();
            tab.Initialize(gameOptions[i]);
            tab.Position = new Vector2(0, currentY);

            // Connect pressed signal
            tab.Pressed += () => SubmitVote(tab.data.GameType);

            VotingTabContainer.AddChild(tab);
            tab.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            votingTabs.Add(tab);

            currentY += 145 + spacing; // 145 is the height of a tab
        }

        EnableTabs();
        countdownTimer.Start();
        UpdateTimeDisplay();
    }

    private VotingTab GetTabForIndex(int index)
    {
        return index >= 0 && index < votingTabs.Count ? votingTabs[index] : null;
    }

    private void OnVoteSubmitted(int playerId, GameManager.GameType gameType, string playerName, Color playerColor)
    {
        // Store whether this is a vote change
        bool isChangeVote = playerVotes.ContainsKey(playerId);
        GameManager.GameType? oldVote = null;
        VotingTab oldTab = null;

        // Remove old vote sticker if it exists
        if (isChangeVote)
        {
            oldVote = playerVotes[playerId];
            oldTab = GetTabForGameType(oldVote.Value);
            if (oldTab != null)
            {
                oldTab.RemovePlayerSticker(playerId);
            }
        }

        // Update votes dictionary
        playerVotes[playerId] = gameType;

        // Add new sticker after a short delay to ensure old one is cleaned up
        GetTree().CreateTimer(0.1).Timeout += () =>
        {
            var votedTab = GetTabForGameType(gameType);
            if (votedTab != null)
            {
                votedTab.AddPlayerSticker(playerId, playerName, playerColor);
            }
            UpdateVoteCounts();
        };
    }

    private VotingTab GetTabForGameType(GameManager.GameType gameType)
    {
        return votingTabs.Find(tab => tab.data.GameType == gameType);
    }

    private void UpdateVoteCounts()
    {
        var votes = new Dictionary<GameManager.GameType, int>();
        foreach (var vote in playerVotes.Values)
        {
            if (!votes.ContainsKey(vote)) votes[vote] = 0;
            votes[vote]++;
        }

        foreach (var tab in votingTabs)
        {
            tab.UpdateVotes(votes.GetValueOrDefault(tab.data.GameType));
        }
    }

    private void SubmitVote(GameManager.GameType gameType)
    {
        try
        {
            // Validate network state
            if (NetworkManager.Instance == null || !IsInstanceValid(GameManager.Instance))
            {
                GD.PrintErr("[VotingUI] Cannot vote: NetworkManager or GameManager not valid");
                StatusLabel.Text = "Cannot vote: Server connection issue";
                return;
            }

            if (Multiplayer.MultiplayerPeer == null)
            {
                GD.PrintErr("[VotingUI] Cannot vote: No active multiplayer peer");
                StatusLabel.Text = "Cannot vote: Not connected to server";
                return;
            }

            var myId = Multiplayer.GetUniqueId();
            if (myId == 0)
            {
                GD.PrintErr("[VotingUI] Cannot vote: Invalid peer ID");
                StatusLabel.Text = "Cannot vote: Invalid player ID";
                return;
            }

            GD.Print($"[VotingUI] Submitting vote for {gameType} from peer {myId}");

            // Register vote through GameManager
            GameManager.Instance.RegisterVote(gameType);

            // Visual feedback only for the voter
            StatusLabel.Text = $"You voted for {gameType}!";
            ResetTabColors();

            var selectedTab = GetTabForGameType(gameType);
            if (selectedTab != null)
            {
                selectedTab.Modulate = new Color(1.2f, 1.2f, 1.2f);
            }

            // Store the vote locally
            lastVotedTab = selectedTab;
            hasVoted = true;

            // Disable voting UI temporarily to prevent spam
            DisableTabs();
            GetTree().CreateTimer(0.5f).Timeout += EnableTabs;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[VotingUI] Error submitting vote: {e.Message}");
            StatusLabel.Text = "Error submitting vote. Try again.";
        }
    }

    private void EnableTabs()
    {
        foreach (var tab in votingTabs)
        {
            tab.Disabled = false;
        }
        ResetTabColors();
    }

    private void DisableTabs()
    {
        foreach (var tab in votingTabs)
        {
            tab.Disabled = true;
        }
    }

    private void ResetTabColors()
    {
        foreach (var tab in votingTabs)
        {
            tab.Modulate = Colors.White;
        }
    }

    private void UpdateCountdown(double delta)
    {
        timeLeft -= (float)delta;
        UpdateTimeDisplay();

        if (timeLeft <= 0)
        {
            countdownTimer.Stop();
            Hide();
        }
    }

    private void UpdateTimeDisplay()
    {
        TimeLabel.Text = $"Time: {Mathf.CeilToInt(timeLeft)}s";
    }

    private void OnGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.Voting:
                // Will be handled by StartVoting call
                break;
            case GameManager.GameState.Starting:
            case GameManager.GameState.Playing:
            case GameManager.GameState.Lobby:
                Hide();
                countdownTimer.Stop();
                break;
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameStateChanged -= OnGameStateChanged;
        }
    }
    
    public override void _Process(double delta)
    {
        
        if (GameManager.Instance != null && (GameManager.Instance.GetCurrentState() == GameManager.GameState.Voting))
        {
            UpdateCountdown(delta);
        }
    }
}
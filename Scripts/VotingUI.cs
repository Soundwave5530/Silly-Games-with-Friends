using Godot;
using System;

public partial class VotingUI : CanvasLayer
{
    [Export] public Button TagButton;
    [Export] public Button HideSeekButton;
    [Export] public Button MurderMysteryButton;
    [Export] public Label TimeLabel;
    [Export] public Label StatusLabel;
    
    private Timer countdownTimer;
    private float timeLeft;
    private bool hasVoted = false;

    public override void _Ready()
    {
        Hide();
        
        // Setup buttons
        TagButton.Pressed += () => SubmitVote(GameManager.GameType.Tag);
        HideSeekButton.Pressed += () => SubmitVote(GameManager.GameType.HideAndSeek);
        MurderMysteryButton.Pressed += () => SubmitVote(GameManager.GameType.MurderMystery);
        
        // Setup countdown timer
        countdownTimer = new Timer { WaitTime = 1f };
        AddChild(countdownTimer);
        countdownTimer.Timeout += UpdateCountdown;
        
        // Connect to GameManager if it exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameStateChanged += OnGameStateChanged;
        }
    }

    public void StartVoting(float duration)
    {
        timeLeft = duration;
        hasVoted = false;
        
        Show();
        EnableButtons();
        StatusLabel.Text = "Vote for your favorite game!";
        
        countdownTimer.Start();
        UpdateTimeDisplay();
    }

    private void SubmitVote(GameManager.GameType gameType)
    {
        if (hasVoted) return;
        
        hasVoted = true;
        GameManager.Instance?.SubmitVote((int)gameType);
        
        DisableButtons();
        StatusLabel.Text = $"You voted for {gameType}!";
        
        // Change button colors to show which was selected
        ResetButtonColors();
        Button selectedButton = gameType switch
        {
            GameManager.GameType.Tag => TagButton,
            GameManager.GameType.HideAndSeek => HideSeekButton,
            GameManager.GameType.MurderMystery => MurderMysteryButton,
            _ => null
        };
        
        if (selectedButton != null)
        {
            selectedButton.Modulate = Colors.Green;
        }
    }

    private void EnableButtons()
    {
        TagButton.Disabled = false;
        HideSeekButton.Disabled = false;
        MurderMysteryButton.Disabled = false;
        ResetButtonColors();
    }

    private void DisableButtons()
    {
        TagButton.Disabled = true;
        HideSeekButton.Disabled = true;
        MurderMysteryButton.Disabled = true;
    }

    private void ResetButtonColors()
    {
        TagButton.Modulate = Colors.White;
        HideSeekButton.Modulate = Colors.White;
        MurderMysteryButton.Modulate = Colors.White;
    }

    private void UpdateCountdown()
    {
        timeLeft -= 1f;
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
}
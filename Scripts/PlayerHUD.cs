using Godot;
using System;

public partial class PlayerHUD : CanvasLayer
{
    public static PlayerHUD Instance { get; private set; }
    public bool active = false;

    [Export] public TextureRect playerFace;
    [Export] public TextureRect playerHead;
    [Export] public TextureRect playerHat;
    [Export] public Label playerName;

    [Export] public Label InteractionPrompt;

    [Export] public Label GameRoleLabel;
    [Export] public Label GameTimeRemainingLabel;

    private int faceFrameIndex = 0;
    private float faceFrameTime = 0f;
    private float faceFrameDelay = 0.15f;

    private int hatFrameIndex = 0;
    private float hatFrameTime = 0f;
    private float hatFrameDelay = 0.15f;

    private FacialExpression face;
    private Cosmetic hat;
    private CharacterTypePreset character;
    private Color color;
    
    // Game state tracking
    private float gameTimeRemaining = 0f;
    private bool isGameActive = false;
    private string currentRole = "";

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

    public override void _Ready()
    {
        // Only hide HUD for dedicated servers (headless servers), not for host players
        if (NetworkManager.Instance?.IsDedicatedServer == true)
        {
            Visible = false;
            return;
        }
        
        Instance = this;
        active = true;
        InitializeHUD();
        
        UpdateGameUI();
        
        // Connect to game signals after a short delay to ensure GameManager is ready
        CallDeferred(nameof(ConnectGameSignalsDeferred));
    }

    private void ConnectGameSignalsDeferred()
    {
        if (GameManager.Instance != null)
        {
            ConnectGameSignals();
        }
        else
        {
            GetTree().CreateTimer(0.1f).Timeout += ConnectGameSignalsDeferred;
        }
    }

    public void ConnectGameSignals()
    {
        if (GameManager.Instance == null) return;
        
        // Disconnect first to avoid duplicate connections
        try
        {
            GameManager.Instance.GameStateChanged -= OnGameStateChanged;
            GameManager.Instance.GameStarted -= OnGameStarted;
            GameManager.Instance.GameEnded -= OnGameEnded;
        }
        catch
        {
            // Ignore errors if signals weren't connected yet
        }
        
        // Connect the signals
        GameManager.Instance.GameStateChanged += OnGameStateChanged;
        GameManager.Instance.GameStarted += OnGameStarted;
        GameManager.Instance.GameEnded += OnGameEnded;
        
        // Update role immediately if we're in a game
        if (GameManager.Instance.GetCurrentState() == GameManager.GameState.Playing)
        {
            UpdatePlayerRole();
        }
    }

    public override void _ExitTree()
    {
        active = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameStateChanged -= OnGameStateChanged;
            GameManager.Instance.GameStarted -= OnGameStarted;
            GameManager.Instance.GameEnded -= OnGameEnded;
        }
    }

    public void InitializeHUD()
    {
        playerHead.SelfModulate = color;
        playerHat.Modulate = (hat != null && hat.modulatesWithPlayerColor) ? color : Colors.White;

        faceFrameIndex = 0;
        hatFrameIndex = 0;

        RefreshFromSettings();
    }

    public override void _Process(double delta)
    {
        if (!active || character == null || face == null)
            return;

        faceFrameTime += (float)delta;
        hatFrameTime += (float)delta;

        if (faceFrameTime >= faceFrameDelay)
        {
            faceFrameTime = 0f;
            AdvanceFaceFrame();
        }

        if (hatFrameTime >= hatFrameDelay)
        {
            hatFrameTime = 0f;
            AdvanceHatFrame();
        }
        
        // Update game timer
        if (isGameActive && gameTimeRemaining > 0)
        {
            gameTimeRemaining -= (float)delta;
            if (gameTimeRemaining <= 0)
            {
                gameTimeRemaining = 0;
                UpdateGameUI();
            }
            else
            {
                UpdateTimeDisplay();
            }
        }
    }

    private void AdvanceFaceFrame()
    {
        if (face.FrontExpression.Count == 0) return;
        faceFrameIndex = (faceFrameIndex + 1) % face.FrontExpression.Count;
        playerFace.Texture = face.FrontExpression[faceFrameIndex];
    }

    private void AdvanceHatFrame()
    {
        if (hat == null || hat.CosmeticID == "none")
        {
            playerHat.Texture = null;
            return;
        }

        var frames = hat.FrontSprites;
        if (frames == null || frames.Count == 0) return;

        hatFrameIndex = (hatFrameIndex + 1) % frames.Count;
        playerHat.Texture = frames[hatFrameIndex];
    }

    public void RefreshFromSettings()
    {
        face = ResourceDatabase.Expressions[SettingsManager.CurrentSettings.SavedExpressionID];
        hat = ResourceDatabase.Hats[SettingsManager.CurrentSettings.SavedHatID];
        character = PlayerData.CurrentCharacter;
        color = new Color(SettingsManager.CurrentSettings.ColorR, SettingsManager.CurrentSettings.ColorG, SettingsManager.CurrentSettings.ColorB);

        playerHead.SelfModulate = color;
        playerHat.Modulate = (hat != null && hat.modulatesWithPlayerColor) ? color : Colors.White;

        faceFrameIndex = 0;
        hatFrameIndex = 0;

        if (face.FrontExpression.Count > 0)
            playerFace.Texture = face.FrontExpression[0];

        if (hat != null && hat.CosmeticID != "none" && hat.FrontSprites.Count > 0)
            playerHat.Texture = hat.FrontSprites[0];
        else
            playerHat.Texture = null;

        if (!string.IsNullOrEmpty(SettingsManager.CurrentSettings.Username))
            playerName.Text = SettingsManager.CurrentSettings.Username;
    }
    
    public void ShowInteractionPrompt(string text)
    {
        if (!active || InteractionPrompt == null) return;
        InteractionPrompt.Text = text;
        InteractionPrompt.Visible = true;
    }

    public void HideInteractionPrompt()
    {
        if (!active || InteractionPrompt == null) return;
        InteractionPrompt.Visible = false;
    }
    
    #region Game State Management
    
    private void OnGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.Playing:
                isGameActive = true;
                UpdatePlayerRole();
                break;
            case GameManager.GameState.Lobby:
            case GameManager.GameState.GameOver:
                isGameActive = false;
                currentRole = "";
                gameTimeRemaining = 0f;
                break;
        }
        UpdateGameUI();
    }
    
    private void OnGameStarted(GameManager.GameType gameType)
    {
        isGameActive = true;
        UpdatePlayerRole();

        gameTimeRemaining = GameDataProvider.Instance.GetGamedataFromType(gameType).GameTime;

        UpdateGameUI();
    }
    private void OnGameEnded()
    {
        isGameActive = false;
        currentRole = "";
        gameTimeRemaining = 0f;
        UpdateGameUI();
    }
    
    public void UpdatePlayerRole()
    {
        if (!isGameActive || GameManager.Instance == null)
        {
            currentRole = "";
            UpdateGameUI();
            return;
        }
        
        var gameType = GameManager.Instance.GetCurrentGameType();
        int localPlayerId = GetSafeUniqueId(); // Use safe method instead of direct call
        
        switch (gameType)
        {
            case GameManager.GameType.Tag:
                currentRole = GameManager.Instance.IsPlayerTagger(localPlayerId) ? "Tagger" : "Runner";
                break;
            case GameManager.GameType.HideAndSeek:
                currentRole = GameManager.Instance.IsPlayerSeeker(localPlayerId) ? "Seeker" : "Hider";
                break;
            case GameManager.GameType.MurderMystery:
                // You'll need to implement these methods in GameManager
                if (GameManager.Instance.IsPlayerMurderer(localPlayerId))
                    currentRole = "Murderer";
                else if (GameManager.Instance.IsPlayerSheriff(localPlayerId))
                    currentRole = "Sheriff";
                else
                    currentRole = "Innocent";
                break;
            default:
                currentRole = "";
                break;
        }
        
        UpdateGameUI();
    }
    
    private void UpdateGameUI()
    {
        if (!active) return;
        
        if (GameRoleLabel != null)
        {
            if (isGameActive && !string.IsNullOrEmpty(currentRole))
            {
                GameRoleLabel.Text = currentRole;
                GameRoleLabel.Visible = true;

                switch (currentRole)
                {
                    case "Tagger":
                    case "Seeker":
                    case "Murderer":
                        GameRoleLabel.Modulate = Colors.Red;
                        break;
                    case "Sheriff":
                    case "Runner":
                        GameRoleLabel.Modulate = Colors.Blue;
                        break;
                    case "Hider":
                    case "Innocent":
                        GameRoleLabel.Modulate = Colors.Green;
                        break;
                    default:
                        GameRoleLabel.Modulate = Colors.White;
                        break;
                }
            }
            else
            {
                GameRoleLabel.Visible = false;
            }
        }
        
        UpdateTimeDisplay();
    }
    
    private void UpdateTimeDisplay()
    {
        if (!active || GameTimeRemainingLabel == null) return;
        
        if (isGameActive && gameTimeRemaining > 0)
        {
            int minutes = (int)(gameTimeRemaining / 60);
            int seconds = (int)(gameTimeRemaining % 60);
            GameTimeRemainingLabel.Text = $"{minutes:D2}:{seconds:D2} Remaining";
            GameTimeRemainingLabel.Visible = true;
        }
        else
        {
            GameTimeRemainingLabel.Visible = false;
        }
    }
    
    // Called by GameManager when game time is updated from server
    public void UpdateGameTimeRemaining(float timeRemaining)
    {
        gameTimeRemaining = timeRemaining;
        UpdateTimeDisplay();
    }
    
    // Called when a new player joins mid-game to sync their role
    public void SyncGameState(bool gameActive, string role, float timeRemaining)
    {
        isGameActive = gameActive;
        currentRole = role;
        gameTimeRemaining = timeRemaining;
        UpdateGameUI();
    }
    
    #endregion
}
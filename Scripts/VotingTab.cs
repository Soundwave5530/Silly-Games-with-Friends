using Godot;
using System;
using System.Collections.Generic;

public partial class VotingTab : Button
{
    [ExportCategory("Game Data")]
    [Export] public GameData data = null;

    [ExportCategory("UI Elements")]
    [Export] public Label gameNameLabel;
    [Export] public Label voteForLabel;
    [Export] public Label currentVotesLabel;
    [Export] public Label minimumPlayersLabel;
    [Export] public TextureRect gameIcon;

    [Export] private Control playerStickersContainer;

    public Tween tween;

    private float RotationAngle = Mathf.DegToRad(5f);
    private float AnimDuration = 0.2f;
    
    private Dictionary<int, PlayerSticker> playerStickers = new();

    public override void _Ready()
    {
        PivotOffset = Size / 2;
        voteForLabel.Modulate = new Color(0, 0, 0, 0);

        MouseEntered += () =>
        {
            AnimateTo(RotationAngle);
        };

        MouseExited += () =>
        {
            AnimateTo(0);
        };
    }

    public void Initialize(GameData gameData)
    {
        data = gameData;
        gameNameLabel.Text = data.GameName ?? "Invalid Name";
        gameIcon.Texture = data.Icon ?? null;

        voteForLabel.Text = $"Vote for {data.GameName}" ?? "Cannot Vote, Invalid Data";
        minimumPlayersLabel.Text = $"Minimum Players: {data.MinimumPlayers}" ?? "";
    }

    public void AnimateTo(float rotation)
    {
        if (tween != null && tween.IsRunning() && tween.IsValid())
            tween.Kill();

        tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Spring);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "rotation", rotation, AnimDuration);

        tween.Parallel().TweenMethod(Callable.From<float>((v) =>
        {
            voteForLabel.Modulate = new Color(1, 1, 1, v);
        }), voteForLabel.Modulate.A, rotation != 0 ? 1 : 0, AnimDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Expo);
    }
    
    public void UpdateVotes(int currentVotes)
    {
        currentVotesLabel.Text = $"Current Votes: {currentVotes}";
    }

    public void AddPlayerSticker(int playerId, string playerName, Color playerColor)
    {
        if (playerStickersContainer == null)
        {
            GD.PrintErr($"[VotingTab] No sticker container found for game {data?.GameName}!");
            return;
        }
        
        if (playerStickers.ContainsKey(playerId))
        {
            GD.Print($"[VotingTab] Removing existing sticker for player {playerId}");
            RemovePlayerSticker(playerId);
        }

        GD.Print($"[VotingTab] Adding sticker for player {playerId} in {data?.GameName}");
        var stickerScene = GD.Load<PackedScene>("res://Scenes/PlayerSticker.tscn");
        if (stickerScene == null)
        {
            GD.PrintErr("[VotingTab] Failed to load PlayerSticker scene!");
            return;
        }

        var sticker = stickerScene.Instantiate<PlayerSticker>();
        if (sticker == null)
        {
            GD.PrintErr("[VotingTab] Failed to instantiate PlayerSticker!");
            return;
        }

        sticker.Name = $"PlayerSticker_{playerId}";
        
        // Get player appearance from network data if available
        string expressionId = "smile";
        string hatId = "none";
        
        if (NetworkManager.Instance != null)
        {
            var player = NetworkManager.Instance.GetPlayerFromID(playerId);
            if (player != null)
            {
                expressionId = player.SyncExpressionId;
                hatId = player.SyncHatId;
                GD.Print($"[VotingTab] Got player data for {playerId}: {expressionId}, {hatId}");
            }
            else
            {
                GD.Print($"[VotingTab] No player data found for {playerId}, using defaults");
            }
        }
        else
        {
            GD.Print("[VotingTab] NetworkManager not ready, using default cosmetics");
        }
        
        GD.Print($"[VotingTab] Updating sticker with expression: {expressionId}, hat: {hatId}");
        sticker.UpdateSticker(playerId, playerColor, expressionId, hatId);

        // Calculate random position within the icon area
        var random = new Random();
        var containerWidth = Size.X - (87 * 2) - 6;  // Subtract left/right margins and border
        var containerHeight = Size.Y - 147 - 30 - 6;  // Subtract top/bottom margins and border
        float x = (float)random.NextDouble() * (containerWidth - 48);  // 48 is sticker width
        float y = (float)random.NextDouble() * (containerHeight - 48); // 48 is sticker height
        sticker.Position = new Vector2(x, y);
        
        // Start with zero scale
        sticker.SetScale(0);
        playerStickersContainer.AddChild(sticker);
        playerStickers[playerId] = sticker;

        // Animate sticker appearance
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Elastic);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sticker, "scale", Vector2.One, 0.5f);
    }

    public void RemovePlayerSticker(int playerId)
    {
        if (!playerStickers.TryGetValue(playerId, out var sticker) || !IsInstanceValid(sticker))
        {
            playerStickers.Remove(playerId);
            return;
        }

        // Immediately remove from dictionary to prevent re-entry
        playerStickers.Remove(playerId);
        
        // Quick fade out and shrink animation
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);
        
        // Animate scale and alpha simultaneously
        tween.TweenProperty(sticker, "scale", Vector2.Zero, 0.2f);
        tween.Parallel().TweenProperty(sticker, "modulate:a", 0.0f, 0.2f);
        
        // Clean up the node after animation
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(sticker))
            {
                sticker.QueueFree();
            }
        }));
    }

    public void ClearAllStickers()
    {
        foreach (var sticker in playerStickers.Values)
        {
            if (!IsInstanceValid(sticker)) continue;
            
            // Create a quick fade-out animation
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Sine);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sticker, "modulate:a", 0f, 0.2f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(sticker))
                {
                    sticker.QueueFree();
                }
            }));
        }
        playerStickers.Clear();
    }
}

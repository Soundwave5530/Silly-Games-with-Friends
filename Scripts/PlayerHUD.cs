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

    public override void _Ready()
    {
        if (Multiplayer.IsServer() && !NetworkManager.Instance.IsPlayerHost)
        {
            Visible = false;
            return;
        }
        Instance = this;
        active = true;
        InitializeHUD();
    }

    public override void _ExitTree()
    {
        active = false;
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
        face = CosmeticDatabase.Expressions[SettingsManager.CurrentSettings.SavedExpressionID];
        hat = CosmeticDatabase.Hats[SettingsManager.CurrentSettings.SavedHatID];
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


}

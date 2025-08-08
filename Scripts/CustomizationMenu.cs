using Godot;
using System;
using Godot.Collections;
using System.Collections.Generic;

public partial class CustomizationMenu : Control
{
    [Export] public TextureRect playerPreview;
    [Export] public TextureRect playerPreviewFace;
    [Export] public TextureRect playerPreviewHat;

    [Export] public CustomizeSelection characterSelector;
    [Export] public CustomizeSelection faceSelector;
    [Export] public CustomizeSelection hatSelector;

    [Export] public HSlider redSlider;
    [Export] public HSlider greenSlider;
    [Export] public HSlider blueSlider;

    [Export] public ColorRect background;
    [Export] public TextureRect scrollingBackground;

    [Export] public Label colorLabel;

    private CharacterTypePreset currentPreset;
    private FacialExpression currentFace;

    private Color playerColor;

    private int frameIndex = 0;
    private float frameTime = 0f;
    private float frameDelay = 0.15f;

    private int faceFrameIndex = 0;
    private float faceFrameTime = 0f;
    private float faceFrameDelay = 0.15f;

    private Cosmetic currentHat;
    private int hatFrameIndex = 0;
    private float hatFrameTime  = 0f;
    private float hatFrameDelay = 0.15f;


    private float hsvHue = 0f;

    private AnimationManager.PlayerAnimTypes currentAnimType = AnimationManager.PlayerAnimTypes.Idle;

    bool isEmoting = false;

    public override void _Ready()
    {
        Visible = false;

        var s = SettingsManager.CurrentSettings;
        Color playerColor = new Color(s.ColorR, s.ColorG, s.ColorB);

        redSlider.Value = playerColor.R;
        greenSlider.Value = playerColor.G;
        blueSlider.Value = playerColor.B;

        playerPreview.SelfModulate = playerColor;

        playerColor.ToHsv(out hsvHue, out _, out _);

        UpdateRGBColor();
        redSlider.ValueChanged += UpdateRGBColor;
        greenSlider.ValueChanged += UpdateRGBColor;
        blueSlider.ValueChanged += UpdateRGBColor;

        characterSelector.SelectionChanged += OnSelectionChanged;
        faceSelector.SelectionChanged += OnSelectionChanged;
        hatSelector.SelectionChanged += OnSelectionChanged;

        currentPreset = characterSelector.CurrentCharacter;
        currentFace = faceSelector.CurrentFace;
        currentHat = CosmeticDatabase.Hats[s.SavedHatID];

        VisibilityChanged += () =>
        {
            if (!Visible)
            {
                if (PlayerHUD.Instance != null && PlayerHUD.Instance.active)
                    PlayerHUD.Instance.RefreshFromSettings();

                SettingsManager.Save();
                return;
            }
            playerColor = SettingsManager.GetCurrentColorFromCurrentSettings();
            redSlider.Value = playerColor.R;
            greenSlider.Value = playerColor.G;
            blueSlider.Value = playerColor.B;

            playerPreview.SelfModulate = playerColor;

            if (currentHat != null && currentHat.modulatesWithPlayerColor)
            {
                playerPreviewHat.Modulate = playerColor;
            }
        };

        AdvanceHatFrame();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        if (currentPreset == null || currentFace == null) return;

        if (isEmoting)
        {
            frameDelay = 0.05f;
        }
        else
        {
            frameDelay = 0.2f;
        }

        frameTime += (float)delta;
        faceFrameTime += (float)delta;
        hatFrameTime += (float)delta;

        if (frameTime >= frameDelay)
        {
            frameTime = 0f;
            AdvanceFrame();
        }

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

        Vector2 offset = currentPreset.FaceOffsets[AnimationManager.AnimFacingType.Front][currentAnimType][frameIndex];
        var xOffset = offset.X * 200f;
        var yOffset = offset.Y * 200f;
        playerPreviewFace.Position = new Vector2(xOffset, -yOffset) - 32 * Vector2.One;

        var hatoff = currentPreset.HatOffsets[AnimationManager.AnimFacingType.Front][currentAnimType][frameIndex];
        var xOff = hatoff.X * 200f;
        var yOff = hatoff.Y * 200f;
        playerPreviewHat.Position = new Vector2(xOff, -yOff) - 32 * Vector2.One;
    }

    private void SetAnimation(AnimationManager.PlayerAnimTypes animType)
    {
        if (currentAnimType == animType) return;

        currentAnimType = animType;
        var frames = currentPreset.Animations[AnimationManager.AnimFacingType.Front].GetValueOrDefault(animType);
        if (frames == null || frames.Count == 0) return;

        frameIndex = 0;
        frameTime = 0f;
        playerPreview.Texture = frames[frameIndex];
    }

    private void AdvanceFrame()
    {
        var frames = currentPreset.Animations[AnimationManager.AnimFacingType.Front].GetValueOrDefault(currentAnimType);
        if (frames == null || frames.Count == 0) return;

        frameIndex = (frameIndex + 1) % frames.Count;
        playerPreview.Texture = frames[frameIndex];


        if (((frameIndex + 1) % frames.Count) == 0 && isEmoting)
        {
            SetAnimation(AnimationManager.PlayerAnimTypes.Idle);
            isEmoting = false;
            frameIndex = 0;
            return;
        }

        if (playerPreview.SelfModulate != playerColor) playerPreview.SelfModulate = playerColor;
    }

    private void AdvanceFaceFrame()
    {
        faceFrameIndex++;
        if (faceFrameIndex >= currentFace.FrontExpression.Count) faceFrameIndex = 0;

        playerPreviewFace.Texture = currentFace.FrontExpression[faceFrameIndex];
    }
    
    private void AdvanceHatFrame()
    {
        if (currentHat == null || currentHat.CosmeticID == "none")
        {
            playerPreviewHat.Texture = null;
            return;
        }

        var frames = currentHat.FrontSprites;
        if (frames == null || frames.Count == 0) return;

        hatFrameIndex = (hatFrameIndex + 1) % frames.Count;
        playerPreviewHat.Texture = frames[hatFrameIndex];
    }




    private void OnSelectionChanged(CustomizeSelection.SelectionType type, int index)
    {
        SetAnimation(AnimationManager.PlayerAnimTypes.Wave);
        isEmoting = true;
        switch (type)
        {
            case CustomizeSelection.SelectionType.Face:
                var newFace = faceSelector.CurrentFace;
                if (currentFace != newFace)
                {
                    currentFace = newFace;
                    faceFrameIndex = 0;
                    GD.Print("[CustomizationMenu] Face changed to: ", currentFace.ExpressionId.Capitalize());
                    SettingsManager.CurrentSettings.SavedExpressionID = currentFace.ExpressionId;
                    SettingsManager.Save();
                    if (PlayerHUD.Instance != null && PlayerHUD.Instance.active)
                        PlayerHUD.Instance.RefreshFromSettings();
                }
                break;

            case CustomizeSelection.SelectionType.Hat:
            
                var newHat = hatSelector.CurrentHat;

                if (currentHat != newHat)
                {
                    currentHat = newHat;
                    hatFrameIndex = 0;
                    GD.Print("[CustomizationMenu] Hat changed to: ", currentHat.CosmeticID.Capitalize());

                    SettingsManager.CurrentSettings.SavedHatID = currentHat.CosmeticID;
                    SettingsManager.Save();
                    if (PlayerHUD.Instance != null && PlayerHUD.Instance.active)
                        PlayerHUD.Instance.RefreshFromSettings();

                    if (currentHat.CosmeticID != "none")
                        playerPreviewHat.Texture = currentHat.FrontSprites[0];
                    else
                        playerPreviewHat.Texture = null;

                    if (currentHat.modulatesWithPlayerColor)
                    {
                        playerPreviewHat.Modulate = playerColor;
                    }
                    else
                    {
                        playerPreviewHat.Modulate = Colors.White;
                    }
                }
                break;
        }
    }

    public void UpdateRGBColor(double value = 0)
    {
        playerColor = new Color((float)redSlider.Value, (float)greenSlider.Value, (float)blueSlider.Value);
        playerPreview.SelfModulate = playerColor;

        playerColor.ToHsv(out hsvHue, out float sat, out float val);

        background.Color = Color.FromHsv(hsvHue, sat, Math.Max(0.2f, val - 0.3f), 1);
        scrollingBackground.Modulate = Color.FromHsv(hsvHue, sat, Math.Max(val + 0.1f, 0.1f), 0.54f);


        if (currentHat != null && currentHat.modulatesWithPlayerColor)
        {
            playerPreviewHat.Modulate = playerColor;
        }

        SettingsManager.CurrentSettings.ColorR = playerColor.R;
        SettingsManager.CurrentSettings.ColorG = playerColor.G;
        SettingsManager.CurrentSettings.ColorB = playerColor.B;

        SettingsManager.Save();

        if (Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected && !Multiplayer.IsServer())
            NetworkManager.Instance.RpcId(1, nameof(NetworkManager.Instance.RegisterColor), playerColor);
        else if (Multiplayer.IsServer() && NetworkManager.Instance.IsPlayerHost)
        {
            NetworkManager.Instance.RegisterColor(playerColor);
        }

    }
}

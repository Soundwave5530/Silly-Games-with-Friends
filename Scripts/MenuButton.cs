using Godot;
using System;

public partial class MenuButton : Button
{
    public static bool allowedHover = true;
    public Label buttonLabel;
    private Tween tween;

    [Export] private float baseHeight = 50f;
    [Export] private float hoverHeight = 100f;

    [Export] private int baseFontSize = 36;
    [Export] private int hoverFontSize = 68;

    [Export] private Color hoverColor = new Color(1, 0.8f, 0.2f);
    private Color baseColor = Colors.White;

    public override void _Ready()
    {
        buttonLabel = GetNode<Label>("Label");
        buttonLabel.LabelSettings = new();
        buttonLabel.LabelSettings.FontSize = baseFontSize;
        buttonLabel.LabelSettings.FontColor = baseColor;
        buttonLabel.LabelSettings.OutlineColor = new Color(0, 0.247f, 0.435f, 1);
        buttonLabel.LabelSettings.OutlineSize = 25;

        CustomMinimumSize = new Vector2(CustomMinimumSize.X, baseHeight);

        MouseEntered += () => AnimateTo(hoverHeight, hoverFontSize, hoverColor);
        MouseExited += () => AnimateTo(baseHeight, baseFontSize, baseColor);
    }

    private void AnimateTo(float targetHeight, int targetFontSize, Color targetColor)
    {
        // Kill old tween to prevent overlaps
        if (tween != null && tween.IsRunning() && tween.IsValid())
            tween.Kill();

        tween = CreateTween();

        // Animate button height
        tween.TweenProperty(this, "custom_minimum_size:y", targetHeight, 0.3)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        // Animate font size in parallel
        tween.Parallel().TweenMethod(Callable.From<float>((v) => {
            buttonLabel.LabelSettings.FontSize = Mathf.RoundToInt(v);
        }), buttonLabel.LabelSettings.FontSize, targetFontSize, 0.3)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Spring);

        // Animate font color in parallel
        tween.Parallel().TweenMethod(Callable.From<Color>((c) => {
            buttonLabel.LabelSettings.FontColor = c;
        }), buttonLabel.LabelSettings.FontColor, targetColor, 0.3)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
    }

}

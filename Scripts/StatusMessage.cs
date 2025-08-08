using Godot;
using System;

public partial class StatusMessage : Control
{
    [Export] public float PopTime = 0.2f;
    [Export] public float Duration = 3f;
    [Export] public float FadeTime = 1f;

    private Label label;
    private Tween tween;

    public override void _Ready()
    {
        label = GetNode<Label>("Label");
        label.LabelSettings = new();
        label.LabelSettings.FontSize = 50;
        label.LabelSettings.FontColor = Colors.White;
        label.LabelSettings.OutlineSize = 16;
        label.LabelSettings.OutlineColor = new Color(0, 0.247f, 0.435f, 1);

        label.Scale = Vector2.Zero;
        label.PivotOffset = label.Size / 2;
        CustomMinimumSize = new Vector2(1000, 42);
        Modulate = new Color(1, 1, 1, 1); 

        tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);

        tween.TweenProperty(label, "scale", Vector2.One, PopTime);

        tween.TweenProperty(this, "custom_minimum_size:y", 82, PopTime);

        tween.TweenInterval(Duration);
        tween.TweenProperty(this, "modulate:a", 0.0f, FadeTime);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
    
    public void SetMessageType(StatusMessageManager.MessageType type)
    {
        switch (type)
        {
            case StatusMessageManager.MessageType.Info:
                label.LabelSettings.FontColor = new Color(1f, 1f, 1f);
                label.LabelSettings.OutlineColor = new Color(0, 0.247f, 0.435f, 1);
                break;
            case StatusMessageManager.MessageType.Warning:
                label.LabelSettings.FontColor = new Color(1f, 1f, 0.36f);
                label.LabelSettings.OutlineColor = new Color(0.5f, 0.5f, 0.18f);
                break;
            case StatusMessageManager.MessageType.Error:
                label.LabelSettings.FontColor = new Color(1.0f, 0.2f, 0.2f);
                label.LabelSettings.OutlineColor = new Color(0.4f, 0.05f, 0.05f);
                break;
        }
    }

    public void SetText(string text)
    {
        label.Text = text;
    }
}

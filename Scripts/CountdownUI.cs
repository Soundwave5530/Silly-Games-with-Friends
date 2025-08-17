using Godot;
using System;

public partial class CountdownUI : Control
{
    private Label countdownLabel;
    private Timer countdownTimer;
    private int currentCount = 3;
    private Action onCountdownComplete;

    [Signal] public delegate void CountdownFinishedEventHandler();

    public override void _Ready()
    {
        // Set up the UI to be full screen and centered
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        
        // Create countdown label
        countdownLabel = new Label();
        countdownLabel.Text = currentCount.ToString();
        countdownLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        countdownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        countdownLabel.VerticalAlignment = VerticalAlignment.Center;
        
        // Style the label
        var labelSettings = new LabelSettings();
        labelSettings.Font = ThemeDB.FallbackFont;
        labelSettings.FontSize = 120;
        labelSettings.FontColor = Colors.White;
        labelSettings.OutlineSize = 8;
        labelSettings.OutlineColor = Colors.Black;
        countdownLabel.LabelSettings = labelSettings;
        
        AddChild(countdownLabel);
        
        // Create timer
        countdownTimer = new Timer();
        countdownTimer.WaitTime = 1.0f;
        countdownTimer.Timeout += OnCountdownTick;
        AddChild(countdownTimer);
        
        // Start hidden and at normal scale
        countdownLabel.Modulate = new Color(1, 1, 1, 0);
        countdownLabel.Scale = Vector2.One;
        countdownLabel.Rotation = 0;
    }

    public void StartCountdown(Action onComplete = null)
    {
        onCountdownComplete = onComplete;
        currentCount = 3;
        countdownLabel.Text = currentCount.ToString();
        
        // Set initial font size for numbers
        countdownLabel.LabelSettings.FontSize = 120;
        countdownLabel.LabelSettings.FontColor = Colors.White;
        
        // Play initial count animation
        PlayNumberAnimation();
        
        // Start the countdown timer
        countdownTimer.Start();
        
        GD.Print("[CountdownUI] Starting countdown...");
    }

    private void OnCountdownTick()
    {
        currentCount--;
        
        if (currentCount > 0)
        {
            countdownLabel.Text = currentCount.ToString();
            PlayNumberAnimation();
        }
        else
        {
            countdownLabel.Text = "GO!";
            countdownLabel.LabelSettings.FontSize = 140;
            countdownLabel.LabelSettings.FontColor = Colors.LimeGreen;
            PlayGoAnimation();
            
            // Stop timer
            countdownTimer.Stop();
            
            // Hide after showing GO!
            GetTree().CreateTimer(1.2f).Timeout += () => {
                EmitSignal(SignalName.CountdownFinished);
                onCountdownComplete?.Invoke();
                QueueFree();
            };
            
            GD.Print("[CountdownUI] Countdown complete!");
        }
    }

    private void PlayNumberAnimation()
    {
        // Random rotation between -30 and 30 degrees
        float randomRotation = Mathf.DegToRad((float)GD.RandRange(-30f, 30f));
        
        // Start from center, normal scale, invisible
        countdownLabel.Scale = Vector2.One;
        countdownLabel.Rotation = randomRotation;
        countdownLabel.Modulate = new Color(1, 1, 1, 0);
        
        var tween = CreateTween();
        tween.SetParallel(true);
        
        // Quick scale up with immediate visibility
        tween.TweenProperty(countdownLabel, "scale", new Vector2(1.8f, 1.8f), 0.15f)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Back);
        
        // Fade in quickly
        tween.TweenProperty(countdownLabel, "modulate:a", 1.0f, 0.1f);
        
        // Hold at large scale briefly, then scale back down
        tween.TweenProperty(countdownLabel, "scale", Vector2.One, 0.2f)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad)
             .SetDelay(0.3f);
        
        // Fade out towards the end
        tween.TweenProperty(countdownLabel, "modulate:a", 0.3f, 0.3f)
             .SetDelay(0.5f);
    }

    private void PlayGoAnimation()
    {
        // Random rotation for GO! as well
        float randomRotation = Mathf.DegToRad((float)GD.RandRange(-30f, 30f));
        
        // Start GO! even more dramatically
        countdownLabel.Scale = Vector2.One;
        countdownLabel.Rotation = randomRotation;
        countdownLabel.Modulate = new Color(1, 1, 1, 0);
        
        var tween = CreateTween();
        tween.SetParallel(true);
        
        // MASSIVE scale up for GO! 
        tween.TweenProperty(countdownLabel, "scale", new Vector2(2.5f, 2.5f), 0.2f)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Back);
        
        // Quick fade in
        tween.TweenProperty(countdownLabel, "modulate:a", 1.0f, 0.1f);
        
        // Hold at large scale longer for GO!
        tween.TweenProperty(countdownLabel, "scale", new Vector2(1.5f, 1.5f), 0.3f)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Bounce)
             .SetDelay(0.4f);
        
        // Fade out more gradually
        tween.TweenProperty(countdownLabel, "modulate:a", 0.0f, 0.5f)
             .SetDelay(0.7f);
    }

    // Method to cancel countdown if needed
    public void CancelCountdown()
    {
        countdownTimer.Stop();
        var tween = CreateTween();
        tween.TweenProperty(countdownLabel, "modulate:a", 0.0f, 0.3f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
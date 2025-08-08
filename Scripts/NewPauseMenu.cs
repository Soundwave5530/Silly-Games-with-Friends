using Godot;
using System;

public partial class NewPauseMenu : Control
{
    public static NewPauseMenu Instance { get; private set; }

    [ExportSubgroup("Main")]
    [Export] public MenuButton resumeButton;
    [Export] public MenuButton settingsButton;
    [Export] public MenuButton customizeButton;
    [Export] public MenuButton leaveGameButton;

    [ExportSubgroup("Settings")]
    [Export] private MenuButton settingsGeneralButton;
    [Export] private MenuButton settingsVideoButton;
    [Export] private MenuButton settingsAudioButton;
    [Export] private MenuButton settingsControlsButton;
    [Export] private MenuButton settingsBackButton;

    [ExportSubgroup("Customize")]
    [Export] private MenuButton customizeBackButton;

    private AnimationPlayer animationPlayer;

    public static bool IsOpen = false;
    public static bool customizationOpen = false;
    private bool transitioning = true;

    private PackedScene transitionScreenScene = GD.Load<PackedScene>("res://Scenes/TransitionScreen.tscn");
    private CustomizationMenu CustomizationMenu;
    private Panel settingsContainer;

    public override void _Ready()
    {
        Instance = this;

        Hide();
        animationPlayer = GetNode<AnimationPlayer>("MenuAnims");
        CustomizationMenu = GetParent().GetNode<CustomizationMenu>("Customization Menu");

        animationPlayer.AnimationFinished += AnimFinished;
        settingsContainer = GetParent().GetNode<Panel>("Settings/MarginContainer/Container");
        LoadSettingsPage(generalSettingsScene);

        resumeButton.Pressed += () => MenuButtonPressed(resumeButton);
        settingsButton.Pressed += () => MenuButtonPressed(settingsButton);
        customizeButton.Pressed += () => MenuButtonPressed(customizeButton);
        leaveGameButton.Pressed += () => MenuButtonPressed(leaveGameButton);

        settingsGeneralButton.Pressed += () => MenuButtonPressed(settingsGeneralButton);
        settingsVideoButton.Pressed += () => MenuButtonPressed(settingsVideoButton);
        settingsAudioButton.Pressed += () => MenuButtonPressed(settingsAudioButton);
        settingsControlsButton.Pressed += () => MenuButtonPressed(settingsControlsButton);
        settingsBackButton.Pressed += () => MenuButtonPressed(settingsBackButton);

        customizeBackButton.Pressed += () => MenuButtonPressed(customizeBackButton);

    }

    public void OpenMenu()
    {
        Visible = true;
        IsOpen = true;
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);
        animationPlayer.Play("Open");
        LoadSettingsPage(generalSettingsScene);
    }

    public void CloseMenu()
    {
        animationPlayer.Play("Close");
        CustomizationMenu.Visible = false;
        IsOpen = false;
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);

        ReleaseFocus();
        resumeButton.ReleaseFocus();
        settingsButton.ReleaseFocus();
        customizeButton.ReleaseFocus();
        leaveGameButton.ReleaseFocus();
        settingsGeneralButton.ReleaseFocus();
        settingsVideoButton.ReleaseFocus();
        settingsAudioButton.ReleaseFocus();
        settingsControlsButton.ReleaseFocus();
        settingsBackButton.ReleaseFocus();
        customizeBackButton.ReleaseFocus();
        settingsContainer.QueueFreeChildren();
    }


    public void OpenCustomizationMenu()
    {
        customizationOpen = true;
        GD.Print("Opening customization menu...");

        TransitionScreen transitionScreen = transitionScreenScene.Instantiate<TransitionScreen>();
        GetTree().Root.AddChild(transitionScreen);
        transitionScreen.TransitionInFinished += () =>
        {
            CustomizationMenu.Visible = true;
        };
    }

    public void MenuButtonPressed(MenuButton button)
    {
        if (button == resumeButton) OnResumeButtonPressed();
        else if (button == settingsButton) OnSettingsButtonPressed();
        else if (button == customizeButton) OnCustomizeButtonPressed();
        else if (button == leaveGameButton) OnLeaveGameButtonPressed();
        else if (button == settingsBackButton) OnBackButtonPressed();

        else if (button == settingsGeneralButton) OnSettingsGeneralButtonPressed();
        else if (button == settingsVideoButton) OnSettingsVideoButtonPressed();
        else if (button == settingsAudioButton) OnSettingsAudioButtonPressed();
        else if (button == settingsControlsButton) OnSettingsControlsButtonPressed();
        else if (button == customizeBackButton) OnCustomizeBackButtonPressed();
    }

    private Timer CreateTimer(float waitTime)
    {
        Timer newTimer = new() { WaitTime = 0.5f, Autostart = false };
        AddChild(newTimer);
        newTimer.Start();
        return newTimer;
    }

    private void AnimFinished(StringName animName)
    {
        transitioning = false;
    }

    public void OnResumeButtonPressed()
    {
        CloseMenu();
        transitioning = true;
        Timer timer = CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            timer.QueueFree();
        };
    }
    public void OnSettingsButtonPressed()
    {
        GD.Print("Settings button pressed. Opening settings...");
        animationPlayer.Play("Open Settings");
        transitioning = true;
    }
    public async void OnCustomizeButtonPressed()
    {
        GD.Print("Customize button pressed. Opening customization menu...");

        TransitionScreen transitionScreen = GD.Load<PackedScene>("res://Scenes/TransitionScreen.tscn").Instantiate() as TransitionScreen;
        AddChild(transitionScreen);

        await ToSignal(GetTree().CreateTimer(0.5), "timeout");

        CustomizationMenu.Visible = true;
    }

    private async void OnCustomizeBackButtonPressed()
    {
        TransitionScreen transitionScreen = GD.Load<PackedScene>("res://Scenes/TransitionScreen.tscn").Instantiate() as TransitionScreen;
        AddChild(transitionScreen);

        await ToSignal(GetTree().CreateTimer(0.5), "timeout");

        CustomizationMenu.Visible = false;
        customizationOpen = false;
    }

    public async void OnLeaveGameButtonPressed()
    {
        CloseMenu();
        await ToSignal(GetTree().CreateTimer(0.1), "timeout");
        NetworkManager.Instance.DisconnectFromServer();
    }


    public void OnBackButtonPressed()
    {
        animationPlayer.Play("Close Settings");
        transitioning = true;
    }

    private PackedScene generalSettingsScene = GD.Load<PackedScene>("res://Scenes/GeneralSettings.tscn");
    private PackedScene videoSettingsScene = GD.Load<PackedScene>("res://Scenes/VideoSettings.tscn");
    private PackedScene audioSettingsScene = GD.Load<PackedScene>("res://Scenes/AudioSettings.tscn");
    private PackedScene controlsSettingsScene = GD.Load<PackedScene>("res://Scenes/Keybindings.tscn");

    private void LoadSettingsPage(PackedScene scene)
    {
        settingsContainer.QueueFreeChildren();
        var page = scene.Instantiate<Control>();
        settingsContainer.AddChild(page);
    }

    private void OnSettingsGeneralButtonPressed()
    {
        LoadSettingsPage(generalSettingsScene);
    }

    private void OnSettingsVideoButtonPressed()
    {
        LoadSettingsPage(videoSettingsScene);
    }

    private void OnSettingsAudioButtonPressed()
    {
        LoadSettingsPage(audioSettingsScene);
    }

    private void OnSettingsControlsButtonPressed()
    {
        LoadSettingsPage(controlsSettingsScene);
    }
}

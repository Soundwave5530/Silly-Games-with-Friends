using Godot;
using System;

public partial class MenuUI : Control
{
    public enum MenuType
    {
        MainMenu,
        PlayMenu,
        SettingsMenu,
        CustomizeMenu
    }

    private MenuType currentMenuType = MenuType.MainMenu;

    [ExportGroup("Buttons")]
    [ExportSubgroup("Main Menu")]
    [Export] private MenuButton playButton;
    [Export] private MenuButton settingsButton;
    [Export] private MenuButton customizeButton;
    [Export] private MenuButton exitButton;
    [Export] private MenuButton creditsButton;
    [ExportSubgroup("Play Menu")]
    [Export] private MenuButton hostAsPlayerButton;
    [Export] private MenuButton hostAsDedicatedServerButton;
    [Export] private MenuButton joinButton;
    [Export] private MenuButton backButton;
    [ExportSubgroup("Server List")]
    [Export] private MenuButton addServerButton;
    [Export] private MenuButton refreshServerButton;
    [Export] private MenuButton deleteAllServersButton;
    [Export] private MenuButton backfromServerButton;
    [ExportSubgroup("Settings")]
    [Export] private MenuButton settingsGeneralButton;
    [Export] private MenuButton settingsVideoButton;
    [Export] private MenuButton settingsAudioButton;
    [Export] private MenuButton settingsControlsButton;
    [Export] private MenuButton settingsBackButton;
    [ExportSubgroup("Customization")]
    [Export] private MenuButton customizationBackButton;
    [ExportSubgroup("Extras")]
    [Export] private Label versionLabel;

    private Control imageContainer;
    private TextureRect currentBGTexture;
    private TextureRect nextBGTexture;
    private Panel menuPanel;
    private VBoxContainer buttonContainer;
    private Label titleLabel;

    private VBoxContainer serverListButtonContainer;
    private Label serverListTitleLabel;
    private MarginContainer serverContainer;
    private Panel settingsContainer;
    private Control customizationMenu;
    private VBoxContainer settingsButtonContainer;
    private Label settingsTitle;

    private Texture2D playImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Play.png");
    private Texture2D settingsImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Settings.png");
    private Texture2D customizeImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Customize.png");
    private Texture2D exitImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Exit.png");
    private Texture2D creditsImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Credits.png");
    private PackedScene ServerEntryScene = GD.Load<PackedScene>("res://Scenes/ServerUI.tscn");

    private Texture2D hostImage;
    private Texture2D joinImage;

    private AnimationPlayer animationPlayer;

    private Texture2D targetImage = null;
    private float fadeSpeed = 5.0f;
    private bool isFading = false;
    private bool transitioning = true;
    private bool serverListOpen = false;
    private bool deleting = false;

    public override void _Ready()
    {

        versionLabel.Text = NetworkManager.GAMEVERSION;
        
        hostImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Play Host.png");
        joinImage = GD.Load<Texture2D>("res://Assets/Sprites/Menus/Play Play.png");

        buttonContainer = GetNode<VBoxContainer>("ButtonContainer");
        titleLabel = GetNode<Label>("Title");
        animationPlayer = GetParent().GetNode<AnimationPlayer>("MenuAnimations");

        serverListButtonContainer = GetParent().GetNode<VBoxContainer>("ServerListBG/ButtonContainer");
        serverListTitleLabel = GetParent().GetNode<Label>("ServerListBG/Title");
        serverContainer = GetParent().GetNode<MarginContainer>("ServerListBG/MarginContainer");
        customizationMenu = GetParent().GetNode<Control>("Customization Menu");

        imageContainer = GetNode<Control>("/root/MainMenu/ImageContainer");
        currentBGTexture = GetNode<TextureRect>("/root/MainMenu/ImageContainer/Current");
        nextBGTexture = GetNode<TextureRect>("/root/MainMenu/ImageContainer/Next");
        nextBGTexture.Modulate = new Color(1, 1, 1, 0);

        menuPanel = GetNode<Panel>("/root/MainMenu/BG2");
        settingsContainer = GetParent().GetNode<Panel>("Settings/MarginContainer/Container");
        settingsButtonContainer = GetParent().GetNode<VBoxContainer>("Settings/ButtonContainer");
        settingsTitle = GetParent().GetNode<Label>("Settings/Title");

        playButton.MouseEntered += () => StartFade(playImage);
        settingsButton.MouseEntered += () => StartFade(settingsImage);
        customizeButton.MouseEntered += () => StartFade(customizeImage);
        exitButton.MouseEntered += () => StartFade(exitImage);
        hostAsPlayerButton.MouseEntered += () => StartFade(hostImage);
        hostAsDedicatedServerButton.MouseEntered += () => StartFade(hostImage);
        joinButton.MouseEntered += () => StartFade(joinImage);
        backButton.MouseEntered += () => StartFade(exitImage);
        creditsButton.MouseEntered += () => StartFade(creditsImage);

        playButton.Pressed += () => MenuButtonPressed(playButton);
        settingsButton.Pressed += () => MenuButtonPressed(settingsButton);
        customizeButton.Pressed += () => MenuButtonPressed(customizeButton);
        exitButton.Pressed += () => MenuButtonPressed(exitButton);
        hostAsPlayerButton.Pressed += () => MenuButtonPressed(hostAsPlayerButton);
        hostAsDedicatedServerButton.Pressed += () => MenuButtonPressed(hostAsDedicatedServerButton);
        joinButton.Pressed += () => MenuButtonPressed(joinButton);
        backButton.Pressed += () => MenuButtonPressed(backButton);

        addServerButton.Pressed += () => MenuButtonPressed(addServerButton);
        refreshServerButton.Pressed += () => MenuButtonPressed(refreshServerButton);
        deleteAllServersButton.Pressed += () => MenuButtonPressed(deleteAllServersButton);
        backfromServerButton.Pressed += () => MenuButtonPressed(backfromServerButton);

        settingsGeneralButton.Pressed += () => MenuButtonPressed(settingsGeneralButton);
        settingsVideoButton.Pressed += () => MenuButtonPressed(settingsVideoButton);
        settingsAudioButton.Pressed += () => MenuButtonPressed(settingsAudioButton);
        settingsControlsButton.Pressed += () => MenuButtonPressed(settingsControlsButton);
        settingsBackButton.Pressed += () => MenuButtonPressed(settingsBackButton);

        customizationBackButton.Pressed += () => MenuButtonPressed(customizationBackButton);

        animationPlayer.AnimationFinished += AnimFinished;
        LoadSettingsPage(generalSettingsScene);

        hostAsPlayerButton.Visible = false;
        hostAsDedicatedServerButton.Visible = false;
        joinButton.Visible = false;
        backButton.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!transitioning)
        {
            Vector2 mousePosition = GetViewport().GetMousePosition();
            Vector2 offset = mousePosition * 0.015f;

            if (!serverListOpen && (currentMenuType != MenuType.SettingsMenu))
            {
                imageContainer.Position = new Vector2(450, 0) + new Vector2(-offset.X, -offset.Y);
                menuPanel.Position = new Vector2(-273, -465) - new Vector2(-offset.X, -offset.Y);
                buttonContainer.Position = new Vector2(79, 372) - new Vector2(-offset.X, -offset.Y);
                titleLabel.Position = new Vector2(76, 44) - new Vector2(-offset.X, -offset.Y);
            }
            else if (serverListOpen)
            {
                serverListButtonContainer.Position = new Vector2(51, 528) - new Vector2(-offset.X, -offset.Y);
                serverListTitleLabel.Position = new Vector2(36, 49) - new Vector2(-offset.X, -offset.Y);
                serverContainer.Position = new Vector2(526, 42) + new Vector2(-offset.X, -offset.Y);
            }
            else if (currentMenuType == MenuType.SettingsMenu)
            {
                // Apply parallax to settings container in the same style as other menus
                settingsContainer.GetParent<Control>().Position = new Vector2(526, 42) + new Vector2(-offset.X, -offset.Y);
                settingsButtonContainer.Position = new Vector2(51, 468) - new Vector2(-offset.X, -offset.Y);
                settingsTitle.Position = new Vector2(36, 49) - new Vector2(-offset.X, -offset.Y);
            }
        }

        UpdateBackgroundImages();

        float lerpSpeed = 10f * (float)delta;
    }

    private void StartFade(Texture2D newImage)
    {
        if (currentMenuType == MenuType.SettingsMenu) return;
        if (!MenuButton.allowedHover) return;
        if (currentBGTexture.Texture == newImage) return;

        currentBGTexture.Modulate = new Color(1, 1, 1, 1);
        nextBGTexture.Modulate = new Color(1, 1, 1, 0);
        nextBGTexture.Texture = newImage;
        targetImage = newImage;
        isFading = true;
    }

    public void MenuButtonPressed(MenuButton button)
    {
        if (button == playButton) OnPlayButtonPressed();
        else if (button == settingsButton) OnSettingsButtonPressed();
        else if (button == customizeButton) OnCustomizeButtonPressed();
        else if (button == exitButton) OnExitButtonPressed();

        else if (button == backButton || button == settingsBackButton) OnBackButtonPressed();

        else if (button == joinButton) OnJoinButtonPressed();
        else if (button == backfromServerButton) OnBackFromServerButtonPressed();
        else if (button == addServerButton) OnAddServerButtonPressed();
        else if (button == hostAsPlayerButton) OnHostAsPlayerButtonPressed();
        else if (button == hostAsDedicatedServerButton) OnHostAsDedicatedServerButtonPressed();
        else if (button == refreshServerButton) OnRefreshButtonPressed();
        else if (button == deleteAllServersButton) OnDeleteAllServersButtonPressed();

        else if (button == settingsGeneralButton) OnSettingsGeneralButtonPressed();
        else if (button == settingsVideoButton) OnSettingsVideoButtonPressed();
        else if (button == settingsAudioButton) OnSettingsAudioButtonPressed();
        else if (button == settingsControlsButton) OnSettingsControlsButtonPressed();

        else if (button == customizationBackButton) OnCustomizeBackButtonPressed();
    }

    private void UpdateBackgroundImages()
    {
        if (!isFading || targetImage == null)
            return;

        float alphaDelta = (float)fadeSpeed * (float)GetProcessDeltaTime();

        float nextAlpha = Mathf.Min(nextBGTexture.Modulate.A + alphaDelta, 1.0f);
        nextBGTexture.Modulate = new Color(1, 1, 1, nextAlpha);

        float currentAlpha = Mathf.Max(currentBGTexture.Modulate.A - alphaDelta, 0.0f);
        currentBGTexture.Modulate = new Color(1, 1, 1, currentAlpha);

        if (nextAlpha >= 1.0f)
        {
            currentBGTexture.Texture = targetImage;
            currentBGTexture.Modulate = new Color(1, 1, 1, 1);
            nextBGTexture.Modulate = new Color(1, 1, 1, 0);
            isFading = false;
            targetImage = null;
        }
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


    public void OnPlayButtonPressed()
    {
        animationPlayer.Play("Swap Menu");
        transitioning = true;
        Timer timer = CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            currentMenuType = MenuType.PlayMenu;
            ToggleMainMenuButtonVisibility(false);
            TogglePlayMenuButtonVisibility(true);
            backButton.Show();
            titleLabel.Text = "Host or join?";

            timer.QueueFree();
        };
    }
    public void OnSettingsButtonPressed()
    {
        GD.Print("Settings button pressed. Opening settings...");
        animationPlayer.Play("Open Settings");
        transitioning = true;

        Timer timer = CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            currentMenuType = MenuType.SettingsMenu;
            timer.QueueFree();
        };
    }

    public async void OnCustomizeButtonPressed()
    {
        GD.Print("Customize button pressed. Opening customization menu...");
        currentMenuType = MenuType.CustomizeMenu;

        TransitionScreen transitionScreen = GD.Load<PackedScene>("res://Scenes/TransitionScreen.tscn").Instantiate() as TransitionScreen;
        AddChild(transitionScreen);

        await ToSignal(GetTree().CreateTimer(0.5), "timeout");

        customizationMenu.Visible = true;
    }

    public void OnExitButtonPressed()
    {
        GD.Print("Exit button pressed. Exiting game...");
        GetTree().Quit();
    }
    public async void OnJoinButtonPressed()
    {
        animationPlayer.Play("Open Server List");
        transitioning = true;
        serverListOpen = true;
        GetParent().GetNode<ServerList>("ServerListBG/MarginContainer/ScrollContainer/VBoxContainer").RefreshTimer.Start();
        await GetParent().GetNode<ServerList>("ServerListBG/MarginContainer/ScrollContainer/VBoxContainer").RefreshAllServers();

    }
    public void OnBackButtonPressed()
    {
        if (currentMenuType == MenuType.SettingsMenu) animationPlayer.Play("Close Settings");
        else animationPlayer.Play("Swap Menu");

        transitioning = true;
        Timer timer = CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            currentMenuType = MenuType.MainMenu;
            ToggleMainMenuButtonVisibility(true);
            TogglePlayMenuButtonVisibility(false);
            backButton.Hide();
            titleLabel.Text = "Silly Games\n       with Friends";

            timer.QueueFree();
        };
    }
    private void OnBackFromServerButtonPressed()
    {
        animationPlayer.Play("Close Server List");
        transitioning = true;
        serverListOpen = false;
    }
    private void OnAddServerButtonPressed()
    {
        var newServer = (ServerEntryDisplay)ServerEntryScene.Instantiate();
        var list = GetParent().GetNode<ServerList>("ServerListBG/MarginContainer/ScrollContainer/VBoxContainer");
        list.AddChild(newServer);

        list.SaveServerList();
    }

    private void OnHostAsDedicatedServerButtonPressed()
    {
        NetworkManager.Instance.StartServer(isDedicated: true);
        GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
    }
    public void OnHostAsPlayerButtonPressed()
    {
        NetworkManager.Instance.StartServer(isDedicated: false);
        GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
    }

    private async void OnRefreshButtonPressed()
    {
        await GetParent().GetNode<ServerList>("ServerListBG/MarginContainer/ScrollContainer/VBoxContainer").RefreshAllServers();
    }

    private PackedScene generalSettingsScene = GD.Load<PackedScene>("res://Scenes/GeneralSettings.tscn");
    private PackedScene videoSettingsScene = GD.Load<PackedScene>("res://Scenes/VideoSettings.tscn");
    private PackedScene audioSettingsScene = GD.Load<PackedScene>("res://Scenes/AudioSettings.tscn");
    private PackedScene controlsSettingsScene = GD.Load<PackedScene>("res://Scenes/Keybindings.tscn");

    private void LoadSettingsPage(PackedScene scene)
    {
        foreach (var child in settingsContainer.GetChildren()) child.QueueFree();
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


    private void ToggleMainMenuButtonVisibility(bool visibility)
    {
        playButton.Visible = visibility;
        settingsButton.Visible = visibility;
        customizeButton.Visible = visibility;
        exitButton.Visible = visibility;
        creditsButton.Visible = visibility;
    }

    private void TogglePlayMenuButtonVisibility(bool visibility)
    {
        hostAsPlayerButton.Visible = visibility;
        hostAsDedicatedServerButton.Visible = visibility;
        joinButton.Visible = visibility;
    }

    private async void OnDeleteAllServersButtonPressed()
    {
        deleting = !deleting;
        deleteAllServersButton.buttonLabel.Text = deleting ? "CANCEL" : "DELETE ALL";

        if (!deleting)
        {
            StatusMessageManager.Instance.ShowMessage("Successfully cancelled.", StatusMessageManager.MessageType.Info);
            return;
        }
        
        StatusMessageManager.Instance.ShowMessage("WARNING: All saved servers will be deleted in...", StatusMessageManager.MessageType.Warning);
        await ToSignal(GetTree().CreateTimer(1), "timeout");
        if (!deleting) return;
        StatusMessageManager.Instance.ShowMessage("3", StatusMessageManager.MessageType.Warning);
        await ToSignal(GetTree().CreateTimer(1), "timeout");
        if (!deleting) return;
        StatusMessageManager.Instance.ShowMessage("2", StatusMessageManager.MessageType.Warning);
        await ToSignal(GetTree().CreateTimer(1), "timeout");
        if (!deleting) return;
        StatusMessageManager.Instance.ShowMessage("1", StatusMessageManager.MessageType.Warning);
        await ToSignal(GetTree().CreateTimer(1), "timeout");
        if (!deleting) return;

        serverContainer.GetNode<ServerList>("ScrollContainer/VBoxContainer").RemoveAllServers();

        StatusMessageManager.Instance.ShowMessage("All saved servers successfully deleted!", StatusMessageManager.MessageType.Info);
        
        deleting = false;
        deleteAllServersButton.buttonLabel.Text = "DELETE ALL";
    }

    private async void OnCustomizeBackButtonPressed()
    {
        currentMenuType = MenuType.MainMenu;

        TransitionScreen transitionScreen = GD.Load<PackedScene>("res://Scenes/TransitionScreen.tscn").Instantiate() as TransitionScreen;
        AddChild(transitionScreen);

        await ToSignal(GetTree().CreateTimer(0.5), "timeout");

        customizationMenu.Visible = false;
    }
}

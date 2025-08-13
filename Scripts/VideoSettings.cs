using Godot;
using System;

public partial class VideoSettings : Control
{
    [Export] public BetterCheckButton VSyncButton;
    [Export] public OptionButton FPSCapButton;
    [Export] public OptionButton WindowModeButton;
    [Export] public OptionButton ResolutionButton;


    public enum DisplayMode
    {
        Windowed = 0,
        Borderless = 1,
        Fullscreen = 2
    }

    public static readonly int[] FpsOptions = { 0, 144, 90, 75, 60, 30 };
    // 0 is Unlimited

    public static readonly Vector2I[] SupportedResolutions =
    {
        new(1920, 1080),
        new(1600, 900),
        new(1366, 768),
        new(1280, 720),
        new(1024, 576),
        new(800, 600)
    };

    public override void _Ready()
    {
        LoadSettings();

        WindowModeButton.ItemSelected += (index) =>
        {
            SettingsManager.CurrentSettings.DisplayModeIndex = (int)index;
            ApplyVideo((DisplayMode)index);
            SettingsManager.Save();
        };

        FPSCapButton.ItemSelected += (index) =>
        {
            SettingsManager.CurrentSettings.FpsCapIndex = (int)index;
            ApplyFpsCap(FpsOptions[index]);
            SettingsManager.Save();
        };

        ResolutionButton.ItemSelected += (index) =>
        {
            SettingsManager.CurrentSettings.ResolutionIndex = (int)index;
            ApplyResolution(SupportedResolutions[index]);
            SettingsManager.Save();
        };

        VSyncButton.Toggled += pressed =>
        {
            SettingsManager.CurrentSettings.VSync = pressed;
            ApplyVSync(pressed);
            FPSCapButton.Disabled = pressed;

            if (pressed)
            {
                ApplyFpsCap(0);
                FPSCapButton.Select(0);
                Input.UseAccumulatedInput = false;
            }
            else
            {
                FPSCapButton.Select(SettingsManager.CurrentSettings.FpsCapIndex);
                ApplyFpsCap(FpsOptions[SettingsManager.CurrentSettings.FpsCapIndex]);
                Input.UseAccumulatedInput = true;
            }
            
            SettingsManager.Save();
        };
    }

    public void LoadSettings()
    {
        WindowModeButton.Select(SettingsManager.CurrentSettings.DisplayModeIndex);


        foreach (var fps in FpsOptions)
        {
            string label = fps == 0 ? "Unlimited" : $"{fps} FPS";
            FPSCapButton.AddItem(label);
        }

        if (!SettingsManager.CurrentSettings.VSync) FPSCapButton.Select(SettingsManager.CurrentSettings.FpsCapIndex);


        for (int i = 0; i < SupportedResolutions.Length; i++)
        {
            var res = SupportedResolutions[i];
            ResolutionButton.AddItem($"{res.X} x {res.Y}");
        }

        ResolutionButton.Select(SettingsManager.CurrentSettings.ResolutionIndex);

        VSyncButton.ButtonPressed = SettingsManager.CurrentSettings.VSync;
        FPSCapButton.Disabled = SettingsManager.CurrentSettings.VSync;
    }

    void ApplyFpsCap(int fps)
    {
        Engine.MaxFps = fps == 0 ? 0 : fps;
    }

    void ApplyVSync(bool enabled)
    {
        DisplayServer.WindowSetVsyncMode(enabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        FPSCapButton.Select(0);
    }

    void ApplyVideo(DisplayMode displayMode)
    {
        var s = SettingsManager.CurrentSettings;
        switch (displayMode)
        {
            case VideoSettings.DisplayMode.Windowed:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                break;
            case VideoSettings.DisplayMode.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                break;
            case VideoSettings.DisplayMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
        }
    }
    
    void ApplyResolution(Vector2I res)
    {
        ProjectSettings.SetSetting("display/window/size/viewport_width", res.X);
        ProjectSettings.SetSetting("display/window/size/viewport_height", res.Y);

        DisplayServer.WindowSetSize(res);
    }
}

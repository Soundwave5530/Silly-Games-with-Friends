using System;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// This Class handles all operations that are required upon booting the application. An `initialization` class basically.
/// </summary>
public partial class BootManager : Node
{
    public static BootManager Instance { get; private set; }

    public Array<string> titles = new()
    {
        "Silly Games with Friends",
        "Silly Games with Friends: Silly Edition",
        "Silly Games with Friends: Andrew's New Game",
        "Silly Games with Friends: Networking at its finest",
        "Silly Games with Friends: Unemployed edition",
        "Silly Games with Friends: Get a Job Edition",
        "Silly Games with Friends: 2025 Edition",
        "Silly Games with Friends: The Silliest Around!",
        "Silly Games with Friends: Bug Simulator",
        "Silly Games with Friends: Made in Godot!",
        "Silly Games with Friends"
    };

    public override void _Ready()
    {
        Instance = this;
        int idx = GD.RandRange(0, titles.Count - 1);
        GetTree().Root.Title = titles[idx];
        GD.Print("[BootManager] Applying Window Title: " + titles[idx]);


        CosmeticDatabase.LoadAll();
        GD.Print("[BootManager] Cosmetic Database loaded");

        GD.Print("[BootManager] Loading Settings");
        SettingsManager.Load();

        GD.Print("[BootManager] Applying Settings");
        ApplySettingsOnBoot();

        GD.Print("[BootManager] Applying Keybinds from Settings");
        ApplySavedKeybinds();

        PlayerData.CurrentCharacter = CosmeticDatabase.Characters.First();

        if (SettingsManager.CurrentSettings.SavedHatID == "")
        {
            SettingsManager.CurrentSettings.SavedHatID = "none";
            SettingsManager.Save();
        }
    }

    void ApplySettingsOnBoot()
    {
        UserSettings us = SettingsManager.CurrentSettings;

        switch ((VideoSettings.DisplayMode)us.DisplayModeIndex)
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

        DisplayServer.WindowSetSize(VideoSettings.SupportedResolutions[us.ResolutionIndex]);

        DisplayServer.WindowSetVsyncMode(us.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

        Input.UseAccumulatedInput = !SettingsManager.CurrentSettings.VSync;

        GD.Print("[BootManager] Applied Settings");
    }

    void ApplySavedKeybinds()
    {
        foreach (var kv in SettingsManager.CurrentSettings.SavedKeybinds)
        {
            var action = kv.Key;
            var rec = kv.Value;

            InputMap.ActionEraseEvents(action);

            if (rec.IsMouse)
            {
                var me = new InputEventMouseButton { ButtonIndex = (MouseButton)rec.KeyCode };
                InputMap.ActionAddEvent(action, me);
            }
            else
            {
                var ke = new InputEventKey
                {
                    PhysicalKeycode = (Key)rec.KeyCode,
                    Keycode = (Key)rec.KeyCode,
                    ShiftPressed = rec.Shift,
                    CtrlPressed = rec.Ctrl,
                    AltPressed = rec.Alt,
                    MetaPressed = rec.Meta
                };
                InputMap.ActionAddEvent(action, ke);
            }
        }
        
        GD.Print("[BootManager] Keybinds from settings successful.");
    }



}
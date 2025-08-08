using System;
using Godot;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Drawing;


[Serializable]
public class KeybindRecord
{
    public bool IsMouse; // true = mouse button, false = keyboard
    public int KeyCode; // Key (as int) or MouseButton index
    public bool Shift;
    public bool Ctrl;
    public bool Alt;
    public bool Meta;
}


[Serializable]
public class UserSettings
{
    // Server List
    [JsonInclude]
    public List<ServerEntry> SavedServers { get; set; } = new();

    // Keybinds
    public Dictionary<string, KeybindRecord> SavedKeybinds { get; set; } = new();

    // General Settings
    public string Username { get; set; } = "Player";
    public float MouseSensitivity { get; set; } = 1f;
    public int FOV { get; set; } = 90;
    public bool CameraSmoothing { get; set; } = false;
    public bool UseSystemMouse { get; set; } = false;
    public float CustomMouseScale { get; set; } = 2f;

    // Video Settings
    public bool VSync { get; set; } = false;
    public int FpsCapIndex { get; set; } = 0; // 0 = Unlimited
    public int DisplayModeIndex { get; set; } = (int)VideoSettings.DisplayMode.Windowed;
    public int ResolutionIndex { get; set; } = 0; // Default to 1920x1080

    // Audio Settings
    public int MasterVolume { get; set; } = 100;
    public int MusicVolume { get; set; } = 100;
    public int SfxVolume { get; set; } = 100;
    public string OutputDevice { get; set; } = "Default";

    // Customization
    public string SavedHatID { get; set; } = "none";
    public string SavedExpressionID { get; set; } = "face";
    public float ColorR { get; set; } = 1;
    public float ColorG { get; set; } = 1;
    public float ColorB { get; set; } = 1;
}


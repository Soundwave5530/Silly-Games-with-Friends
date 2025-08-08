using Godot;
using System;
using System.Text.Json;

public static class SettingsManager
{
    private const string SavePath = "user://user_settings.json";

    public static UserSettings CurrentSettings = new();

    public static void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true
            };

            string json = JsonSerializer.Serialize(CurrentSettings, options);
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            file.StoreString(json);
            GD.Print("[SettingsManager] Settings saved.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SettingsManager] Failed to save settings: {e.Message}");
        }
    }

    public static void Load()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            GD.Print("[SettingsManager] No settings file found, using defaults.");
            CurrentSettings = new UserSettings();
            return;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                IncludeFields = true      // ← important!
            };

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            // Pass the options here:
            CurrentSettings = JsonSerializer.Deserialize<UserSettings>(json, options)
                            ?? new UserSettings();

            GD.Print("[SettingsManager] \tSettings loaded.");
            GD.Print(json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SettingsManager] Failed to load settings: {e.Message}");
            CurrentSettings = new UserSettings();
        }
    }

    public static string GetKeyNameFromInputMap(string actionName)
    {
        var events = InputMap.ActionGetEvents(actionName);
        if (events.Count == 0)
            return "Unbound";

        var inputEvent = events[0];

        if (inputEvent is InputEventKey keyEvent)
        {
            return OS.GetKeycodeString((Key)keyEvent.Keycode);
        }
        else if (inputEvent is InputEventMouseButton mouseEvent)
        {
            return mouseEvent.ButtonIndex.ToString();
        }

        return "Unknown";
    }

    public static Color GetCurrentColorFromCurrentSettings()
    {
        return new Color(CurrentSettings.ColorR, CurrentSettings.ColorG, CurrentSettings.ColorB);
    }
}

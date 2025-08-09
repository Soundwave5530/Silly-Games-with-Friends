using Godot;
using System;
using System.Xml.Schema;

public partial class GeneralSettings : Control
{
    [Export] public LineEdit NameField;
    [Export] public HSlider SensitivitySlider;
    [Export] public HSlider FOVSlider;
    [Export] public BetterCheckButton CameraSmoothingButton;
    [Export] public BetterCheckButton UseSystemMouse;
    [Export] public HSlider MouseScaleSlider;
    [Export] public MenuButton ResetSettingsButton;

    [Export] public Label CameraSensitivityLabel;
    [Export] public Label FOVLabel;
    [Export] public Label MouseScaleLabel;

    public override void _Ready()
    {
        LoadSettings();

        NameField.TextSubmitted += newName =>
        {
            if (NameField.Text.Length == 0 || NameField.Text.ToLower() == "0 characters" || NameField.Text.ToLower() == "zero characters" || NameField.Text.ToLower() == "zerocharacters")
            {
                NameField.Text = SettingsManager.CurrentSettings.Username;
                StatusMessageManager.Instance.ShowMessage("Error: Name cannot be 0 characters!", StatusMessageManager.MessageType.Error);
                return;
            }
            SettingsManager.CurrentSettings.Username = newName;
            if (Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected && !Multiplayer.IsServer())
                NetworkManager.Instance.RpcId(1, nameof(NetworkManager.Instance.RegisterName), newName, true);
            SettingsManager.Save();

            if (PlayerHUD.Instance != null && PlayerHUD.Instance.active)
                PlayerHUD.Instance.RefreshFromSettings();
            
            StatusMessageManager.Instance.ShowMessage("Successfully changed name to: " + newName, StatusMessageManager.MessageType.Info);
        };

        SensitivitySlider.ValueChanged += newValue =>
        {
            decimal newValueR = Math.Round((decimal)newValue, 1);
            SettingsManager.CurrentSettings.MouseSensitivity = (float)newValueR;
            CameraSensitivityLabel.Text = $"{newValueR:0.0} ";
            SettingsManager.Save();
        };

        FOVSlider.ValueChanged += newValue =>
        {
            SettingsManager.CurrentSettings.FOV = (int)newValue;
            FOVLabel.Text = newValue.ToString() + " ";
            if (Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected && !Multiplayer.IsServer())
                GetViewport().GetCamera3D().Fov = (float)newValue;
            SettingsManager.Save();
        };

        CameraSmoothingButton.Toggled += pressed =>
        {
            SettingsManager.CurrentSettings.CameraSmoothing = pressed;
            SettingsManager.Save();
        };

        UseSystemMouse.Toggled += pressed =>
        {
            SettingsManager.CurrentSettings.UseSystemMouse = pressed;
            MouseManager.Instance.UpdateCursorMode();
            SettingsManager.Save();
        };

        MouseScaleSlider.ValueChanged += newValue =>
        {
            decimal newValueR = Math.Round((decimal)newValue, 1);
            SettingsManager.CurrentSettings.CustomMouseScale = (float)newValueR;
            MouseScaleLabel.Text = $"{newValueR:0.0}x";
            MouseManager.Instance.UpdateMouseScale();
            SettingsManager.Save();
        };

        ResetSettingsButton.Pressed += () =>
        {
            SettingsManager.CurrentSettings = new();
            SettingsManager.Save();
            StatusMessageManager.Instance.ShowMessage("Successfully reset all settings!", StatusMessageManager.MessageType.Info);
            QueueFree();
        };
    }

    public void LoadSettings()
    {
        var s = SettingsManager.CurrentSettings;

        NameField.Text = s.Username;

        SensitivitySlider.Value = s.MouseSensitivity;
        CameraSensitivityLabel.Text = $"{s.MouseSensitivity:0.0} ";

        FOVSlider.Value = s.FOV;
        FOVLabel.Text = s.FOV.ToString();

        CameraSmoothingButton.ButtonPressed = s.CameraSmoothing;

        UseSystemMouse.ButtonPressed = s.UseSystemMouse;

        MouseScaleSlider.Value = s.CustomMouseScale;
        MouseScaleLabel.Text = $"{s.CustomMouseScale:0.0}x";
    }
}

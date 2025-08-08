using Godot;
using System;

public partial class AudioSettings : Control
{
    [Export] public HSlider MasterSlider;
    [Export] public HSlider MusicSlider;
    [Export] public HSlider SFXSlider;
    [Export] public OptionButton AudioOutputButton;

    [Export] public Label MasterLabel;
    [Export] public Label MusicLabel;
    [Export] public Label SFXLabel;

    public override void _Ready()
    {
        LoadSettings();

        MasterSlider.ValueChanged += v => OnVolumeChanged("Master", v);
        MusicSlider.ValueChanged += v => OnVolumeChanged("Music", v);
        SFXSlider.ValueChanged += v => OnVolumeChanged("SFX", v);

        PopulateAudioDevices();
        AudioOutputButton.ItemSelected += OnAudioOutputSelected;
    }

    public void LoadSettings()
    {
        var s = SettingsManager.CurrentSettings;
        MasterSlider.Value = s.MasterVolume;
        MusicSlider.Value = s.MusicVolume;
        SFXSlider.Value = s.SfxVolume;
        MasterLabel.Text = $"{s.MasterVolume}%";
        MusicLabel.Text = $"{s.MusicVolume}%";
        SFXLabel.Text = $"{s.SfxVolume}%";
        ApplyVolumes();
    }

    private void OnVolumeChanged(string type, double value)
    {
        switch (type)
        {
            case "Master":
                SettingsManager.CurrentSettings.MasterVolume = (int)value;
                MasterLabel.Text = $"{value}%";
                break;
            case "Music":
                SettingsManager.CurrentSettings.MusicVolume = (int)value;
                MusicLabel.Text = $"{value}%";
                break;
            case "SFX":
                SettingsManager.CurrentSettings.SfxVolume = (int)value;
                SFXLabel.Text = $"{value}%";
                break;
        }

        ApplyVolumes();
        SettingsManager.Save();
    }

    private void ApplyVolumes()
    {
        var s = SettingsManager.CurrentSettings;
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(s.MasterVolume));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Music"), Mathf.LinearToDb(s.MusicVolume));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("SFX"), Mathf.LinearToDb(s.SfxVolume));
    }

    private void PopulateAudioDevices()
    {
        AudioOutputButton.Clear();

        string[] devices = AudioServer.GetOutputDeviceList();
        string currentDevice = AudioServer.OutputDevice;

        int selectedIndex = 0;

        for (int i = 0; i < devices.Length; i++)
        {
            string device = devices[i];
            AudioOutputButton.AddItem(device);

            if (device == SettingsManager.CurrentSettings.OutputDevice) selectedIndex = i + 1; 
        }

        if (string.IsNullOrEmpty(SettingsManager.CurrentSettings.OutputDevice) || 
            SettingsManager.CurrentSettings.OutputDevice == "Default")
        {
            selectedIndex = 0;
        }

        AudioOutputButton.Selected = selectedIndex;
    }

    
    private void OnAudioOutputSelected(long index)
    {
        if (index == 0)
        {
            AudioServer.OutputDevice = ""; // defaults
            SettingsManager.CurrentSettings.OutputDevice = "";
        }
        else
        {
            string selectedDevice = AudioOutputButton.GetItemText((int)index);
            AudioServer.OutputDevice = selectedDevice;
            SettingsManager.CurrentSettings.OutputDevice = selectedDevice;
        }

        SettingsManager.Save();
    }


}

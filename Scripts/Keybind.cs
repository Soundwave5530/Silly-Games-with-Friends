using Godot;
using System;

public partial class Keybind : Control
{
    [Export] public Label ActionLabel;
    [Export] public MenuButton BindButton;
    [Export] public MenuButton ClearButton;

    public string ActionName { get; private set; }
    private Action<string> onRebindRequested;

    public void Setup(string actionName, string keyName, Action<string> rebindCallback)
    {
        ActionName = actionName;
        ActionLabel.Text = actionName.Capitalize();
        BindButton.buttonLabel.Text = keyName;
        onRebindRequested = rebindCallback;

        BindButton.Pressed += OnBindPressed;
        ClearButton.Pressed += OnClearPressed;
    }

    private void OnBindPressed()
    {
        BindButton.buttonLabel.Text = "Awaiting Key...";
        onRebindRequested?.Invoke(ActionName);
    }

    private void OnClearPressed()
    {
        InputMap.ActionEraseEvents(ActionName);
        SetKeyLabel("Unbound");
        ClearButton.Disabled = true;
        GD.Print($"Cleared binding for: {ActionName}");
    }


    public void SetKeyLabel(string key)
    {
        BindButton.buttonLabel.Text = key;
        ClearButton.Disabled = key == "Unbound";
        SettingsManager.Save();
    }

}

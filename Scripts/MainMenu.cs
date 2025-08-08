using Godot;
using System;

public partial class MainMenu : Control
{
/*
    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        nameInput = GetNode<LineEdit>("MenuUI/VBoxContainer/HBoxContainer/NameInput");
        nameInput.Text = PlayerData.LocalPlayerName;
        
        joinButton = GetNode<Button>("MenuUI/VBoxContainer/Join");
        hostButton = GetNode<Button>("MenuUI/VBoxContainer/Host");
        continueButton = GetNode<Button>("ErrorUI/Continue");

        continueButton.ButtonUp += () =>
        {
            ErrorUi.Hide();
            MenuUi.Show();
        };
    }

    private void OnJoinPressed()
    {
        if (nameInput.Text.Trim() == "")
        {
            OS.Alert("Please enter a name before joining.");
            return;
        }
        string playerName = nameInput.Text;
        PlayerData.LocalPlayerName = playerName;

        NetworkManager.Instance.JoinServer();
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    public void CallError(string message)
    {
        ErrorUi.Show();
        MenuUi.Hide();
        var errorMessage = GetNode<Label>("ErrorUI/Body");
        errorMessage.Text = message;
    }






*/
}

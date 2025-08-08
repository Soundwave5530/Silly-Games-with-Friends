
/*using Godot;
using System;
using System.Collections.Generic;
using System.Data;

public partial class PauseMenuManager : CanvasLayer
{
    public static bool IsOpen = false;
    public static bool customizationOpen = false;

    private Button resumeButton => GetNode<Button>("Main Menu/VBoxContainer/Resume");
    private Button optionsButton => GetNode<Button>("Main Menu/VBoxContainer/Options");
    private Button customizeButton => GetNode<Button>("Main Menu/VBoxContainer/Customize");
    private Button leaveGameButton => GetNode<Button>("Main Menu/VBoxContainer/Quit");
    
    private PackedScene transitionScreenScene = GD.Load<PackedScene>("res://Scenes/TransitionScreen.tscn");

    private Control MainMenu => GetNode<Control>("Main Menu");
    private Control OptionsMenu => GetNode<Control>("Options Menu");
    private CustomizationMenu CustomizationMenu => GetNode<CustomizationMenu>("Customization Menu");

    public override void _Ready()
    {
        Hide();
        resumeButton.ButtonUp += () =>
        {
            CloseMenu();
        };
        optionsButton.ButtonUp += () =>
        {
            return;
        };
        customizeButton.ButtonUp += () =>
        {
            OpenCustomizationMenu();
        };
        leaveGameButton.ButtonUp += () =>
        {
            CloseMenu();
            Multiplayer.MultiplayerPeer.DisconnectPeer(Multiplayer.GetUniqueId());
        };

        CustomizationMenu.backButton.ButtonUp += () =>
        {
            TransitionScreen transitionScreen = transitionScreenScene.Instantiate<TransitionScreen>();
            GetTree().Root.AddChild(transitionScreen);
            transitionScreen.TransitionInFinished += () =>
            {
                CustomizationMenu.Visible = false;
                MainMenu.Visible = true;
                customizationOpen = false;
            };
        };
    }


    public void OpenMenu()
    {
        Visible = true;
        IsOpen = true;
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);
    }

    public void CloseMenu()
    {
        Visible = false;
        IsOpen = false;
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);
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
            MainMenu.Visible = false;
        };
    }
}
*/
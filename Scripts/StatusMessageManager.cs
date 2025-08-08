using Godot;
using System;

public partial class StatusMessageManager : CanvasLayer
{
    [Export] public PackedScene StatusMessageScene;
    private VBoxContainer messageBox;

    public enum MessageType
    {
        Info,
        Warning,
        Error
    }

    public static StatusMessageManager Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        messageBox = GetNode<VBoxContainer>("MarginContainer/MessageBox");
    }

    public void ShowMessage(string text, MessageType messageType)
    {
        if (StatusMessageScene == null)
        {
            GD.PrintErr("No status message scene assigned!");
            return;
        }

        var msg = StatusMessageScene.Instantiate<StatusMessage>();
        messageBox.AddChild(msg);
        msg.SetMessageType(messageType);
        msg.SetText(text);
        GD.Print(text);
    }
}

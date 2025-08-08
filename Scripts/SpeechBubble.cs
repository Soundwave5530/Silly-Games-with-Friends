using Godot;
using System;
using System.Collections;
using System.Linq;

public partial class SpeechBubble : Node3D
{
    [Export] public Vector3 SyncPosition;
    public Player FromPlayer;
    public string Message;

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SpeechSetText(string msg)
    {
        Message = msg;

        string formatted = FormatSpeechText(msg);

        Label3D label = GetNode<Label3D>("Bubble/Label3D");
        label.Text = formatted;
    }

    public override void _Ready()
    {
        TimeToDelete();
    }

    public override void _Process(double delta)
    {
        if (!IsMultiplayerAuthority())
        {
            GlobalPosition = SyncPosition;
        }
        else
        {
            GlobalPosition = FromPlayer.SyncGlobalPosition;
            SyncPosition = FromPlayer.SyncGlobalPosition;
        }

        var camera = GetViewport().GetCamera3D();
        if (camera != null)
        {
            var dir = (SyncPosition - camera.GlobalPosition).Normalized();
            var targetRotationY = Mathf.Atan2(dir.X, dir.Z);
            GlobalRotation = new Vector3(0, targetRotationY, 0);
        }
    }


    public async void TimeToDelete()
    {
        await ToSignal(GetTree().CreateTimer(6), "timeout");
        QueueFree();
    }

    private string FormatSpeechText(string input, int maxLineLength = 20, int maxLines = 6)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new System.Collections.Generic.List<string>();
        string currentLine = "";

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 <= maxLineLength)
            {
                currentLine += (currentLine.Length > 0 ? " " : "") + word;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;

                if (lines.Count == maxLines - 1)
                {
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
        }

        int totalLength = string.Join(" ", words).Length;
        int displayedLength = string.Join(" ", lines).Length;
        if (displayedLength < totalLength)
        {
            if (lines[^1].Length + 3 <= maxLineLength) lines[^1] += "...";
            else lines[^1] = lines[^1].Substring(0, Math.Max(0, maxLineLength - 3)) + "...";
        }

        return string.Join("\n", lines);
    }


}

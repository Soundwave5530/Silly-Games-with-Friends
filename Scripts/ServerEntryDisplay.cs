using Godot;
using System;

public partial class ServerEntryDisplay : Control
{
    [Export] private Label ServerNameLabel;
    [Export] private Label StatusLabel;
    [Export] private Label PingLabel;
    [Export] private MenuButton JoinButton;
    [Export] private MenuButton EditButton;

    [Export] private LineEdit newNameEdit;
    [Export] private LineEdit newIPEdit;
    [Export] private Button DeleteServerButton;

    [Export] private float nonHoveredMinY = 170;
    [Export] private float HoveredMinY = 320;


    public bool editMode = false;
    public bool tempEdit = false;
    private float ExpandProgress;

    public ServerEntry serverData = new() { ServerName = "New Server", IP = "127.0.0.1" };

    public override void _Ready()
    {
        ExpandProgress = nonHoveredMinY;
        UpdateFromData(serverData);
        EditButton.Pressed += () => editMode = !editMode;

        DeleteServerButton.Pressed += () =>
        {
            ServerList listNode = GetParent() as ServerList;
            listNode.RemoveServer(this);
        };

        JoinButton.Pressed += JoinServer;

        StatusLabel.LabelSettings.FontColor = new Color(1, 79/255, 69/255);
    }

    public override void _Process(double delta)
    {
        if (tempEdit != editMode)
        {
            EditModeChanged(editMode);
        }
        tempEdit = editMode;

        CustomMinimumSize = CustomMinimumSize.Lerp(new Vector2(CustomMinimumSize.X, ExpandProgress), (float)delta * 10);
    }

    private void JoinServer()
    {
        NetworkManager.Instance.JoinServer(serverData.IP);
    }

    private void EditModeChanged(bool enabled)
    {
        if (enabled)
        {
            EditButton.buttonLabel.Text = "Confirm";
            ExpandProgress = HoveredMinY;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newNameEdit.Text))
            {
                newNameEdit.Text = serverData.ServerName;
            }
            else
            {
                serverData.ServerName = newNameEdit.Text;
                ServerNameLabel.Text = serverData.ServerName;
            }

            if (string.IsNullOrWhiteSpace(newIPEdit.Text))
            {
                newIPEdit.Text = serverData.IP;
            }
            else
            {
                serverData.IP = newIPEdit.Text;
            }

            EditButton.buttonLabel.Text = "Edit";
            ExpandProgress = nonHoveredMinY;

            (GetParent() as ServerList).SaveServerList();
        }
    }

    public void UpdateServerDisplay(bool isOnline, float ping, bool versionMatch)
    {
        
        StatusLabel.Text = isOnline ? "ONLINE" : "OFFLINE";
        if (isOnline && !versionMatch) StatusLabel.Text = "VERSION MISMATCH";
        
        PingLabel.Text = ping >= 0
                            ? $"Ping:    {ping} ms"
                            : "--";

        JoinButton.Disabled = !isOnline || !versionMatch;

        StatusLabel.LabelSettings.FontColor = isOnline && versionMatch ? new Color(0, 1, 19/255) : new Color(1, 79/255, 69/255);
    }

    public void UpdateFromData(ServerEntry entry)
    {
        serverData = entry;
        ServerNameLabel.Text = serverData.ServerName;
        newNameEdit.Text = serverData.ServerName;
        newIPEdit.Text = serverData.IP;
    }


}

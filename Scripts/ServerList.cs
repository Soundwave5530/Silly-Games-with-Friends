using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ServerList : VBoxContainer
{
    private PackedScene ServerEntryScene = GD.Load<PackedScene>("res://Scenes/ServerUI.tscn");

    [Export] public Timer RefreshTimer;

    public override void _Ready()
    {
        LoadServerList();
        
        RefreshTimer.Timeout += async () =>
        {
            RefreshTimer.Start();
            await RefreshAllServers();
        };
    }

    public void SaveServerList()
    {
        var servers = new List<ServerEntry>();
        foreach (ServerEntryDisplay child in GetChildren())
            servers.Add(child.serverData);

        SettingsManager.CurrentSettings.SavedServers = servers;

        SettingsManager.Save();
    }


    public void LoadServerList()
    {
        foreach (Node child in GetChildren()) child.QueueFree();

        foreach (var data in SettingsManager.CurrentSettings.SavedServers)
        {
            var entry = new ServerEntry
            {
                ServerName = data.ServerName,
                IP = data.IP,
            };

            var node = ServerEntryScene.Instantiate<ServerEntryDisplay>();
            node.UpdateFromData(entry);
            AddChild(node);
        }

        GD.Print($"[ServerList] Loaded {SettingsManager.CurrentSettings.SavedServers.Count} servers.");
    }

    public void RemoveServer(ServerEntryDisplay entry)
    {
        entry.TreeExited += SaveServerList;
        entry.QueueFree();
    }

    public void RemoveAllServers()
    {
        SettingsManager.CurrentSettings.SavedServers.Clear();
        this.QueueFreeChildren();
        SaveServerList();
    }

    public async Task RefreshAllServers()
    {
        foreach (ServerEntryDisplay entry in GetChildren())
        {
            (float? maybePing, bool version) = await NetworkManager.Instance.PingServerAsync(entry.serverData.IP,
                                                 timeoutSec: 1f);

            entry.UpdateServerDisplay(
                isOnline: maybePing.HasValue,
                ping: maybePing ?? -1,
                versionMatch: version
            );
        }
    }

}

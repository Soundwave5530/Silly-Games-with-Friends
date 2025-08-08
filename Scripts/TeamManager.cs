using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TeamManager : Node
{
    public static TeamManager Instance { get; private set; }

    public override void _Ready()
    {
        if (Instance == null) Instance = this;
    }

    public class Team
    {
        public int TeamId;
        public string TeamName;
        public Color TeamColor;
        public List<int> MemberIds = new();

        public Team(int id, string name, Color color)
        {
            TeamId = id;
            TeamName = name;
            TeamColor = color;
        }
    }

    public Dictionary<int, Team> Teams = new();

    public void CreateTeam(int id, string name, Color color)
    {
        if (!Teams.ContainsKey(id))
            Teams[id] = new Team(id, name, color);

        // Immediately sync to all clients
        Rpc(nameof(SyncTeam), id, name, color, Array.Empty<int>());
    }


    public void AddPlayerToTeam(int peerId, int teamId)
    {
        foreach (var tteam in Teams.Values)
            tteam.MemberIds.Remove(peerId); // remove from previous

        if (Teams.TryGetValue(teamId, out var team))
        {
            team.MemberIds.Add(peerId);
            Rpc(nameof(SyncTeam), team.TeamId, team.TeamName, team.TeamColor, team.MemberIds.ToArray());
        }
    }

    public void RemovePlayerFromTeam(int peerId)
    {
        foreach (var team in Teams.Values)
        {
            if (team.MemberIds.Remove(peerId))
            {
                Rpc(nameof(SyncTeam), team.TeamId, team.TeamName, team.TeamColor, team.MemberIds.ToArray());
                return;
            }
        }
    }


    public bool AreTeammates(int peerA, int peerB)
    {
        return Teams.Values.Any(team => team.MemberIds.Contains(peerA) && team.MemberIds.Contains(peerB));
    }

    public Color GetTeamColor(int teamId)
    {
        if (Teams.TryGetValue(teamId, out var team))
            return team.TeamColor;

        return Colors.White;
    }

    public int GetPlayerTeam(int peerId)
    {
        foreach (var team in Teams.Values)
        {
            if (team.MemberIds.Contains(peerId))
                return team.TeamId;
        }
        return 0;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncTeam(int teamId, string name, Color color, int[] members)
    {
        if (Teams.ContainsKey(teamId))
            Teams[teamId].MemberIds.Clear();
        else
            Teams[teamId] = new Team(teamId, name, color);

        Teams[teamId].MemberIds.AddRange(members);
    }

}

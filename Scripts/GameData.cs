using Godot;
using System;

[GlobalClass]
[Tool]
public partial class GameData : Resource
{
    [Export] public string GameName;
    [Export] public byte MinimumPlayers = 2;
    [Export] public Texture2D Icon;
    [Export] public GameManager.GameType GameType;
    [Export] public ushort GameTime;
}
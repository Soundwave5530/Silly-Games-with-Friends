using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public partial class GameDataProvider : Node
{
    public static GameDataProvider Instance { get; private set; }

    private static Dictionary<GameManager.GameType, GameData> GameDataByType = new();
    private const string GAME_DATA_PATH = "res://Assets/Game Data/";

    public override void _Ready()
    {
        Instance = this;
        GameDataByType = ResourceDatabase.Games;
    }

    public GameData GetGamedataFromType(GameManager.GameType gameType)
    {
        if (GameDataByType.TryGetValue(gameType, out GameData data) && data != null)
        {
            return data;
        }
        return null;
    }

    public List<GameData> GetRandomGames(int count = 3)
    {
        var allTypes = new List<GameManager.GameType>();
        foreach (var type in GameDataByType.Keys)
        {
            if (type != GameManager.GameType.None && GameDataByType[type] != null)
                allTypes.Add(type);
        }

        for (int i = allTypes.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            var temp = allTypes[i];
            allTypes[i] = allTypes[j];
            allTypes[j] = temp;
        }

        var result = new List<GameData>();
        for (int i = 0; i < Math.Min(count, allTypes.Count); i++)
        {
            result.Add(GetGamedataFromType(allTypes[i]));
        }
        return result;
    }
}

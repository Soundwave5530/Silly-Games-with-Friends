using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public partial class GameDataProvider : Node
{
    public static GameDataProvider Instance { get; private set; }

    private Dictionary<GameManager.GameType, List<GameData>> gameDataByType = new();
    private const string GAME_DATA_PATH = "res://Assets/Game Data/";

    public override void _Ready()
    {
        Instance = this;
        LoadAllGameData();
    }

    private void LoadAllGameData()
    {
        var dir = DirAccess.Open(GAME_DATA_PATH);
        if (dir == null)
        {
            GD.PrintErr("Failed to access Game Data directory");
            return;
        }

        dir.ListDirBegin();
        var fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.EndsWith(".tres") && !fileName.EndsWith(".uid"))
            {
                var gameData = GD.Load<GameData>($"{GAME_DATA_PATH}{fileName}");
                if (gameData != null)
                {
                    if (!gameDataByType.ContainsKey(gameData.GameType))
                        gameDataByType[gameData.GameType] = new List<GameData>();
                    
                    gameDataByType[gameData.GameType].Add(gameData);
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        GD.Print($"[GameDataProvider] Loaded {gameDataByType.Sum(kvp => kvp.Value.Count)} game data resources");
    }

    public GameData GetRandomGameData(GameManager.GameType gameType)
    {
        if (gameDataByType.TryGetValue(gameType, out var dataList) && dataList.Count > 0)
        {
            return dataList[GD.RandRange(0, dataList.Count - 1)];
        }
        return null;
    }

    public List<GameData> GetRandomGameOptions(int count = 3)
    {
        var allTypes = new List<GameManager.GameType>();
        foreach (var type in gameDataByType.Keys)
        {
            if (type != GameManager.GameType.None && gameDataByType[type].Count > 0)
                allTypes.Add(type);
        }

        // Shuffle types
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
            result.Add(GetRandomGameData(allTypes[i]));
        }
        return result;
    }
}

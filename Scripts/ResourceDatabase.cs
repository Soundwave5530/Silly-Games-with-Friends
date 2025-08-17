using Godot;
using System;
using System.Collections.Generic;

public static class ResourceDatabase
{
    public static Dictionary<string, FacialExpression> Expressions = new();
    public static Dictionary<string, Cosmetic> Hats = new();
    public static List<Cosmetic> Accessories = new();
    public static List<CharacterTypePreset> Characters = new();
    public static Dictionary<GameManager.GameType, GameData> Games = new();

    public static void LoadAll()
    {
        var registry = ResourceLoader.Load<ResourceRegistry>("res://Assets/ResourceRegistry.tres");

        if (registry == null)
        {
            GD.PrintErr("❌ Failed to load ResourceRegistry.tres");
            return;
        }

        Expressions.Clear();
        foreach (var item in registry.Expressions)
        {
            var expr = item.As<FacialExpression>();
            if (expr != null && !string.IsNullOrEmpty(expr.ExpressionId))
                Expressions[expr.ExpressionId] = expr;
        }

        Hats.Clear();
        foreach (var item in registry.Hats)
        {
            var hat = item.As<Cosmetic>();
            if (hat != null)
                Hats.Add(hat.CosmeticID, hat);
        }

        Accessories.Clear();
        foreach (var item in registry.Accessories)
        {
            var acc = item.As<Cosmetic>();
            if (acc != null)
                Accessories.Add(acc);
        }

        Characters.Clear();
        foreach (var item in registry.Characters)
        {
            var character = item.As<CharacterTypePreset>();
            if (character != null)
                Characters.Add(character);
        }

        Games.Clear();
        foreach (var item in registry.Games)
        {
            var expr = item.As<GameData>();
            if (expr != null)
                Games[expr.GameType] = expr;
        }

        GD.Print($"[CosmeticDatabase] Expressions  loaded: {Expressions.Count}");
        GD.Print($"[CosmeticDatabase] Hats         loaded: {Hats.Count}");
        GD.Print($"[CosmeticDatabase] Accessories  loaded: {Accessories.Count}");
        GD.Print($"[CosmeticDatabase] Characters   loaded: {Characters.Count}");
        GD.Print($"[CosmeticDatabase] Games        loaded: {Games.Count}");
    }

    public static bool TryGetExpression(string id, out FacialExpression expression)
    {
        return Expressions.TryGetValue(id, out expression);
    }
}

using Godot;
using System;
using System.Collections.Generic;

public static class CosmeticDatabase
{
    public static Dictionary<string, FacialExpression> Expressions = new();
    public static Dictionary<string, Cosmetic> Hats = new();
    public static List<Cosmetic> Accessories = new();
    public static List<CharacterTypePreset> Characters = new();

    public static void LoadAll()
    {
        var registry = ResourceLoader.Load<CosmeticRegistry>("res://Assets/CosmeticRegistry.tres");

        if (registry == null)
        {
            GD.PrintErr("❌ Failed to load CosmeticRegistry.tres");
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

        GD.Print($"✅ Expressions loaded: {Expressions.Count}");
        GD.Print($"✅ Hats loaded: {Hats.Count}");
        GD.Print($"✅ Accessories loaded: {Accessories.Count}");
        GD.Print($"✅ Characters loaded: {Characters.Count}");
    }

    public static bool TryGetExpression(string id, out FacialExpression expression)
    {
        return Expressions.TryGetValue(id, out expression);
    }
}

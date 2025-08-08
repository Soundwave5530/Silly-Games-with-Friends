/*
using Godot;
using System;
using Godot.Collections;
using System.Collections.Generic;


[Tool]
[GlobalClass]
public partial class CosmeticRegistryBuilder : EditorPlugin
{
	public override void _Ready()
	{
		string registryPath = "res://Assets/CosmeticRegistry.tres";
		var registry = ResourceLoader.Load<CosmeticRegistry>(registryPath) ?? new CosmeticRegistry();

		registry.Expressions = LoadResources<FacialExpression>("res://Assets/Expressions/");
		registry.Hats = LoadFilteredCosmetics("res://Assets/Cosmetics/Hats/", Cosmetic.CosmeticType.Hat);
		registry.Accessories = LoadFilteredCosmetics("res://Assets/Cosmetics/Accessories/", Cosmetic.CosmeticType.Body); // or a new enum type if needed
		registry.Characters = LoadResources<CharacterTypePreset>("res://Assets/Characters/");

		ResourceSaver.Save(registry, registryPath);
		GD.Print("✅ Cosmetic registry rebuilt.");
	}

	private Godot.Collections.Array LoadResources<T>(string folderPath) where T : Resource
	{
		var result = new Godot.Collections.Array();
		var stack = new Stack<string>();
		stack.Push(folderPath);

		while (stack.Count > 0)
		{
			var currentPath = stack.Pop();
			var dir = DirAccess.Open(currentPath);
			if (dir == null) continue;

			dir.ListDirBegin();
			string file;
			while ((file = dir.GetNext()) != "")
			{
				if (dir.CurrentIsDir() && file != "." && file != "..")
				{
					stack.Push(currentPath + file + "/");
				}
				else
				{
					var fullPath = currentPath + file;
					var res = ResourceLoader.Load(fullPath);
					if (res is T typed)
						result.Add(typed);
				}
			}
			dir.ListDirEnd();
		}

		return result;
	}
	
	private Godot.Collections.Array LoadFilteredCosmetics(string folderPath, Cosmetic.CosmeticType type)
	{
		var all = LoadResources<Cosmetic>(folderPath);
		var filtered = new Godot.Collections.Array<Cosmetic>();

		foreach (Variant res in all)
		{
			Cosmetic cosmetic = (Cosmetic)res;
			if (cosmetic == null)
			{
				continue;
			}

			if (cosmetic.cosmeticType == type)
			{
				filtered.Add(cosmetic);
			}
		}

		return (Godot.Collections.Array)filtered;
	}
}
*/
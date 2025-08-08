using Godot;
using System;
using Godot.Collections;

[GlobalClass]
[Tool]
public partial class Cosmetic : Resource
{
    public enum CosmeticType { Hat, Face, Body };

    [Export] public string CosmeticID;
    [Export] public bool modulatesWithPlayerColor;

    [Export] public CosmeticType cosmeticType;

    [ExportSubgroup("Sprites")]
    [Export] public Array<Texture2D> FrontSprites;
    [Export] public Array<Texture2D> SideSprites;
    [Export] public Array<Texture2D> BackSprites;
}

using Godot;
using System;

[GlobalClass]
[Tool]
public partial class ResourceRegistry : Resource
{
    [Export] public Godot.Collections.Array Expressions = new();
    [Export] public Godot.Collections.Array Hats = new();
    [Export] public Godot.Collections.Array Accessories = new();
    [Export] public Godot.Collections.Array Characters = new();
    [Export] public Godot.Collections.Array Games = new();
}


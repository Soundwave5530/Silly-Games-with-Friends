using Godot;
using System;
using Godot.Collections;

[GlobalClass]
public partial class MouseCursorPreset : Resource
{
    [Export] public Array<Texture2D> Pointer = new();
    [Export] public Array<Texture2D> Clickable = new();
    [Export] public Array<Texture2D> IBeam = new();
    [Export] public Array<Texture2D> Busy = new();
}

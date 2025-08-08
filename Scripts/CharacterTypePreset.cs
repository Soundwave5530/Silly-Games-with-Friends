using Godot;
using System;
using Godot.Collections;

[GlobalClass]
[Tool]
public partial class CharacterTypePreset : Resource
{

    [Export] public string Name;

    [Export] public Dictionary<AnimationManager.AnimFacingType, Dictionary<AnimationManager.PlayerAnimTypes, Array<Texture2D>>> Animations = new();

    [Export] public Dictionary<AnimationManager.AnimFacingType, Dictionary<AnimationManager.PlayerAnimTypes, Array<Vector2>>> HatOffsets = new();
    [Export] public Dictionary<AnimationManager.AnimFacingType, Dictionary<AnimationManager.PlayerAnimTypes, Array<Vector2>>> FaceOffsets = new();
    [Export] public Dictionary<AnimationManager.AnimFacingType, Dictionary<AnimationManager.PlayerAnimTypes, Array<Vector2>>> TieOffsets = new();

}

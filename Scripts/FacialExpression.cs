using Godot;
using System;
using Godot.Collections;

[GlobalClass]
[Tool]
public partial class FacialExpression : Resource
{
    [Export] public string ExpressionId = "";

    [ExportSubgroup("Sprites")]
    [Export] public Array<Texture2D> FrontExpression;
    [Export] public Array<Texture2D> SideExpression;
}

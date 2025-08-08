using Godot;
using System;

[GlobalClass]
[Icon("res://Assets/Sprites/UI/Editor Tools/Icons/SVG Checkbox.svg")]
[Tool]
public partial class BetterCheckButton : CheckBox
{
    [Export] private TextureRect _icon;
    [Export] Texture2D iconChecked;
    [Export] Texture2D iconUnchecked;

    public override void _Ready()
    {
        Toggled += OnToggled;
    }

    private void OnToggled(bool pressed)
    {
        if (_icon != null)
        {
            _icon.Texture = pressed ? iconUnchecked : iconChecked;
        }
    }
}

using Godot;
using System;
using Godot.Collections;

public partial class Mouse : TextureRect
{
    [Export] public MouseCursorPreset Preset;
    [Export] public MouseManager.MouseCursorType CursorType = MouseManager.MouseCursorType.Pointer;
    [Export] public float FrameRate = 4f;

    private float _frameTimer;
    private int _frameIndex;

    public override void _Process(double delta)
    {
        _frameTimer += (float)delta;
        if (_frameTimer >= 1f / FrameRate)
        {
            _frameTimer = 0f;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        var frames = GetCurrentFrameList();
        if (frames.Count == 0)
            return;

        _frameIndex = (_frameIndex + 1) % frames.Count;
        Texture = frames[_frameIndex];
    }

    private Array<Texture2D> GetCurrentFrameList()
    {
        return CursorType switch
        {
            MouseManager.MouseCursorType.Pointer => Preset.Pointer,
            MouseManager.MouseCursorType.Clickable => Preset.Clickable,
            MouseManager.MouseCursorType.IBeam => Preset.IBeam,
            MouseManager.MouseCursorType.Busy => Preset.Busy,
            _ => new Array<Texture2D>()
        };
    }

    public void SetCursorType(MouseManager.MouseCursorType type)
    {
        CursorType = type;
        _frameIndex = 0;
        _frameTimer = 0;
        AdvanceFrame();
    }
}

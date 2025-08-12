using Godot;
using System;
using Godot.Collections;

public partial class MouseManager : CanvasLayer
{
    public static MouseManager Instance { get; private set; }

    PackedScene CursorScene;
    public Mouse CustomMouse;

    public MouseCursorType CurrentCursorType { get; private set; } = MouseCursorType.Pointer;


    public enum MouseCursorType
    {
        Pointer,
        Clickable,
        IBeam,
        Busy
    }

    public override void _Ready()
    {
        Layer = 100;
        Instance = this;

        CursorScene = GD.Load<PackedScene>("res://Scenes/Mouse.tscn");
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        var mouseInstance = CursorScene.Instantiate<Mouse>();

        CustomMouse = mouseInstance;

        AddChild(CustomMouse);

        UpdateCursorMode();
        UpdateMouseScale();
    }

    [Export] public float SwingStrength = 0.01f;
    [Export] public float MaxSwingAngle = 1f;
    [Export] public float SwingDamping = 8f;

    private Vector2 _lastPos;

    public override void _Process(double delta)
    {
        Vector2 mousePos = GetViewport().GetMousePosition();
        CustomMouse.Position = mousePos;

        // Swinging
        Vector2 velocity = -1 * (mousePos - _lastPos) / (float)delta;
        float target = Mathf.Clamp(-velocity.X * SwingStrength, -MaxSwingAngle, MaxSwingAngle);
        CustomMouse.Rotation = Mathf.Lerp(CustomMouse.Rotation, target, SwingDamping * (float)delta);
        _lastPos = mousePos;

        if (CustomMouse.Visible)
        {
            var systemShape = Input.GetCurrentCursorShape();
            var newType = systemShape switch
            {
                Input.CursorShape.Arrow => MouseCursorType.Pointer,
                Input.CursorShape.PointingHand => MouseCursorType.Clickable,
                Input.CursorShape.Ibeam => MouseCursorType.IBeam,
                Input.CursorShape.Wait => MouseCursorType.Busy,
                _ => MouseCursorType.Pointer
            };

            if (newType != CurrentCursorType)
            {
                SetCursor(newType);
            }
        }
    }


    public void SetCursor(MouseCursorType type)
    {
        if (CustomMouse == null || CurrentCursorType == type) return;

        CurrentCursorType = type;
        CustomMouse.SetCursorType(type);
    }

    public void UpdateCursorMode()
    {
        bool useSystem = SettingsManager.CurrentSettings.UseSystemMouse;

        if (useSystem)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            CustomMouse.Visible = false;
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Hidden;
            CustomMouse.Visible = true;

            SetCursor(CurrentCursorType);
            SyncFromSystemCursor();
        }
    }

    public void SyncFromSystemCursor()
    {
        var systemShape = Input.GetCurrentCursorShape();

        var type = systemShape switch
        {
            Input.CursorShape.Arrow => MouseCursorType.Pointer,
            Input.CursorShape.PointingHand => MouseCursorType.Clickable,
            Input.CursorShape.Ibeam => MouseCursorType.IBeam,
            Input.CursorShape.Wait => MouseCursorType.Busy,
            _ => MouseCursorType.Pointer
        };

        SetCursor(type);
    }

    public void UpdateMouseScale()
    {
        float scale = SettingsManager.CurrentSettings.CustomMouseScale;

        CustomMouse.Size = 32 * Vector2.One * scale;
    }

    public void UpdateMouseType(Input.MouseModeEnum type, bool force = false)
    {
        bool useSystem = force ? true : SettingsManager.CurrentSettings.UseSystemMouse;
        switch (type)
        {
            case Input.MouseModeEnum.Visible:
                if (useSystem || force)
                {
                    Input.MouseMode = Input.MouseModeEnum.Visible;
                    CustomMouse.Visible = false;
                }
                else
                {
                    Input.MouseMode = Input.MouseModeEnum.Hidden;
                    CustomMouse.Visible = true;

                    SetCursor(CurrentCursorType);
                    SyncFromSystemCursor();
                }
                break;
            case Input.MouseModeEnum.Hidden:
                Input.MouseMode = Input.MouseModeEnum.Hidden;
                CustomMouse.Visible = false;

                SetCursor(CurrentCursorType);
                SyncFromSystemCursor();
                break;
            case Input.MouseModeEnum.Captured:
                if (!force) // Don't capture if force flag is set
                {
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                    CustomMouse.Visible = false;

                    SetCursor(CurrentCursorType);
                    SyncFromSystemCursor();
                }
                break;
        }
    }
}


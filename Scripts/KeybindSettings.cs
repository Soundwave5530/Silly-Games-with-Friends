using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class KeybindSettings : Control
{
    [Export] public VBoxContainer BindListContainer;
    private string actionWaitingForRebind = null;
    public static bool bindingInProgress = false;

    public override void _Ready()
    {
        PopulateKeybinds();
    }

    private void PopulateKeybinds()
    {
        BindListContainer.QueueFreeChildren();

        foreach (string action in InputMap.GetActions())
        {
            if (action.StartsWith("ui_")) continue;

            PackedScene keybindScene = GD.Load<PackedScene>("res://Scenes/Keybind.tscn");
            Keybind newBind = keybindScene.Instantiate<Keybind>();

            string keyName = GetKeyForAction(action);

            BindListContainer.AddChild(newBind);
            newBind.Setup(action, keyName, StartRebinding);
        }
    }

    private void StartRebinding(string action)
    {
        actionWaitingForRebind = action;
    }

    private string GetKeyForAction(string action)
    {
        var events = InputMap.ActionGetEvents(action);
        foreach (var ev in events)
        {
            if (ev is InputEventKey keyEvent)
            {
                return OS.GetKeycodeString(keyEvent.PhysicalKeycode);
            }
            else if (ev is InputEventMouseButton mouseEvent)
            {
                return GetMouseButtonName(mouseEvent.ButtonIndex);
            }
        }
        return "Unbound";
    }

    private string GetMouseButtonName(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "Left Click",
            MouseButton.Right => "Right Click",
            MouseButton.Middle => "Middle Click",
            MouseButton.WheelUp => "Scroll Up",
            MouseButton.WheelDown => "Scroll Down",
            MouseButton.WheelLeft => "Scroll Left",
            MouseButton.WheelRight => "Scroll Right",
            MouseButton.Xbutton1 => "Mouse Button 4",
            MouseButton.Xbutton2 => "Mouse Button 5",
            _ => $"Mouse Button {((int)button)}"
        };
    }

    private string GetInputName(InputEvent input)
    {
        if (input is InputEventKey key)
        {
            string name = "";
            if (key.ShiftPressed) name += "Shift + ";
            if (key.CtrlPressed) name += "Ctrl + ";
            if (key.AltPressed) name += "Alt + ";
            if (key.MetaPressed) name += "Meta + ";
            name += OS.GetKeycodeString(key.PhysicalKeycode);
            return name;
        }
        if (input is InputEventMouseButton mouse)
        {
            return $"Mouse Button {mouse.ButtonIndex}";
        }
        return "Unknown";
    }

    public override void _Input(InputEvent @event)
    {
        if (actionWaitingForRebind == null)
            return;

        if (!@event.IsPressed())
            return;

        if (@event is InputEventKey keyEvent)
        {
            // Skip modifier keys when pressed alone
            if (keyEvent.Keycode is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta)
                return;

            var newKeyEvent = new InputEventKey
            {
                PhysicalKeycode = keyEvent.PhysicalKeycode,
                Keycode = keyEvent.Keycode,
                ShiftPressed = keyEvent.ShiftPressed,
                CtrlPressed = keyEvent.CtrlPressed,
                AltPressed = keyEvent.AltPressed,
                MetaPressed = keyEvent.MetaPressed
            };

            // If escape is pressed during binding, treat it as a normal key
            // This allows any key to be bound to any action

            if (IsKeyAlreadyBound(newKeyEvent, actionWaitingForRebind))
            {
                StatusMessageManager.Instance?.ShowMessage("Key already bound to another action!", StatusMessageManager.MessageType.Error);
                actionWaitingForRebind = null;
                PopulateKeybinds();
                return;
            }

            InputMap.ActionEraseEvents(actionWaitingForRebind);
            InputMap.ActionAddEvent(actionWaitingForRebind, newKeyEvent);

            StoreBinding(actionWaitingForRebind, newKeyEvent);

            GD.Print($"Bound '{actionWaitingForRebind}' to {GetInputName(newKeyEvent)}");
            actionWaitingForRebind = null;
            bindingInProgress = false;
            PopulateKeybinds();
            GetViewport().SetInputAsHandled();
            return;
        }
        else if (@event is InputEventMouseButton mouseEvent)
        {
            var newMouseEvent = new InputEventMouseButton
            {
                ButtonIndex = mouseEvent.ButtonIndex
            };

            InputMap.ActionEraseEvents(actionWaitingForRebind);
            InputMap.ActionAddEvent(actionWaitingForRebind, newMouseEvent);

            StoreBinding(actionWaitingForRebind, newMouseEvent);

            GD.Print($"Bound '{actionWaitingForRebind}' to Mouse Button {mouseEvent.ButtonIndex}");
            actionWaitingForRebind = null;
            PopulateKeybinds();
        }
    }


    void StoreBinding(string action, InputEvent ev)
    {
        var rec = new KeybindRecord();
        if (ev is InputEventKey k)
        {
            rec.IsMouse = false;
            rec.KeyCode = (int)k.PhysicalKeycode;
            rec.Shift = k.ShiftPressed;
            rec.Ctrl = k.CtrlPressed;
            rec.Alt = k.AltPressed;
            rec.Meta = k.MetaPressed;
        }
        else if (ev is InputEventMouseButton m)
        {
            rec.IsMouse = true;
            rec.KeyCode = (int)m.ButtonIndex;
        }
        SettingsManager.CurrentSettings.SavedKeybinds[action] = rec;
        SettingsManager.Save();
    }

    private bool IsKeyAlreadyBound(InputEvent newEvent, string excludeAction)
    {
        foreach (string action in InputMap.GetActions())
        {
            if (action.StartsWith("ui_") || action == excludeAction) continue;

            var events = InputMap.ActionGetEvents(action);
            foreach (var existingEvent in events)
            {
                if (AreEventsEqual(existingEvent, newEvent))
                    return true;
            }
        }
        return false;
    }

    private bool AreEventsEqual(InputEvent a, InputEvent b)
    {
        if (a is InputEventKey keyA && b is InputEventKey keyB)
        {
            return keyA.PhysicalKeycode == keyB.PhysicalKeycode &&
                keyA.ShiftPressed == keyB.ShiftPressed &&
                keyA.CtrlPressed == keyB.CtrlPressed &&
                keyA.AltPressed == keyB.AltPressed;
        }
        if (a is InputEventMouseButton mouseA && b is InputEventMouseButton mouseB)
        {
            return mouseA.ButtonIndex == mouseB.ButtonIndex;
        }
        return false;
    }
}

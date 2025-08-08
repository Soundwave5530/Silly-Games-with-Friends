using Godot;
using System;

public static class NodeExtensions
{
    public static void QueueFreeChildren(this Node node)
    {
        foreach (Node child in node.GetChildren()) child.QueueFree();
    }

    public static void SetVisibleRecursively(this CanvasItem item, bool visible)
    {
        item.Visible = visible;
        foreach (Node child in item.GetChildren())
        {
            if (child is CanvasItem canvasChild)
                canvasChild.SetVisibleRecursively(visible);
        }
    }

    public static T FindChildByName<T>(this Node node, string name) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.Name == name && child is T typedChild)
                return typedChild;
        }
        return null;
    }
}

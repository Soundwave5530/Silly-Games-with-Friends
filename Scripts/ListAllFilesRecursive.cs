using Godot;
using System;
using System.Collections.Generic;

public static class ResourceDebugger
{
    public static void ListAllFiles(string startPath = "res://")
    {
        GD.Print($"🔍 Listing all files under: {startPath}");

        Stack<string> stack = new();
        stack.Push(startPath);

        while (stack.Count > 0)
        {
            string currentPath = stack.Pop();
            DirAccess dir = DirAccess.Open(currentPath);
            if (dir == null)
            {
                GD.PrintErr($"❌ Failed to open: {currentPath}");
                continue;
            }

            dir.ListDirBegin();
            string file;
            while ((file = dir.GetNext()) != "")
            {
                if (file == "." || file == "..") continue;

                string fullPath = currentPath + file;
                if (dir.CurrentIsDir())
                {
                    stack.Push(fullPath + "/");
                }
                else
                {
                    GD.Print("📄 " + fullPath);
                }
            }
            dir.ListDirEnd();
        }
    }
}

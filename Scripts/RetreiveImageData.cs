using Godot;
using System.Collections.Generic;

public static class RetrieveImageData
{
    public static List<T> LoadResourcesFromFolder<T>(string folderPath) where T : Resource
    {
        List<T> resources = new();

        if (!DirAccess.DirExistsAbsolute(folderPath))
        {
            GD.PrintErr($"Directory not found: {folderPath}");
            return resources;
        }

        DirAccess dir = DirAccess.Open(folderPath);
        if (dir == null)
        {
            GD.PrintErr($"Failed to open directory: {folderPath}");
            return resources;
        }

        dir.ListDirBegin();
        string fileName;
        while ((fileName = dir.GetNext()) != "")
        {
            if (fileName.EndsWith(".tres"))
            {
                string fullPath = folderPath + "/" + fileName;
                T res = ResourceLoader.Load<T>(fullPath);
                if (res != null)
                    resources.Add(res);
            }
        }

        dir.ListDirEnd();
        return resources;
    }
}

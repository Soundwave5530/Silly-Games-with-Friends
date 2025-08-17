
using Godot;
using System;
using System.IO;
using Godot.Collections;
using System.Collections.Generic;

#if TOOLS
[Tool]
[GlobalClass]
public partial class AnimationPresetBaker : EditorPlugin
{
    private VBoxContainer dock;
    private FileDialog presetPicker;
    private Label presetLabel;
    private Button bakeAllBtn;

    private OptionButton animDropdown;
    private OptionButton frameDropdown;
    private OptionButton facingDropdown;
    private OptionButton typeDropdown;
    private Button bakeOffsetBtn;
    private Button previewBtn;
    private Button selectMarkerBtn;
    private Button selectSpriteBtn;

    private CharacterTypePreset selectedPreset;
    private Node3D markerTarget;
    private Sprite3D previewSprite;

    public override void _EnterTree()
    {
        dock = new VBoxContainer { Name = "Offset & Anim Baker" };

        presetLabel = new Label { Text = "No preset selected" };
        dock.AddChild(presetLabel);

        presetPicker = new FileDialog
        {
            Access = FileDialog.AccessEnum.Resources,
            Filters = new string[] { "*.tres ; CharacterTypePreset" },
            FileMode = FileDialog.FileModeEnum.OpenFile
        };
        presetPicker.FileSelected += path =>
        {
            var res = GD.Load(path);
            if (res is CharacterTypePreset cp)
            {
                selectedPreset = cp;
                presetLabel.Text = "Preset: " + Path.GetFileName(path);
            }
        };
        dock.AddChild(presetPicker);

        var pickBtn = new Button { Text = "Select CharacterTypePreset…" };
        pickBtn.Pressed += () => presetPicker.PopupCentered();
        dock.AddChild(pickBtn);

        selectMarkerBtn = new Button { Text = "Select Pivot Node (Marker3D)" };
        selectMarkerBtn.Pressed += () =>
        {
            var selection = EditorInterface.Singleton.GetSelection();
            foreach (var node in selection.GetSelectedNodes())
            {
                if (node is Node3D marker)
                {
                    markerTarget = marker;
                    GD.Print("✅ Marker set to: " + marker.Name);
                    break;
                }
            }
        };
        dock.AddChild(selectMarkerBtn);

        selectSpriteBtn = new Button { Text = "Use Selected Sprite3D for Preview" };
        selectSpriteBtn.Pressed += () =>
        {
            var selection = EditorInterface.Singleton.GetSelection();
            foreach (var node in selection.GetSelectedNodes())
            {
                if (node is Sprite3D sprite)
                {
                    previewSprite = sprite;
                    GD.Print("✅ Preview sprite set to: " + sprite.Name);
                    break;
                }
            }
        };
        dock.AddChild(selectSpriteBtn);

        bakeAllBtn = new Button { Text = "Bake All Animation Frames" };
        bakeAllBtn.Pressed += OnBakeAllPressed;
        dock.AddChild(bakeAllBtn);

        dock.AddChild(new HSeparator());

        animDropdown = new OptionButton();
        foreach (var name in Enum.GetNames(typeof(AnimationManager.PlayerAnimTypes)))
            animDropdown.AddItem(name);
        dock.AddChild(animDropdown);

        frameDropdown = new OptionButton();
        for (int i = 0; i < 16; i++) frameDropdown.AddItem(i.ToString());
        dock.AddChild(frameDropdown);

        facingDropdown = new OptionButton();
        foreach (var name in Enum.GetNames(typeof(AnimationManager.FacingType)))
            facingDropdown.AddItem(name);
        dock.AddChild(facingDropdown);

        typeDropdown = new OptionButton();
        typeDropdown.AddItem("Face");
        typeDropdown.AddItem("Hat");
        typeDropdown.AddItem("Tie");
        dock.AddChild(typeDropdown);

        bakeOffsetBtn = new Button { Text = "Bake Current Offset from Marker" };
        bakeOffsetBtn.Pressed += OnBakeOffsetPressed;
        dock.AddChild(bakeOffsetBtn);

        previewBtn = new Button { Text = "Preview Frame on Sprite3D" };
        previewBtn.Pressed += OnPreviewPressed;
        dock.AddChild(previewBtn);

        AddControlToDock(DockSlot.RightUl, dock);
    }

    public override void _ExitTree()
    {
        RemoveControlFromDocks(dock);
        dock.QueueFree();
    }

    private void OnBakeAllPressed()
    {
        if (selectedPreset == null) return;

        string folder = $"res://Assets/Sprites/{selectedPreset.Name}";
        var dir = DirAccess.Open(folder);
        if (dir == null) return;

        selectedPreset.Animations = new();

        dir.ListDirBegin();
        List<string> files = new();
        string fname;
        while ((fname = dir.GetNext()) != "")
        {
            if (!dir.CurrentIsDir() && fname.ToLower().EndsWith(".png"))
                files.Add(fname);
        }
        dir.ListDirEnd();

        files.Sort((a, b) =>
        {
            int ExtractFrame(string filename)
            {
                var name = Path.GetFileNameWithoutExtension(filename);
                var parts = name.Split(' ');
                if (parts.Length < 3) return 0;

                return int.TryParse(parts[2], out int frameNum) ? frameNum : 0;
            }

            var nameA = Path.GetFileNameWithoutExtension(a).Split(' ');
            var nameB = Path.GetFileNameWithoutExtension(b).Split(' ');

            string animNameA = nameA[0] + nameA[1];
            string animNameB = nameB[0] + nameB[1];

            int animCompare = animNameA.CompareTo(animNameB);
            if (animCompare != 0) return animCompare;

            return ExtractFrame(a).CompareTo(ExtractFrame(b));
        });


        foreach (string file in files)
        {
            var parts = Path.GetFileNameWithoutExtension(file).Split(' ');
            if (parts.Length < 3) continue;

            if (!Enum.TryParse(parts[0], out AnimationManager.PlayerAnimTypes anim)) continue;
            if (!Enum.TryParse(parts[1], out AnimationManager.AnimFacingType facing)) continue;

            var tex = GD.Load<Texture2D>($"{folder}/{file}");
            if (tex == null) continue;

            if (!selectedPreset.Animations.ContainsKey(facing))
                selectedPreset.Animations[facing] = new();

            if (!selectedPreset.Animations[facing].ContainsKey(anim))
                selectedPreset.Animations[facing][anim] = new Array<Texture2D>();

            selectedPreset.Animations[facing][anim].Add(tex);
        }

        ResourceSaver.Save(selectedPreset, selectedPreset.ResourcePath);
        GD.Print("✅ Baked all frames for " + selectedPreset.Name);
    }

    private void OnBakeOffsetPressed()
    {
        if (selectedPreset == null || markerTarget == null) return;

        var anim = (AnimationManager.PlayerAnimTypes)animDropdown.GetSelectedId();
        var facing = (AnimationManager.AnimFacingType)facingDropdown.GetSelectedId();
        int frame = frameDropdown.GetSelectedId();
        string type = typeDropdown.GetItemText(typeDropdown.GetSelectedId());

        Vector2 offset = new Vector2(markerTarget.Position.X, markerTarget.Position.Y);

        var targetDict = type switch
        {
            "Face" => selectedPreset.FaceOffsets,
            "Hat" => selectedPreset.HatOffsets,
            "Tie" => selectedPreset.TieOffsets,
            _ => null
        };

        if (!targetDict.ContainsKey(facing))
            targetDict[facing] = new();

        if (!targetDict[facing].ContainsKey(anim))
            targetDict[facing][anim] = new Array<Vector2>();

        while (targetDict[facing][anim].Count <= frame)
            targetDict[facing][anim].Add(Vector2.Zero);

        targetDict[facing][anim][frame] = offset;

        ResourceSaver.Save(selectedPreset, selectedPreset.ResourcePath);
        GD.Print($"Offset baked: {type}, {facing}, {anim}, frame {frame}");
    }

    private void OnPreviewPressed()
    {
        if (selectedPreset == null || previewSprite == null) return;

        var anim = (AnimationManager.PlayerAnimTypes)animDropdown.GetSelectedId();
        var facing = (AnimationManager.AnimFacingType)facingDropdown.GetSelectedId();
        int frame = frameDropdown.GetSelectedId();


        if (selectedPreset.Animations.TryGetValue(facing, out var animDict) &&
            animDict.TryGetValue(anim, out var frames) && frame < frames.Count)
        {
            previewSprite.Texture = frames[frame];
        }
        else
        {
            GD.PrintErr($"Frame not found: {facing} {anim} [{frame}]");
        }


        switch (typeDropdown.Selected)
        {
            case 0:
                if (selectedPreset.FaceOffsets.TryGetValue(facing, out var offsetsDict) &&
                    offsetsDict.TryGetValue(anim, out var offsets) && frame < offsets.Count)
                {
                    Vector2 offset = offsets[frame];
                    markerTarget.Position = new Vector3(offset.X, offset.Y, markerTarget.Position.Z);
                }
                break;
            case 1:
                if (selectedPreset.HatOffsets.TryGetValue(facing, out var hatDict) &&
                    hatDict.TryGetValue(anim, out var hatOffsets) && frame < hatOffsets.Count)
                {
                    Vector2 offset = hatOffsets[frame];
                    markerTarget.Position = new Vector3(offset.X, offset.Y, markerTarget.Position.Z);
                }
                break;
        }


        
    }
}
#endif
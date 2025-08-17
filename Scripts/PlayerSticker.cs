using Godot;
using System;

public partial class PlayerSticker : TextureRect
{
    [Export] public TextureRect Face;
    [Export] public TextureRect Hat;
    [Export] public TextureRect Head;

    private int playerId;
    private FacialExpression face;
    private Cosmetic hat;
    private float frameTime;
    private const float FRAME_DELAY = 0.15f;
    private int faceFrameIndex;
    private int hatFrameIndex;

    public override void _Ready()
    {
        Face ??= GetNode<TextureRect>("Face");
        Hat ??= GetNode<TextureRect>("Hat");
        Head ??= GetNode<TextureRect>("Head");

        Random random = new Random();
        RotationDegrees = random.Next(-20, 20);
    }

    public void UpdateSticker(int id, Color color, string expressionId = "smile", string hatId = "none")
    {
        playerId = id;
        
        // Safety check for required nodes
        if (Head == null || Face == null)
        {
            GD.PrintErr($"[PlayerSticker] Missing required nodes! Head: {Head != null}, Face: {Face != null}");
            return;
        }
        
        Head.SelfModulate = color;
        
        // Get cosmetics from database
        if (ResourceDatabase.TryGetExpression(expressionId, out face))
        {
            if (face == null || face.FrontExpression == null || face.FrontExpression.Count == 0)
            {
                GD.PrintErr($"[PlayerSticker] Invalid expression data for ID: {expressionId}");
                return;
            }
            
            Face.Texture = face.FrontExpression[0];
            faceFrameIndex = 0;
            GD.Print($"[PlayerSticker] Updated face for player {id} with expression {expressionId}");
        }
        else
        {
            GD.PrintErr($"[PlayerSticker] Failed to get expression: {expressionId}");
        }

        hat = null;
        Hat.Visible = false;
        if (hatId != "none" && ResourceDatabase.Hats.TryGetValue(hatId, out hat) && hat.FrontSprites.Count > 0)
        {
            Hat.Visible = true;
            Hat.Texture = hat.FrontSprites[0];
            Hat.Modulate = hat.modulatesWithPlayerColor ? color : Colors.White;
            hatFrameIndex = 0;
        }
    }

    public override void _Process(double delta)
    {
        frameTime += (float)delta;
        if (frameTime >= FRAME_DELAY)
        {
            frameTime = 0;
            
            // Animate face if it has multiple frames
            if (face?.FrontExpression.Count > 1)
            {
                faceFrameIndex = (faceFrameIndex + 1) % face.FrontExpression.Count;
                Face.Texture = face.FrontExpression[faceFrameIndex];
            }
            
            // Animate hat if it has multiple frames
            if (hat?.FrontSprites.Count > 1)
            {
                hatFrameIndex = (hatFrameIndex + 1) % hat.FrontSprites.Count;
                Hat.Texture = hat.FrontSprites[hatFrameIndex];
            }
        }
    }

    public void SetScale(float scale)
    {
        Scale = new Vector2(scale, scale);
    }
}

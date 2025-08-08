using Godot;
using System;

[GlobalClass]
public partial class PreviewVisualManager : Sprite3D
{
    private PreviewAnimationManager animManager;

    [Export] private Node3D origin;
    [Export] private Marker3D facePivot;
    [Export] private Sprite3D faceSprite;
    
    [Export] private Marker3D hatPivot;
    [Export] private Sprite3D hatSprite;


    public override void _Ready()
    {
        animManager = GetParent().GetNode<PreviewAnimationManager>("PrevAnimManager");
    }

    public override void _Process(double delta)
    {

        Vector3 playerForward = -origin.GlobalTransform.Basis.Z;

        Camera3D cam = GetViewport().GetCamera3D();

        Vector3 toCam = cam.GlobalTransform.Origin - GlobalTransform.Origin;
        Vector3 flatToCam = toCam;
        flatToCam.Y = 0;

        if (flatToCam.LengthSquared() > 0.01f)
        {
            LookAt(GlobalTransform.Origin + flatToCam, Vector3.Up);
            RotateY(Mathf.Pi);
        }

        Vector3 spriteForward = -GlobalTransform.Basis.Z;
        Vector3 spriteRight = GlobalTransform.Basis.X;

        float forwardDot = playerForward.Dot(spriteForward);
        float rightDot = playerForward.Dot(spriteRight);

        AnimationManager.FacingType facing;
        if (forwardDot <= -0.35f && Mathf.Abs(rightDot) < 0.6f)
            facing = AnimationManager.FacingType.Front;
        else if (rightDot > 0.6f)
            facing = AnimationManager.FacingType.Right;
        else if (rightDot < -0.6f)
            facing = AnimationManager.FacingType.Left;
        else
            facing = AnimationManager.FacingType.Back;



        animManager.currentFacing = facing;


        Texture2D bodyFrame = animManager.GetCurrentBodyFrame();
        if (bodyFrame != null)
            Texture = bodyFrame;

        Texture2D faceFrame = animManager.GetCurrentFaceFrame();
        if (faceFrame == null)
        {
            facePivot.Hide();
            return;
        }

        AnimationManager.AnimFacingType animFacing = AnimationManager.ToAnimFacingType(facing);

        Vector2 bakedOffset = Vector2.Zero;
        if (animManager.currentPreset != null && PlayerData.CurrentCharacter.FaceOffsets.TryGetValue(animFacing, out var facingOffsets))
        {
            if (facingOffsets.TryGetValue(animManager.currentAnim, out var offsetFrames))
            {
                var frames = animManager.currentPreset.Animations[animFacing][animManager.currentAnim];
                int currentFrame = frames.IndexOf(animManager.GetCurrentBodyFrame());
                if (currentFrame >= 0 && currentFrame < offsetFrames.Count)
                    bakedOffset = offsetFrames[currentFrame];
            }
        }

        if (facing == AnimationManager.FacingType.Left)
            bakedOffset.X *= -1f;

        float perspectiveX = rightDot;
        float cameraHeightDelta = cam.GlobalTransform.Origin.Y - GlobalTransform.Origin.Y;
        float perspectiveY = Mathf.Clamp(cameraHeightDelta * 0.05f, -0.5f, 0.5f);

        float sidewaysPerspectiveX = (facing == AnimationManager.FacingType.Front)
            ? Mathf.Clamp(perspectiveX, -1f, 1f) * 0.2f
            : 0f;

        Vector3 finalPosition = new Vector3(
            bakedOffset.X + sidewaysPerspectiveX,
            bakedOffset.Y,
            0.003f
        );


        Vector3 pivotRotation = Vector3.Zero;
        bool flipH = false;

        switch (facing)
        {
            case AnimationManager.FacingType.Front:
                facePivot.Show();
                faceSprite.Texture = faceFrame;
                break;
            case AnimationManager.FacingType.Left:
                facePivot.Show();
                flipH = true;
                pivotRotation = new Vector3(0, Mathf.Pi, 0);
                faceSprite.Texture = faceFrame;
                break;
            case AnimationManager.FacingType.Right:
                facePivot.Show();
                faceSprite.Texture = faceFrame;
                break;
            case AnimationManager.FacingType.Back:
                facePivot.Hide();
                flipH = true;
                faceSprite.Position = Vector3.Zero;
                break;
        }

        FlipH = flipH;
        facePivot.Rotation = pivotRotation;
        facePivot.Position = finalPosition;


        //
        // HATS
        //

        Texture2D hatFrame = animManager.GetCurrentHatFrame();

        animFacing = AnimationManager.ToAnimFacingType(facing);

        int currentHatFrame = animManager.GetCurrentHatFrameIndex();
        Vector2 hatOffset = animManager.GetHatOffset(animFacing, animManager.currentAnim, currentHatFrame);


        Vector3 finalHatPos = new Vector3(hatOffset.X * (facing == AnimationManager.FacingType.Left ? -1 : 1), hatOffset.Y, 0.003f);


        if (facing == AnimationManager.FacingType.Left) hatOffset.X *= -1f;

        if (facing == AnimationManager.FacingType.Back) hatSprite.FlipH = true;
        else hatSprite.FlipH = false;


        if (hatFrame == null)
        {
            hatPivot.Hide();
        }
        else
        {
            hatPivot.Show();
            hatSprite.Texture = hatFrame;
        }

        hatPivot.Rotation = pivotRotation;
        hatPivot.Position = finalHatPos;

    }

    public void UpdateCosmeticColor()
    {
        if (animManager.currentHat != null)
        {
            hatSprite.Modulate = animManager.currentHat.modulatesWithPlayerColor
                ? new Color(SettingsManager.CurrentSettings.ColorR, SettingsManager.CurrentSettings.ColorG, SettingsManager.CurrentSettings.ColorB)
                : Colors.White;
        }

    }


}

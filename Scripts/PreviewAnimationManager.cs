using Godot;
using System;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Linq;

[GlobalClass]
public partial class PreviewAnimationManager : Node
{
    public Sprite3D bodySprite => GetParent().GetNode<Sprite3D>("Person");
    public Sprite3D faceSprite => GetParent().GetNode<Sprite3D>("Person/FacePivot/Face");

    [Export] public AnimationManager.PlayerAnimTypes currentAnim;
    public AnimationManager.FacingType currentFacing = AnimationManager.FacingType.Front;
    public Cosmetic currentHat;

    private int frameIndex = 0;
    private float frameTime = 0f;
    private float frameDelay = 0.15f;

    private int faceFrameIndex = 0;
    private float faceFrameTime = 0f;
    private float faceFrameDelay = 0.2f;

    private bool isUsingEmote = false;
    private float emoteDuration = 5f;
    private float emoteTimer = 0f;
    float rotation = 0;

    private FacialExpression currentExpression;
    public CharacterTypePreset currentPreset;

    public override void _Ready()
    {
        currentPreset = CosmeticDatabase.Characters.First();
        SetExpression(CosmeticDatabase.Expressions[SettingsManager.CurrentSettings.SavedExpressionID]);
    }

    public override void _Process(double delta)
    {
        frameTime += (float)delta;
        faceFrameTime += (float)delta;

        if (frameTime >= frameDelay)
        {
            frameTime = 0f;
            AdvanceFrame();
        }
        if (faceFrameTime >= faceFrameDelay)
        {
            faceFrameTime = 0f;
            AdvanceFaceFrame();
        }

        var facePivot = GetParent().GetNode<Node3D>("Person/FacePivot");

        AnimationManager.AnimFacingType animfacing = currentFacing switch
        {
            AnimationManager.FacingType.Front => AnimationManager.AnimFacingType.Front,
            AnimationManager.FacingType.Back => AnimationManager.AnimFacingType.Back,
            AnimationManager.FacingType.Left => AnimationManager.AnimFacingType.Side,
            AnimationManager.FacingType.Right => AnimationManager.AnimFacingType.Side,
            _ => AnimationManager.AnimFacingType.Front
        };

        var hatPivot = GetParent().GetNode<Node3D>("Person/HatPivot");

        if (PlayerData.CurrentCharacter.HatOffsets.TryGetValue(animfacing, out var hatFacingDict))
        {
            if (hatFacingDict.TryGetValue(currentAnim, out var offsets) && frameIndex < offsets.Count)
            {
                hatPivot.Position = new Vector3(offsets[frameIndex].X, offsets[frameIndex].Y, hatPivot.Position.Z);
            }
        }
        else
        {
            hatPivot.Position = new Vector3(0, 0.458f, 0.005f);
        }


        SetExpression(CosmeticDatabase.Expressions[SettingsManager.CurrentSettings.SavedExpressionID]);
        if (currentPreset != PlayerData.CurrentCharacter) SetCharacter(PlayerData.CurrentCharacter);
        SetHat(CosmeticDatabase.Hats[SettingsManager.CurrentSettings.SavedHatID]);


        rotation += (float)delta;

        (GetParent() as Node3D).Rotation = new Vector3(0, rotation, 0);
    }









    public void PlayAnim(AnimationManager.PlayerAnimTypes anim)
    {
        if (anim == currentAnim) return;
        currentAnim = anim;
        frameIndex = 0;
        frameTime = 0f;
    }

    private void AdvanceFrame()
    {
        Array<Texture2D> frames = GetCurrentFrameList();
        if (frames == null || frames.Count == 0) return;

        frameIndex = (frameIndex + 1) % frames.Count;

        bodySprite.Texture = frames[frameIndex];
    }

    private void AdvanceFaceFrame()
    {
        if (currentExpression == null || faceSprite == null) return;

        Array<Texture2D> faceFrames = currentFacing switch
        {
            AnimationManager.FacingType.Front => currentExpression.FrontExpression,
            AnimationManager.FacingType.Back => currentExpression.FrontExpression,
            AnimationManager.FacingType.Left => currentExpression.SideExpression,
            AnimationManager.FacingType.Right => currentExpression.SideExpression,
            _ => currentExpression.FrontExpression
        };

        if (faceFrames.Count == 0) return;

        faceFrameIndex = (faceFrameIndex + 1) % faceFrames.Count;
        faceSprite.Texture = faceFrames[faceFrameIndex];
    }

    private Array<Texture2D> GetCurrentFrameList()
    {
        AnimationManager.AnimFacingType animfacing = currentFacing switch
        {
            AnimationManager.FacingType.Front => AnimationManager.AnimFacingType.Front,
            AnimationManager.FacingType.Back => AnimationManager.AnimFacingType.Back,
            AnimationManager.FacingType.Left => AnimationManager.AnimFacingType.Side,
            AnimationManager.FacingType.Right => AnimationManager.AnimFacingType.Side,
            _ => AnimationManager.AnimFacingType.Front
        };

        if (PlayerData.CurrentCharacter.Animations.TryGetValue(animfacing, out var facingDict))
        {
            if (facingDict.TryGetValue(currentAnim, out var animFrames) && animFrames.Count > 0)
            {
                return animFrames;
            }
        }

        if (animfacing == AnimationManager.AnimFacingType.Back &&
            PlayerData.CurrentCharacter.Animations.TryGetValue(AnimationManager.AnimFacingType.Front, out var frontDict))
        {
            if (frontDict.TryGetValue(currentAnim, out var fallbackFrames) && fallbackFrames.Count > 0)
            {
                return fallbackFrames;
            }
        }

        return null;
    }


    public void SetExpression(FacialExpression expression)
    {
        currentExpression = expression;
    }

    public void SetCharacter(CharacterTypePreset preset)
    {
        if (preset == null) return;

        currentPreset = preset;

        frameIndex = 0;
        frameTime = 0f;
    }

    public Texture2D GetCurrentBodyFrame()
    {
        Array<Texture2D> list = GetCurrentFrameList();
        if (list == null || list.Count == 0) return null;
        if (frameIndex > list.Count - 1)
            frameIndex = 0;
        return (list != null && list.Count > 0) ? list[frameIndex] : null;
    }

    public Texture2D GetCurrentFaceFrame()
    {
        if (currentExpression == null) return null;

        var faces = currentFacing switch
        {
            AnimationManager.FacingType.Front => currentExpression.FrontExpression,
            AnimationManager.FacingType.Left or AnimationManager.FacingType.Right => currentExpression.SideExpression,
            _ => currentExpression.FrontExpression
        };

        if (faceFrameIndex > faces.Count - 1)
            faceFrameIndex = 0;

        return (faces != null && faces.Count > 0) ? faces[faceFrameIndex] : null;
    }

    public static AnimationManager.AnimFacingType ToAnimFacingType(AnimationManager.FacingType facing) =>
    facing == AnimationManager.FacingType.Front ? AnimationManager.AnimFacingType.Front :
    facing == AnimationManager.FacingType.Back ? AnimationManager.AnimFacingType.Back : AnimationManager.AnimFacingType.Side;

    public void SetHat(Cosmetic hat)
    {
        currentHat = hat;
        GetParent().GetNode<PreviewVisualManager>("Person").UpdateCosmeticColor();
    }


    public Texture2D GetCurrentHatFrame()
    {
        if (currentHat == null)
            return null;

        Array<Texture2D> hatFrames;

        switch (currentFacing)
        {
            case AnimationManager.FacingType.Front:
                hatFrames = currentHat.FrontSprites;
                break;

            case AnimationManager.FacingType.Back:
                if (currentHat.BackSprites != null && currentHat.BackSprites.Count > 0)
                    hatFrames = currentHat.BackSprites;
                else
                    hatFrames = currentHat.FrontSprites;
                break;


            case AnimationManager.FacingType.Left:
            case AnimationManager.FacingType.Right:
                if (currentHat.SideSprites != null && currentHat.SideSprites.Count > 0)
                    hatFrames = currentHat.SideSprites;
                else
                    hatFrames = currentHat.FrontSprites;
                break;

            default:
                hatFrames = currentHat.FrontSprites;
                break;
        }

        if (hatFrames == null || hatFrames.Count == 0)
            return null;

        int idx = frameIndex % hatFrames.Count;
        return hatFrames[idx];
    }

    public Vector2 GetHatOffset(AnimationManager.AnimFacingType facing, AnimationManager.PlayerAnimTypes anim, int frame)
    {
        Godot.Collections.Dictionary<AnimationManager.PlayerAnimTypes, Godot.Collections.Array<Vector2>> hatDict;


        if (!currentPreset.HatOffsets.TryGetValue(facing, out hatDict))
        {
            currentPreset.HatOffsets.TryGetValue(AnimationManager.AnimFacingType.Front, out hatDict);
        }

        if (hatDict != null && hatDict.TryGetValue(anim, out var offsets))
        {
            if (frame >= 0 && frame < offsets.Count)
                return offsets[frame];
        }

        return Vector2.Zero;
    }

    public int GetCurrentHatFrameIndex() => frameIndex;
    


}

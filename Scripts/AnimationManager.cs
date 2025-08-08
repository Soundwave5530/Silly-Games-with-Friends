
using Godot;
using System;
using Godot.Collections;
using System.Linq;

[GlobalClass]
public partial class AnimationManager : Node
{
    [Signal] public delegate void AnimationFinishedEventHandler(PlayerAnimTypes animType);

    public enum PlayerAnimTypes
    {
        Idle,
        Walk,
        Jump,
        Fall,
        CrouchIdle,
        CrouchWalk,
        SlideFaceFirst,
        SlideFeetFirst,
        Wave,
        SwimIdle,
        Swim,
        CarryIdle,
        Carry
    };

    public enum AnimFacingType { Front, Back, Side };
    public enum FacingType { Front, Back, Left, Right };

    public Player player => GetParent() as Player;

    public Sprite3D bodySprite => GetParent().GetNode<Sprite3D>("Person");
    public Sprite3D faceSprite => GetParent().GetNode<Sprite3D>("Person/FacePivot/Face");

    public PlayerAnimTypes currentAnim;
    public FacingType currentFacing = FacingType.Front;
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

        if (isUsingEmote)
        {
            emoteTimer -= (float)delta;
            if (emoteTimer <= 0f)
            {
                SetExpression(CosmeticDatabase.Expressions[player.SyncExpressionId]);
                isUsingEmote = false;
            }
        }

        var facePivot = GetParent().GetNode<Node3D>("Person/FacePivot");

        AnimFacingType animfacing = currentFacing switch
        {
            FacingType.Front => AnimFacingType.Front,
            FacingType.Back => AnimFacingType.Back,
            FacingType.Left => AnimFacingType.Side,
            FacingType.Right => AnimFacingType.Side,
            _ => AnimFacingType.Front
        };
        
        /*
        if (PlayerData.CurrentCharacter.FaceOffsets.TryGetValue(animfacing, out var facingDict))
        {
            if (facingDict.TryGetValue(currentAnim, out var offsets))
            {
                if (frameIndex < offsets.Count)
                {
                    facePivot.Position = new Vector3(offsets[frameIndex].X, offsets[frameIndex].Y, facePivot.Position.Z);
                }
            }
        }
        else
        {
            facePivot.Position = new Vector3(0, 0.313f, facePivot.Position.Z);
        }
        */

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

        if (player != null && player.IsMultiplayerAuthority())
        {
            player.SyncAnimType = (int)currentAnim;
            player.SyncExpressionId = SettingsManager.CurrentSettings.SavedExpressionID;
            player.SyncHatId = SettingsManager.CurrentSettings.SavedHatID;
            SetExpression(CosmeticDatabase.Expressions[SettingsManager.CurrentSettings.SavedExpressionID]);
            if (currentPreset != PlayerData.CurrentCharacter) SetCharacter(PlayerData.CurrentCharacter);
            SetHat(CosmeticDatabase.Hats[SettingsManager.CurrentSettings.SavedHatID]);
        }
        else if (player != null)
        {
            currentAnim = (PlayerAnimTypes)player.SyncAnimType;
            SetExpression(CosmeticDatabase.Expressions[player.SyncExpressionId]);
            if (currentPreset == null || currentPreset.Name != player.SyncCharacterId)
                SetCharacter(CosmeticDatabase.Characters.Find(p => p.Name == player.SyncCharacterId));

            if (player.SyncHatId != "")
            {
                SetHat(CosmeticDatabase.Hats[player.SyncHatId]);
            }
        }
        else
        {
            GD.Print("[Animation Manager]\t Error, player is invalid");
        }
    }


    public void PlayAnim(PlayerAnimTypes anim)
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
        if (frameIndex == frames.Count - 1)
            EmitSignal(SignalName.AnimationFinished, (int)currentAnim);
        bodySprite.Texture = frames[frameIndex];
    }

    private void AdvanceFaceFrame()
    {
        if (currentExpression == null || faceSprite == null) return;

        Array<Texture2D> faceFrames = currentFacing switch
        {
            FacingType.Front => currentExpression.FrontExpression,
            FacingType.Back => currentExpression.FrontExpression,
            FacingType.Left => currentExpression.SideExpression,
            FacingType.Right => currentExpression.SideExpression,
            _ => currentExpression.FrontExpression
        };

        if (faceFrames.Count == 0) return;

        faceFrameIndex = (faceFrameIndex + 1) % faceFrames.Count;
        faceSprite.Texture = faceFrames[faceFrameIndex];
    }

    private Array<Texture2D> GetCurrentFrameList()
    {
        AnimFacingType animfacing = currentFacing switch
        {
            FacingType.Front => AnimFacingType.Front,
            FacingType.Back => AnimFacingType.Back,
            FacingType.Left => AnimFacingType.Side,
            FacingType.Right => AnimFacingType.Side,
            _ => AnimFacingType.Front
        };

        if (PlayerData.CurrentCharacter.Animations.TryGetValue(animfacing, out var facingDict))
        {
            if (facingDict.TryGetValue(currentAnim, out var animFrames) && animFrames.Count > 0)
            {
                return animFrames;
            }
        }

        if (animfacing == AnimFacingType.Back &&
            PlayerData.CurrentCharacter.Animations.TryGetValue(AnimFacingType.Front, out var frontDict))
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

    public void PlayEmote(FacialExpression emote, float duration = 5f)
    {
        if (emote == null) return;
        SetExpression(emote);
        isUsingEmote = true;
        emoteTimer = duration;
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
            FacingType.Front => currentExpression.FrontExpression,
            FacingType.Left or FacingType.Right => currentExpression.SideExpression,
            _ => currentExpression.FrontExpression
        };

        if (faceFrameIndex > faces.Count - 1)
            faceFrameIndex = 0;

        return (faces != null && faces.Count > 0) ? faces[faceFrameIndex] : null;
    }

    public static AnimFacingType ToAnimFacingType(FacingType facing) =>
    facing == FacingType.Front ? AnimFacingType.Front :
    facing == FacingType.Back ? AnimFacingType.Back : AnimFacingType.Side;

    public void SetHat(Cosmetic hat)
    {
        currentHat = hat;
        GetParent<Player>().GetNode<CharacterVisualManager>("Person").UpdateCosmeticColor();
    }



    public Texture2D GetCurrentHatFrame()
    {
        if (currentHat == null)
            return null;

        Array<Texture2D> hatFrames;

        switch (currentFacing)
        {
            case FacingType.Front:
                hatFrames = currentHat.FrontSprites;
                break;

            case FacingType.Back:
                if (currentHat.BackSprites != null && currentHat.BackSprites.Count > 0)
                    hatFrames = currentHat.BackSprites;
                else
                    hatFrames = currentHat.FrontSprites; 
                break;


            case FacingType.Left:
            case FacingType.Right:
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



    public Vector2 GetHatOffset(AnimFacingType facing, PlayerAnimTypes anim, int frame)
    {
        Godot.Collections.Dictionary<PlayerAnimTypes, Godot.Collections.Array<Vector2>> hatDict;


        if (!currentPreset.HatOffsets.TryGetValue(facing, out hatDict))
        {
            currentPreset.HatOffsets.TryGetValue(AnimFacingType.Front, out hatDict);
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

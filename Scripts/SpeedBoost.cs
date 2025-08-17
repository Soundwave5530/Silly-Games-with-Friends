using Godot;
using System;

public partial class SpeedBoost : Area3D
{
    [Export] public float SpeedMultiplier = 2.0f;
    [Export] public float Duration = 5.0f;
    [Export] public float Cooldown = 10.0f;

    public bool AbleToBeGrabbed = true;
    private float cooldownTimer = 0.0f;
    private Player playerThatUsed = null;
    private bool isOnCooldown = false;

    private CollisionShape3D collisionShape;
    private MeshInstance3D meshInstance;
    public StandardMaterial3D material = new();

    private static Color enabledColor = new Color(1f, 1f, 0f);
    private static Color disabledColor = new Color(1f, 0f, 0f);
    private static Color activatedColor = new Color(0f, 1f, 0f);

    private const string SPEED_BOOST_MODIFIER_ID = "speedboost_pickup";

    public override void _Ready()
    {
        collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        collisionShape.Disabled = false;

        BodyEntered += Activate;

        material.Set("albedo_color", AbleToBeGrabbed ? enabledColor : disabledColor);
        material.RimEnabled = true;
        material.Rim = 1.0f;

        meshInstance.SetSurfaceOverrideMaterial(0, material);
    }

    public override void _Process(double delta)
    {
        meshInstance.RotateY((float)delta * 2.0f);

        if (isOnCooldown)
        {
            cooldownTimer -= (float)delta;
            if (cooldownTimer <= 0.0f)
            {
                CooldownEnded();
            }
        }

        // Check if the current user still has the modifier active
        if (playerThatUsed != null)
        {
            if (!playerThatUsed.HasSpeedModifier(SPEED_BOOST_MODIFIER_ID))
            {
                // Speed boost expired, reset our state
                GD.Print("[Speed Boost] Speed boost effect expired for player");
                playerThatUsed = null;
                material.Set("albedo_color", disabledColor);
            }
        }
    }

    public void Activate(Node activator)
    {
        if (activator is not Player player || !AbleToBeGrabbed) return;

        // Check if player already has a speed boost
        if (player.HasSpeedModifier(SPEED_BOOST_MODIFIER_ID))
        {
            GD.Print("[Speed Boost] Player already has a speed boost active!");
            return;
        }

        GD.Print($"[Speed Boost] Activating speed boost for {player.Name}!");

        playerThatUsed = player;
        
        // Add speed modifier to the player using the integrated system
        player.AddSpeedModifier(SPEED_BOOST_MODIFIER_ID, SpeedMultiplier, 0f, Duration);

        // Visual feedback
        SetDeferred(nameof(collisionShape.Disabled), true);
        AbleToBeGrabbed = false;
        material.Set("albedo_color", AbleToBeGrabbed ? enabledColor : disabledColor);
        // Start cooldown
        cooldownTimer = Cooldown;
        isOnCooldown = true;

        // Optional: Add some particle effects or sound here
        CreateSpeedBoostEffect(player);
    }

    public void CooldownEnded()
    {
        GD.Print("[Speed Boost] Cooldown Ended");
        collisionShape.Disabled = false;
        material.Set("albedo_color", enabledColor);
        AbleToBeGrabbed = true;
        isOnCooldown = false;
        playerThatUsed = null;
    }

    private void CreateSpeedBoostEffect(Player player)
    {
        // You can add particle effects, sound, or other visual feedback here
        // For example:
        // var particles = GetNode<GPUParticles3D>("SpeedBoostParticles");
        // if (particles != null) particles.Emitting = true;
        
        GD.Print($"[Speed Boost] Speed boost effect created for {player.Name}");
    }

    // Optional: Method to get remaining boost time for UI display
    public float GetRemainingBoostTime()
    {
        if (playerThatUsed == null) return 0f;
        
        return playerThatUsed.GetSpeedModifierTimeRemaining(SPEED_BOOST_MODIFIER_ID);
    }

    public void ForceEndBoost()
    {
        if (playerThatUsed != null)
        {
            playerThatUsed.RemoveSpeedModifier(SPEED_BOOST_MODIFIER_ID);
            playerThatUsed = null;
        }
    }
}
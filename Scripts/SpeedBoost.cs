using Godot;
using System;

public partial class SpeedBoost : Area3D
{
    [Export] public float SpeedMultiplier = 2.0f;
    [Export] public float Duration = 5.0f;
    [Export] public float Cooldown = 10.0f;


    public bool AbleToBeGrabbed = true;
    private float timer = 0.0f;
    private float cooldownTimer = 0.0f;
    private Player playerThatUsed = null;
    private bool isCurrentlyActive = false;

    private CollisionShape3D collisionShape;
    private MeshInstance3D meshInstance;

    private static Color enabledColor = new Color(1f, 1f, 0f);
    private static Color disabledColor = new Color(1f, 0f, 0f);

    public override void _Ready()
    {
        collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        collisionShape.Disabled = false;

        BodyEntered += Activate;

        meshInstance.GetActiveMaterial(0).Set("albedo_color", AbleToBeGrabbed ? enabledColor : disabledColor);

        GD.Print("[Speed Boost] Ready. AbleToBeGrabbed: ", AbleToBeGrabbed);
    }
    public override void _PhysicsProcess(double delta)
    {
        meshInstance.RotateY((float)delta * 2.0f);

        if (!isCurrentlyActive) return;

        if (playerThatUsed != null)
        {
            timer -= (float)delta;
            if (timer <= 0.0f)
            {
                Deactivate();
            }
        }
        else
        {
            cooldownTimer -= (float)delta;
            if (cooldownTimer <= 0.0f)
            {
                CooldownEnded();
            }
        }
    }

    public void Activate(Node activater)
    {
        if (activater is not Player player || !AbleToBeGrabbed) return;

        GD.Print("[Speed Boost] SIGMA!!!!!!!");

        playerThatUsed = player;
        playerThatUsed.Speed *= SpeedMultiplier;

        timer = Duration;
        cooldownTimer = Cooldown;

        meshInstance.GetActiveMaterial(0).Set("albedo_color", disabledColor);
        SetDeferred(nameof(collisionShape.Disabled), true);
        AbleToBeGrabbed = false;
        isCurrentlyActive = true;
    }

    public void Deactivate()
    {
        GD.Print("[Speed Boost] Deactivating SpeedBoost");
        playerThatUsed.Speed /= SpeedMultiplier;
        playerThatUsed = null;
    }

    public void CooldownEnded()
    {
        GD.Print("[Speed Boost] Cooldown Ended");
        collisionShape.Disabled = false;
        meshInstance.GetActiveMaterial(0).Set("albedo_color", enabledColor);
        AbleToBeGrabbed = true;

        isCurrentlyActive = false;
    }
}

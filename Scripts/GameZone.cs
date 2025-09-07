using Godot;
using System;

[Tool]
public partial class GameZone : Area3D
{
    // The size of the game zone for visuals
    [Export] public Vector3 GameZoneSize = new Vector3(1, 1, 1);
    [Export] public Vector3 ResetPosition = Vector3.Zero;

    public override void _Ready()
    {
        BodyExited += PlayerLeftZone;
        GetNode<CollisionShape3D>("ZoneCShape").Shape = new BoxShape3D() { Size = GameZoneSize };
    }
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            (GetNode<CollisionShape3D>("ZoneCShape").Shape as BoxShape3D).Size = GameZoneSize;
    }


    // The function for if a player leaves the game zone, push them back in
    public void PlayerLeftZone(Node3D body)
    {
        if (body is Player player && player == NetworkManager.Instance.GetLocalPlayer())
        {
            if (player.GlobalPosition.Y < (GlobalPosition.Y - GameZoneSize.Y / 2f))
            {
                // Player fell below the zone, reset their position to (0,0,0)
                player.GlobalPosition = ResetPosition;
                player.Velocity = Vector3.Zero;
                return;
            }
            
            Vector3 zoneCenter = GlobalPosition;
            
            Vector3 toCenter = zoneCenter - player.GlobalPosition;
            toCenter.Y = 0; // We'll handle Y separately
            
            float boundsDistance = Mathf.Max(
                Mathf.Abs(toCenter.X) - (GameZoneSize.X / 2f),
                Mathf.Abs(toCenter.Z) - (GameZoneSize.Z / 2f)
            );
            
            // Calculate bounce force (stronger the further out we are)
            float bounceStrength = toCenter.Length() / 2f; // Strength based on distance from center
            Vector3 bounceForce = toCenter.Normalized() * bounceStrength;
            
            // Add upward force to help prevent getting stuck
            bounceForce += Vector3.Up / 3f;
            
            // Apply the force through the new system
            player.ApplyExternalForce(bounceForce, 0.5f);
            
            // Show warning message
            StatusMessageManager.Instance?.ShowMessage("You are leaving the play area!", StatusMessageManager.MessageType.Warning);
            GD.Print($"[GameZone] Applied bounce force: {bounceForce} to player at position {player.GlobalPosition}");
        }
    }
}

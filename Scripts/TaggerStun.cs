using Godot;
using System;

public partial class TaggerStun : Area3D
{
    [Export] public float StunDuration = 3f;
    [Export] public float Cooldown = 10f;
    [Export] public Color availableColor = new Color(1, 0, 0); // Red
    [Export] public Color cooldownColor = new Color(0.5f, 0.5f, 0.5f);

    public bool AbleToBeGrabbed = true;
    private float cooldownTimer = 0.0f;
    private bool isOnCooldown = false;

    private MeshInstance3D meshInstance;
    private CollisionShape3D collisionShape;
    public StandardMaterial3D material = new();

    private const string STUN_ID = "stun_freeze";

    public override void _Ready()
    {
        meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        collisionShape.Disabled = false;

        BodyEntered += OnBodyEntered;

        material.AlbedoColor = AbleToBeGrabbed ? availableColor : cooldownColor;
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
    }

    private void OnBodyEntered(Node3D body)
    {
        // Only process pickup logic on server
        if (!Multiplayer.IsServer()) return;
        
        // Check if pickup is available
        if (!AbleToBeGrabbed) return;

        if (body is Player player)
        {
            GD.Print($"[TaggerStun] Player {player.Name} (Authority: {player.GetMultiplayerAuthority()}) entered pickup");
            
            // Get current tagger ID
            int currentTagger = GameManager.Instance.GetCurrentTagger();
            GD.Print($"[TaggerStun] Current tagger ID: {currentTagger}");
            
            // Check if there is a valid tagger
            if (currentTagger == -1)
            {
                GD.Print("[TaggerStun] No tagger found!");
                return;
            }
            
            string playerName = NetworkManager.Instance.PlayerNames.TryGetValue(player.GetMultiplayerAuthority(), out var pName) ? pName : "Unknown";
            string taggerName = NetworkManager.Instance.PlayerNames.TryGetValue(currentTagger, out var tName) ? tName : "Unknown";
            
            GD.Print($"[TaggerStun] {playerName} stunned the tagger ({taggerName}) for {StunDuration} seconds!");
            NetworkManager.Instance.SendSystemMessage($"{playerName} stunned the tagger for {StunDuration} seconds!");

            // Apply stun to the tagger via RPC - this will run on the tagger's client
            Rpc(nameof(ApplyStunToTagger), currentTagger, StunDuration);
            ApplyStunToTagger(currentTagger, StunDuration); // Also apply on server

            // Notify all clients to update visual state
            Rpc(nameof(SetPickupUsed));
            SetPickupUsed(); // Also call locally on server
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ApplyStunToTagger(int taggerId, float duration)
    {
        // Find the tagger player object on this client
        Player taggerPlayer = NetworkManager.Instance.GetPlayerFromID(taggerId);
        if (taggerPlayer == null)
        {
            GD.Print($"[TaggerStun] Could not find tagger player object for ID {taggerId} on this client");
            return;
        }
        
        GD.Print($"[TaggerStun] Applying stun to tagger {taggerPlayer.Name} for {duration} seconds");
        
        // Apply stun effect on this client
        taggerPlayer.AddSpeedModifier(STUN_ID, 0f, 0f, duration);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SetPickupUsed()
    {
        // Visual feedback - runs on all clients
        // Use SetDeferred to avoid collision query flush errors
        SetDeferred(nameof(SetCollisionDisabled), true);
        AbleToBeGrabbed = false;
        material.AlbedoColor = cooldownColor;
        
        // Start cooldown timer - only on server
        if (Multiplayer.IsServer())
        {
            cooldownTimer = Cooldown;
            isOnCooldown = true;
            
            // Schedule reset
            GetTree().CreateTimer(Cooldown).Timeout += () => {
                Rpc(nameof(ResetPickup));
                ResetPickup();
            };
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ResetPickup()
    {
        GD.Print("[TaggerStun] Reset and available for use");
        SetDeferred(nameof(SetCollisionDisabled), false);
        material.AlbedoColor = availableColor;
        AbleToBeGrabbed = true;
        isOnCooldown = false;
    }

    private void CooldownEnded()
    {
        if (Multiplayer.IsServer())
        {
            Rpc(nameof(ResetPickup));
            ResetPickup();
        }
    }

    // Helper method to safely set collision state
    private void SetCollisionDisabled(bool disabled)
    {
        if (collisionShape != null)
        {
            collisionShape.Disabled = disabled;
        }
    }
}
using Godot;
using System;

public partial class BeachBall : RigidBody3D
{
    [Export] public bool IsHeld = false;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private const int ServerId = 1;

    public override void _Ready()
    {
        AddToGroup("carryable");

        if (Multiplayer.IsServer())
        {
            if (GetMultiplayerAuthority() == 0)
                SetMultiplayerAuthority(ServerId);
        }

        targetPosition = GlobalPosition;
        targetRotation = GlobalTransform.Basis.GetRotationQuaternion();
    }

    public override void _PhysicsProcess(double delta)
    {
        bool amAuthority = IsMultiplayerAuthority();

        if (!amAuthority)
        {
            // We’re a client watching the ball
            Freeze = true;

            GlobalPosition = GlobalPosition.Lerp(targetPosition, 10f * (float)delta);
            Quaternion interpRot = GlobalTransform.Basis.GetRotationQuaternion().Slerp(targetRotation, 10f * (float)delta);
            GlobalTransform = new Transform3D(new Basis(interpRot), GlobalPosition);
        }
        else
        {
            // We’re the authority – either player or server
            Freeze = IsHeld;

            if (IsHeld)
            {
                // Lock to carry socket
                var localPlayer = NetworkManager.Instance.GetLocalPlayer();
                /*
                if (localPlayer != null && localPlayer.carriedObject == this)
                {
                    GlobalPosition = localPlayer.carrySocket.GlobalPosition;
                    LinearVelocity = Vector3.Zero;
                    AngularVelocity = Vector3.Zero;
                }
                */
            }
            else
            {
                // Free physics – keep synced
                if (GlobalPosition.Y < -10)
                {
                    GlobalPosition = new Vector3(3 * GD.Randf(), 2, 3 * GD.Randf());
                    LinearVelocity = Vector3.Zero;
                    AngularVelocity = Vector3.Zero;
                }

                // Server syncs to everyone else
                foreach (int peerId in NetworkManager.Instance.InGamePlayers)
                {
                    if (peerId != Multiplayer.GetUniqueId())
                        RpcId(peerId, nameof(SyncTransform), GlobalPosition, GlobalTransform.Basis.GetRotationQuaternion());
                }
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncTransform(Vector3 pos, Quaternion rot)
    {
        // Let server and non-authoritative peers accept transform sync
        if (Multiplayer.GetUniqueId() != GetMultiplayerAuthority())
        {
            targetPosition = pos;
            targetRotation = rot;
        }
    }

    //
    // === PICKUP/DROP AUTHORITY ===
    //

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void RequestPickupAuthority()
    {
        if (!Multiplayer.IsServer()) return;

        int requestingPeer = Multiplayer.GetRemoteSenderId();
        SetMultiplayerAuthority(requestingPeer);
        Rpc(nameof(SyncAuthority), requestingPeer);
        Rpc(nameof(SyncHeldState), true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void RequestDropAuthority()
    {
        if (!Multiplayer.IsServer()) return;

        SetMultiplayerAuthority(ServerId);
        Rpc(nameof(SyncAuthority), ServerId);
        Rpc(nameof(SyncHeldState), false);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncAuthority(int newAuthority)
    {
        SetMultiplayerAuthority(newAuthority);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SyncHeldState(bool held)
    {
        IsHeld = held;
    }
}

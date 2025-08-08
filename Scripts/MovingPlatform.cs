using Godot;
using System;

public partial class MovingPlatform : AnimatableBody3D
{
    [Export] public Path3D Path3D;
    public Curve3D Curve;

    [Export] public float Speed = 5f;
    [Export] public bool Loop = true;
    [Export] public float WaitBetweenLoops = 3f;

    public Vector3 Velocity { get; private set; }

    private float distanceTraveled = 0f;
    private float maxLength;
    private bool waiting = false;
    private float waitTimer = 0f;

    public Vector3 SyncGlobalPosition;

    public override void _Ready()
    {
        Curve = Path3D.Curve;
        SetMultiplayerAuthority(1);
        if (Curve != null && Curve.GetPointCount() >= 2) maxLength = Curve.GetBakedLength();

        GlobalPosition = Curve.GetPointPosition(0);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority())
        {
            GlobalPosition = GlobalPosition.Lerp(SyncGlobalPosition, 10f * (float)delta);
            return;
        }
        if (Curve == null || Curve.GetPointCount() < 2) return;

        if (waiting)
        {
            waitTimer -= (float)delta;
            if (waitTimer <= 0f)
                waiting = false;

            return;
        }

        distanceTraveled += Speed * (float)delta;

        if (!Loop && distanceTraveled >= maxLength)
        {
            distanceTraveled = maxLength;
        }
        else if (Loop && distanceTraveled >= maxLength)
        {
            distanceTraveled = 0f;
            waiting = true;
            waitTimer = WaitBetweenLoops;
        }

        var newPos = Curve.SampleBaked(distanceTraveled);
        GlobalPosition = newPos;

        foreach (int peerId in NetworkManager.Instance.InGamePlayers)
        {
            RpcId(peerId, nameof(RpcSyncPosition), newPos);
        }

    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RpcSyncPosition(Vector3 position)
    {
        if (IsMultiplayerAuthority()) return;
        SyncGlobalPosition = position;
        
    }

}

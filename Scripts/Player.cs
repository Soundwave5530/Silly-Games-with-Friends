using Godot;
using System;

[GlobalClass]
public partial class Player : CharacterBody3D
{
    [ExportSubgroup("Settings")]
    [Export] public float Speed = 5f;
    [Export] public float JumpForce = 8f;
    [Export] public float Gravity = 9.8f;
    [Export] public float MouseSensitivity = 0.005f;

    [Export] public Vector3 SyncGlobalPosition;
    [Export] public float SyncRotation;
    [Export] public float SyncCameraPitch;

    [Export] public int SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Idle;
    [Export] public string SyncExpressionId = "smile";
    [Export] public string SyncCharacterId = "goober";
    [Export] public string SyncHatId = "none";

    private Vector3 velocity = Vector3.Zero;
    private Label3D nameLabel;
    private CollisionShape3D collision;
    private Node3D headPivot;

    private float pitch = 0f;
    private float yaw = 0f;
    public bool crouching = false;
    public int perspectiveMode = 0;

    public string displayName = "";

    private Sprite3D playerSprite;

    public Color playerColor;

    private bool isEmoting = false;
    private float emoteTimer = 0f;

    public Camera3D Camera;

    private Marker3D perspective1 => GetNode<Marker3D>("HeadPivot/FirstPerson");
    private Marker3D perspective2 => GetNode<Marker3D>("HeadPivot/SecondPerson");
    private Marker3D perspective3 => GetNode<Marker3D>("HeadPivot/ThirdPerson");

    private AnimationManager animationManager => GetNode<AnimationManager>("AnimationManager");

    private Vector3 targetCameraPosition = new();
    private Vector3 targetCameraRotation = new();

    private bool canRotateCamera = true;
    private bool wasOnFloor = false;

    public bool isSwimming = false;

    private RayCast3D interactRay;
    //public Node3D carriedObject = null;
    //public Marker3D carrySocket;
    //public bool isCarrying => carriedObject != null;

    private bool lastChatState = false;

    bool CanCrouch() => IsOnFloor() && !isSwimming && !NewPauseMenu.IsOpen && !ChatManager.chatOpen;

    public void SetDisplayName(string name)
    {
        displayName = name;

        if (nameLabel != null) nameLabel.Text = name;
    }

    public void SetPlayerColor(Color color)
    {
        // Don't allow color changes during tag game unless it's from the GameManager
        if (GameManager.Instance != null && 
            GameManager.Instance.GetCurrentGameType() == GameManager.GameType.Tag && 
            GameManager.Instance.GetCurrentState() == GameManager.GameState.Playing && 
            !GameManager.Instance.IsColorChangeFromGame)
        {
            return;
        }

        // Store the color even if nodes aren't ready yet
        playerColor = color;

        // Wait until _Ready has initialized our nodes
        if (!ArePlayerNodesReady())
            return;

        // Apply color to visual elements
        if (playerSprite != null)
            playerSprite.Modulate = color;
            
        if (nameLabel != null)
            nameLabel.Modulate = color;

        var visualManager = GetNodeOrNull<CharacterVisualManager>("Person");
        visualManager?.UpdateCosmeticColor();
    }

    private bool ArePlayerNodesReady()
    {
        return IsInsideTree() && playerSprite != null && nameLabel != null;
    }

    public void SetTeam(int teamId)
    {
        PlayerData.TeamId = teamId;
        int peerId = Multiplayer.GetUniqueId();
        TeamManager.Instance.AddPlayerToTeam(peerId, teamId);

        SetPlayerColor(TeamManager.Instance.GetTeamColor(teamId));
    }

    private bool IsMultiplayerValid()
    {
        try
        {
            if (!IsInsideTree())
                return false;
                
            if (Multiplayer == null)
                return false;

            var peer = Multiplayer.MultiplayerPeer;
            if (peer == null || peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
                return false;

            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Player] Error checking multiplayer state: {e.Message}");
            return false;
        }
    }

    public override void _Ready()
    {
        try
        {
            // Initialize basic settings
            playerColor = new Color(SettingsManager.CurrentSettings.ColorR,
                                  SettingsManager.CurrentSettings.ColorG,
                                  SettingsManager.CurrentSettings.ColorB);

            // Initialize camera positions
            targetCameraPosition = perspective1.Position;
            targetCameraRotation = perspective1.Rotation;

            // Get node references
            nameLabel = GetNode<Label3D>("NameLabel");
            collision = GetNode<CollisionShape3D>("Collision");
            headPivot = GetNode<Node3D>("HeadPivot");
            playerSprite = GetNode<Sprite3D>("Person");
            interactRay = GetNode<RayCast3D>("HeadPivot/PlayerCamera/InteractRay");

            Camera = GetNode<Camera3D>("HeadPivot/PlayerCamera");

            if (!IsInsideTree())
            {
                GD.PrintErr("[Player] Node not in scene tree during _Ready");
                return;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Player] Error in _Ready initialization: {e.Message}");
            return;
        }

        // Set initial display name if available
        if (!string.IsNullOrEmpty(displayName))
        {
            nameLabel.Text = displayName;
        }

        // Wait one frame to ensure proper multiplayer initialization
        GetTree().CreateTimer(0).Timeout += () =>
        {
            if (!IsInsideTree()) return;

            if (Multiplayer.MultiplayerPeer != null && IsMultiplayerAuthority())
            {
                // Handle host player specific setup
                bool isHostPlayer = NetworkManager.Instance?.IsPlayerHost == true && Multiplayer.GetUniqueId() == 1;
                if (isHostPlayer && perspectiveMode == 0)
                {
                    playerSprite.Hide();
                }
                
                // Configure local player settings
                MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);
                nameLabel.Visible = false;
                SyncExpressionId = SettingsManager.CurrentSettings.SavedExpressionID;
                GetNode<Camera3D>("HeadPivot/PlayerCamera").MakeCurrent();
                GetNode<Camera3D>("HeadPivot/PlayerCamera").Fov = SettingsManager.CurrentSettings.FOV;

                GD.Print("[Player] Camera activated for authority player");
                SetPlayerColor(playerColor);
            }
            else
            {
                GD.Print("[Player] Initialized as non-authority player");
            }
        };

        // Connect signals
        GetNode<AnimationManager>("AnimationManager").AnimationFinished += OnAnimationFinished;
    }

    public override void _Process(double delta)
    {
        if (!IsInsideTree()) return;
        
        try
        {
            // Check multiplayer state and handle single player
            bool isMultiplayerActive = IsMultiplayerValid();
            if (!isMultiplayerActive)
            {
                if (GetViewport().GetCamera3D() != Camera)
                {
                    Camera.Current = true;
                }
                return;
            }

            bool hasAuthority = false;
            try
            {
                hasAuthority = IsMultiplayerAuthority();
                if (!hasAuthority)
                {
                    // Non-authority processing
                    return;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Player] Error checking multiplayer authority: {e.Message}");
                return;
            }

            // Set camera if needed
            if (GetViewport().GetCamera3D() != Camera && hasAuthority)
            {
                Camera.Current = true;
            }

            // Process authority-specific logic
            if (hasAuthority)
            {
                // Handle chat state changes
                if (ChatManager.chatOpen != lastChatState)
                {
                    if (ChatManager.chatOpen)
                        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);
                    else if (!NewPauseMenu.IsOpen)
                        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);

                    lastChatState = ChatManager.chatOpen;
                }

                // Handle Changing perspective
                if (Input.IsActionJustPressed("change_perspective") && !ChatManager.chatOpen && !NewPauseMenu.IsOpen)
                {
                    float currentPitch = pitch; // Store current pitch
                    perspectiveMode = (perspectiveMode + 1) % 3;

                    switch (perspectiveMode)
                    {
                        case 0:
                            targetCameraPosition = perspective1.Position;
                            targetCameraRotation = perspective1.Rotation;
                            break;
                        case 1:
                            targetCameraPosition = perspective2.Position;
                            targetCameraRotation = perspective2.Rotation;
                            break;
                        case 2:
                            targetCameraPosition = perspective3.Position;
                            targetCameraRotation = perspective3.Rotation;
                            break;
                    }

                    if (perspectiveMode == 0) playerSprite.Hide();
                    else playerSprite.Show();
                }

                if (Camera.Position.DistanceTo(targetCameraPosition) > 0.01)
                {
                    float smoothingSpeed = SettingsManager.CurrentSettings.CameraSmoothing ? 10f : 50f;
                    Camera.Position = Camera.Position.Lerp(targetCameraPosition, smoothingSpeed * (float)delta);
                    Camera.Rotation = Camera.Rotation.Lerp(targetCameraRotation, smoothingSpeed * (float)delta);
                    pitch = Camera.Rotation.X;
                    if (canRotateCamera) canRotateCamera = false;
                }
                else
                {
                    Camera.Position = targetCameraPosition;
                    if (!canRotateCamera) canRotateCamera = true;
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Player] Error in _Process: {e.Message}");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInsideTree()) return;

        try
        {
            bool isMultiplayerActive = IsMultiplayerValid();
            if (!isMultiplayerActive)
            {
                // Handle single-player mode
                HandleMovement(delta);
                return;
            }

            bool hasAuthority;
            try
            {
                hasAuthority = IsMultiplayerAuthority();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Player] Error checking multiplayer authority: {e.Message}");
                HandleMovement(delta); // Fallback to single player behavior on error
                return;
            }

            if (!hasAuthority)
            {
                // Handle non-authority state synchronization
                try
                {
                    GlobalPosition = SyncGlobalPosition;
                    Rotation = new Vector3(0, SyncRotation, 0);
                    if (headPivot != null)
                    {
                        headPivot.Rotation = new Vector3(SyncCameraPitch, headPivot.Rotation.Y, headPivot.Rotation.Z);
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Player] Error in non-authority sync: {e.Message}");
                }
                return;
            }

            SyncCharacterId = PlayerData.CurrentCharacter.Name;

            float waterSurfaceY = -2.78f;
            float transitionRange = 0.5f;

            float playerFeetY = GlobalPosition.Y;
            bool touchingFloor = IsOnFloor();

            bool deepEnough = playerFeetY < (waterSurfaceY - transitionRange);
            bool shallowEnough = playerFeetY > (waterSurfaceY + transitionRange);

            bool aboveWaterCompletely = GlobalPosition.Y > (waterSurfaceY + 0.5f);

            // Enter swimming if deep
            if (!isSwimming && deepEnough)
            {
                isSwimming = true;
                crouching = false;
                Speed = crouching ? Speed / 2 : Speed;
                headPivot.Position = crouching ? Vector3.Zero : Vector3.Up * 0.2f;
            }

            // Exit swimming if fully above water OR if touching floor above water line
            if (isSwimming && (aboveWaterCompletely || (touchingFloor && shallowEnough)))
            {
                isSwimming = false;
            }

            /*
            if (interactRay.IsColliding() && !isSwimming && !crouching)
            {
                var target = interactRay.GetCollider();
                if (target is Node3D node && node.IsInGroup("carryable"))
                {
                    PlayerHUD.Instance?.ShowInteractionPrompt($"Press [E] to pick up");
                }
                else
                {
                    if (PlayerHUD.Instance.InteractionPrompt.Visible)
                        PlayerHUD.Instance?.HideInteractionPrompt();
                }
            }
            else
            {
                if (PlayerHUD.Instance.InteractionPrompt.Visible)
                    PlayerHUD.Instance?.HideInteractionPrompt();
            }
            */


            HandleInputAndMenuLogic();

            if (isEmoting)
            {
                if (!IsOnFloor()) velocity.Y -= Gravity * (float)delta;
                else velocity.Y = 0;

                Velocity = velocity;
                MoveAndSlide();
                SyncGlobalPosition = GlobalPosition;
                return;
            }

            if (isSwimming)
            {
                HandleSwimming(delta);
            }
            else
            {
                HandleMovement(delta);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Player] Error in _PhysicsProcess: {e.Message}");
        }
    }

    private void HandleInputAndMenuLogic()
    {
        if (!IsInsideTree()) return;

        try
        {
            if (Input.IsActionJustPressed("escape") && !ChatManager.chatOpen && !NewPauseMenu.customizationOpen)
            {
                if (!NewPauseMenu.IsOpen)
                {
                    NewPauseMenu.Instance.OpenMenu();
                    MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);
                }
                else
                {
                    NewPauseMenu.Instance.CloseMenu();
                    // Only capture mouse if we're still in a valid multiplayer session
                    if (Multiplayer.MultiplayerPeer != null && IsMultiplayerAuthority())
                    {
                        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Player] Error in HandleInputAndMenuLogic: {e.Message}");
        }

        /*
        if (Input.IsActionJustPressed("interact") && !isSwimming && !crouching && !isEmoting && IsOnFloor())
        {
            if (!isCarrying)
            {
                if (interactRay.IsColliding())
                {
                    var collider = interactRay.GetCollider();
                    if (collider is BeachBall node && node.IsInGroup("carryable"))
                    {
                        carriedObject = node;
                        carriedObject.RpcId(1, "RequestPickupAuthority");

                        SyncAnimType = (int)AnimationManager.PlayerAnimTypes.CarryIdle;
                        animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.CarryIdle);
                    }
                }
            }
            else
            {
                carriedObject.RpcId(1, "RequestDropAuthority");

                GetTree().CreateTimer(0.2f).Timeout += () =>
                {
                    if (carriedObject != null && carriedObject.GetMultiplayerAuthority() == 1)
                    {
                        carriedObject = null;
                        SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Idle;
                        animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Idle);
                    }
                };
            }
        }
        */
    }

    public override void _Input(InputEvent @event)
    {
        try
        {
            // First check multiplayer state
            if (!IsMultiplayerValid() || !IsMultiplayerAuthority())
            {
                // In single player or non-authority mode, still handle mouse events for voting
                if (@event is InputEventMouseMotion && GameManager.Instance?.GetCurrentState() == GameManager.GameState.Voting)
                {
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            // Skip input if any of these conditions are true
            if (NewPauseMenu.IsOpen || 
                ChatManager.chatOpen || 
                !canRotateCamera || 
                GameManager.Instance == null ||
                GameManager.Instance.GetCurrentState() == GameManager.GameState.Voting)
            {
                if (@event is InputEventMouseMotion && GameManager.Instance?.GetCurrentState() == GameManager.GameState.Voting)
                {
                    // Consume the mouse motion event during voting to prevent camera movement
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (@event is InputEventMouseMotion motion)
            {
                try
                {
                    yaw -= motion.Relative.X * MouseSensitivity * SettingsManager.CurrentSettings.MouseSensitivity;
                    pitch -= motion.Relative.Y * MouseSensitivity * SettingsManager.CurrentSettings.MouseSensitivity;
                    if (perspectiveMode == 0)
                    {
                        pitch = Mathf.Clamp(pitch, (-Mathf.Pi / 2) + 0.01f, (Mathf.Pi / 2) - 0.01f);
                    }
                    else
                    {
                        pitch = Mathf.Clamp(pitch, (-Mathf.Pi / 2) / 2 + 0.01f, (Mathf.Pi / 2) / 2 - 0.01f);
                    }

                    SyncRotation = yaw;
                    SyncCameraPitch = pitch;

                    Rotation = new Vector3(0, yaw, 0);

                    if (headPivot != null)
                    {
                        headPivot.Rotation = new Vector3(pitch, headPivot.Rotation.Y, headPivot.Rotation.Z);
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Player] Error processing mouse motion: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Player] Error in _Input: {e.Message}");
        }
    }


    private void HandleMovement(double delta)
    {
        if (Input.IsActionJustPressed("crouch") && CanCrouch())
        {
            crouching = !crouching;
            Speed = crouching ? Speed / 2 : Speed;
            headPivot.Position = crouching ? Vector3.Zero : Vector3.Up * 0.2f;
        }

        if (!ChatManager.chatOpen && !NewPauseMenu.IsOpen && (GameManager.Instance.GetCurrentState() != GameManager.GameState.Voting))
        {
            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

            var camTransform = Camera.GlobalTransform;
            Vector3 forward = Transform.Basis.Z;
            Vector3 right = Transform.Basis.X;

            Vector3 direction = (forward * inputDir.Y + right * inputDir.X).Normalized();

            velocity.X = direction.X * Speed;
            velocity.Z = direction.Z * Speed;
        }
        else
        {
            velocity.X = 0;
            velocity.Z = 0;
        }

        if (IsOnFloor())
        {
            if (!isSwimming && Input.IsActionJustPressed("jump") && !ChatManager.chatOpen && !NewPauseMenu.IsOpen)
            {
                SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Jump;
                animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Jump);
                velocity.Y = crouching ? 1.5f * JumpForce : JumpForce;

                crouching = false;
                Speed = 5f;
                headPivot.Position = Vector3.Up * 0.2f;
            }

            else velocity.Y = 0;
        }
        else if (IsOnCeiling())
        {
            velocity.Y = -Gravity * (float)delta;
        }
        else
        {
            velocity.Y -= Gravity * (float)delta;
        }

        if (GlobalPosition.Y < -30)
        {
            GlobalPosition = new Vector3(0, 5, 0);
        }


        Velocity = velocity;

        if (Velocity.Length() == 0 && IsOnFloor())
        {
            if (crouching)
            {
                SyncAnimType = (int)AnimationManager.PlayerAnimTypes.CrouchIdle;
                animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.CrouchIdle);
            }
            else
            {
                SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Idle;
                animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Idle);
            }

        }
        else if (Velocity.Y < 0.01 && IsOnFloor())
        {
            if (crouching)
            {
                SyncAnimType = (int)AnimationManager.PlayerAnimTypes.CrouchWalk;
                animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.CrouchWalk);
            }
            else
            {
                SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Walk;
                animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Walk);
            }
        }
        else if (Velocity.Y < 0)
        {
            crouching = false;
            SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Fall;
            animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Fall);
        }
        MoveAndSlide();

        SyncGlobalPosition = GlobalPosition;
    }

    private void HandleParticles(double delta)
    {
        bool nowOnFloor = IsOnFloor();

        if (!wasOnFloor && nowOnFloor && Velocity.Y <= 0)
        {
            Vector3 landPos = GlobalTransform.Origin;
            NetworkManager.Instance.RpcId(1, "PlayerLanded", landPos);
        }

        wasOnFloor = nowOnFloor;
    }

    private void HandleSwimming(double delta)
    {
        Vector2 inputDir;
        if (!ChatManager.chatOpen && !NewPauseMenu.IsOpen)
        {
            inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        }
        else inputDir = Vector2.Zero;

        var camTransform = Camera.GlobalTransform;

        Vector3 camForward = camTransform.Basis.Z;
        Vector3 camRight = camTransform.Basis.X;
        Vector3 camUp = Vector3.Up;

        camForward = camForward.Normalized();
        camRight = camRight.Normalized();

        Vector3 direction = (camForward * inputDir.Y + camRight * inputDir.X);

        if (perspectiveMode == 2)
            direction *= -1;


        if (Input.IsActionPressed("jump"))
            direction += camUp;

        if (Input.IsActionPressed("crouch"))
            direction -= camUp;

        velocity = direction.Normalized() * Speed * 0.75f;

        Velocity = velocity;
        MoveAndSlide();

        SyncGlobalPosition = GlobalPosition;

        if (velocity.Length() > 0.1f)
        {
            SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Swim;
            animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Swim);
        }
        else
        {
            SyncAnimType = (int)AnimationManager.PlayerAnimTypes.SwimIdle;
            animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.SwimIdle);
        }
    }

    public void PlayEmote(AnimationManager.PlayerAnimTypes emoteAnim, float duration = 2f)
    {
        if (isEmoting) return;

        isEmoting = true;
        emoteTimer = duration;

        SyncAnimType = (int)emoteAnim;
        animationManager.PlayAnim(emoteAnim);

        Velocity = Vector3.Zero;
    }

    private void OnAnimationFinished(AnimationManager.PlayerAnimTypes finishedAnim)
    {
        if (!isEmoting) return;

        if (finishedAnim == (AnimationManager.PlayerAnimTypes)SyncAnimType)
        {
            isEmoting = false;
            SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Idle;
            animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Idle);
        }
    }
    /*
    private void HandleCarryMovement(double delta)
    {
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

        var camTransform = playerCamera.GlobalTransform;
        Vector3 camForward = camTransform.Basis.Z;
        Vector3 camRight = camTransform.Basis.X;

        camForward.Y = 0;
        camRight.Y = 0;
        camForward = camForward.Normalized();
        camRight = camRight.Normalized();

        Vector3 direction = (camForward * inputDir.Y + camRight * inputDir.X).Normalized();

        if (perspectiveMode == 2)
            direction *= -1;
        
        velocity.X = direction.X * Speed;
        velocity.Z = direction.Z * Speed;

        if (!IsOnFloor()) velocity.Y -= Gravity * (float)delta;
        else velocity.Y = 0;

        Velocity = velocity;
        MoveAndSlide();

        SyncGlobalPosition = GlobalPosition;

        if (Velocity.Length() > 0.1f)
        {
            SyncAnimType = (int)AnimationManager.PlayerAnimTypes.Carry;
            animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.Carry);
        }
        else
        {
            SyncAnimType = (int)AnimationManager.PlayerAnimTypes.CarryIdle;
            animationManager.PlayAnim(AnimationManager.PlayerAnimTypes.CarryIdle);
        }
    }
    */
    
    public void SetPlayerSpeed(float newSpeed)
    {
        Speed = newSpeed;
    }
}

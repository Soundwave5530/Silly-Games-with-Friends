using Godot;
using System;
using Godot.Collections;

public partial class DebugUi : CanvasLayer
{
    public static DebugUi Instance { get; private set; }
    public Label infoLabel;
    
    private float[] fpsHistory = new float[60];  // Store last 60 FPS readings
    private int fpsIndex = 0;
    private float avgFps = 0;
    private float minFps = float.MaxValue;
    private float maxFps = float.MinValue;

    public override void _Ready()
    {
        Instance = this;
        Layer = 999;
        Visible = false;
        infoLabel = new Label();
        infoLabel.Name = "InfoLabel";
        AddChild(infoLabel);
        infoLabel.Position = new Vector2(10, 10);
        infoLabel.LabelSettings = new();
        infoLabel.LabelSettings.FontSize = 16;
        infoLabel.LabelSettings.FontColor = new Color(1, 1, 1, 1);
        infoLabel.LabelSettings.OutlineSize = 5;
        infoLabel.LabelSettings.OutlineColor = new Color(0, 0, 0, 1);
    }
    public override void _Process(double delta)
    {
        UpdatePerformanceMetrics(delta);

        if (Input.IsActionJustPressed("debug_mode"))
        {
            Visible = !Visible;
            if (Visible)
            {
                if (PlayerHUD.Instance != null && PlayerHUD.Instance.active && PlayerHUD.Instance.Visible)
                {
                    PlayerHUD.Instance.Hide();
                }
                StatusMessageManager.Instance.ShowMessage("Debug UI enabled", StatusMessageManager.MessageType.Info);
            }
            else
            {
                if (PlayerHUD.Instance != null && PlayerHUD.Instance.active && !PlayerHUD.Instance.Visible)
                {
                    PlayerHUD.Instance.Show();
                }
                StatusMessageManager.Instance.ShowMessage("Debug UI disabled", StatusMessageManager.MessageType.Info);
            }
        }

        if (!Visible) return;

        if (infoLabel != null)
        {
            infoLabel.Text = " --- Performance Metrics --- \n";
            infoLabel.Text += $"FPS: {Engine.GetFramesPerSecond():F1} (Avg: {avgFps:F1}, Min: {minFps:F1}, Max: {maxFps:F1})\n";
            infoLabel.Text += $"Frame Time: {(float)Performance.GetMonitor(Performance.Monitor.TimeFps):F2}ms\n";
            infoLabel.Text += $"Physics Time: {(float)Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess):F2}ms\n";
            infoLabel.Text += $"Process Time: {(float)Performance.GetMonitor(Performance.Monitor.TimeProcess):F2}ms\n";

            infoLabel.Text += "\n --- System Information --- \n";
            infoLabel.Text += $"OS: {OS.GetName()} {OS.GetVersion()}\n";
            Dictionary memData = OS.GetMemoryInfo();
            float physicalMB = (long)memData["physical"] / (1024f * 1024f);
            float freeMB = (long)memData["free"] / (1024f * 1024f);
            float availableMB = (long)memData["available"] / (1024f * 1024f);
            infoLabel.Text += $"Memory: {physicalMB:F0}MB (Free: {freeMB:F0}MB, Available: {availableMB:F0}MB)\n";
            infoLabel.Text += $"CPU: {OS.GetProcessorCount()} cores, {OS.GetProcessorName()}\n";
            infoLabel.Text += $"Window Size: {DisplayServer.WindowGetSize().X}x{DisplayServer.WindowGetSize().Y}\n";

            infoLabel.Text += "\n --- Network Information --- \n";
            string networkStatus = Multiplayer.MultiplayerPeer != null ? Multiplayer.MultiplayerPeer.GetConnectionStatus().ToString() : "Not Connected to Server";
            infoLabel.Text += $"Network Status: {networkStatus}\n";

            if (Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
            {
                var peer = Multiplayer.MultiplayerPeer;
                int myId = peer.GetUniqueId();
                bool isServer = NetworkManager.Instance?.IsServer ?? false;
                
                infoLabel.Text += $"Role: {(isServer ? (NetworkManager.Instance.IsDedicatedServer ? "Dedicated Server" : "Player Host") : "Client")}\n";
                infoLabel.Text += $"Cheats Enabled: False\n";
                infoLabel.Text += $"Peer ID: {myId}\n";
                infoLabel.Text += $"Player Count: {NetworkManager.Instance.PlayerNames.Count}\n";
                infoLabel.Text += $"Max Players: {NetworkManager.MAXPLAYERS}\n";
                infoLabel.Text += $"Connected Players: {string.Join(", ", NetworkManager.Instance.PlayerNames.Values)}\n";
            }

            infoLabel.Text += "\n --- Game State --- \n";
            infoLabel.Text += $"Current Scene: {GetTree().CurrentScene?.Name ?? "None"}\n";
            if (GameManager.Instance != null)
            {
                infoLabel.Text += $"Game Type: {GameManager.Instance.GetCurrentGameType()}\n";
                infoLabel.Text += $"Game State: {GameManager.Instance.GetCurrentState()}\n";
            }
            
            var localPlayer = NetworkManager.Instance?.GetLocalPlayer();
            if (localPlayer != null)
            {
                infoLabel.Text += $"\n --- Local Player --- \n";
                infoLabel.Text += $"Display Name: {localPlayer.displayName}\n";
                infoLabel.Text += $"Position: {localPlayer.GlobalPosition:F1}\n";
                infoLabel.Text += $"Velocity: {localPlayer.Velocity.Length():F1} m/s\n";
                infoLabel.Text += $"On Floor: {localPlayer.IsOnFloor()}\n";
                infoLabel.Text += $"Is Crouching: {localPlayer.crouching}\n";
                infoLabel.Text += $"Is Swimming: {localPlayer.isSwimming}\n";
                infoLabel.Text += $"Perspective Mode: {localPlayer.perspectiveMode}\n";
                infoLabel.Text += $"Current Animation: {((AnimationManager.PlayerAnimTypes)localPlayer.SyncAnimType).ToString()}\n";
                infoLabel.Text += $"Current Hat: {localPlayer.SyncHatId.Capitalize()}\n";
                infoLabel.Text += $"Current Expression: {localPlayer.SyncExpressionId.Capitalize()}\n";
                
            }
        }
    }

    private void UpdatePerformanceMetrics(double delta)
    {
        // Update FPS history
        float currentFps = (float)Engine.GetFramesPerSecond();
        fpsHistory[fpsIndex] = currentFps;
        fpsIndex = (fpsIndex + 1) % fpsHistory.Length;

        // Calculate FPS statistics
        float sum = 0;
        minFps = float.MaxValue;
        maxFps = float.MinValue;
        for (int i = 0; i < fpsHistory.Length; i++)
        {
            float fps = fpsHistory[i];
            if (fps > 0)  // Only count initialized values
            {
                sum += fps;
                minFps = Mathf.Min(minFps, fps);
                maxFps = Mathf.Max(maxFps, fps);
            }
        }
        avgFps = sum / fpsHistory.Length;
    }}

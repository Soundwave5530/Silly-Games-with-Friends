using Godot;
using System;

public partial class SpeedModifier : RefCounted
{
    public float Multiplier { get; set; } = 1f;
    public float FlatBonus { get; set; } = 0f;
    public float Duration { get; set; } = 0f;
    public bool IsPermanent { get; set; } = false;
    public DateTime StartTime { get; set; }
    
    public SpeedModifier()
    {
        StartTime = DateTime.Now;
    }
    
    public SpeedModifier(float multiplier = 1f, float flatBonus = 0f, float duration = 0f, bool isPermanent = false)
    {
        Multiplier = multiplier;
        FlatBonus = flatBonus;
        Duration = duration;
        IsPermanent = isPermanent;
        StartTime = DateTime.Now;
    }
    
    public bool IsExpired => !IsPermanent && (DateTime.Now - StartTime).TotalSeconds >= Duration;
    
    public float TimeRemaining => IsPermanent ? float.MaxValue : Math.Max(0f, Duration - (float)(DateTime.Now - StartTime).TotalSeconds);
}
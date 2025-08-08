using Godot;
using System;
using Godot.Collections;
using System.Collections;

public partial class TransitionScreen : CanvasLayer
{
    [Signal] public delegate void TransitionInFinishedEventHandler();
    [Signal] public delegate void TransitionOutFinishedEventHandler();

    private AnimationPlayer anim => GetNode<AnimationPlayer>("AnimationPlayer");

    private const float defaultScreenTime = 1f;
    private float overrideScreenTime = defaultScreenTime;
    private float TimeElapsed = 0;
    public bool MustTriggerOut = false;

    /*
    public static Array<string> TransitionMessages = new()
    {
        "I'm loading...",
        "I feel like I'm loading...",
        "Gimme a freaking minute...",
        "Doing my best...",
        "Chill out, I'm working on it...",
        "I feel pretty",
        "Woah man gimme a sec...",
        "Working on it...",
        "Why did Minecart make me?",
        "I don't know what I'm doing...",
        "I'm loading it so good...",
        "Loading... I guess?",
        "Silly games with friends is fun!",
        "I hope you like this game!",
        "Try the new Character Customizer!",
        "I hope you like the new update!",
        "I'm running out of prompts for this screen...",
        "I'm a transition screen!",
        "This screen is taking forever to load...",
        "I hate this screen...",
        "This is a transition screen, not a loading screen...",
        "I don't know what to say...",
        "Try to enjoy this screen...",
        "Fastest. Screen. Ever.",
        "Slowest. Screen. Ever.",
        "Try my Minecraft Server!",
        "Buster is my favorite cat!",
        "I love making games!",
        "Death Deceit is dead I'm calling it",
        "Goober is my favorite character",
        "Minecart made like 50 prompts for this screen",
        "Why is this screen so long?",
        "I hope you like this game, Minecart worked hard on it!",
        "This is a loading screen, not a transition screen!",
        "What do I even say here?",
        "I don't know what to say, I'm just a screen!",
        "I feel so sigma!!!",
        "Pressure is goated",
        "All art drawn by BucketMan!1!11!!",
        "I love BucketMan's art style!",
        "All Programming by Minecart!",
        "I love Minecart's programming style!",
        "I HATE THIS SCREEN!",
        "This screen is so annoying!",
        "Also try Party Climbers!"
    };
    */

    public override void _Ready()
    {
        anim.AnimationFinished += OnAnimationFinished;
        anim.Play("in");
    }

    public override void _Process(double delta)
    {
        if (MustTriggerOut) return;
        TimeElapsed += (float)delta;

        if (TimeElapsed >= overrideScreenTime)
        {
            TimeElapsed = 0;
            anim.Play("out");
        }
    }

    public void OnAnimationFinished(StringName animName)
    {
        if (animName == "in")
        {
            EmitSignal(SignalName.TransitionInFinished);
        }
        else if (animName == "out")
        {
            EmitSignal(SignalName.TransitionOutFinished);
            QueueFree();
        }
    }

    public void PlayAnim(string animName)
    {
        anim.Play(animName);
    }
}

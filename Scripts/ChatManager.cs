using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ChatManager : CanvasLayer
{
    [Export] public LineEdit ChatInput;
    [Export] public RichTextLabel ChatLog;
    [Export] public Timer HideTimer;
    [Export] public Panel Chatbox;

    private Player localPlayer => NetworkManager.Instance.GetLocalPlayer();

    public static bool chatOpen = false;
    private bool hidingChatSeq = false;
    private float hidingAmount = 1;

    public override void _Ready()
    {
        ChatInput.Hide();
        ChatInput.TextSubmitted += OnChatSubmitted;
        DisplaySystemMessage($"Press '{SettingsManager.GetKeyNameFromInputMap("open_chat").ToLower()}' to open chat and type /help to see all commands");
        DisplaySystemMessage($"Press '{SettingsManager.GetKeyNameFromInputMap("commands")}' to open commands");

        HideTimer.Timeout += () =>
        {
            if (!chatOpen)
            {
                hidingChatSeq = true;
                hidingAmount = 1;
            }
        };
    }

    public override void _Process(double delta)
    {
        if (chatOpen) return;
        if (Input.IsActionJustPressed("open_chat") && !NewPauseMenu.IsOpen)
        {
            OpenChat();
        }
        else if (Input.IsActionJustPressed("commands"))
        {
            OpenChat();
            ChatInput.Text += "/";
            ChatInput.CaretColumn = 1;
        }

        if (hidingChatSeq)
            {
                Chatbox.Modulate = new Color(1, 1, 1, hidingAmount);
                hidingAmount -= (float)delta;
                if (hidingAmount <= 0)
                {
                    hidingChatSeq = false;
                    hidingAmount = 1;
                    Hide();
                }
            }
    }

    private void OpenChat()
    {
        Show();
        ChatInput.Show();
        ChatInput.GrabFocus();
        chatOpen = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        hidingChatSeq = false;
        hidingAmount = 1;
        Chatbox.Modulate = new Color(1, 1, 1, 1);
    }

    private async void CloseChat()
    {
        ChatInput.ReleaseFocus();
        ChatInput.Text = "";
        chatOpen = false;
        ChatInput.Hide();

        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

        if (!Multiplayer.IsServer()) MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);

        CreateTimerToHide();
    }

    private void OnChatSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            CloseChat();
            return;
        }
        if (text.StartsWith('/'))
        {
            HandleCommand(text);
            CloseChat();
            return;
        }
        if (Multiplayer.IsServer())
        {
            string prefix = NetworkManager.Instance.IsDedicatedServer ? "[SERVER]" : NetworkManager.Instance.PlayerNames[1];
            Rpc(nameof(SendChatMessage), prefix, text);
            SendChatMessage(prefix, text);
            CloseChat();
            return;
        }
        
        RpcId(1, nameof(SendChatToServer), text);
        CloseChat();
    }

    private void CreateTimerToHide()
    {
        hidingChatSeq = false;
        hidingAmount = 1;
        Chatbox.Modulate = new Color(1, 1, 1, 1);
        HideTimer.Stop();
        HideTimer.Start();
    }

    private void HandleCommand(string cmd)
    {
        var args = cmd.Split(" ");
        string command = args[0].ToLower();

        switch (command)
        {
            case "/help":
                ChatLog.AppendText("[color=yellow]Available commands: /help, /list, /wave, /team, /vote, /endgame[/color]\n");
                ChatLog.AppendText("[color=yellow]Type a command without any arguments to learn about it[/color]\n");
                break;
                
            case "/wave":
                if (localPlayer.isSwimming) return;
                localPlayer.PlayEmote(AnimationManager.PlayerAnimTypes.Wave);
                break;
                
            case "/list":
                string playerList = "";
                foreach (var kvp in NetworkManager.Instance.PlayerNames)
                    playerList += kvp.Value + ", ";

                if (playerList.Length > 2)
                    playerList = playerList.Substring(0, playerList.Length - 2);
                ChatLog.AppendText($"[color=yellow]Players online ({NetworkManager.Instance.PlayerNames.Count}): {playerList}[/color]\n");
                break;
                
            case "/team":
                HandleTeamCommand(args);
                break;
                
            // NEW GAME COMMANDS
            case "/vote":
                HandleVoteCommand(args);
                break;
                
            case "/endgame":
                HandleEndGameCommand();
                break;
                
            case "/startgame":
                HandleStartGameCommand(args);
                break;

            default:
                ChatLog.AppendText($"[color=red]Unknown command: {command}[/color]\n");
                break;
        }
    }

    private void HandleVoteCommand(string[] args)
    {
        if (GameManager.Instance == null)
        {
            DisplaySystemMessage("[color=red]Game system not available[/color]");
            return;
        }

        var currentState = GameManager.Instance.GetCurrentState();
        
        if (args.Length < 2)
        {
            if (currentState == GameManager.GameState.Lobby)
            {
                DisplaySystemMessage("[color=green]Usage: /vote start - Start a voting session (server only)[/color]");
            }
            else if (currentState == GameManager.GameState.Voting)
            {
                DisplaySystemMessage("[color=green]Usage: /vote <tag|hide|murder> - Vote for a game[/color]");
            }
            else
            {
                DisplaySystemMessage("[color=yellow]No voting session active[/color]");
            }
            return;
        }

        string subCommand = args[1].ToLower();

        if (subCommand == "start")
        {
            if (!Multiplayer.IsServer())
            {
                DisplaySystemMessage("[color=red]Only the server can start voting[/color]");
                return;
            }

            if (currentState != GameManager.GameState.Lobby)
            {
                DisplaySystemMessage("[color=red]Can only start voting in lobby[/color]");
                return;
            }

            GameManager.Instance.StartVoting();
        }
        else if (currentState == GameManager.GameState.Voting)
        {
            DisplaySystemMessage("[color=yellow]Please use the voting UI to cast your vote![/color]");
            return;
        }
        else
        {
            DisplaySystemMessage("[color=yellow]No voting session active. Server can use '/vote start'[/color]");
        }
    }

    private void HandleEndGameCommand()
    {
        if (!Multiplayer.IsServer())
        {
            DisplaySystemMessage("[color=red]Only the server can end games[/color]");
            return;
        }

        if (GameManager.Instance == null)
        {
            DisplaySystemMessage("[color=red]Game system not available[/color]");
            return;
        }

        var currentState = GameManager.Instance.GetCurrentState();
        if (currentState == GameManager.GameState.Playing)
        {
            GameManager.Instance.EndGame();
            DisplaySystemMessage("[color=yellow]Game ended by server[/color]");
        }
        else
        {
            DisplaySystemMessage("[color=yellow]No active game to end[/color]");
        }
    }

    private void HandleStartGameCommand(string[] args)
    {
        if (!Multiplayer.IsServer())
        {
            DisplaySystemMessage("[color=red]Only the server can start games directly[/color]");
            return;
        }

        if (GameManager.Instance == null)
        {
            DisplaySystemMessage("[color=red]Game system not available[/color]");
            return;
        }

        if (GameManager.Instance.GetCurrentState() != GameManager.GameState.Lobby)
        {
            DisplaySystemMessage("[color=red]Can only start games from lobby[/color]");
            return;
        }

        if (args.Length < 2)
        {
            DisplaySystemMessage("[color=green]Usage: /startgame <tag|hide|murder> - Start a game directly[/color]");
            return;
        }

        GameManager.GameType gameType = args[1].ToLower() switch
        {
            "tag" => GameManager.GameType.Tag,
            "hide" => GameManager.GameType.HideAndSeek,
            "murder" => GameManager.GameType.MurderMystery,
            _ => GameManager.GameType.None
        };

        if (gameType == GameManager.GameType.None)
        {
            DisplaySystemMessage("[color=red]Invalid game type. Use: tag, hide, or murder[/color]");
            return;
        }

        GameManager.Instance.StartGame(gameType);
        DisplaySystemMessage($"[color=green]Starting {gameType} game directly![/color]");
    }

    private void HandleTeamCommand(string[] args)
    {
        if (args.Length < 2)
        {
            DisplaySystemMessage("[color=green]Usage: /team <create|join|leave|list> ...[/color]");
            return;
        }

        string subCommand = args[1].ToLower();
        switch (subCommand)
        {
            case "create":
                if (!Multiplayer.IsServer())
                {
                    DisplaySystemMessage("[color=red]You do not have the permissions to run this command[/color]");
                    return;
                }
                if (args.Length < 6)
                {
                    DisplaySystemMessage("[color=green]Usage: /team create <id> <name> <r> <g> <b>[/color]");
                    return;
                }

                if (!int.TryParse(args[2], out int teamId) ||
                    !float.TryParse(args[4], out float r) ||
                    !float.TryParse(args[5], out float g) ||
                    !float.TryParse(args[6], out float b))
                {
                    DisplaySystemMessage("[color=red]Invalid arguments for /team create[/color]");
                    return;
                }

                string teamName = args[3];
                Color color = new Color(r / 255f, g / 255f, b / 255f);

                TeamManager.Instance.CreateTeam(teamId, teamName, color);
                DisplaySystemMessage($"Created team {teamName} (ID: {teamId}) with color RGB({r},{g},{b})");
                break;

            case "join":
                if (args.Length < 3 || !int.TryParse(args[2], out int joinId))
                {
                    DisplaySystemMessage("[color=green]Usage: /team join <teamId>[/color]");
                    return;
                }

                TeamManager.Instance.AddPlayerToTeam(Multiplayer.GetUniqueId(), joinId);
                DisplaySystemMessage($"Joined team {joinId}");
                break;

            case "leave":
                TeamManager.Instance.RemovePlayerFromTeam(Multiplayer.GetUniqueId());
                DisplaySystemMessage($"Left your current team.");
                break;

            case "list":
                foreach (var team in TeamManager.Instance.Teams.Values)
                {
                    string members = string.Join(", ", team.MemberIds
                        .Select(id => NetworkManager.Instance.PlayerNames.TryGetValue(id, out var name) ? name : id.ToString()));
                    DisplaySystemMessage($"[Team {team.TeamId}] {team.TeamName}: {(members.Length > 0 ? members : "No members")}");
                }
                break;

            default:
                DisplaySystemMessage("[color=red]Unknown /team subcommand.[/color]");
                break;
        }
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void DisplaySystemMessage(string msg)
    {
        ChatLog.AppendText($"{msg}\n");

        if (!chatOpen)
        {
            Show();
            CreateTimerToHide();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void SendChatToServer(string message)
    {
        if (!Multiplayer.IsServer()) return;
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200) return;

        int senderId = Multiplayer.GetRemoteSenderId();

        if (IsSpamming(senderId))
        {
            RpcId(senderId, nameof(DisplaySystemMessage), "[color=red]You're sending messages too quickly. Slow down![/color]");
            return;
        }

        string senderName = NetworkManager.Instance.PlayerNames.TryGetValue(senderId, out var name) ? name : "Player";
        string sanitized = SanitizeMessage(message);

        Rpc(nameof(SendChatMessage), senderName, sanitized);
        SendChatMessage(senderName, sanitized);
    }



    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SendChatMessage(string senderName, string message)
    {
        string formatted = $"[b]{senderName}[/b]: {message}";
        ChatLog.AppendText(formatted + "\n");

        if (!chatOpen)
        {
            Show();
            CreateTimerToHide();
        }

        var senderEntry = NetworkManager.Instance.PlayerNames.FirstOrDefault(kvp => kvp.Value == senderName);

        if (!senderEntry.Equals(default(KeyValuePair<int, string>)))
        {
            //ShowSpeechBubble(senderEntry.Key, message);
        }
    }

    /*
    public PackedScene SpeechBubbleScene = GD.Load<PackedScene>("res://Scenes/SpeechBubble.tscn");

    public void ShowSpeechBubble(int senderId, string msg)
    {
        Player player = GetNodeOrNull<Player>($"/root/NetworkManager/Players/Player_{senderId}");
        if (player == null)
        {
            GD.Print("error");
            return;
        }
        SpeechBubble bubble = SpeechBubbleScene.Instantiate<SpeechBubble>();
        bubble.FromPlayer = player;
        bubble.SetMultiplayerAuthority(senderId);

        AddChild(bubble);
        bubble.SpeechSetText(msg);
        bubble.Rpc(nameof(SpeechBubble.SpeechSetText), msg);
    }
    */

    //
    //
    // Server side verification
    //
    //

    private static readonly HashSet<string> AllowedTags = new() { "b", "i", "u", "color" };
    private static readonly List<string> FilteredWords = new() { "shit", "ass", "bitch", "fuck", "linux" }; // Words that get filtered

    private Dictionary<int, List<double>> messageTimestamps = new();
    private Dictionary<int, double> cooldownTimestamps = new();
    private const int MaxMessagesPerWindow = 5;
    private const float MessageWindowSec = 10f;
    private const float CooldownDuration = 5f;

    private static string SanitizeMessage(string input)
    {
        string output = input;

        foreach (var word in FilteredWords)
        {
            var regex = new System.Text.RegularExpressions.Regex($@"\b{word}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            output = regex.Replace(output, new string('*', word.Length));
        }

        output = System.Text.RegularExpressions.Regex.Replace(output, @"\[(\/?)(\w+)([^\]]*)\]", match =>
        {
            string tag = match.Groups[2].Value.ToLower();
            return AllowedTags.Contains(tag) ? match.Value : "";
        });

        Stack<string> tagStack = new();
        var balancedOutput = "";
        var tagRegex = new System.Text.RegularExpressions.Regex(@"\[(\/?)(\w+)([^\]]*)\]");
        int lastIndex = 0;

        foreach (System.Text.RegularExpressions.Match match in tagRegex.Matches(output))
        {
            balancedOutput += output.Substring(lastIndex, match.Index - lastIndex);
            string slash = match.Groups[1].Value;
            string tag = match.Groups[2].Value.ToLower();
            string paramsStr = match.Groups[3].Value;

            if (AllowedTags.Contains(tag))
            {
                if (slash == "") 
                {
                    tagStack.Push(tag);
                    balancedOutput += $"[{tag}{paramsStr}]";
                }
                else 
                {
                    if (tagStack.Contains(tag))
                    {
                        var tempStack = new Stack<string>(tagStack.Reverse());
                        var newStack = new Stack<string>();
                        bool removed = false;
                        while (tempStack.Count > 0)
                        {
                            var popped = tempStack.Pop();
                            if (popped == tag && !removed)
                            {
                                removed = true;
                                continue;
                            }
                            newStack.Push(popped);
                        }
                        tagStack = newStack;
                        balancedOutput += $"[/{tag}]";
                    }
                    else
                    {
                        // Skip
                    }
                }
            }
            lastIndex = match.Index + match.Length;
        }


        balancedOutput += output.Substring(lastIndex);


        while (tagStack.Count > 0)
        {
            string openTag = tagStack.Pop();
            balancedOutput += $"[/{openTag}]";
        }

        return balancedOutput;
    }

    private bool IsSpamming(int senderId)
    {
        double now = Time.GetUnixTimeFromSystem();


        if (cooldownTimestamps.TryGetValue(senderId, out double cooldownUntil) && now < cooldownUntil)
            return true;

        if (!messageTimestamps.ContainsKey(senderId))
            messageTimestamps[senderId] = new List<double>();


        messageTimestamps[senderId].RemoveAll(t => now - t > MessageWindowSec);


        messageTimestamps[senderId].Add(now);

        if (messageTimestamps[senderId].Count > MaxMessagesPerWindow)
        {
            cooldownTimestamps[senderId] = now + CooldownDuration;
            return true;
        }

        return false;
    }



}

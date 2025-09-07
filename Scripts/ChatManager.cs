using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        if (Input.IsActionJustPressed("open_chat") && !NewPauseMenu.IsOpen && !chatOpen)
        {
            OpenChat();
        }
        else if (Input.IsActionJustPressed("commands") && !chatOpen)
        {
            OpenChat();
            ChatInput.Text += "/";
            ChatInput.CaretColumn = 1;
        }
        else if (Input.IsActionJustPressed("escape") && chatOpen)
        {
            if (chatOpen)
            {
                CloseChat();
            }
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
        MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible, true); // Force system cursor
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

        await ToSignal(GetTree().CreateTimer(0.05f), "timeout");

        if (Multiplayer.MultiplayerPeer != null && !NetworkManager.Instance.IsDedicatedServer)
        {
            if (GameManager.Instance.GetCurrentState() != GameManager.GameState.Voting)
            {
                MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Captured);
            }
            else
            {
                MouseManager.Instance?.UpdateMouseType(Input.MouseModeEnum.Visible);
            }
        }

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
                DisplaySystemMessage("[color=green]Usage: /vote end - End vote early[/color]");
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
        else if (subCommand == "end")
        {
            if (!Multiplayer.IsServer())
            {
                DisplaySystemMessage("[color=red]Only the server can cancel voting early[/color]");
            }

            if (currentState == GameManager.GameState.Voting)
            {
                GameManager.Instance.EndVotingEarly();
            }

            return;
        }
        else
        {
            DisplaySystemMessage("[color=red]Invalid arguments[/color]");
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
        
        int senderId = Multiplayer.GetRemoteSenderId();

        // Basic validation
        if (string.IsNullOrWhiteSpace(message))
        {
            RpcId(senderId, nameof(DisplaySystemMessage), "[color=red]Message cannot be empty.[/color]");
            return;
        }

        if (message.Length > MaxMessageLength)
        {
            RpcId(senderId, nameof(DisplaySystemMessage), $"[color=red]Message too long. Maximum length is {MaxMessageLength} characters.[/color]");
            return;
        }

        // Spam check
        if (IsSpamming(senderId))
        {
            RpcId(senderId, nameof(DisplaySystemMessage), "[color=red]You're sending messages too quickly. Please wait a moment.[/color]");
            return;
        }

        string senderName = NetworkManager.Instance.PlayerNames.TryGetValue(senderId, out var name) ? name : "Player";
        string sanitized = SanitizeMessage(message);

        // If message was completely filtered or empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            RpcId(senderId, nameof(DisplaySystemMessage), "[color=yellow]Your message was blocked due to inappropriate content.[/color]");
            return;
        }

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

    //
    // Server side verification - FIXED VERSION
    //

    private static readonly HashSet<string> AllowedTags = new HashSet<string> { "b", "i", "u", "color" };
    
    // Improved word filter with better patterns and variations
    private static readonly List<Regex> FilteredWordsRegex = new List<Regex>
    {
        // Fuck variations
        new Regex(@"\bf[\*_\-\.\s]*[u\*_\-\.\s]*[c\*_\-\.\s]*k[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        new Regex(@"\bf[\*_\-\.\s]*[\*_\-\.\s]*[\*_\-\.\s]*k[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        new Regex(@"fck|fuk", RegexOptions.IgnoreCase),
        
        // Shit variations
        new Regex(@"\bs[\*_\-\.\s]*h[\*_\-\.\s]*[i1\*_\-\.\s]*t[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        new Regex(@"\bs[\*_\-\.\s]*[\*_\-\.\s]*[\*_\-\.\s]*t[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        
        // Bitch variations
        new Regex(@"\bb[\*_\-\.\s]*[i1\*_\-\.\s]*t[\*_\-\.\s]*c[\*_\-\.\s]*h[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        new Regex(@"\bb[\*_\-\.\s]*[\*_\-\.\s]*t[\*_\-\.\s]*c[\*_\-\.\s]*h[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        
        // Ass variations
        new Regex(@"\b(ass|@ss|a\$\$|azz)", RegexOptions.IgnoreCase),
        
        // Damn variations
        new Regex(@"\bd[\*_\-\.\s]*a[\*_\-\.\s]*m[\*_\-\.\s]*n[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        
        // Hell variations
        new Regex(@"\bh[\*_\-\.\s]*e[\*_\-\.\s]*l[\*_\-\.\s]*l[\*_\-\.\s]*", RegexOptions.IgnoreCase),
        
        // Crap variations
        new Regex(@"\bc[\*_\-\.\s]*r[\*_\-\.\s]*a[\*_\-\.\s]*p[\*_\-\.\s]*", RegexOptions.IgnoreCase)
    };

    private Dictionary<int, List<double>> messageTimestamps = new Dictionary<int, List<double>>();
    private Dictionary<int, double> cooldownTimestamps = new Dictionary<int, double>();
    private const int MaxMessagesPerWindow = 4;
    private const float MessageWindowSec = 10f;
    private const float CooldownDuration = 10f;
    private const int MaxMessageLength = 200;
    private const int MaxFormattingTags = 5;

    private static string SanitizeMessage(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxMessageLength)
            return "";

        string output = input.Trim();

        // Filter curse words using improved patterns
        foreach (var regex in FilteredWordsRegex)
        {
            output = regex.Replace(output, match => new string('*', match.Value.Length));
        }

        // Count and limit formatting tags
        var tagMatches = Regex.Matches(output, @"\[(?:\/)?(?:b|i|u|color)[^\]]*\]");
        if (tagMatches.Count > MaxFormattingTags)
        {
            // Strip all formatting if too many tags
            output = Regex.Replace(output, @"\[[^\]]*\]", "");
            return output.Trim();
        }

        // Clean and validate BBCode tags
        output = ValidateAndCleanBBCode(output);

        return output.Trim();
    }

    private static string ValidateAndCleanBBCode(string input)
    {
        var result = "";
        var tagStack = new Stack<string>();
        var tagRegex = new Regex(@"\[(\/?)(\w+)([^\]]*)\]");
        var lastIndex = 0;

        foreach (Match match in tagRegex.Matches(input))
        {
            // Add text before this tag
            result += input.Substring(lastIndex, match.Index - lastIndex);
            
            string slash = match.Groups[1].Value;
            string tag = match.Groups[2].Value.ToLower();
            string parameters = match.Groups[3].Value;

            if (!AllowedTags.Contains(tag))
            {
                // Skip invalid tags
                lastIndex = match.Index + match.Length;
                continue;
            }

            if (string.IsNullOrEmpty(slash)) // Opening tag
            {
                // Validate color tag parameters
                if (tag == "color")
                {
                    if (!IsValidColorParameter(parameters))
                    {
                        lastIndex = match.Index + match.Length;
                        continue; // Skip invalid color tag
                    }
                }
                
                tagStack.Push(tag);
                result += match.Value;
            }
            else // Closing tag
            {
                // Find and remove the matching opening tag
                if (tagStack.Contains(tag))
                {
                    var tempTags = new List<string>();
                    
                    // Close all tags until we find the matching one
                    while (tagStack.Count > 0)
                    {
                        var poppedTag = tagStack.Pop();
                        if (poppedTag == tag)
                        {
                            result += $"[/{tag}]";
                            break;
                        }
                        else
                        {
                            // Close the intermediate tag
                            result += $"[/{poppedTag}]";
                            tempTags.Add(poppedTag);
                        }
                    }
                    
                    // Reopen the intermediate tags in reverse order
                    for (int i = tempTags.Count - 1; i >= 0; i--)
                    {
                        tagStack.Push(tempTags[i]);
                        result += $"[{tempTags[i]}]";
                    }
                }
                // If no matching opening tag, skip this closing tag
            }
            
            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        result += input.Substring(lastIndex);

        // Close any remaining open tags
        while (tagStack.Count > 0)
        {
            string openTag = tagStack.Pop();
            result += $"[/{openTag}]";
        }

        return result;
    }

    private static bool IsValidColorParameter(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
            return false;

        // Remove the = sign
        if (!parameters.StartsWith("="))
            return false;
        
        string colorValue = parameters.Substring(1);
        
        // Check for valid color names
        var validColorNames = new HashSet<string> { "red", "blue", "green", "yellow", "white", "black", "gray", "orange", "purple", "pink", "brown" };
        if (validColorNames.Contains(colorValue.ToLower()))
            return true;

        // Check for valid hex color format
        if (Regex.IsMatch(colorValue, @"^#[0-9a-fA-F]{6}$"))
            return true;

        return false;
    }

    private bool IsSpamming(int senderId)
    {
        double now = Time.GetUnixTimeFromSystem();

        // Check if player is still in cooldown
        if (cooldownTimestamps.ContainsKey(senderId) && now < cooldownTimestamps[senderId])
            return true;

        // Initialize message timestamps for new players
        if (!messageTimestamps.ContainsKey(senderId))
            messageTimestamps[senderId] = new List<double>();

        // Remove old timestamps outside the window
        messageTimestamps[senderId].RemoveAll(t => now - t > MessageWindowSec);

        // Add current message timestamp
        messageTimestamps[senderId].Add(now);

        // Check if exceeded message limit
        if (messageTimestamps[senderId].Count > MaxMessagesPerWindow)
        {
            cooldownTimestamps[senderId] = now + CooldownDuration;
            return true;
        }

        return false;
    }
}
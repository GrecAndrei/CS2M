using Colossal.UI.Binding;
using CS2M.API;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;
using Game.UI.InGame;
using Unity.Entities;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CS2M.UI
{
    /// <summary>
    /// Displays a chat to the users screen in a pop up bubble.
    /// Allows a user to send messages to other players and view
    /// events such as server startup and player connections.
    /// </summary>
    public class ChatPanel : EntityGamePanel, IChat
    {
        public readonly struct Message : IJsonWritable
        {
            public string Timestamp { get; }
            public string User { get; }
            public string Text { get; }

            public Message(string timestamp, string user, string text)
            {
                Timestamp = timestamp;
                User = user;
                Text = text;
            }

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(this.GetType().FullName);
                writer.PropertyName("timestamp");
                writer.Write(this.Timestamp);
                writer.PropertyName("user");
                writer.Write(this.User);
                writer.PropertyName("text");
                writer.Write(this.Text);
                writer.TypeEnd();
            }
        }

        public ValueBinding<List<Message>> ChatMessages { get; }
        public ValueBinding<string> CurrentUsername { get; }
        public ValueBinding<string> LocalChatMessage { get; }
        public TriggerBinding SendChatMessage { get; }
        public TriggerBinding<string> SetLocalChatMessage { get; }

        public override LayoutPosition position => LayoutPosition.Right;

        public ChatPanel()
        {
            Chat.Instance = this;

            ChatMessages = new ValueBinding<List<Message>>(Mod.Name, nameof(ChatMessages), new List<Message>(),
                new ListWriter<Message>(new ValueWriter<Message>()));
            CurrentUsername = new ValueBinding<string>(Mod.Name, nameof(CurrentUsername), GetCurrentUsername());
            LocalChatMessage = new ValueBinding<string>(Mod.Name, nameof(LocalChatMessage), string.Empty);
            SendChatMessage = new TriggerBinding(Mod.Name, nameof(SendChatMessage), () => SendMessage());
            SetLocalChatMessage = new TriggerBinding<string>(Mod.Name, nameof(SetLocalChatMessage),
                message => UpdateChatMessage(message));
        }

        private void UpdateChatMessage(string message)
        {
            if (message.EndsWith("\n"))
            {
                // User has pressed 'Enter' so we send the message they have input.
                SendMessage();
            }
            else
            {
                LocalChatMessage.Update(message);
            }
        }

        private void SendMessage()
        {
            string rawText = LocalChatMessage.value?.Trim();
            if (string.IsNullOrEmpty(rawText))
            {
                LocalChatMessage.Update(string.Empty);
                return;
            }

            // Command Interception
            if (rawText.StartsWith("/"))
            {
                HandleSlashCommand(rawText);
                LocalChatMessage.Update(string.Empty);
                return;
            }

            string username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username))
            {
                username = "Local";
            }

            PrintChatMessage(username, rawText);

            ChatMessageCommand message = new ChatMessageCommand()
            {
                Username = GetCurrentUsername(),
                Message = rawText
            };
            CommandInternal.Instance.SendToAll(message);

            LocalChatMessage.Update(string.Empty);
        }

        private void HandleSlashCommand(string cmdText)
        {
            try
            {
                string[] parts = cmdText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return;

                string command = parts[0].ToLower();
                string argsText = string.Join(" ", parts.Skip(1));

                switch (command)
                {
                    case "/help":
                        PrintGameMessage("--- CS2M Cooperative Console Commands ---");
                        PrintGameMessage("/help - Display all available lobby terminal actions");
                        PrintGameMessage("/ping - Query current network latency");
                        PrintGameMessage("/clear - Clear active chat window");
                        PrintGameMessage("/tp <username> - Teleport camera pivot to player");
                        PrintGameMessage("/money <amount> - [Host] Sync city funds");
                        PrintGameMessage("/kick <username> - [Host] Disconnect player");
                        break;

                    case "/clear":
                        ChatMessages.value.Clear();
                        ChatMessages.TriggerUpdate();
                        break;

                    case "/ping":
                        long latency = NetworkInterface.Instance.LocalPlayer.Latency;
                        PrintGameMessage($"Current Latency: {(latency > 0 ? latency.ToString() : "0")}ms");
                        break;

                    case "/tp":
                        if (parts.Length < 2)
                        {
                            PrintGameMessage("Usage: /tp <username>");
                            break;
                        }
                        string tpTarget = parts[1];
                        var foundTp = NetworkInterface.Instance.PlayerListJoined
                            .FirstOrDefault(p => string.Equals(p.Username, tpTarget, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundTp == null)
                        {
                            PrintGameMessage($"Player '{tpTarget}' not found in active lobby.");
                        }
                        else if (foundTp.PlayerId == NetworkInterface.Instance.LocalPlayer.PlayerId)
                        {
                            PrintGameMessage("Cannot teleport camera to yourself.");
                        }
                        else
                        {
                            CS2M.Systems.CooperativeSyncSystem.TeleportCameraToPlayer(foundTp.PlayerId);
                            PrintGameMessage($"Snapped camera view to {foundTp.Username}.");
                        }
                        break;

                    case "/money":
                        if (NetworkInterface.Instance.LocalPlayer.PlayerType != PlayerType.SERVER)
                        {
                            PrintGameMessage("Error: Only the Host can run administrative cheats.");
                            break;
                        }
                        if (parts.Length < 2 || !long.TryParse(parts[1], out long moneyVal))
                        {
                            PrintGameMessage("Usage: /money <amount>");
                            break;
                        }
                        var moneySys = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CS2M.BaseGame.Systems.MoneySyncSystem>();
                        if (moneySys != null)
                        {
                            moneySys.ForceUpdateMoney(moneyVal);
                            PrintGameMessage($"Treasury money balance synced to ${moneyVal:N0}.");
                        }
                        else
                        {
                            PrintGameMessage("Error: MoneySyncSystem not active.");
                        }
                        break;

                    case "/kick":
                        if (NetworkInterface.Instance.LocalPlayer.PlayerType != PlayerType.SERVER)
                        {
                            PrintGameMessage("Error: Only the Host has kicking authority.");
                            break;
                        }
                        if (parts.Length < 2)
                        {
                            PrintGameMessage("Usage: /kick <username>");
                            break;
                        }
                        string kickTarget = parts[1];
                        var foundKick = NetworkInterface.Instance.PlayerListConnected
                            .OfType<RemotePlayer>()
                            .FirstOrDefault(p => string.Equals(p.Username, kickTarget, StringComparison.OrdinalIgnoreCase));

                        if (foundKick == null)
                        {
                            PrintGameMessage($"Player '{kickTarget}' is not connected.");
                        }
                        else
                        {
                            foundKick.Disconnect();
                            PrintGameMessage($"Player '{foundKick.Username}' has been disconnected by host.");
                        }
                        break;

                    default:
                        PrintGameMessage($"Unknown command: {command}. Type /help for a list of lobby console commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                PrintGameMessage($"Command error: {ex.Message}");
            }
        }

        private void PrintMessage(string sender, string msg)
        {
            Log.Info($"Chat message: [{sender}] - {msg}");
            ChatMessages.value.Add(new Message(DateTime.Now.ToShortTimeString(), sender, msg));
            ChatMessages.TriggerUpdate();
        }

        /// <summary>
        /// Prints a game message to the ChatPanel
        /// </summary>
        /// <param name="msg">The message.</param>
        public void PrintGameMessage(string msg)
        {
            PrintMessage(Mod.Name, msg);
        }

        /// <summary>
        /// Prints a game message of a specific type to the ChatPanel
        /// </summary>
        /// <param name="type">The message type</param>
        /// <param name="msg">The message</param>
        /// <exception cref="NotImplementedException"></exception>
        public void PrintGameMessage(Chat.MessageType type, string msg)
        {
            // TODO: Format according to type
            PrintMessage(Mod.Name, msg);
        }

        /// <summary>
        /// Prints a chat message to the ChatPanel
        /// </summary>
        /// <param name="username">The name of the sending user.</param>
        /// <param name="msg">The message.</param>
        public void PrintChatMessage(string username, string msg)
        {
            PrintMessage(username, msg);
        }

        /// <summary>
        /// Fetches the username for the current player and returns it as a string.
        /// </summary>
        /// <returns>The username of the current player</returns>
        public string GetCurrentUsername()
        {
            return NetworkInterface.Instance.LocalPlayer.Username ?? string.Empty;
        }

        public void WelcomeChatMessage()
        {
            PrintGameMessage("Welcome to Cities: Skylines 2 Multiplayer!");
        }
    }
}

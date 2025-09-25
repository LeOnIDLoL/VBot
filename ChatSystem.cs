using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chat System", "BULBARUST", "1.0.0")]
    [Description("Расширенная система чата для сервера BULBARUST")]
    public class ChatSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableChatSystem { get; set; } = true;
            public bool EnableGlobalChat { get; set; } = true;
            public bool EnableLocalChat { get; set; } = true;
            public bool EnablePrivateMessages { get; set; } = true;
            public bool EnableChatChannels { get; set; } = true;
            public bool EnableChatFilters { get; set; } = true;
            public bool EnableChatLogging { get; set; } = true;
            public float LocalChatRadius { get; set; } = 100f;
            public float ChatCooldown { get; set; } = 1f;
            public int MaxMessageLength { get; set; } = 200;
            public List<string> BannedWords { get; set; } = new List<string>();
            public List<ChatChannel> Channels { get; set; } = new List<ChatChannel>();
            public List<ChatFilter> Filters { get; set; } = new List<ChatFilter>();
        }

        private class ChatChannel
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Color { get; set; } = "white";
            public bool IsPublic { get; set; } = true;
            public List<string> RequiredPermissions { get; set; } = new List<string>();
            public float Cooldown { get; set; } = 0f;
        }

        private class ChatFilter
        {
            public string Name { get; set; }
            public string Pattern { get; set; }
            public string Replacement { get; set; } = "***";
            public bool IsRegex { get; set; } = false;
            public bool IsCaseSensitive { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем каналы чата
            config.Channels.AddRange(new List<ChatChannel>
            {
                new ChatChannel
                {
                    Id = "global",
                    Name = "Глобальный",
                    Description = "Общий чат сервера",
                    Color = "white",
                    IsPublic = true
                },
                new ChatChannel
                {
                    Id = "local",
                    Name = "Локальный",
                    Description = "Чат в радиусе 100м",
                    Color = "yellow",
                    IsPublic = true
                },
                new ChatChannel
                {
                    Id = "trade",
                    Name = "Торговля",
                    Description = "Канал для торговли",
                    Color = "green",
                    IsPublic = true
                },
                new ChatChannel
                {
                    Id = "help",
                    Name = "Помощь",
                    Description = "Канал помощи новичкам",
                    Color = "cyan",
                    IsPublic = true
                },
                new ChatChannel
                {
                    Id = "admin",
                    Name = "Админ",
                    Description = "Админский чат",
                    Color = "red",
                    IsPublic = false,
                    RequiredPermissions = new List<string> { "admin" }
                }
            });

            // Добавляем фильтры
            config.Filters.AddRange(new List<ChatFilter>
            {
                new ChatFilter
                {
                    Name = "Реклама",
                    Pattern = "discord\\.gg|vk\\.com|youtube\\.com",
                    IsRegex = true,
                    IsCaseSensitive = false
                },
                new ChatFilter
                {
                    Name = "Оскорбления",
                    Pattern = "дурак|идиот|тупой",
                    IsRegex = false,
                    IsCaseSensitive = false
                }
            });

            // Добавляем запрещенные слова
            config.BannedWords.AddRange(new List<string>
            {
                "реклама",
                "спам",
                "хайп"
            });
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Data
        private Dictionary<ulong, PlayerChatData> playerChat = new Dictionary<ulong, PlayerChatData>();
        private Dictionary<ulong, float> lastMessageTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, string> playerChannels = new Dictionary<ulong, string>();
        private Dictionary<ulong, List<string>> chatHistory = new Dictionary<ulong, List<string>>();
        private Dictionary<ulong, List<string>> ignoredPlayers = new Dictionary<ulong, List<string>>();

        private class PlayerChatData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public string CurrentChannel { get; set; } = "global";
            public bool IsMuted { get; set; } = false;
            public float MuteEndTime { get; set; } = 0f;
            public int MessageCount { get; set; } = 0;
            public int ViolationCount { get; set; } = 0;
            public float LastActivity { get; set; }
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система чата BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableChatSystem)
            {
                timer.Every(60f, ProcessChatMaintenance);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableChatSystem)
            {
                InitializePlayerChat(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerChatData(player.userID);
        }

        object OnPlayerChat(BasePlayer player, string message)
        {
            if (!config.EnableChatSystem) return null;

            // Обрабатываем сообщение
            var processedMessage = ProcessChatMessage(player, message);
            if (processedMessage == null)
            {
                return false; // Блокируем отправку
            }

            return null; // Разрешаем отправку
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerChat = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerChatData>>("player_chat") ?? new Dictionary<ulong, PlayerChatData>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_chat", playerChat);
        }

        private void SavePlayerChatData(ulong playerId)
        {
            if (playerChat.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"chat_{playerId}", playerChat[playerId]);
            }
        }

        private void InitializePlayerChat(BasePlayer player)
        {
            if (!playerChat.ContainsKey(player.userID))
            {
                var chatData = new PlayerChatData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    CurrentChannel = "global",
                    LastActivity = Time.time
                };

                playerChat[player.userID] = chatData;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerChat[player.userID].PlayerName = player.displayName;
            }
        }

        private void ProcessChatMaintenance()
        {
            var currentTime = Time.time;

            // Проверяем истечение мутов
            foreach (var playerId in playerChat.Keys.ToList())
            {
                var chatData = playerChat[playerId];
                if (chatData.IsMuted && currentTime >= chatData.MuteEndTime)
                {
                    chatData.IsMuted = false;
                    chatData.MuteEndTime = 0f;

                    var player = BasePlayer.FindByID(playerId);
                    if (player != null && player.IsConnected)
                    {
                        player.ChatMessage("<color=green>Мут снят!</color>");
                    }
                }
            }

            SaveData();
        }

        private string ProcessChatMessage(BasePlayer player, string message)
        {
            if (!playerChat.ContainsKey(player.userID))
            {
                InitializePlayerChat(player);
            }

            var chatData = playerChat[player.userID];

            // Проверяем мут
            if (chatData.IsMuted)
            {
                if (Time.time < chatData.MuteEndTime)
                {
                    var timeLeft = chatData.MuteEndTime - Time.time;
                    player.ChatMessage($"<color=red>Вы в муте еще {Mathf.CeilToInt(timeLeft / 60)} минут</color>");
                    return null;
                }
                else
                {
                    chatData.IsMuted = false;
                    chatData.MuteEndTime = 0f;
                }
            }

            // Проверяем кулдаун
            if (lastMessageTime.ContainsKey(player.userID))
            {
                var timeLeft = config.ChatCooldown - (Time.time - lastMessageTime[player.userID]);
                if (timeLeft > 0)
                {
                    player.ChatMessage($"<color=red>Осталось ждать: {Mathf.CeilToInt(timeLeft)} секунд</color>");
                    return null;
                }
            }

            // Проверяем длину сообщения
            if (message.Length > config.MaxMessageLength)
            {
                player.ChatMessage($"<color=red>Сообщение слишком длинное (макс. {config.MaxMessageLength} символов)</color>");
                return null;
            }

            // Применяем фильтры
            var filteredMessage = ApplyChatFilters(message);
            if (string.IsNullOrEmpty(filteredMessage))
            {
                player.ChatMessage("<color=red>Сообщение заблокировано фильтром</color>");
                chatData.ViolationCount++;
                return null;
            }

            // Обновляем статистику
            chatData.MessageCount++;
            chatData.LastActivity = Time.time;
            lastMessageTime[player.userID] = Time.time;

            // Логируем сообщение
            if (config.EnableChatLogging)
            {
                LogChatMessage(player, filteredMessage);
            }

            return filteredMessage;
        }

        private string ApplyChatFilters(string message)
        {
            var filteredMessage = message;

            // Проверяем запрещенные слова
            foreach (var bannedWord in config.BannedWords)
            {
                if (filteredMessage.ToLower().Contains(bannedWord.ToLower()))
                {
                    return null; // Блокируем сообщение
                }
            }

            // Применяем фильтры
            foreach (var filter in config.Filters)
            {
                if (filter.IsRegex)
                {
                    var regex = new System.Text.RegularExpressions.Regex(filter.Pattern, 
                        filter.IsCaseSensitive ? System.Text.RegularExpressions.RegexOptions.None : 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    if (regex.IsMatch(filteredMessage))
                    {
                        filteredMessage = regex.Replace(filteredMessage, filter.Replacement);
                    }
                }
                else
                {
                    var comparison = filter.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    if (filteredMessage.IndexOf(filter.Pattern, comparison) >= 0)
                    {
                        filteredMessage = filteredMessage.Replace(filter.Pattern, filter.Replacement, comparison);
                    }
                }
            }

            return filteredMessage;
        }

        private void LogChatMessage(BasePlayer player, string message)
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {player.displayName}: {message}";
            Puts(logEntry);
        }

        private bool CanUseChannel(ulong playerId, string channelId)
        {
            var channel = config.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel == null) return false;

            if (channel.IsPublic) return true;

            var player = BasePlayer.FindByID(playerId);
            if (player == null) return false;

            return channel.RequiredPermissions.All(p => permission.UserHasPermission(player.UserIDString, p));
        }

        private void SendChannelMessage(BasePlayer player, string channelId, string message)
        {
            var channel = config.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel == null) return;

            var channelColor = channel.Color;
            var formattedMessage = $"<color={channelColor}>[{channel.Name}] {player.displayName}: {message}</color>";

            // Отправляем сообщение в зависимости от типа канала
            switch (channelId)
            {
                case "global":
                    SendGlobalMessage(formattedMessage);
                    break;
                case "local":
                    SendLocalMessage(player, formattedMessage);
                    break;
                default:
                    SendChannelMessageToPlayers(channelId, formattedMessage);
                    break;
            }
        }

        private void SendGlobalMessage(string message)
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                p.ChatMessage(message);
            }
        }

        private void SendLocalMessage(BasePlayer sender, string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Vector3.Distance(sender.transform.position, player.transform.position) <= config.LocalChatRadius)
                {
                    player.ChatMessage(message);
                }
            }
        }

        private void SendChannelMessageToPlayers(string channelId, string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (playerChannels.ContainsKey(player.userID) && playerChannels[player.userID] == channelId)
                {
                    player.ChatMessage(message);
                }
            }
        }

        private void SendPrivateMessage(BasePlayer sender, BasePlayer target, string message)
        {
            var senderMessage = $"<color=purple>[PM -> {target.displayName}] {message}</color>";
            var targetMessage = $"<color=purple>[PM от {sender.displayName}] {message}</color>";

            sender.ChatMessage(senderMessage);
            target.ChatMessage(targetMessage);
        }

        private void MutePlayer(ulong playerId, float duration, string reason = "")
        {
            if (!playerChat.ContainsKey(playerId)) return;

            var chatData = playerChat[playerId];
            chatData.IsMuted = true;
            chatData.MuteEndTime = Time.time + duration;
            chatData.ViolationCount++;

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                var durationText = duration >= 60 ? $"{Mathf.CeilToInt(duration / 60)} минут" : $"{Mathf.CeilToInt(duration)} секунд";
                player.ChatMessage($"<color=red>Вы получили мут на {durationText}</color>");
                if (!string.IsNullOrEmpty(reason))
                {
                    player.ChatMessage($"<color=red>Причина: {reason}</color>");
                }
            }

            SaveData();
        }

        private void UnmutePlayer(ulong playerId)
        {
            if (!playerChat.ContainsKey(playerId)) return;

            var chatData = playerChat[playerId];
            chatData.IsMuted = false;
            chatData.MuteEndTime = 0f;

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage("<color=green>Мут снят!</color>");
            }

            SaveData();
        }
        #endregion

        #region Commands
        [ChatCommand("chat")]
        private void ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableChatSystem)
            {
                player.ChatMessage("Система чата отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowChatHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "channel":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /chat channel <канал>");
                        return;
                    }
                    ChangeChannelCommand(player, args[1]);
                    break;
                case "pm":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /chat pm <игрок> <сообщение>");
                        return;
                    }
                    PrivateMessageCommand(player, args[1], string.Join(" ", args.Skip(2)));
                    break;
                case "reply":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /chat reply <сообщение>");
                        return;
                    }
                    ReplyCommand(player, string.Join(" ", args.Skip(1)));
                    break;
                case "ignore":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /chat ignore <игрок>");
                        return;
                    }
                    IgnorePlayerCommand(player, args[1]);
                    break;
                case "unignore":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /chat unignore <игрок>");
                        return;
                    }
                    UnignorePlayerCommand(player, args[1]);
                    break;
                case "channels":
                    ShowAvailableChannels(player);
                    break;
                case "settings":
                    ShowChatSettings(player);
                    break;
            }
        }

        [ChatCommand("g")]
        private void GlobalChatCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /g <сообщение>");
                return;
            }

            var message = string.Join(" ", args);
            SendChannelMessage(player, "global", message);
        }

        [ChatCommand("l")]
        private void LocalChatCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /l <сообщение>");
                return;
            }

            var message = string.Join(" ", args);
            SendChannelMessage(player, "local", message);
        }

        [ChatCommand("pm")]
        private void PMCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /pm <игрок> <сообщение>");
                return;
            }

            var targetName = args[0];
            var message = string.Join(" ", args.Skip(1));
            PrivateMessageCommand(player, targetName, message);
        }
        #endregion

        #region Command Methods
        private void ShowChatHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ ЧАТА ===</color>");
            player.ChatMessage("/chat channel <канал> - сменить канал");
            player.ChatMessage("/chat pm <игрок> <сообщение> - личное сообщение");
            player.ChatMessage("/chat reply <сообщение> - ответить на ЛС");
            player.ChatMessage("/chat ignore <игрок> - игнорировать игрока");
            player.ChatMessage("/chat unignore <игрок> - снять игнор");
            player.ChatMessage("/chat channels - доступные каналы");
            player.ChatMessage("/chat settings - настройки чата");
            player.ChatMessage("/g <сообщение> - глобальный чат");
            player.ChatMessage("/l <сообщение> - локальный чат");
            player.ChatMessage("/pm <игрок> <сообщение> - личное сообщение");
        }

        private void ChangeChannelCommand(BasePlayer player, string channelId)
        {
            if (!CanUseChannel(player.userID, channelId))
            {
                player.ChatMessage("У вас нет доступа к этому каналу");
                return;
            }

            if (!playerChat.ContainsKey(player.userID))
            {
                InitializePlayerChat(player);
            }

            var chatData = playerChat[player.userID];
            chatData.CurrentChannel = channelId;
            playerChannels[player.userID] = channelId;

            var channel = config.Channels.FirstOrDefault(c => c.Id == channelId);
            player.ChatMessage($"<color=green>Переключен на канал: {channel?.Name}</color>");
            player.ChatMessage($"<color=white>{channel?.Description}</color>");

            SaveData();
        }

        private void PrivateMessageCommand(BasePlayer player, string targetName, string message)
        {
            if (!config.EnablePrivateMessages)
            {
                player.ChatMessage("Личные сообщения отключены");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (target == player)
            {
                player.ChatMessage("Нельзя отправить сообщение самому себе");
                return;
            }

            SendPrivateMessage(player, target, message);
        }

        private void ReplyCommand(BasePlayer player, string message)
        {
            // Здесь должна быть логика ответа на последнее ЛС
            player.ChatMessage("Функция ответа пока не реализована");
        }

        private void IgnorePlayerCommand(BasePlayer player, string targetName)
        {
            if (!ignoredPlayers.ContainsKey(player.userID))
            {
                ignoredPlayers[player.userID] = new List<string>();
            }

            if (ignoredPlayers[player.userID].Contains(targetName))
            {
                player.ChatMessage($"<color=yellow>Игрок '{targetName}' уже в списке игнорируемых</color>");
                return;
            }

            ignoredPlayers[player.userID].Add(targetName);
            player.ChatMessage($"<color=green>Игрок '{targetName}' добавлен в список игнорируемых</color>");
        }

        private void UnignorePlayerCommand(BasePlayer player, string targetName)
        {
            if (!ignoredPlayers.ContainsKey(player.userID) || !ignoredPlayers[player.userID].Contains(targetName))
            {
                player.ChatMessage($"<color=yellow>Игрок '{targetName}' не в списке игнорируемых</color>");
                return;
            }

            ignoredPlayers[player.userID].Remove(targetName);
            player.ChatMessage($"<color=green>Игрок '{targetName}' удален из списка игнорируемых</color>");
        }

        private void ShowAvailableChannels(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ КАНАЛЫ ===</color>");
            foreach (var channel in config.Channels)
            {
                if (CanUseChannel(player.userID, channel.Id))
                {
                    var status = playerChannels.ContainsKey(player.userID) && playerChannels[player.userID] == channel.Id ? " (текущий)" : "";
                    player.ChatMessage($"<color={channel.Color}>{channel.Name}{status}</color>");
                    player.ChatMessage($"  {channel.Description}");
                }
            }
        }

        private void ShowChatSettings(BasePlayer player)
        {
            if (!playerChat.ContainsKey(player.userID))
            {
                InitializePlayerChat(player);
            }

            var chatData = playerChat[player.userID];

            player.ChatMessage("<color=yellow>=== НАСТРОЙКИ ЧАТА ===</color>");
            player.ChatMessage($"Текущий канал: {chatData.CurrentChannel}");
            player.ChatMessage($"Сообщений отправлено: {chatData.MessageCount}");
            player.ChatMessage($"Нарушений: {chatData.ViolationCount}");
            player.ChatMessage($"Статус: {(chatData.IsMuted ? "Заглушен" : "Активен")}");
            
            if (chatData.IsMuted)
            {
                var timeLeft = chatData.MuteEndTime - Time.time;
                player.ChatMessage($"Осталось мута: {Mathf.CeilToInt(timeLeft / 60)} минут");
            }
        }
        #endregion
    }
}
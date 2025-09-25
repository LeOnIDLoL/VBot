using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Moderation System", "BULBARUST", "1.0.0")]
    [Description("Система модерации для сервера BULBARUST")]
    public class ModerationSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableModerationSystem { get; set; } = true;
            public bool EnableAutoModeration { get; set; } = true;
            public bool EnableChatModeration { get; set; } = true;
            public bool EnableBehaviorModeration { get; set; } = true;
            public bool EnableReportSystem { get; set; } = true;
            public bool EnableAppealSystem { get; set; } = true;
            public ModerationSettings ModerationSettings { get; set; } = new ModerationSettings();
            public ChatModerationSettings ChatModerationSettings { get; set; } = new ChatModerationSettings();
            public BehaviorModerationSettings BehaviorModerationSettings { get; set; } = new BehaviorModerationSettings();
            public ReportSettings ReportSettings { get; set; } = new ReportSettings();
        }

        private class ModerationSettings
        {
            public bool EnableWarnings { get; set; } = true;
            public bool EnableKicks { get; set; } = true;
            public bool EnableBans { get; set; } = true;
            public bool EnableMutes { get; set; } = true;
            public int MaxWarnings { get; set; } = 3;
            public int MaxKicks { get; set; } = 2;
            public float BanDuration { get; set; } = 86400f; // 24 часа
            public float MuteDuration { get; set; } = 3600f; // 1 час
            public List<string> ModeratorPermissions { get; set; } = new List<string>();
            public List<string> AdminPermissions { get; set; } = new List<string>();
        }

        private class ChatModerationSettings
        {
            public bool EnableSpamProtection { get; set; } = true;
            public bool EnableCapsProtection { get; set; } = true;
            public bool EnableFloodProtection { get; set; } = true;
            public bool EnableProfanityFilter { get; set; } = true;
            public int MaxMessagesPerMinute { get; set; } = 10;
            public int MaxCapsPercentage { get; set; } = 70;
            public float SpamCooldown { get; set; } = 30f;
            public List<string> BannedWords { get; set; } = new List<string>();
            public List<string> BannedPhrases { get; set; } = new List<string>();
        }

        private class BehaviorModerationSettings
        {
            public bool EnableGriefingProtection { get; set; } = true;
            public bool EnableHarassmentProtection { get; set; } = true;
            public bool EnableExploitProtection { get; set; } = true;
            public int MaxGriefingReports { get; set; } = 3;
            public int MaxHarassmentReports { get; set; } = 2;
            public float ProtectionRadius { get; set; } = 10f;
            public List<string> ProtectedBlocks { get; set; } = new List<string>();
        }

        private class ReportSettings
        {
            public bool EnablePlayerReports { get; set; } = true;
            public bool EnableAdminReports { get; set; } = true;
            public bool EnableAnonymousReports { get; set; } = true;
            public int MaxReportsPerPlayer { get; set; } = 5;
            public float ReportCooldown { get; set; } = 300f; // 5 минут
            public List<string> ReportTypes { get; set; } = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Настройки модерации
            config.ModerationSettings.ModeratorPermissions.AddRange(new List<string>
            {
                "moderator.warn", "moderator.kick", "moderator.mute", "moderator.report"
            });

            config.ModerationSettings.AdminPermissions.AddRange(new List<string>
            {
                "admin.ban", "admin.unban", "admin.teleport", "admin.god", "admin.noclip"
            });

            // Настройки чата
            config.ChatModerationSettings.BannedWords.AddRange(new List<string>
            {
                "спам", "реклама", "чит", "хаки", "обман"
            });

            config.ChatModerationSettings.BannedPhrases.AddRange(new List<string>
            {
                "купить аккаунт", "продать аккаунт", "бесплатные предметы"
            });

            // Настройки поведения
            config.BehaviorModerationSettings.ProtectedBlocks.AddRange(new List<string>
            {
                "foundation", "wall", "door", "chest", "furnace"
            });

            // Настройки жалоб
            config.ReportSettings.ReportTypes.AddRange(new List<string>
            {
                "griefing", "harassment", "cheating", "spam", "inappropriate_behavior"
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
        private Dictionary<ulong, PlayerModerationData> playerModerationData = new Dictionary<ulong, PlayerModerationData>();
        private Dictionary<ulong, List<ModerationAction>> moderationHistory = new Dictionary<ulong, List<ModerationAction>>();
        private Dictionary<ulong, List<PlayerReport>> playerReports = new Dictionary<ulong, List<PlayerReport>>();
        private Dictionary<ulong, List<ChatMessage>> chatHistory = new Dictionary<ulong, List<ChatMessage>>();
        private Dictionary<ulong, float> lastChatMessage = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> messageCount = new Dictionary<ulong, int>();
        private Dictionary<ulong, float> muteEndTime = new Dictionary<ulong, float>();

        private class PlayerModerationData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public int WarningCount { get; set; } = 0;
            public int KickCount { get; set; } = 0;
            public int BanCount { get; set; } = 0;
            public int MuteCount { get; set; } = 0;
            public bool IsMuted { get; set; } = false;
            public bool IsBanned { get; set; } = false;
            public float LastWarningTime { get; set; }
            public float LastKickTime { get; set; }
            public float LastBanTime { get; set; }
            public float LastMuteTime { get; set; }
            public List<string> ViolationTypes { get; set; } = new List<string>();
            public Dictionary<string, object> ModerationFlags { get; set; } = new Dictionary<string, object>();
        }

        private class ModerationAction
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string Reason { get; set; }
            public ulong ModeratorId { get; set; }
            public string ModeratorName { get; set; }
            public float Timestamp { get; set; }
            public string Severity { get; set; } = "medium";
            public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        }

        private class PlayerReport
        {
            public string Id { get; set; }
            public ulong ReporterId { get; set; }
            public string ReporterName { get; set; }
            public ulong ReportedPlayerId { get; set; }
            public string ReportedPlayerName { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public float Timestamp { get; set; }
            public string Status { get; set; } = "pending";
            public ulong ModeratorId { get; set; }
            public string ModeratorName { get; set; }
            public string Resolution { get; set; }
        }

        private class ChatMessage
        {
            public string Message { get; set; }
            public float Timestamp { get; set; }
            public bool IsFiltered { get; set; } = false;
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система модерации BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableModerationSystem)
            {
                timer.Every(60f, ProcessModerationActions);
                timer.Every(300f, CleanupOldData);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableModerationSystem)
            {
                InitializePlayerModeration(player);
                CheckPlayerMuteStatus(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerModerationData(player.userID);
        }

        object OnPlayerChat(BasePlayer player, string message)
        {
            if (!config.EnableModerationSystem || !config.EnableChatModeration) return null;

            // Проверяем, не заглушен ли игрок
            if (IsPlayerMuted(player))
            {
                player.ChatMessage("<color=red>Вы заглушены и не можете писать в чат</color>");
                return false;
            }

            // Проверяем спам
            if (CheckChatSpam(player, message))
            {
                return false;
            }

            // Проверяем фильтр слов
            if (CheckProfanityFilter(player, message))
            {
                return false;
            }

            // Записываем сообщение
            RecordChatMessage(player, message);

            return null;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableModerationSystem || !config.EnableBehaviorModeration) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            CheckGriefingProtection(player, go);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerModerationData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerModerationData>>("player_moderation_data") ?? new Dictionary<ulong, PlayerModerationData>();
            moderationHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ModerationAction>>>("moderation_history") ?? new Dictionary<ulong, List<ModerationAction>>();
            playerReports = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PlayerReport>>>("player_reports") ?? new Dictionary<ulong, List<PlayerReport>>();
            chatHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ChatMessage>>>("chat_history") ?? new Dictionary<ulong, List<ChatMessage>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_moderation_data", playerModerationData);
            Interface.Oxide.DataFileSystem.WriteObject("moderation_history", moderationHistory);
            Interface.Oxide.DataFileSystem.WriteObject("player_reports", playerReports);
            Interface.Oxide.DataFileSystem.WriteObject("chat_history", chatHistory);
        }

        private void SavePlayerModerationData(ulong playerId)
        {
            if (playerModerationData.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"moderation_data_{playerId}", playerModerationData[playerId]);
            }
        }

        private void InitializePlayerModeration(BasePlayer player)
        {
            if (!playerModerationData.ContainsKey(player.userID))
            {
                var moderationData = new PlayerModerationData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    WarningCount = 0,
                    KickCount = 0,
                    BanCount = 0,
                    MuteCount = 0,
                    IsMuted = false,
                    IsBanned = false
                };

                playerModerationData[player.userID] = moderationData;
            }

            if (!chatHistory.ContainsKey(player.userID))
            {
                chatHistory[player.userID] = new List<ChatMessage>();
            }

            if (!moderationHistory.ContainsKey(player.userID))
            {
                moderationHistory[player.userID] = new List<ModerationAction>();
            }
        }

        private void CheckPlayerMuteStatus(BasePlayer player)
        {
            if (muteEndTime.ContainsKey(player.userID))
            {
                if (Time.time < muteEndTime[player.userID])
                {
                    playerModerationData[player.userID].IsMuted = true;
                }
                else
                {
                    muteEndTime.Remove(player.userID);
                    playerModerationData[player.userID].IsMuted = false;
                }
            }
        }

        private bool IsPlayerMuted(BasePlayer player)
        {
            if (!playerModerationData.ContainsKey(player.userID)) return false;
            return playerModerationData[player.userID].IsMuted;
        }

        private bool CheckChatSpam(BasePlayer player, string message)
        {
            if (!config.ChatModerationSettings.EnableSpamProtection) return false;

            var currentTime = Time.time;
            var playerId = player.userID;

            // Проверяем количество сообщений в минуту
            if (!messageCount.ContainsKey(playerId))
            {
                messageCount[playerId] = 0;
            }

            if (!lastChatMessage.ContainsKey(playerId))
            {
                lastChatMessage[playerId] = currentTime;
            }

            // Сбрасываем счетчик каждую минуту
            if (currentTime - lastChatMessage[playerId] >= 60f)
            {
                messageCount[playerId] = 0;
                lastChatMessage[playerId] = currentTime;
            }

            messageCount[playerId]++;

            if (messageCount[playerId] > config.ChatModerationSettings.MaxMessagesPerMinute)
            {
                WarnPlayer(player, "Спам в чате", "chat_spam");
                player.ChatMessage("<color=red>Слишком много сообщений! Остановитесь на минуту</color>");
                return true;
            }

            return false;
        }

        private bool CheckProfanityFilter(BasePlayer player, string message)
        {
            if (!config.ChatModerationSettings.EnableProfanityFilter) return false;

            var lowerMessage = message.ToLower();

            // Проверяем запрещенные слова
            foreach (var word in config.ChatModerationSettings.BannedWords)
            {
                if (lowerMessage.Contains(word.ToLower()))
                {
                    WarnPlayer(player, "Использование запрещенных слов", "profanity");
                    player.ChatMessage("<color=red>Ваше сообщение содержит запрещенные слова</color>");
                    return true;
                }
            }

            // Проверяем запрещенные фразы
            foreach (var phrase in config.ChatModerationSettings.BannedPhrases)
            {
                if (lowerMessage.Contains(phrase.ToLower()))
                {
                    WarnPlayer(player, "Использование запрещенных фраз", "profanity");
                    player.ChatMessage("<color=red>Ваше сообщение содержит запрещенные фразы</color>");
                    return true;
                }
            }

            return false;
        }

        private void RecordChatMessage(BasePlayer player, string message)
        {
            if (!chatHistory.ContainsKey(player.userID))
            {
                chatHistory[player.userID] = new List<ChatMessage>();
            }

            var chatMessage = new ChatMessage
            {
                Message = message,
                Timestamp = Time.time
            };

            chatHistory[player.userID].Add(chatMessage);

            // Ограничиваем историю
            if (chatHistory[player.userID].Count > 1000)
            {
                chatHistory[player.userID] = chatHistory[player.userID].OrderByDescending(c => c.Timestamp).Take(500).ToList();
            }
        }

        private void CheckGriefingProtection(BasePlayer player, GameObject go)
        {
            if (!config.BehaviorModerationSettings.EnableGriefingProtection) return;

            var blockName = go.name.ToLower();
            if (config.BehaviorModerationSettings.ProtectedBlocks.Contains(blockName))
            {
                // Здесь должна быть логика проверки на грифинг
                // Например, проверка на разрушение чужих блоков
            }
        }

        private void WarnPlayer(BasePlayer player, string reason, string violationType)
        {
            if (!playerModerationData.ContainsKey(player.userID))
            {
                InitializePlayerModeration(player);
            }

            var moderationData = playerModerationData[player.userID];
            moderationData.WarningCount++;
            moderationData.LastWarningTime = Time.time;
            moderationData.ViolationTypes.Add(violationType);

            var action = new ModerationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = "warning",
                Reason = reason,
                ModeratorId = 0, // Система
                ModeratorName = "Система",
                Timestamp = Time.time,
                Severity = "low"
            };

            if (!moderationHistory.ContainsKey(player.userID))
            {
                moderationHistory[player.userID] = new List<ModerationAction>();
            }

            moderationHistory[player.userID].Add(action);

            player.ChatMessage($"<color=red>ПРЕДУПРЕЖДЕНИЕ: {reason}</color>");
            player.ChatMessage($"<color=yellow>Предупреждений: {moderationData.WarningCount}/{config.ModerationSettings.MaxWarnings}</color>");

            // Проверяем на автоматические действия
            if (moderationData.WarningCount >= config.ModerationSettings.MaxWarnings)
            {
                MutePlayer(player, "Превышено количество предупреждений", 0);
            }

            SaveData();
        }

        private void MutePlayer(BasePlayer player, string reason, ulong moderatorId)
        {
            if (!config.ModerationSettings.EnableMutes) return;

            if (!playerModerationData.ContainsKey(player.userID))
            {
                InitializePlayerModeration(player);
            }

            var moderationData = playerModerationData[player.userID];
            moderationData.IsMuted = true;
            moderationData.MuteCount++;
            moderationData.LastMuteTime = Time.time;

            muteEndTime[player.userID] = Time.time + config.ModerationSettings.MuteDuration;

            var action = new ModerationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = "mute",
                Reason = reason,
                ModeratorId = moderatorId,
                ModeratorName = moderatorId == 0 ? "Система" : BasePlayer.FindByID(moderatorId)?.displayName ?? "Неизвестно",
                Timestamp = Time.time,
                Severity = "medium"
            };

            if (!moderationHistory.ContainsKey(player.userID))
            {
                moderationHistory[player.userID] = new List<ModerationAction>();
            }

            moderationHistory[player.userID].Add(action);

            player.ChatMessage($"<color=red>ВЫ ЗАГЛУШЕНЫ: {reason}</color>");
            player.ChatMessage($"<color=yellow>Длительность: {config.ModerationSettings.MuteDuration / 60f:F0} минут</color>");

            SaveData();
        }

        private void ProcessModerationActions()
        {
            var currentTime = Time.time;
            var playersToUnmute = new List<ulong>();

            foreach (var playerId in muteEndTime.Keys.ToList())
            {
                if (currentTime >= muteEndTime[playerId])
                {
                    playersToUnmute.Add(playerId);
                }
            }

            foreach (var playerId in playersToUnmute)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player != null)
                {
                    playerModerationData[playerId].IsMuted = false;
                    player.ChatMessage("<color=green>Заглушение снято</color>");
                }
                muteEndTime.Remove(playerId);
            }
        }

        private void CleanupOldData()
        {
            var currentTime = Time.time;
            var cutoffTime = currentTime - 86400f; // 24 часа

            foreach (var playerId in chatHistory.Keys.ToList())
            {
                chatHistory[playerId] = chatHistory[playerId].Where(c => c.Timestamp > cutoffTime).ToList();
            }

            foreach (var playerId in moderationHistory.Keys.ToList())
            {
                moderationHistory[playerId] = moderationHistory[playerId].Where(m => m.Timestamp > cutoffTime).ToList();
            }
        }
        #endregion

        #region Commands
        [ChatCommand("mod")]
        private void ModCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "moderator"))
            {
                player.ChatMessage("У вас нет прав на модерацию");
                return;
            }

            if (args.Length == 0)
            {
                ShowModerationHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "warn":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /mod warn <имя> <причина>");
                        return;
                    }
                    WarnPlayerCommand(player, args[1], string.Join(" ", args.Skip(2)));
                    break;
                case "mute":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /mod mute <имя> <причина>");
                        return;
                    }
                    MutePlayerCommand(player, args[1], string.Join(" ", args.Skip(2)));
                    break;
                case "unmute":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /mod unmute <имя>");
                        return;
                    }
                    UnmutePlayerCommand(player, args[1]);
                    break;
                case "kick":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /mod kick <имя> <причина>");
                        return;
                    }
                    KickPlayerCommand(player, args[1], string.Join(" ", args.Skip(2)));
                    break;
                case "ban":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /mod ban <имя> <причина>");
                        return;
                    }
                    BanPlayerCommand(player, args[1], string.Join(" ", args.Skip(2)));
                    break;
                case "info":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /mod info <имя>");
                        return;
                    }
                    ShowPlayerModerationInfo(player, args[1]);
                    break;
                case "history":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /mod history <имя>");
                        return;
                    }
                    ShowPlayerModerationHistory(player, args[1]);
                    break;
            }
        }

        [ChatCommand("report")]
        private void ReportCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableReportSystem)
            {
                player.ChatMessage("Система жалоб отключена");
                return;
            }

            if (args.Length < 3)
            {
                player.ChatMessage("Использование: /report <имя> <тип> <описание>");
                player.ChatMessage("Типы: griefing, harassment, cheating, spam, inappropriate_behavior");
                return;
            }

            ReportPlayer(player, args[0], args[1], string.Join(" ", args.Skip(2)));
        }
        #endregion

        #region Command Methods
        private void ShowModerationHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ МОДЕРАЦИИ ===</color>");
            player.ChatMessage("/mod warn <имя> <причина> - предупреждение");
            player.ChatMessage("/mod mute <имя> <причина> - заглушить");
            player.ChatMessage("/mod unmute <имя> - снять заглушение");
            player.ChatMessage("/mod kick <имя> <причина> - кикнуть");
            player.ChatMessage("/mod ban <имя> <причина> - забанить");
            player.ChatMessage("/mod info <имя> - информация об игроке");
            player.ChatMessage("/mod history <имя> - история нарушений");
            player.ChatMessage("/report <имя> <тип> <описание> - пожаловаться");
        }

        private void WarnPlayerCommand(BasePlayer moderator, string playerName, string reason)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerModerationData.ContainsKey(targetPlayer.userID))
            {
                InitializePlayerModeration(targetPlayer);
            }

            var moderationData = playerModerationData[targetPlayer.userID];
            moderationData.WarningCount++;
            moderationData.LastWarningTime = Time.time;

            var action = new ModerationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = "warning",
                Reason = reason,
                ModeratorId = moderator.userID,
                ModeratorName = moderator.displayName,
                Timestamp = Time.time,
                Severity = "low"
            };

            if (!moderationHistory.ContainsKey(targetPlayer.userID))
            {
                moderationHistory[targetPlayer.userID] = new List<ModerationAction>();
            }

            moderationHistory[targetPlayer.userID].Add(action);

            targetPlayer.ChatMessage($"<color=red>ПРЕДУПРЕЖДЕНИЕ от {moderator.displayName}: {reason}</color>");
            moderator.ChatMessage($"<color=green>Предупреждение выдано игроку {playerName}</color>");

            SaveData();
        }

        private void MutePlayerCommand(BasePlayer moderator, string playerName, string reason)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            MutePlayer(targetPlayer, reason, moderator.userID);
            moderator.ChatMessage($"<color=green>Игрок {playerName} заглушен</color>");
        }

        private void UnmutePlayerCommand(BasePlayer moderator, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerModerationData.ContainsKey(targetPlayer.userID))
            {
                moderator.ChatMessage($"Нет данных об игроке {playerName}");
                return;
            }

            var moderationData = playerModerationData[targetPlayer.userID];
            moderationData.IsMuted = false;

            if (muteEndTime.ContainsKey(targetPlayer.userID))
            {
                muteEndTime.Remove(targetPlayer.userID);
            }

            targetPlayer.ChatMessage("<color=green>Заглушение снято</color>");
            moderator.ChatMessage($"<color=green>Заглушение с игрока {playerName} снято</color>");

            SaveData();
        }

        private void KickPlayerCommand(BasePlayer moderator, string playerName, string reason)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerModerationData.ContainsKey(targetPlayer.userID))
            {
                InitializePlayerModeration(targetPlayer);
            }

            var moderationData = playerModerationData[targetPlayer.userID];
            moderationData.KickCount++;
            moderationData.LastKickTime = Time.time;

            var action = new ModerationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = "kick",
                Reason = reason,
                ModeratorId = moderator.userID,
                ModeratorName = moderator.displayName,
                Timestamp = Time.time,
                Severity = "medium"
            };

            if (!moderationHistory.ContainsKey(targetPlayer.userID))
            {
                moderationHistory[targetPlayer.userID] = new List<ModerationAction>();
            }

            moderationHistory[targetPlayer.userID].Add(action);

            targetPlayer.Kick($"Вы кикнуты: {reason}");
            moderator.ChatMessage($"<color=green>Игрок {playerName} кикнут</color>");

            SaveData();
        }

        private void BanPlayerCommand(BasePlayer moderator, string playerName, string reason)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerModerationData.ContainsKey(targetPlayer.userID))
            {
                InitializePlayerModeration(targetPlayer);
            }

            var moderationData = playerModerationData[targetPlayer.userID];
            moderationData.IsBanned = true;
            moderationData.BanCount++;
            moderationData.LastBanTime = Time.time;

            var action = new ModerationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = "ban",
                Reason = reason,
                ModeratorId = moderator.userID,
                ModeratorName = moderator.displayName,
                Timestamp = Time.time,
                Severity = "high"
            };

            if (!moderationHistory.ContainsKey(targetPlayer.userID))
            {
                moderationHistory[targetPlayer.userID] = new List<ModerationAction>();
            }

            moderationHistory[targetPlayer.userID].Add(action);

            targetPlayer.Kick($"Вы забанены: {reason}");
            moderator.ChatMessage($"<color=green>Игрок {playerName} забанен</color>");

            SaveData();
        }

        private void ShowPlayerModerationInfo(BasePlayer moderator, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerModerationData.ContainsKey(targetPlayer.userID))
            {
                moderator.ChatMessage($"Нет данных об игроке {playerName}");
                return;
            }

            var moderationData = playerModerationData[targetPlayer.userID];
            var violations = moderationHistory.ContainsKey(targetPlayer.userID) ? moderationHistory[targetPlayer.userID].Count : 0;

            moderator.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О {playerName.ToUpper()} ===</color>");
            moderator.ChatMessage($"Предупреждений: {moderationData.WarningCount}");
            moderator.ChatMessage($"Киков: {moderationData.KickCount}");
            moderator.ChatMessage($"Банов: {moderationData.BanCount}");
            moderator.ChatMessage($"Заглушений: {moderationData.MuteCount}");
            moderator.ChatMessage($"Заглушен: {(moderationData.IsMuted ? "Да" : "Нет")}");
            moderator.ChatMessage($"Забанен: {(moderationData.IsBanned ? "Да" : "Нет")}");
            moderator.ChatMessage($"Всего нарушений: {violations}");
        }

        private void ShowPlayerModerationHistory(BasePlayer moderator, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                moderator.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!moderationHistory.ContainsKey(targetPlayer.userID))
            {
                moderator.ChatMessage($"У игрока {playerName} нет нарушений");
                return;
            }

            var violations = moderationHistory[targetPlayer.userID].OrderByDescending(v => v.Timestamp).Take(10);

            moderator.ChatMessage($"<color=yellow>=== ИСТОРИЯ {playerName.ToUpper()} ===</color>");
            foreach (var violation in violations)
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - violation.Timestamp).TotalMinutes;
                moderator.ChatMessage($"{violation.Type}: {violation.Reason} ({timeAgo:F1} мин назад)");
            }
        }

        private void ReportPlayer(BasePlayer reporter, string playerName, string reportType, string description)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                reporter.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!config.ReportSettings.ReportTypes.Contains(reportType))
            {
                reporter.ChatMessage($"Неверный тип жалобы: {reportType}");
                return;
            }

            var report = new PlayerReport
            {
                Id = Guid.NewGuid().ToString(),
                ReporterId = reporter.userID,
                ReporterName = reporter.displayName,
                ReportedPlayerId = targetPlayer.userID,
                ReportedPlayerName = targetPlayer.displayName,
                Type = reportType,
                Description = description,
                Timestamp = Time.time,
                Status = "pending"
            };

            if (!playerReports.ContainsKey(targetPlayer.userID))
            {
                playerReports[targetPlayer.userID] = new List<PlayerReport>();
            }

            playerReports[targetPlayer.userID].Add(report);

            reporter.ChatMessage($"<color=green>Жалоба на {playerName} отправлена</color>");
            
            // Уведомляем модераторов
            foreach (var moderator in BasePlayer.activePlayerList.Where(p => p.IsAdmin || permission.UserHasPermission(p.UserIDString, "moderator")))
            {
                moderator.ChatMessage($"<color=yellow>НОВАЯ ЖАЛОБА: {reporter.displayName} на {playerName} ({reportType})</color>");
            }

            SaveData();
        }
        #endregion
    }
}
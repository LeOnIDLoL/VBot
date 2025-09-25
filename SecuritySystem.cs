using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Security System", "BULBARUST", "1.0.0")]
    [Description("Система безопасности для сервера BULBARUST")]
    public class SecuritySystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableSecuritySystem { get; set; } = true;
            public bool EnableAntiCheat { get; set; } = true;
            public bool EnableAntiSpam { get; set; } = true;
            public bool EnableAntiGrief { get; set; } = true;
            public bool EnableAntiRaid { get; set; } = true;
            public bool EnablePlayerProtection { get; set; } = true;
            public bool EnableLogging { get; set; } = true;
            public bool EnableAlerts { get; set; } = true;
            public SecuritySettings SecuritySettings { get; set; } = new SecuritySettings();
            public AntiCheatSettings AntiCheatSettings { get; set; } = new AntiCheatSettings();
            public AntiSpamSettings AntiSpamSettings { get; set; } = new AntiSpamSettings();
            public AntiGriefSettings AntiGriefSettings { get; set; } = new AntiGriefSettings();
            public AntiRaidSettings AntiRaidSettings { get; set; } = new AntiRaidSettings();
            public PlayerProtectionSettings PlayerProtectionSettings { get; set; } = new PlayerProtectionSettings();
        }

        private class SecuritySettings
        {
            public bool EnableAutoKick { get; set; } = true;
            public bool EnableAutoBan { get; set; } = true;
            public bool EnableTemporaryBan { get; set; } = true;
            public int MaxViolations { get; set; } = 5;
            public float BanDuration { get; set; } = 3600f; // 1 час
            public List<string> ProtectedCommands { get; set; } = new List<string>();
            public List<string> AdminCommands { get; set; } = new List<string>();
        }

        private class AntiCheatSettings
        {
            public bool EnableSpeedCheck { get; set; } = true;
            public bool EnableFlyCheck { get; set; } = true;
            public bool EnableNoclipCheck { get; set; } = true;
            public bool EnableAimbotCheck { get; set; } = true;
            public bool EnableWallhackCheck { get; set; } = true;
            public float MaxSpeed { get; set; } = 10f;
            public float MaxFlyHeight { get; set; } = 50f;
            public int MaxViolations { get; set; } = 3;
            public float CheckInterval { get; set; } = 1f;
        }

        private class AntiSpamSettings
        {
            public bool EnableChatSpamProtection { get; set; } = true;
            public bool EnableCommandSpamProtection { get; set; } = true;
            public bool EnableBuildSpamProtection { get; set; } = true;
            public int MaxMessagesPerMinute { get; set; } = 10;
            public int MaxCommandsPerMinute { get; set; } = 5;
            public int MaxBuildsPerMinute { get; set; } = 20;
            public float SpamCooldown { get; set; } = 30f;
        }

        private class AntiGriefSettings
        {
            public bool EnableFoundationProtection { get; set; } = true;
            public bool EnableWallProtection { get; set; } = true;
            public bool EnableDoorProtection { get; set; } = true;
            public bool EnableChestProtection { get; set; } = true;
            public float ProtectionRadius { get; set; } = 10f;
            public int MaxDamagePerMinute { get; set; } = 100;
            public List<string> ProtectedBlocks { get; set; } = new List<string>();
        }

        private class AntiRaidSettings
        {
            public bool EnableRaidProtection { get; set; } = true;
            public bool EnableOfflineProtection { get; set; } = true;
            public bool EnableNewPlayerProtection { get; set; } = true;
            public float ProtectionDuration { get; set; } = 3600f; // 1 час
            public int MinPlayTime { get; set; } = 1800; // 30 минут
            public List<string> ProtectedItems { get; set; } = new List<string>();
        }

        private class PlayerProtectionSettings
        {
            public bool EnableSpawnProtection { get; set; } = true;
            public bool EnableNewPlayerProtection { get; set; } = true;
            public bool EnableAdminProtection { get; set; } = true;
            public float SpawnProtectionDuration { get; set; } = 300f; // 5 минут
            public float NewPlayerProtectionDuration { get; set; } = 1800f; // 30 минут
            public List<string> ProtectedPlayers { get; set; } = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Настройки безопасности
            config.SecuritySettings.ProtectedCommands.AddRange(new List<string>
            {
                "home", "sethome", "delhome", "homes", "tpa", "tpaccept", "tpdeny",
                "warp", "warps", "kit", "kits", "shop", "buy", "sell", "balance"
            });

            config.SecuritySettings.AdminCommands.AddRange(new List<string>
            {
                "ban", "unban", "kick", "mute", "unmute", "teleport", "tp", "god", "noclip"
            });

            // Настройки античита
            config.AntiCheatSettings.ProtectedBlocks.AddRange(new List<string>
            {
                "foundation", "wall", "door", "chest", "furnace", "campfire"
            });

            // Настройки антирейда
            config.AntiRaidSettings.ProtectedItems.AddRange(new List<string>
            {
                "explosive.timed", "explosive.satchel", "rocket.launcher", "ammo.rocket.basic"
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
        private Dictionary<ulong, PlayerSecurityData> playerSecurityData = new Dictionary<ulong, PlayerSecurityData>();
        private Dictionary<ulong, List<SecurityViolation>> securityViolations = new Dictionary<ulong, List<SecurityViolation>>();
        private Dictionary<ulong, List<ChatMessage>> chatHistory = new Dictionary<ulong, List<ChatMessage>>();
        private Dictionary<ulong, List<CommandUsage>> commandHistory = new Dictionary<ulong, List<CommandUsage>>();
        private Dictionary<ulong, List<BuildAction>> buildHistory = new Dictionary<ulong, List<BuildAction>>();
        private Dictionary<ulong, float> lastActivity = new Dictionary<ulong, float>();

        private class PlayerSecurityData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public bool IsProtected { get; set; } = false;
            public float ProtectionStartTime { get; set; }
            public float ProtectionDuration { get; set; }
            public int ViolationCount { get; set; } = 0;
            public bool IsBanned { get; set; } = false;
            public float BanEndTime { get; set; }
            public List<string> ViolationTypes { get; set; } = new List<string>();
            public Dictionary<string, object> SecurityFlags { get; set; } = new Dictionary<string, object>();
        }

        private class SecurityViolation
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public float Timestamp { get; set; }
            public string Severity { get; set; } = "medium";
            public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        }

        private class ChatMessage
        {
            public string Message { get; set; }
            public float Timestamp { get; set; }
        }

        private class CommandUsage
        {
            public string Command { get; set; }
            public string[] Args { get; set; }
            public float Timestamp { get; set; }
        }

        private class BuildAction
        {
            public string Action { get; set; }
            public string BlockType { get; set; }
            public Vector3 Position { get; set; }
            public float Timestamp { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система безопасности BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableSecuritySystem)
            {
                timer.Every(config.AntiCheatSettings.CheckInterval, CheckAntiCheat);
                timer.Every(60f, ProcessSecurityViolations);
                timer.Every(300f, CleanupOldData);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableSecuritySystem)
            {
                InitializePlayerSecurity(player);
                CheckPlayerProtection(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerSecurityData(player.userID);
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableSecuritySystem || !config.AntiGriefSettings.EnableBuildSpamProtection) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            RecordBuildAction(player, "build", go.name, go.transform.position);
            CheckBuildSpam(player);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableSecuritySystem) return;

            var player = info.InitiatorPlayer;
            if (player == null) return;

            CheckAntiGrief(player, entity);
        }

        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (!config.EnableSecuritySystem) return null;

            var target = info.HitEntity as BasePlayer;
            if (target == null) return null;

            if (IsPlayerProtected(target))
            {
                attacker.ChatMessage($"<color=red>Игрок {target.displayName} защищен от атак</color>");
                return false;
            }

            return null;
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerSecurityData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerSecurityData>>("player_security_data") ?? new Dictionary<ulong, PlayerSecurityData>();
            securityViolations = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<SecurityViolation>>>("security_violations") ?? new Dictionary<ulong, List<SecurityViolation>>();
            chatHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ChatMessage>>>("chat_history") ?? new Dictionary<ulong, List<ChatMessage>>();
            commandHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<CommandUsage>>>("command_history") ?? new Dictionary<ulong, List<CommandUsage>>();
            buildHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<BuildAction>>>("build_history") ?? new Dictionary<ulong, List<BuildAction>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_security_data", playerSecurityData);
            Interface.Oxide.DataFileSystem.WriteObject("security_violations", securityViolations);
            Interface.Oxide.DataFileSystem.WriteObject("chat_history", chatHistory);
            Interface.Oxide.DataFileSystem.WriteObject("command_history", commandHistory);
            Interface.Oxide.DataFileSystem.WriteObject("build_history", buildHistory);
        }

        private void SavePlayerSecurityData(ulong playerId)
        {
            if (playerSecurityData.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"security_data_{playerId}", playerSecurityData[playerId]);
            }
        }

        private void InitializePlayerSecurity(BasePlayer player)
        {
            if (!playerSecurityData.ContainsKey(player.userID))
            {
                var securityData = new PlayerSecurityData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    IsProtected = false,
                    ViolationCount = 0,
                    IsBanned = false
                };

                playerSecurityData[player.userID] = securityData;
            }

            if (!chatHistory.ContainsKey(player.userID))
            {
                chatHistory[player.userID] = new List<ChatMessage>();
            }

            if (!commandHistory.ContainsKey(player.userID))
            {
                commandHistory[player.userID] = new List<CommandUsage>();
            }

            if (!buildHistory.ContainsKey(player.userID))
            {
                buildHistory[player.userID] = new List<BuildAction>();
            }

            if (!securityViolations.ContainsKey(player.userID))
            {
                securityViolations[player.userID] = new List<SecurityViolation>();
            }
        }

        private void CheckPlayerProtection(BasePlayer player)
        {
            var securityData = playerSecurityData[player.userID];
            var playTime = Time.time - player.connectionTime;

            // Защита новичков
            if (config.PlayerProtectionSettings.EnableNewPlayerProtection && playTime < config.PlayerProtectionSettings.NewPlayerProtectionDuration)
            {
                securityData.IsProtected = true;
                securityData.ProtectionStartTime = Time.time;
                securityData.ProtectionDuration = config.PlayerProtectionSettings.NewPlayerProtectionDuration;
                player.ChatMessage("<color=green>Вы защищены от атак в течение 30 минут</color>");
            }

            // Защита админов
            if (config.PlayerProtectionSettings.EnableAdminProtection && player.IsAdmin)
            {
                securityData.IsProtected = true;
                securityData.ProtectionStartTime = Time.time;
                securityData.ProtectionDuration = float.MaxValue;
            }
        }

        private bool IsPlayerProtected(BasePlayer player)
        {
            if (!playerSecurityData.ContainsKey(player.userID)) return false;

            var securityData = playerSecurityData[player.userID];
            if (!securityData.IsProtected) return false;

            var protectionTime = Time.time - securityData.ProtectionStartTime;
            if (protectionTime >= securityData.ProtectionDuration)
            {
                securityData.IsProtected = false;
                return false;
            }

            return true;
        }

        private void CheckAntiCheat()
        {
            if (!config.EnableAntiCheat) return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || player.IsAdmin) continue;

                CheckSpeed(player);
                CheckFly(player);
                CheckNoclip(player);
            }
        }

        private void CheckSpeed(BasePlayer player)
        {
            if (!config.AntiCheatSettings.EnableSpeedCheck) return;

            var velocity = player.estimatedVelocity.magnitude;
            if (velocity > config.AntiCheatSettings.MaxSpeed)
            {
                RecordViolation(player, "speed", $"Скорость: {velocity:F2} (макс: {config.AntiCheatSettings.MaxSpeed})", "high");
            }
        }

        private void CheckFly(BasePlayer player)
        {
            if (!config.AntiCheatSettings.EnableFlyCheck) return;

            var height = player.transform.position.y;
            if (height > config.AntiCheatSettings.MaxFlyHeight)
            {
                RecordViolation(player, "fly", $"Высота: {height:F2} (макс: {config.AntiCheatSettings.MaxFlyHeight})", "high");
            }
        }

        private void CheckNoclip(BasePlayer player)
        {
            if (!config.AntiCheatSettings.EnableNoclipCheck) return;

            // Проверка на прохождение через стены
            var position = player.transform.position;
            var hits = Physics.RaycastAll(position, Vector3.down, 10f);
            
            if (hits.Length == 0 && player.transform.position.y > 0)
            {
                RecordViolation(player, "noclip", "Обнаружено прохождение через стены", "high");
            }
        }

        private void CheckBuildSpam(BasePlayer player)
        {
            if (!config.AntiSpamSettings.EnableBuildSpamProtection) return;

            var currentTime = Time.time;
            var recentBuilds = buildHistory[player.userID].Where(b => currentTime - b.Timestamp < 60f).Count();

            if (recentBuilds > config.AntiSpamSettings.MaxBuildsPerMinute)
            {
                RecordViolation(player, "build_spam", $"Спам строительства: {recentBuilds} за минуту", "medium");
                player.ChatMessage("<color=red>Слишком много строительства! Остановитесь на минуту</color>");
            }
        }

        private void CheckAntiGrief(BasePlayer player, BaseCombatEntity entity)
        {
            if (!config.EnableAntiGrief) return;

            var damage = entity.lastDamage;
            if (damage > config.AntiGriefSettings.MaxDamagePerMinute)
            {
                RecordViolation(player, "grief", $"Чрезмерный урон: {damage}", "high");
            }
        }

        private void RecordViolation(BasePlayer player, string type, string description, string severity)
        {
            if (!securityViolations.ContainsKey(player.userID))
            {
                securityViolations[player.userID] = new List<SecurityViolation>();
            }

            var violation = new SecurityViolation
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Description = description,
                Timestamp = Time.time,
                Severity = severity
            };

            securityViolations[player.userID].Add(violation);

            if (!playerSecurityData.ContainsKey(player.userID))
            {
                InitializePlayerSecurity(player);
            }

            var securityData = playerSecurityData[player.userID];
            securityData.ViolationCount++;
            securityData.ViolationTypes.Add(type);

            // Отправляем уведомление админам
            if (config.EnableAlerts)
            {
                SendAdminAlert($"Нарушение безопасности: {player.displayName} - {type}: {description}");
            }

            // Проверяем на бан
            if (securityData.ViolationCount >= config.SecuritySettings.MaxViolations)
            {
                BanPlayer(player, "Превышено количество нарушений безопасности");
            }
        }

        private void RecordBuildAction(BasePlayer player, string action, string blockType, Vector3 position)
        {
            if (!buildHistory.ContainsKey(player.userID))
            {
                buildHistory[player.userID] = new List<BuildAction>();
            }

            var buildAction = new BuildAction
            {
                Action = action,
                BlockType = blockType,
                Position = position,
                Timestamp = Time.time
            };

            buildHistory[player.userID].Add(buildAction);

            // Ограничиваем историю
            if (buildHistory[player.userID].Count > 1000)
            {
                buildHistory[player.userID] = buildHistory[player.userID].OrderByDescending(b => b.Timestamp).Take(500).ToList();
            }
        }

        private void ProcessSecurityViolations()
        {
            var currentTime = Time.time;
            var playersToBan = new List<ulong>();

            foreach (var playerId in securityViolations.Keys.ToList())
            {
                var violations = securityViolations[playerId];
                var recentViolations = violations.Where(v => currentTime - v.Timestamp < 300f).ToList();

                if (recentViolations.Count >= config.SecuritySettings.MaxViolations)
                {
                    playersToBan.Add(playerId);
                }
            }

            foreach (var playerId in playersToBan)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player != null)
                {
                    BanPlayer(player, "Автоматический бан за нарушения безопасности");
                }
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

            foreach (var playerId in commandHistory.Keys.ToList())
            {
                commandHistory[playerId] = commandHistory[playerId].Where(c => c.Timestamp > cutoffTime).ToList();
            }

            foreach (var playerId in buildHistory.Keys.ToList())
            {
                buildHistory[playerId] = buildHistory[playerId].Where(b => b.Timestamp > cutoffTime).ToList();
            }

            foreach (var playerId in securityViolations.Keys.ToList())
            {
                securityViolations[playerId] = securityViolations[playerId].Where(v => v.Timestamp > cutoffTime).ToList();
            }
        }

        private void BanPlayer(BasePlayer player, string reason)
        {
            if (!config.SecuritySettings.EnableAutoBan) return;

            var securityData = playerSecurityData[player.userID];
            securityData.IsBanned = true;
            securityData.BanEndTime = Time.time + config.SecuritySettings.BanDuration;

            player.Kick($"Вы забанены: {reason}");
            
            if (config.EnableLogging)
            {
                Puts($"Игрок {player.displayName} ({player.userID}) забанен: {reason}");
            }

            SaveData();
        }

        private void SendAdminAlert(string message)
        {
            foreach (var admin in BasePlayer.activePlayerList.Where(p => p.IsAdmin))
            {
                admin.ChatMessage($"<color=red>[БЕЗОПАСНОСТЬ] {message}</color>");
            }
        }
        #endregion

        #region Commands
        [ChatCommand("security")]
        private void SecurityCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав на использование команд безопасности");
                return;
            }

            if (args.Length == 0)
            {
                ShowSecurityHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "status":
                    ShowSecurityStatus(player);
                    break;
                case "player":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /security player <имя>");
                        return;
                    }
                    ShowPlayerSecurityInfo(player, args[1]);
                    break;
                case "violations":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /security violations <имя>");
                        return;
                    }
                    ShowPlayerViolations(player, args[1]);
                    break;
                case "protect":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /security protect <имя>");
                        return;
                    }
                    ProtectPlayer(player, args[1]);
                    break;
                case "unprotect":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /security unprotect <имя>");
                        return;
                    }
                    UnprotectPlayer(player, args[1]);
                    break;
                case "ban":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /security ban <имя> [причина]");
                        return;
                    }
                    SecurityBanPlayer(player, args[1], args.Length > 2 ? string.Join(" ", args.Skip(2)) : "Нарушение безопасности");
                    break;
                case "unban":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /security unban <имя>");
                        return;
                    }
                    UnbanPlayer(player, args[1]);
                    break;
            }
        }

        [ChatCommand("protect")]
        private void ProtectCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав на использование этой команды");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /protect <имя>");
                return;
            }

            ProtectPlayer(player, args[0]);
        }
        #endregion

        #region Command Methods
        private void ShowSecurityHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ БЕЗОПАСНОСТИ ===</color>");
            player.ChatMessage("/security status - статус системы");
            player.ChatMessage("/security player <имя> - информация об игроке");
            player.ChatMessage("/security violations <имя> - нарушения игрока");
            player.ChatMessage("/security protect <имя> - защитить игрока");
            player.ChatMessage("/security unprotect <имя> - снять защиту");
            player.ChatMessage("/security ban <имя> [причина] - забанить");
            player.ChatMessage("/security unban <имя> - разбанить");
            player.ChatMessage("/protect <имя> - защитить игрока");
        }

        private void ShowSecurityStatus(BasePlayer player)
        {
            var totalPlayers = BasePlayer.activePlayerList.Count;
            var protectedPlayers = playerSecurityData.Values.Count(p => p.IsProtected);
            var bannedPlayers = playerSecurityData.Values.Count(p => p.IsBanned);
            var totalViolations = securityViolations.Values.Sum(v => v.Count);

            player.ChatMessage("<color=yellow>=== СТАТУС БЕЗОПАСНОСТИ ===</color>");
            player.ChatMessage($"Всего игроков: {totalPlayers}");
            player.ChatMessage($"Защищенных: {protectedPlayers}");
            player.ChatMessage($"Забаненных: {bannedPlayers}");
            player.ChatMessage($"Всего нарушений: {totalViolations}");
            player.ChatMessage($"Античит: {(config.EnableAntiCheat ? "Включен" : "Выключен")}");
            player.ChatMessage($"Антиспам: {(config.EnableAntiSpam ? "Включен" : "Выключен")}");
            player.ChatMessage($"Антигрифинг: {(config.EnableAntiGrief ? "Включен" : "Выключен")}");
        }

        private void ShowPlayerSecurityInfo(BasePlayer player, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerSecurityData.ContainsKey(targetPlayer.userID))
            {
                player.ChatMessage($"Нет данных об игроке {playerName}");
                return;
            }

            var securityData = playerSecurityData[targetPlayer.userID];
            var violations = securityViolations.ContainsKey(targetPlayer.userID) ? securityViolations[targetPlayer.userID].Count : 0;

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О {playerName.ToUpper()} ===</color>");
            player.ChatMessage($"Защищен: {(securityData.IsProtected ? "Да" : "Нет")}");
            player.ChatMessage($"Забанен: {(securityData.IsBanned ? "Да" : "Нет")}");
            player.ChatMessage($"Нарушений: {securityData.ViolationCount}");
            player.ChatMessage($"Всего нарушений: {violations}");
            player.ChatMessage($"Типы нарушений: {string.Join(", ", securityData.ViolationTypes.Distinct())}");
        }

        private void ShowPlayerViolations(BasePlayer player, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!securityViolations.ContainsKey(targetPlayer.userID))
            {
                player.ChatMessage($"У игрока {playerName} нет нарушений");
                return;
            }

            var violations = securityViolations[targetPlayer.userID].OrderByDescending(v => v.Timestamp).Take(10);

            player.ChatMessage($"<color=yellow>=== НАРУШЕНИЯ {playerName.ToUpper()} ===</color>");
            foreach (var violation in violations)
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - violation.Timestamp).TotalMinutes;
                player.ChatMessage($"{violation.Type}: {violation.Description} ({timeAgo:F1} мин назад)");
            }
        }

        private void ProtectPlayer(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerSecurityData.ContainsKey(targetPlayer.userID))
            {
                InitializePlayerSecurity(targetPlayer);
            }

            var securityData = playerSecurityData[targetPlayer.userID];
            securityData.IsProtected = true;
            securityData.ProtectionStartTime = Time.time;
            securityData.ProtectionDuration = float.MaxValue;

            admin.ChatMessage($"<color=green>Игрок {playerName} защищен</color>");
            targetPlayer.ChatMessage("<color=green>Вы защищены от атак администратором</color>");
            SaveData();
        }

        private void UnprotectPlayer(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerSecurityData.ContainsKey(targetPlayer.userID))
            {
                admin.ChatMessage($"Нет данных об игроке {playerName}");
                return;
            }

            var securityData = playerSecurityData[targetPlayer.userID];
            securityData.IsProtected = false;

            admin.ChatMessage($"<color=green>Защита с игрока {playerName} снята</color>");
            targetPlayer.ChatMessage("<color=yellow>Защита от атак снята</color>");
            SaveData();
        }

        private void SecurityBanPlayer(BasePlayer admin, string playerName, string reason)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerSecurityData.ContainsKey(targetPlayer.userID))
            {
                InitializePlayerSecurity(targetPlayer);
            }

            var securityData = playerSecurityData[targetPlayer.userID];
            securityData.IsBanned = true;
            securityData.BanEndTime = Time.time + config.SecuritySettings.BanDuration;

            targetPlayer.Kick($"Вы забанены: {reason}");
            admin.ChatMessage($"<color=green>Игрок {playerName} забанен: {reason}</color>");
            SaveData();
        }

        private void UnbanPlayer(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerSecurityData.ContainsKey(targetPlayer.userID))
            {
                admin.ChatMessage($"Нет данных об игроке {playerName}");
                return;
            }

            var securityData = playerSecurityData[targetPlayer.userID];
            securityData.IsBanned = false;
            securityData.BanEndTime = 0;

            admin.ChatMessage($"<color=green>Игрок {playerName} разбанен</color>");
            SaveData();
        }
        #endregion
    }
}
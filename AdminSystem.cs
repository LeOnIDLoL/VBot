using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin System", "BULBARUST", "1.0.0")]
    [Description("Система администрации для сервера BULBARUST")]
    public class AdminSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableAdminSystem { get; set; } = true;
            public bool EnableAdminLogging { get; set; } = true;
            public bool EnableAdminNotifications { get; set; } = true;
            public bool EnableAdminCommands { get; set; } = true;
            public bool EnablePlayerReports { get; set; } = true;
            public bool EnableAdminChat { get; set; } = true;
            public float ReportCooldown { get; set; } = 300f; // 5 минут
            public int MaxReportsPerPlayer { get; set; } = 5;
            public List<AdminRank> AdminRanks { get; set; } = new List<AdminRank>();
            public List<AdminCommand> AdminCommands { get; set; } = new List<AdminCommand>();
        }

        private class AdminRank
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public int Level { get; set; }
            public string Color { get; set; } = "red";
            public List<string> Permissions { get; set; } = new List<string>();
            public List<string> Commands { get; set; } = new List<string>();
        }

        private class AdminCommand
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Permission { get; set; }
            public int RequiredLevel { get; set; } = 1;
            public bool IsDangerous { get; set; } = false;
            public string Usage { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем ранги администрации
            config.AdminRanks.AddRange(new List<AdminRank>
            {
                new AdminRank
                {
                    Name = "helper",
                    DisplayName = "Помощник",
                    Level = 1,
                    Color = "green",
                    Permissions = new List<string> { "admin.helper", "admin.kick", "admin.ban" },
                    Commands = new List<string> { "kick", "ban", "mute", "unmute", "teleport" }
                },
                new AdminRank
                {
                    Name = "moderator",
                    DisplayName = "Модератор",
                    Level = 2,
                    Color = "yellow",
                    Permissions = new List<string> { "admin.moderator", "admin.kick", "admin.ban", "admin.teleport", "admin.give" },
                    Commands = new List<string> { "kick", "ban", "mute", "unmute", "teleport", "give", "spawn" }
                },
                new AdminRank
                {
                    Name = "admin",
                    DisplayName = "Администратор",
                    Level = 3,
                    Color = "red",
                    Permissions = new List<string> { "admin.admin", "admin.all" },
                    Commands = new List<string> { "kick", "ban", "mute", "unmute", "teleport", "give", "spawn", "god", "noclip", "fly" }
                },
                new AdminRank
                {
                    Name = "owner",
                    DisplayName = "Владелец",
                    Level = 4,
                    Color = "purple",
                    Permissions = new List<string> { "admin.owner", "admin.all" },
                    Commands = new List<string> { "kick", "ban", "mute", "unmute", "teleport", "give", "spawn", "god", "noclip", "fly", "shutdown", "restart" }
                }
            });

            // Добавляем команды администрации
            config.AdminCommands.AddRange(new List<AdminCommand>
            {
                new AdminCommand
                {
                    Name = "kick",
                    Description = "Кикнуть игрока",
                    Permission = "admin.kick",
                    RequiredLevel = 1,
                    Usage = "/kick <игрок> [причина]"
                },
                new AdminCommand
                {
                    Name = "ban",
                    Description = "Забанить игрока",
                    Permission = "admin.ban",
                    RequiredLevel = 1,
                    IsDangerous = true,
                    Usage = "/ban <игрок> [время] [причина]"
                },
                new AdminCommand
                {
                    Name = "mute",
                    Description = "Заглушить игрока",
                    Permission = "admin.mute",
                    RequiredLevel = 1,
                    Usage = "/mute <игрок> [время] [причина]"
                },
                new AdminCommand
                {
                    Name = "teleport",
                    Description = "Телепортировать игрока",
                    Permission = "admin.teleport",
                    RequiredLevel = 1,
                    Usage = "/teleport <игрок> [цель]"
                },
                new AdminCommand
                {
                    Name = "give",
                    Description = "Выдать предмет",
                    Permission = "admin.give",
                    RequiredLevel = 2,
                    Usage = "/give <игрок> <предмет> [количество]"
                },
                new AdminCommand
                {
                    Name = "spawn",
                    Description = "Создать объект",
                    Permission = "admin.spawn",
                    RequiredLevel = 2,
                    Usage = "/spawn <объект> [количество]"
                },
                new AdminCommand
                {
                    Name = "god",
                    Description = "Режим бога",
                    Permission = "admin.god",
                    RequiredLevel = 3,
                    Usage = "/god [игрок]"
                },
                new AdminCommand
                {
                    Name = "noclip",
                    Description = "Режим полета",
                    Permission = "admin.noclip",
                    RequiredLevel = 3,
                    Usage = "/noclip [игрок]"
                }
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
        private Dictionary<ulong, AdminData> adminData = new Dictionary<ulong, AdminData>();
        private Dictionary<ulong, List<PlayerReport>> playerReports = new Dictionary<ulong, List<PlayerReport>>();
        private Dictionary<ulong, float> lastReportTime = new Dictionary<ulong, float>();
        private List<AdminLog> adminLogs = new List<AdminLog>();

        private class AdminData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public string AdminRank { get; set; } = "";
            public int AdminLevel { get; set; } = 0;
            public bool IsAdmin { get; set; } = false;
            public float LastActivity { get; set; }
            public List<string> Permissions { get; set; } = new List<string>();
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }

        private class PlayerReport
        {
            public string Id { get; set; }
            public ulong ReporterId { get; set; }
            public string ReporterName { get; set; }
            public ulong ReportedId { get; set; }
            public string ReportedName { get; set; }
            public string Reason { get; set; }
            public float Timestamp { get; set; }
            public bool IsProcessed { get; set; } = false;
            public string AdminResponse { get; set; } = "";
        }

        private class AdminLog
        {
            public string Id { get; set; }
            public ulong AdminId { get; set; }
            public string AdminName { get; set; }
            public string Action { get; set; }
            public string Target { get; set; }
            public string Details { get; set; }
            public float Timestamp { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система администрации BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableAdminSystem)
            {
                timer.Every(300f, ProcessAdminMaintenance);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableAdminSystem)
            {
                InitializePlayerAdminData(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerAdminData(player.userID);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            adminData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, AdminData>>("admin_data") ?? new Dictionary<ulong, AdminData>();
            playerReports = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PlayerReport>>>("player_reports") ?? new Dictionary<ulong, List<PlayerReport>>();
            adminLogs = Interface.Oxide.DataFileSystem.ReadObject<List<AdminLog>>("admin_logs") ?? new List<AdminLog>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("admin_data", adminData);
            Interface.Oxide.DataFileSystem.WriteObject("player_reports", playerReports);
            Interface.Oxide.DataFileSystem.WriteObject("admin_logs", adminLogs);
        }

        private void SavePlayerAdminData(ulong playerId)
        {
            if (adminData.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"admin_{playerId}", adminData[playerId]);
            }
        }

        private void InitializePlayerAdminData(BasePlayer player)
        {
            if (!adminData.ContainsKey(player.userID))
            {
                var adminInfo = new AdminData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    IsAdmin = player.IsAdmin,
                    LastActivity = Time.time
                };

                if (player.IsAdmin)
                {
                    // Определяем ранг администратора
                    var adminRank = GetPlayerAdminRank(player);
                    adminInfo.AdminRank = adminRank?.Name ?? "";
                    adminInfo.AdminLevel = adminRank?.Level ?? 0;
                    adminInfo.Permissions = adminRank?.Permissions ?? new List<string>();
                }

                adminData[player.userID] = adminInfo;
                SaveData();
            }
            else
            {
                // Обновляем данные
                adminData[player.userID].PlayerName = player.displayName;
                adminData[player.userID].IsAdmin = player.IsAdmin;
                adminData[player.userID].LastActivity = Time.time;
            }
        }

        private AdminRank GetPlayerAdminRank(BasePlayer player)
        {
            // Определяем ранг администратора на основе прав
            foreach (var rank in config.AdminRanks.OrderByDescending(r => r.Level))
            {
                if (rank.Permissions.Any(p => permission.UserHasPermission(player.UserIDString, p)))
                {
                    return rank;
                }
            }

            return null;
        }

        private void ProcessAdminMaintenance()
        {
            // Очищаем старые логи (старше 30 дней)
            var cutoffTime = Time.time - (30 * 24 * 3600);
            adminLogs.RemoveAll(log => log.Timestamp < cutoffTime);

            // Очищаем старые жалобы (старше 7 дней)
            foreach (var playerId in playerReports.Keys.ToList())
            {
                var reports = playerReports[playerId];
                reports.RemoveAll(report => report.Timestamp < cutoffTime);
                
                if (reports.Count == 0)
                {
                    playerReports.Remove(playerId);
                }
            }

            SaveData();
        }

        private bool HasAdminPermission(ulong playerId, string permission)
        {
            if (!adminData.ContainsKey(playerId)) return false;
            if (!adminData[playerId].IsAdmin) return false;

            return adminData[playerId].Permissions.Contains(permission) || 
                   permission.UserHasPermission(playerId.ToString(), permission);
        }

        private void LogAdminAction(ulong adminId, string action, string target, string details = "")
        {
            if (!config.EnableAdminLogging) return;

            var admin = adminData.ContainsKey(adminId) ? adminData[adminId] : null;
            var adminName = admin?.PlayerName ?? "Unknown";

            var logEntry = new AdminLog
            {
                Id = Guid.NewGuid().ToString(),
                AdminId = adminId,
                AdminName = adminName,
                Action = action,
                Target = target,
                Details = details,
                Timestamp = Time.time
            };

            adminLogs.Add(logEntry);

            // Ограничиваем количество логов
            if (adminLogs.Count > 10000)
            {
                adminLogs.RemoveRange(0, 1000);
            }

            SaveData();
        }

        private void NotifyAdmins(string message, string color = "yellow")
        {
            if (!config.EnableAdminNotifications) return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin || (adminData.ContainsKey(player.userID) && adminData[player.userID].IsAdmin))
                {
                    player.ChatMessage($"<color={color}>[АДМИН] {message}</color>");
                }
            }
        }

        private void KickPlayer(BasePlayer admin, string targetName, string reason = "")
        {
            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                admin.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            if (target == admin)
            {
                admin.ChatMessage("<color=red>Нельзя кикнуть самого себя</color>");
                return;
            }

            var kickMessage = string.IsNullOrEmpty(reason) ? "Вы были кикнуты администратором" : reason;
            target.Kick(kickMessage);

            admin.ChatMessage($"<color=green>Игрок {target.displayName} кикнут</color>");
            LogAdminAction(admin.userID, "kick", target.displayName, reason);
            NotifyAdmins($"{admin.displayName} кикнул {target.displayName}" + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
        }

        private void BanPlayer(BasePlayer admin, string targetName, float duration = 0f, string reason = "")
        {
            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                admin.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            if (target == admin)
            {
                admin.ChatMessage("<color=red>Нельзя забанить самого себя</color>");
                return;
            }

            // Здесь должна быть логика бана игрока
            var banMessage = string.IsNullOrEmpty(reason) ? "Вы были забанены" : reason;
            if (duration > 0)
            {
                banMessage += $" на {Mathf.CeilToInt(duration / 3600)} часов";
            }

            if (target.IsConnected)
            {
                target.Kick(banMessage);
            }

            admin.ChatMessage($"<color=green>Игрок {target.displayName} забанен</color>");
            LogAdminAction(admin.userID, "ban", target.displayName, $"{reason} ({(duration > 0 ? $"{Mathf.CeilToInt(duration / 3600)}ч" : "навсегда")})");
            NotifyAdmins($"{admin.displayName} забанил {target.displayName}" + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
        }

        private void MutePlayer(BasePlayer admin, string targetName, float duration = 0f, string reason = "")
        {
            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                admin.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            // Здесь должна быть логика мута игрока
            admin.ChatMessage($"<color=green>Игрок {target.displayName} заглушен</color>");
            LogAdminAction(admin.userID, "mute", target.displayName, $"{reason} ({(duration > 0 ? $"{Mathf.CeilToInt(duration / 60)}м" : "навсегда")})");
            NotifyAdmins($"{admin.displayName} заглушил {target.displayName}" + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
        }

        private void TeleportPlayer(BasePlayer admin, string targetName, string destination = "")
        {
            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                admin.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            if (string.IsNullOrEmpty(destination))
            {
                // Телепортируем к администратору
                target.Teleport(admin.transform.position);
                admin.ChatMessage($"<color=green>Игрок {target.displayName} телепортирован к вам</color>");
            }
            else
            {
                var destPlayer = BasePlayer.Find(destination);
                if (destPlayer != null)
                {
                    target.Teleport(destPlayer.transform.position);
                    admin.ChatMessage($"<color=green>Игрок {target.displayName} телепортирован к {destPlayer.displayName}</color>");
                }
                else
                {
                    admin.ChatMessage($"<color=red>Целевой игрок '{destination}' не найден</color>");
                    return;
                }
            }

            LogAdminAction(admin.userID, "teleport", target.displayName, destination);
        }

        private void GiveItem(BasePlayer admin, string targetName, string itemName, int amount = 1)
        {
            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                admin.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            // Здесь должна быть логика выдачи предмета
            admin.ChatMessage($"<color=green>Предмет {itemName} x{amount} выдан игроку {target.displayName}</color>");
            LogAdminAction(admin.userID, "give", target.displayName, $"{itemName} x{amount}");
        }

        private void CreateReport(ulong reporterId, string reporterName, ulong reportedId, string reportedName, string reason)
        {
            if (!config.EnablePlayerReports) return;

            // Проверяем кулдаун
            if (lastReportTime.ContainsKey(reporterId))
            {
                var timeLeft = config.ReportCooldown - (Time.time - lastReportTime[reporterId]);
                if (timeLeft > 0)
                {
                    var reporter = BasePlayer.FindByID(reporterId);
                    if (reporter != null && reporter.IsConnected)
                    {
                        reporter.ChatMessage($"<color=red>Осталось ждать: {Mathf.CeilToInt(timeLeft / 60)} минут</color>");
                    }
                    return;
                }
            }

            // Проверяем лимит жалоб
            if (playerReports.ContainsKey(reporterId))
            {
                var recentReports = playerReports[reporterId].Where(r => Time.time - r.Timestamp < 86400f).ToList();
                if (recentReports.Count >= config.MaxReportsPerPlayer)
                {
                    var reporter = BasePlayer.FindByID(reporterId);
                    if (reporter != null && reporter.IsConnected)
                    {
                        reporter.ChatMessage($"<color=red>Превышен лимит жалоб ({config.MaxReportsPerPlayer} в день)</color>");
                    }
                    return;
                }
            }

            var report = new PlayerReport
            {
                Id = Guid.NewGuid().ToString(),
                ReporterId = reporterId,
                ReporterName = reporterName,
                ReportedId = reportedId,
                ReportedName = reportedName,
                Reason = reason,
                Timestamp = Time.time
            };

            if (!playerReports.ContainsKey(reporterId))
            {
                playerReports[reporterId] = new List<PlayerReport>();
            }

            playerReports[reporterId].Add(report);
            lastReportTime[reporterId] = Time.time;

            // Уведомляем администраторов
            NotifyAdmins($"Новая жалоба: {reporterName} -> {reportedName} ({reason})", "orange");

            SaveData();
        }
        #endregion

        #region Commands
        [ChatCommand("admin")]
        private void AdminCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableAdminSystem)
            {
                player.ChatMessage("Система администрации отключена");
                return;
            }

            if (!player.IsAdmin && !HasAdminPermission(player.userID, "admin.helper"))
            {
                player.ChatMessage("У вас нет прав администратора");
                return;
            }

            if (args.Length == 0)
            {
                ShowAdminHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "kick":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /admin kick <игрок> [причина]");
                        return;
                    }
                    KickPlayer(player, args[1], args.Length > 2 ? string.Join(" ", args.Skip(2)) : "");
                    break;
                case "ban":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /admin ban <игрок> [время] [причина]");
                        return;
                    }
                    var duration = args.Length > 2 && float.TryParse(args[2], out float d) ? d * 3600f : 0f;
                    var reason = args.Length > 3 ? string.Join(" ", args.Skip(3)) : (args.Length > 2 ? args[2] : "");
                    BanPlayer(player, args[1], duration, reason);
                    break;
                case "mute":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /admin mute <игрок> [время] [причина]");
                        return;
                    }
                    var muteDuration = args.Length > 2 && float.TryParse(args[2], out float md) ? md * 60f : 0f;
                    var muteReason = args.Length > 3 ? string.Join(" ", args.Skip(3)) : (args.Length > 2 ? args[2] : "");
                    MutePlayer(player, args[1], muteDuration, muteReason);
                    break;
                case "teleport":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /admin teleport <игрок> [цель]");
                        return;
                    }
                    TeleportPlayer(player, args[1], args.Length > 2 ? args[2] : "");
                    break;
                case "give":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /admin give <игрок> <предмет> [количество]");
                        return;
                    }
                    var amount = args.Length > 3 && int.TryParse(args[3], out int a) ? a : 1;
                    GiveItem(player, args[1], args[2], amount);
                    break;
                case "reports":
                    ShowPlayerReports(player);
                    break;
                case "logs":
                    ShowAdminLogs(player, args.Length > 1 ? args[1] : "10");
                    break;
                case "stats":
                    ShowAdminStats(player);
                    break;
            }
        }

        [ChatCommand("report")]
        private void ReportCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnablePlayerReports)
            {
                player.ChatMessage("Система жалоб отключена");
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /report <игрок> <причина>");
                return;
            }

            var targetName = args[0];
            var reason = string.Join(" ", args.Skip(1));

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            if (target == player)
            {
                player.ChatMessage("<color=red>Нельзя пожаловаться на себя</color>");
                return;
            }

            CreateReport(player.userID, player.displayName, target.userID, target.displayName, reason);
            player.ChatMessage($"<color=green>Жалоба на {target.displayName} отправлена</color>");
        }

        [ChatCommand("a")]
        private void AdminChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableAdminChat)
            {
                player.ChatMessage("Админский чат отключен");
                return;
            }

            if (!player.IsAdmin && !HasAdminPermission(player.userID, "admin.helper"))
            {
                player.ChatMessage("У вас нет прав администратора");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /a <сообщение>");
                return;
            }

            var message = string.Join(" ", args);
            NotifyAdmins($"{player.displayName}: {message}", "cyan");
        }
        #endregion

        #region Command Methods
        private void ShowAdminHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== АДМИНСКИЕ КОМАНДЫ ===</color>");
            player.ChatMessage("/admin kick <игрок> [причина] - кикнуть игрока");
            player.ChatMessage("/admin ban <игрок> [время] [причина] - забанить игрока");
            player.ChatMessage("/admin mute <игрок> [время] [причина] - заглушить игрока");
            player.ChatMessage("/admin teleport <игрок> [цель] - телепортировать игрока");
            player.ChatMessage("/admin give <игрок> <предмет> [количество] - выдать предмет");
            player.ChatMessage("/admin reports - показать жалобы");
            player.ChatMessage("/admin logs [количество] - показать логи");
            player.ChatMessage("/admin stats - статистика");
            player.ChatMessage("/report <игрок> <причина> - пожаловаться на игрока");
            player.ChatMessage("/a <сообщение> - админский чат");
        }

        private void ShowPlayerReports(BasePlayer player)
        {
            var allReports = new List<PlayerReport>();
            foreach (var reports in playerReports.Values)
            {
                allReports.AddRange(reports.Where(r => !r.IsProcessed));
            }

            if (allReports.Count == 0)
            {
                player.ChatMessage("Нет необработанных жалоб");
                return;
            }

            player.ChatMessage("<color=yellow>=== ЖАЛОБЫ ИГРОКОВ ===</color>");
            foreach (var report in allReports.OrderByDescending(r => r.Timestamp).Take(10))
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - report.Timestamp).TotalHours;
                player.ChatMessage($"<color=orange>ID: {report.Id.Substring(0, 8)}</color>");
                player.ChatMessage($"  От: {report.ReporterName} -> {report.ReportedName}");
                player.ChatMessage($"  Причина: {report.Reason}");
                player.ChatMessage($"  Время: {timeAgo:F1} часов назад");
            }
        }

        private void ShowAdminLogs(BasePlayer player, string countStr)
        {
            if (!int.TryParse(countStr, out int count) || count <= 0)
            {
                count = 10;
            }

            var recentLogs = adminLogs.OrderByDescending(l => l.Timestamp).Take(count).ToList();

            if (recentLogs.Count == 0)
            {
                player.ChatMessage("Логи пусты");
                return;
            }

            player.ChatMessage($"<color=yellow>=== АДМИНСКИЕ ЛОГИ (последние {count}) ===</color>");
            foreach (var log in recentLogs)
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - log.Timestamp).TotalHours;
                player.ChatMessage($"<color=white>{log.AdminName}: {log.Action} -> {log.Target}</color>");
                if (!string.IsNullOrEmpty(log.Details))
                {
                    player.ChatMessage($"  {log.Details}");
                }
                player.ChatMessage($"  {timeAgo:F1}ч назад");
            }
        }

        private void ShowAdminStats(BasePlayer player)
        {
            var adminInfo = adminData.ContainsKey(player.userID) ? adminData[player.userID] : null;
            if (adminInfo == null)
            {
                player.ChatMessage("Данные администратора не найдены");
                return;
            }

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА АДМИНИСТРАТОРА ===</color>");
            player.ChatMessage($"Ранг: {adminInfo.AdminRank}");
            player.ChatMessage($"Уровень: {adminInfo.AdminLevel}");
            player.ChatMessage($"Последняя активность: {TimeSpan.FromSeconds(Time.time - adminInfo.LastActivity).TotalHours:F1} часов назад");

            var myLogs = adminLogs.Where(l => l.AdminId == player.userID).ToList();
            player.ChatMessage($"Всего действий: {myLogs.Count}");
            player.ChatMessage($"За последние 24 часа: {myLogs.Count(l => Time.time - l.Timestamp < 86400f)}");
        }
        #endregion
    }
}
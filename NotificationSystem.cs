using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Notification System", "BULBARUST", "1.0.0")]
    [Description("Система уведомлений для сервера BULBARUST")]
    public class NotificationSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableNotifications { get; set; } = true;
            public bool EnableGlobalNotifications { get; set; } = true;
            public bool EnablePersonalNotifications { get; set; } = true;
            public bool EnableSystemNotifications { get; set; } = true;
            public bool EnableNotificationSounds { get; set; } = true;
            public float NotificationDuration { get; set; } = 5f;
            public float NotificationFadeTime { get; set; } = 1f;
            public int MaxNotifications { get; set; } = 10;
            public List<NotificationTemplate> Templates { get; set; } = new List<NotificationTemplate>();
            public List<NotificationRule> Rules { get; set; } = new List<NotificationRule>();
        }

        private class NotificationTemplate
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string Color { get; set; } = "white";
            public string Icon { get; set; } = "";
            public float Duration { get; set; } = 5f;
            public bool IsPersistent { get; set; } = false;
            public List<string> RequiredPermissions { get; set; } = new List<string>();
        }

        private class NotificationRule
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Trigger { get; set; }
            public string Condition { get; set; }
            public string TemplateId { get; set; }
            public bool IsActive { get; set; } = true;
            public List<string> TargetPlayers { get; set; } = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем шаблоны уведомлений
            config.Templates.AddRange(new List<NotificationTemplate>
            {
                new NotificationTemplate
                {
                    Id = "welcome",
                    Name = "Добро пожаловать",
                    Title = "Добро пожаловать на BULBARUST!",
                    Message = "Используйте /help для получения списка команд",
                    Color = "green",
                    Icon = "🎉",
                    Duration = 10f
                },
                new NotificationTemplate
                {
                    Id = "server_restart",
                    Name = "Перезагрузка сервера",
                    Title = "⚠️ ПЕРЕЗАГРУЗКА СЕРВЕРА",
                    Message = "Сервер будет перезагружен через {time} минут",
                    Color = "red",
                    Icon = "⚠️",
                    Duration = 15f,
                    IsPersistent = true
                },
                new NotificationTemplate
                {
                    Id = "event_start",
                    Name = "Начало события",
                    Title = "🎮 НОВОЕ СОБЫТИЕ!",
                    Message = "{event_name} началось! Участвуйте и получайте награды!",
                    Color = "yellow",
                    Icon = "🎮",
                    Duration = 8f
                },
                new NotificationTemplate
                {
                    Id = "achievement",
                    Name = "Достижение",
                    Title = "🏆 ДОСТИЖЕНИЕ РАЗБЛОКИРОВАНО!",
                    Message = "Вы получили достижение: {achievement_name}",
                    Color = "gold",
                    Icon = "🏆",
                    Duration = 7f
                },
                new NotificationTemplate
                {
                    Id = "level_up",
                    Name = "Повышение уровня",
                    Title = "⬆️ УРОВЕНЬ ПОВЫШЕН!",
                    Message = "Поздравляем! Вы достигли уровня {level}",
                    Color = "cyan",
                    Icon = "⬆️",
                    Duration = 6f
                },
                new NotificationTemplate
                {
                    Id = "admin_alert",
                    Name = "Админское уведомление",
                    Title = "🔧 АДМИНИСТРАТОР",
                    Message = "{message}",
                    Color = "red",
                    Icon = "🔧",
                    Duration = 10f,
                    RequiredPermissions = new List<string> { "admin" }
                }
            });

            // Добавляем правила уведомлений
            config.Rules.AddRange(new List<NotificationRule>
            {
                new NotificationRule
                {
                    Id = "player_connect",
                    Name = "Подключение игрока",
                    Trigger = "player_connected",
                    Condition = "all",
                    TemplateId = "welcome",
                    TargetPlayers = new List<string> { "self" }
                },
                new NotificationRule
                {
                    Id = "server_restart_warning",
                    Name = "Предупреждение о перезагрузке",
                    Trigger = "server_restart",
                    Condition = "time_before_restart < 300",
                    TemplateId = "server_restart",
                    TargetPlayers = new List<string> { "all" }
                },
                new NotificationRule
                {
                    Id = "event_notification",
                    Name = "Уведомление о событии",
                    Trigger = "event_started",
                    Condition = "event_type == 'special'",
                    TemplateId = "event_start",
                    TargetPlayers = new List<string> { "all" }
                },
                new NotificationRule
                {
                    Id = "achievement_notification",
                    Name = "Уведомление о достижении",
                    Trigger = "achievement_unlocked",
                    Condition = "achievement_points >= 10",
                    TemplateId = "achievement",
                    TargetPlayers = new List<string> { "self" }
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
        private Dictionary<ulong, List<ActiveNotification>> playerNotifications = new Dictionary<ulong, List<ActiveNotification>>();
        private Dictionary<ulong, NotificationSettings> playerSettings = new Dictionary<ulong, NotificationSettings>();
        private Dictionary<string, float> notificationCooldowns = new Dictionary<string, float>();

        private class ActiveNotification
        {
            public string Id { get; set; }
            public string TemplateId { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string Color { get; set; }
            public string Icon { get; set; }
            public float StartTime { get; set; }
            public float Duration { get; set; }
            public bool IsPersistent { get; set; }
            public bool IsRead { get; set; } = false;
        }

        private class NotificationSettings
        {
            public ulong PlayerId { get; set; }
            public bool EnableNotifications { get; set; } = true;
            public bool EnableSounds { get; set; } = true;
            public bool EnableGlobalNotifications { get; set; } = true;
            public bool EnablePersonalNotifications { get; set; } = true;
            public bool EnableSystemNotifications { get; set; } = true;
            public List<string> DisabledTemplates { get; set; } = new List<string>();
            public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система уведомлений BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableNotifications)
            {
                timer.Every(1f, ProcessNotifications);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableNotifications)
            {
                InitializePlayerNotifications(player);
                TriggerNotification("player_connect", player.userID);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerNotificationData(player.userID);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerNotifications = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ActiveNotification>>>("player_notifications") ?? new Dictionary<ulong, List<ActiveNotification>>();
            playerSettings = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, NotificationSettings>>("notification_settings") ?? new Dictionary<ulong, NotificationSettings>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_notifications", playerNotifications);
            Interface.Oxide.DataFileSystem.WriteObject("notification_settings", playerSettings);
        }

        private void SavePlayerNotificationData(ulong playerId)
        {
            if (playerNotifications.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"notifications_{playerId}", playerNotifications[playerId]);
            }
            if (playerSettings.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"notification_settings_{playerId}", playerSettings[playerId]);
            }
        }

        private void InitializePlayerNotifications(BasePlayer player)
        {
            if (!playerNotifications.ContainsKey(player.userID))
            {
                playerNotifications[player.userID] = new List<ActiveNotification>();
            }

            if (!playerSettings.ContainsKey(player.userID))
            {
                var settings = new NotificationSettings
                {
                    PlayerId = player.userID,
                    EnableNotifications = true,
                    EnableSounds = true,
                    EnableGlobalNotifications = true,
                    EnablePersonalNotifications = true,
                    EnableSystemNotifications = true
                };

                playerSettings[player.userID] = settings;
            }
        }

        private void ProcessNotifications()
        {
            var currentTime = Time.time;
            var expiredNotifications = new List<Tuple<ulong, string>>();

            foreach (var playerId in playerNotifications.Keys.ToList())
            {
                var notifications = playerNotifications[playerId];
                var toRemove = new List<ActiveNotification>();

                foreach (var notification in notifications)
                {
                    if (!notification.IsPersistent && currentTime - notification.StartTime >= notification.Duration)
                    {
                        toRemove.Add(notification);
                        expiredNotifications.Add(new Tuple<ulong, string>(playerId, notification.Id));
                    }
                }

                foreach (var notification in toRemove)
                {
                    notifications.Remove(notification);
                }
            }

            if (expiredNotifications.Count > 0)
            {
                SaveData();
            }
        }

        private void SendNotification(ulong playerId, string templateId, Dictionary<string, string> variables = null)
        {
            if (!config.EnableNotifications) return;

            var template = config.Templates.FirstOrDefault(t => t.Id == templateId);
            if (template == null) return;

            var player = BasePlayer.FindByID(playerId);
            if (player == null) return;

            // Проверяем настройки игрока
            if (!playerSettings.ContainsKey(playerId))
            {
                InitializePlayerNotifications(player);
            }

            var settings = playerSettings[playerId];
            if (!settings.EnableNotifications) return;

            // Проверяем права доступа
            if (template.RequiredPermissions.Count > 0)
            {
                if (!template.RequiredPermissions.Any(p => permission.UserHasPermission(player.UserIDString, p)))
                {
                    return;
                }
            }

            // Проверяем, не отключен ли этот шаблон
            if (settings.DisabledTemplates.Contains(templateId)) return;

            // Создаем уведомление
            var notification = new ActiveNotification
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                Title = ProcessVariables(template.Title, variables),
                Message = ProcessVariables(template.Message, variables),
                Color = template.Color,
                Icon = template.Icon,
                StartTime = Time.time,
                Duration = template.Duration,
                IsPersistent = template.IsPersistent
            };

            // Добавляем уведомление
            if (!playerNotifications.ContainsKey(playerId))
            {
                playerNotifications[playerId] = new List<ActiveNotification>();
            }

            playerNotifications[playerId].Add(notification);

            // Ограничиваем количество уведомлений
            if (playerNotifications[playerId].Count > config.MaxNotifications)
            {
                var oldest = playerNotifications[playerId].OrderBy(n => n.StartTime).First();
                playerNotifications[playerId].Remove(oldest);
            }

            // Отправляем уведомление игроку
            DisplayNotification(player, notification);

            SaveData();
        }

        private string ProcessVariables(string text, Dictionary<string, string> variables)
        {
            if (variables == null) return text;

            foreach (var variable in variables)
            {
                text = text.Replace($"{{{variable.Key}}}", variable.Value);
            }

            return text;
        }

        private void DisplayNotification(BasePlayer player, ActiveNotification notification)
        {
            var color = GetColorCode(notification.Color);
            var message = $"{notification.Icon} {notification.Title}";
            
            player.ChatMessage($"<color={color}>{message}</color>");
            player.ChatMessage($"<color=white>{notification.Message}</color>");

            // Воспроизводим звук, если включен
            if (config.EnableNotificationSounds && playerSettings.ContainsKey(player.userID) && playerSettings[player.userID].EnableSounds)
            {
                // Здесь должна быть логика воспроизведения звука
                // player.SendConsoleCommand("play", "assets/sounds/notification.wav");
            }
        }

        private string GetColorCode(string colorName)
        {
            switch (colorName.ToLower())
            {
                case "red": return "red";
                case "green": return "green";
                case "blue": return "blue";
                case "yellow": return "yellow";
                case "orange": return "orange";
                case "purple": return "purple";
                case "cyan": return "cyan";
                case "gold": return "gold";
                case "silver": return "silver";
                case "bronze": return "bronze";
                default: return "white";
            }
        }

        private void TriggerNotification(string trigger, ulong playerId, Dictionary<string, string> variables = null)
        {
            var rules = config.Rules.Where(r => r.IsActive && r.Trigger == trigger).ToList();

            foreach (var rule in rules)
            {
                if (ShouldTriggerRule(rule, playerId))
                {
                    var targetPlayers = GetTargetPlayers(rule, playerId);
                    foreach (var targetPlayerId in targetPlayers)
                    {
                        SendNotification(targetPlayerId, rule.TemplateId, variables);
                    }
                }
            }
        }

        private bool ShouldTriggerRule(NotificationRule rule, ulong playerId)
        {
            // Здесь должна быть логика проверки условий правила
            // Для примера всегда возвращаем true
            return true;
        }

        private List<ulong> GetTargetPlayers(NotificationRule rule, ulong triggerPlayerId)
        {
            var targetPlayers = new List<ulong>();

            foreach (var target in rule.TargetPlayers)
            {
                switch (target.ToLower())
                {
                    case "all":
                        targetPlayers.AddRange(BasePlayer.activePlayerList.Select(p => p.userID));
                        break;
                    case "self":
                        targetPlayers.Add(triggerPlayerId);
                        break;
                    case "admins":
                        targetPlayers.AddRange(BasePlayer.activePlayerList.Where(p => p.IsAdmin).Select(p => p.userID));
                        break;
                    default:
                        // Предполагаем, что это имя игрока
                        var player = BasePlayer.Find(target);
                        if (player != null)
                        {
                            targetPlayers.Add(player.userID);
                        }
                        break;
                }
            }

            return targetPlayers.Distinct().ToList();
        }

        private void SendGlobalNotification(string templateId, Dictionary<string, string> variables = null)
        {
            if (!config.EnableGlobalNotifications) return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                SendNotification(player.userID, templateId, variables);
            }
        }

        private void SendAdminNotification(string templateId, Dictionary<string, string> variables = null)
        {
            foreach (var player in BasePlayer.activePlayerList.Where(p => p.IsAdmin))
            {
                SendNotification(player.userID, templateId, variables);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("notify")]
        private void NotifyCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableNotifications)
            {
                player.ChatMessage("Система уведомлений отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowNotificationHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowPlayerNotifications(player);
                    break;
                case "clear":
                    ClearPlayerNotifications(player);
                    break;
                case "settings":
                    ShowNotificationSettings(player);
                    break;
                case "toggle":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /notify toggle <on|off>");
                        return;
                    }
                    ToggleNotifications(player, args[1]);
                    break;
                case "sound":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /notify sound <on|off>");
                        return;
                    }
                    ToggleNotificationSound(player, args[1]);
                    break;
                case "test":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /notify test <шаблон>");
                        return;
                    }
                    TestNotification(player, args[1]);
                    break;
            }
        }

        [ChatCommand("announce")]
        private void AnnounceCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав на объявления");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /announce <сообщение>");
                return;
            }

            var message = string.Join(" ", args);
            var variables = new Dictionary<string, string> { { "message", message } };
            
            SendGlobalNotification("admin_alert", variables);
        }
        #endregion

        #region Command Methods
        private void ShowNotificationHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ УВЕДОМЛЕНИЙ ===</color>");
            player.ChatMessage("/notify list - мои уведомления");
            player.ChatMessage("/notify clear - очистить уведомления");
            player.ChatMessage("/notify settings - настройки");
            player.ChatMessage("/notify toggle <on|off> - включить/выключить");
            player.ChatMessage("/notify sound <on|off> - звук уведомлений");
            player.ChatMessage("/notify test <шаблон> - тест уведомления");
            player.ChatMessage("/announce <сообщение> - объявление (админ)");
        }

        private void ShowPlayerNotifications(BasePlayer player)
        {
            if (!playerNotifications.ContainsKey(player.userID) || playerNotifications[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет активных уведомлений");
                return;
            }

            var notifications = playerNotifications[player.userID].OrderByDescending(n => n.StartTime).ToList();

            player.ChatMessage("<color=yellow>=== ВАШИ УВЕДОМЛЕНИЯ ===</color>");
            foreach (var notification in notifications.Take(10))
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - notification.StartTime).TotalMinutes;
                var status = notification.IsRead ? "Прочитано" : "Новое";
                var color = notification.IsRead ? "gray" : "white";
                
                player.ChatMessage($"<color={color}>{notification.Icon} {notification.Title}</color>");
                player.ChatMessage($"  {notification.Message}");
                player.ChatMessage($"  {timeAgo:F1} минут назад - {status}");
            }
        }

        private void ClearPlayerNotifications(BasePlayer player)
        {
            if (playerNotifications.ContainsKey(player.userID))
            {
                playerNotifications[player.userID].Clear();
                player.ChatMessage("<color=green>Все уведомления очищены</color>");
                SaveData();
            }
        }

        private void ShowNotificationSettings(BasePlayer player)
        {
            if (!playerSettings.ContainsKey(player.userID))
            {
                InitializePlayerNotifications(player);
            }

            var settings = playerSettings[player.userID];

            player.ChatMessage("<color=yellow>=== НАСТРОЙКИ УВЕДОМЛЕНИЙ ===</color>");
            player.ChatMessage($"Уведомления: {(settings.EnableNotifications ? "Включены" : "Выключены")}");
            player.ChatMessage($"Звук: {(settings.EnableSounds ? "Включен" : "Выключен")}");
            player.ChatMessage($"Глобальные: {(settings.EnableGlobalNotifications ? "Включены" : "Выключены")}");
            player.ChatMessage($"Личные: {(settings.EnablePersonalNotifications ? "Включены" : "Выключены")}");
            player.ChatMessage($"Системные: {(settings.EnableSystemNotifications ? "Включены" : "Выключены")}");
        }

        private void ToggleNotifications(BasePlayer player, string state)
        {
            if (!playerSettings.ContainsKey(player.userID))
            {
                InitializePlayerNotifications(player);
            }

            var settings = playerSettings[player.userID];
            settings.EnableNotifications = state.ToLower() == "on";

            player.ChatMessage($"<color=green>Уведомления {(settings.EnableNotifications ? "включены" : "выключены")}</color>");
            SaveData();
        }

        private void ToggleNotificationSound(BasePlayer player, string state)
        {
            if (!playerSettings.ContainsKey(player.userID))
            {
                InitializePlayerNotifications(player);
            }

            var settings = playerSettings[player.userID];
            settings.EnableSounds = state.ToLower() == "on";

            player.ChatMessage($"<color=green>Звук уведомлений {(settings.EnableSounds ? "включен" : "выключен")}</color>");
            SaveData();
        }

        private void TestNotification(BasePlayer player, string templateId)
        {
            var template = config.Templates.FirstOrDefault(t => t.Id == templateId);
            if (template == null)
            {
                player.ChatMessage($"Шаблон '{templateId}' не найден");
                return;
            }

            var variables = new Dictionary<string, string>
            {
                { "player_name", player.displayName },
                { "time", DateTime.Now.ToString("HH:mm") }
            };

            SendNotification(player.userID, templateId, variables);
            player.ChatMessage($"<color=green>Тестовое уведомление '{template.Name}' отправлено</color>");
        }
        #endregion
    }
}
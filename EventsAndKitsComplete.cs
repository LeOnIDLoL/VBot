using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Events and Kits Complete", "BULBARUST", "2.0.0")]
    [Description("Полная система китов и событий для BULBARUST")]
    public class EventsAndKitsComplete : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableKits { get; set; } = true;
            public bool EnableEvents { get; set; } = true;
            public bool EnableDailyKits { get; set; } = true;
            public bool EnableVoteKits { get; set; } = true;
            public float KitCooldown { get; set; } = 3600f;
            public float DailyKitCooldown { get; set; } = 86400f;
            public float VoteKitCooldown { get; set; } = 86400f;
            public List<Kit> Kits { get; set; } = new List<Kit>();
            public List<Event> Events { get; set; } = new List<Event>();
        }

        private class Kit
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public List<KitItem> Items { get; set; } = new List<KitItem>();
            public bool IsDaily { get; set; } = false;
            public bool IsVote { get; set; } = false;
            public bool RequirePermission { get; set; } = false;
            public string Permission { get; set; } = "";
            public int MaxUses { get; set; } = -1;
            public float Cooldown { get; set; } = 3600f;
        }

        private class KitItem
        {
            public string ItemName { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public int SkinId { get; set; } = 0;
            public int Condition { get; set; } = 100;
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }

        private class Event
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public EventType Type { get; set; }
            public float Duration { get; set; } = 300f;
            public float Interval { get; set; } = 3600f;
            public bool IsActive { get; set; } = false;
            public float StartTime { get; set; } = 0f;
            public List<EventReward> Rewards { get; set; } = new List<EventReward>();
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }

        private class EventReward
        {
            public string ItemName { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public float Chance { get; set; } = 100f;
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }

        private enum EventType
        {
            Airdrop,
            ZombieHorde,
            ResourceBoost,
            PvP,
            Building,
            Mining,
            Special
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем киты
            config.Kits.AddRange(new List<Kit>
            {
                new Kit
                {
                    Id = "starter",
                    Name = "Стартовый",
                    Description = "Базовый набор для новичков",
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "Камень", ItemId = -151838493, Amount = 1000 },
                        new KitItem { ItemName = "Дерево", ItemId = -151838493, Amount = 1000 },
                        new KitItem { ItemName = "Топор", ItemId = -1251354797, Amount = 1, Condition = 100 }
                    }
                },
                new Kit
                {
                    Id = "pvp",
                    Name = "PvP",
                    Description = "Набор для PvP",
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "AK-47", ItemId = -2069578888, Amount = 1, Condition = 100 },
                        new KitItem { ItemName = "Патроны", ItemId = -2069578888, Amount = 200 },
                        new KitItem { ItemName = "Броня", ItemId = -1251354797, Amount = 1, Condition = 100 }
                    },
                    RequirePermission = true,
                    Permission = "kits.pvp"
                },
                new Kit
                {
                    Id = "daily",
                    Name = "Ежедневный",
                    Description = "Ежедневный набор",
                    IsDaily = true,
                    Cooldown = 86400f,
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "Монеты", ItemId = -151838493, Amount = 1000 },
                        new KitItem { ItemName = "Еда", ItemId = -1251354797, Amount = 10 }
                    }
                }
            });

            // Добавляем события
            config.Events.AddRange(new List<Event>
            {
                new Event
                {
                    Id = "airdrop_event",
                    Name = "Аирдроп",
                    Description = "Специальный аирдроп с ценными предметами",
                    Type = EventType.Airdrop,
                    Duration = 600f,
                    Interval = 7200f,
                    Rewards = new List<EventReward>
                    {
                        new EventReward { ItemName = "C4", ItemId = -1569454159, Amount = 1, Chance = 50f },
                        new EventReward { ItemName = "Металл", ItemId = -151838493, Amount = 1000, Chance = 100f }
                    }
                },
                new Event
                {
                    Id = "pvp_event",
                    Name = "PvP Событие",
                    Description = "Увеличенные награды за убийства",
                    Type = EventType.PvP,
                    Duration = 1800f,
                    Interval = 3600f,
                    Rewards = new List<EventReward>
                    {
                        new EventReward { ItemName = "Оружие", ItemId = -2069578888, Amount = 1, Chance = 30f }
                    }
                }
            });
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new JsonException();
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
        private Dictionary<ulong, float> lastKitUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastDailyKit = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastVoteKit = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> kitUses = new Dictionary<ulong, int>();
        private Dictionary<string, float> eventLastRun = new Dictionary<string, float>();
        private List<BasePlayer> eventParticipants = new List<BasePlayer>();
        private Dictionary<ulong, List<string>> playerKitHistory = new Dictionary<ulong, List<string>>();

        private class PlayerKitData
        {
            public ulong PlayerId { get; set; }
            public Dictionary<string, float> LastUse { get; set; } = new Dictionary<string, float>();
            public Dictionary<string, int> UseCount { get; set; } = new Dictionary<string, int>();
            public List<string> AvailableKits { get; set; } = new List<string>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Полная система китов и событий BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableEvents)
            {
                timer.Every(60f, CheckEvents);
                timer.Every(300f, ProcessEvents);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableKits)
            {
                InitializePlayerKits(player);
                CheckPlayerKitStatus(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            eventParticipants.Remove(player);
            SavePlayerKitData(player.userID);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableEvents) return;

            var player = entity as BasePlayer;
            if (player == null) return;

            var killer = info.InitiatorPlayer;
            if (killer == null || killer == player) return;

            ProcessKillEvent(killer, player);
        }

        void OnItemAdded(ItemContainer container, Item item)
        {
            if (!config.EnableEvents) return;

            var player = container.playerOwner;
            if (player == null) return;

            ProcessItemEvent(player, item);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            lastKitUse = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_kit_use") ?? new Dictionary<ulong, float>();
            lastDailyKit = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_daily_kit") ?? new Dictionary<ulong, float>();
            lastVoteKit = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_vote_kit") ?? new Dictionary<ulong, float>();
            kitUses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>("kit_uses") ?? new Dictionary<ulong, int>();
            eventLastRun = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, float>>("event_last_run") ?? new Dictionary<string, float>();
            playerKitHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("player_kit_history") ?? new Dictionary<ulong, List<string>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("last_kit_use", lastKitUse);
            Interface.Oxide.DataFileSystem.WriteObject("last_daily_kit", lastDailyKit);
            Interface.Oxide.DataFileSystem.WriteObject("last_vote_kit", lastVoteKit);
            Interface.Oxide.DataFileSystem.WriteObject("kit_uses", kitUses);
            Interface.Oxide.DataFileSystem.WriteObject("event_last_run", eventLastRun);
            Interface.Oxide.DataFileSystem.WriteObject("player_kit_history", playerKitHistory);
        }

        private void SavePlayerKitData(ulong playerId)
        {
            if (playerKitHistory.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"kit_history_{playerId}", playerKitHistory[playerId]);
            }
        }

        private void InitializePlayerKits(BasePlayer player)
        {
            if (!playerKitHistory.ContainsKey(player.userID))
            {
                playerKitHistory[player.userID] = new List<string>();
            }

            if (!kitUses.ContainsKey(player.userID))
            {
                kitUses[player.userID] = 0;
            }
        }

        private void CheckPlayerKitStatus(BasePlayer player)
        {
            var availableKits = GetAvailableKits(player);
            player.ChatMessage($"<color=yellow>Доступно китов: {availableKits.Count}</color>");
            
            if (availableKits.Count > 0)
            {
                player.ChatMessage("Используйте /kit для просмотра доступных китов");
            }
        }

        private List<Kit> GetAvailableKits(BasePlayer player)
        {
            var availableKits = new List<Kit>();
            var currentTime = Time.time;

            foreach (var kit in config.Kits)
            {
                if (!kit.RequirePermission || string.IsNullOrEmpty(kit.Permission) || 
                    permission.UserHasPermission(player.UserIDString, kit.Permission))
                {
                    var lastUse = lastKitUse.ContainsKey(player.userID) ? lastKitUse[player.userID] : 0f;
                    if (currentTime - lastUse >= kit.Cooldown)
                    {
                        availableKits.Add(kit);
                    }
                }
            }

            return availableKits;
        }

        private void CheckEvents()
        {
            var currentTime = Time.time;

            foreach (var evt in config.Events)
            {
                if (evt.IsActive) continue;

                if (!eventLastRun.ContainsKey(evt.Id) || 
                    currentTime - eventLastRun[evt.Id] >= evt.Interval)
                {
                    StartEvent(evt);
                    eventLastRun[evt.Id] = currentTime;
                }
            }
        }

        private void ProcessEvents()
        {
            var currentTime = Time.time;
            var eventsToEnd = new List<Event>();

            foreach (var evt in config.Events)
            {
                if (evt.IsActive && currentTime - evt.StartTime >= evt.Duration)
                {
                    eventsToEnd.Add(evt);
                }
            }

            foreach (var evt in eventsToEnd)
            {
                EndEvent(evt);
            }
        }

        private void StartEvent(Event evt)
        {
            evt.IsActive = true;
            evt.StartTime = Time.time;

            // Уведомляем всех игроков
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=yellow>=== СОБЫТИЕ: {evt.Name.ToUpper()} ===</color>");
                player.ChatMessage($"<color=white>{evt.Description}</color>");
                player.ChatMessage($"<color=green>Длительность: {evt.Duration / 60} минут</color>");
            }

            // Запускаем логику события
            ProcessEventLogic(evt);
        }

        private void EndEvent(Event evt)
        {
            evt.IsActive = false;

            // Уведомляем всех игроков
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=red>Событие '{evt.Name}' завершено</color>");
            }

            // Очищаем участников
            eventParticipants.Clear();
        }

        private void ProcessEventLogic(Event evt)
        {
            switch (evt.Type)
            {
                case EventType.Airdrop:
                    ProcessAirdropEvent(evt);
                    break;
                case EventType.PvP:
                    ProcessPvPEvent(evt);
                    break;
                case EventType.ResourceBoost:
                    ProcessResourceBoostEvent(evt);
                    break;
            }
        }

        private void ProcessAirdropEvent(Event evt)
        {
            // Создаем аирдроп в случайном месте
            var random = new System.Random();
            var x = random.Next(-1000, 1000);
            var z = random.Next(-1000, 1000);
            var position = new Vector3(x, 200, z);

            // Здесь должна быть логика создания аирдропа
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=green>Аирдроп сброшен в координатах: {x}, {z}</color>");
            }
        }

        private void ProcessPvPEvent(Event evt)
        {
            // Увеличиваем награды за убийства
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage("<color=yellow>PvP событие активно! Увеличенные награды за убийства!</color>");
            }
        }

        private void ProcessResourceBoostEvent(Event evt)
        {
            // Увеличиваем добычу ресурсов
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage("<color=yellow>Буст ресурсов активен! Увеличенная добыча!</color>");
            }
        }

        private void ProcessKillEvent(BasePlayer killer, BasePlayer victim)
        {
            foreach (var evt in config.Events.Where(e => e.IsActive && e.Type == EventType.PvP))
            {
                GiveEventReward(killer, evt);
            }
        }

        private void ProcessItemEvent(BasePlayer player, Item item)
        {
            foreach (var evt in config.Events.Where(e => e.IsActive && e.Type == EventType.ResourceBoost))
            {
                if (IsResourceItem(item))
                {
                    GiveEventReward(player, evt);
                }
            }
        }

        private bool IsResourceItem(Item item)
        {
            var resourceItems = new[] { "wood", "stone", "metal.fragments", "scrap" };
            return resourceItems.Contains(item.info.shortname);
        }

        private void GiveEventReward(BasePlayer player, Event evt)
        {
            foreach (var reward in evt.Rewards)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= reward.Chance)
                {
                    GiveItemToPlayer(player, reward.ItemId, reward.Amount);
                    player.ChatMessage($"<color=green>Событие {evt.Name}: получен {reward.ItemName}</color>");
                }
            }
        }

        private void GiveItemToPlayer(BasePlayer player, int itemId, int amount)
        {
            var item = ItemManager.CreateByItemID(itemId, amount);
            if (item != null)
            {
                player.GiveItem(item);
            }
        }

        private void GiveKit(BasePlayer player, string kitId, bool isDaily = false, bool isVote = false)
        {
            var kit = config.Kits.FirstOrDefault(k => k.Id == kitId);
            if (kit == null)
            {
                player.ChatMessage($"<color=red>Кит '{kitId}' не найден</color>");
                return;
            }

            // Проверяем права
            if (kit.RequirePermission && !string.IsNullOrEmpty(kit.Permission))
            {
                if (!permission.UserHasPermission(player.UserIDString, kit.Permission))
                {
                    player.ChatMessage("<color=red>У вас нет прав на этот кит</color>");
                    return;
                }
            }

            // Проверяем кулдаун
            var currentTime = Time.time;
            var lastUse = lastKitUse.ContainsKey(player.userID) ? lastKitUse[player.userID] : 0f;
            if (currentTime - lastUse < kit.Cooldown)
            {
                var timeLeft = kit.Cooldown - (currentTime - lastUse);
                player.ChatMessage($"<color=red>Кит доступен через {timeLeft:F0} секунд</color>");
                return;
            }

            // Проверяем лимиты использования
            if (kit.MaxUses > 0)
            {
                var useCount = kitUses.ContainsKey(player.userID) ? kitUses[player.userID] : 0;
                if (useCount >= kit.MaxUses)
                {
                    player.ChatMessage($"<color=red>Превышен лимит использования кита ({kit.MaxUses})</color>");
                    return;
                }
            }

            // Выдаем предметы
            foreach (var kitItem in kit.Items)
            {
                var item = ItemManager.CreateByItemID(kitItem.ItemId, kitItem.Amount);
                if (item != null)
                {
                    if (kitItem.Condition < 100)
                    {
                        item.condition = item.maxCondition * (kitItem.Condition / 100f);
                    }

                    player.GiveItem(item);
                }
            }

            // Обновляем данные
            lastKitUse[player.userID] = currentTime;
            if (isDaily) lastDailyKit[player.userID] = currentTime;
            if (isVote) lastVoteKit[player.userID] = currentTime;

            if (kit.MaxUses > 0)
            {
                kitUses[player.userID] = (kitUses.ContainsKey(player.userID) ? kitUses[player.userID] : 0) + 1;
            }

            if (!playerKitHistory.ContainsKey(player.userID))
            {
                playerKitHistory[player.userID] = new List<string>();
            }
            playerKitHistory[player.userID].Add($"{kitId}:{currentTime}");

            player.ChatMessage($"<color=green>Кит '{kit.Name}' выдан!</color>");
            SaveData();
        }
        #endregion

        #region Commands
        [ChatCommand("kit")]
        private void KitCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableKits)
            {
                player.ChatMessage("Система китов отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowAvailableKits(player);
                return;
            }

            var kitId = args[0].ToLower();
            GiveKit(player, kitId);
        }

        [ChatCommand("daily")]
        private void DailyCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableDailyKits)
            {
                player.ChatMessage("Ежедневные киты отключены");
                return;
            }

            var currentTime = Time.time;
            var lastDaily = lastDailyKit.ContainsKey(player.userID) ? lastDailyKit[player.userID] : 0f;

            if (currentTime - lastDaily < config.DailyKitCooldown)
            {
                var timeLeft = config.DailyKitCooldown - (currentTime - lastDaily);
                player.ChatMessage($"<color=red>Ежедневный кит доступен через {timeLeft:F0} секунд</color>");
                return;
            }

            GiveKit(player, "daily", true);
        }

        [ChatCommand("vote")]
        private void VoteCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableVoteKits)
            {
                player.ChatMessage("Киты за голосование отключены");
                return;
            }

            var currentTime = Time.time;
            var lastVote = lastVoteKit.ContainsKey(player.userID) ? lastVoteKit[player.userID] : 0f;

            if (currentTime - lastVote < config.VoteKitCooldown)
            {
                var timeLeft = config.VoteKitCooldown - (currentTime - lastVote);
                player.ChatMessage($"<color=red>Кит за голосование доступен через {timeLeft:F0} секунд</color>");
                return;
            }

            GiveKit(player, "vote", false, true);
        }

        [ChatCommand("event")]
        private void EventCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав на использование этой команды");
                return;
            }

            if (args.Length == 0)
            {
                ShowEventHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "start":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /event start <id>");
                        return;
                    }
                    StartEventCommand(player, args[1]);
                    break;
                case "stop":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /event stop <id>");
                        return;
                    }
                    StopEventCommand(player, args[1]);
                    break;
                case "list":
                    ShowEventList(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowAvailableKits(BasePlayer player)
        {
            var availableKits = GetAvailableKits(player);
            
            if (availableKits.Count == 0)
            {
                player.ChatMessage("Нет доступных китов");
                return;
            }

            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ КИТЫ ===</color>");
            foreach (var kit in availableKits)
            {
                player.ChatMessage($"<color=cyan>/kit {kit.Id}</color> - {kit.Name}");
                player.ChatMessage($"  {kit.Description}");
            }
        }

        private void ShowEventHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ СОБЫТИЙ ===</color>");
            player.ChatMessage("/event start <id> - запустить событие");
            player.ChatMessage("/event stop <id> - остановить событие");
            player.ChatMessage("/event list - список событий");
        }

        private void StartEventCommand(BasePlayer admin, string eventId)
        {
            var evt = config.Events.FirstOrDefault(e => e.Id == eventId);
            if (evt == null)
            {
                admin.ChatMessage($"Событие '{eventId}' не найдено");
                return;
            }

            if (evt.IsActive)
            {
                admin.ChatMessage($"Событие '{evt.Name}' уже активно");
                return;
            }

            StartEvent(evt);
            admin.ChatMessage($"<color=green>Событие '{evt.Name}' запущено</color>");
        }

        private void StopEventCommand(BasePlayer admin, string eventId)
        {
            var evt = config.Events.FirstOrDefault(e => e.Id == eventId);
            if (evt == null)
            {
                admin.ChatMessage($"Событие '{eventId}' не найдено");
                return;
            }

            if (!evt.IsActive)
            {
                admin.ChatMessage($"Событие '{evt.Name}' не активно");
                return;
            }

            EndEvent(evt);
            admin.ChatMessage($"<color=green>Событие '{evt.Name}' остановлено</color>");
        }

        private void ShowEventList(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== СПИСОК СОБЫТИЙ ===</color>");
            foreach (var evt in config.Events)
            {
                var status = evt.IsActive ? "Активно" : "Неактивно";
                player.ChatMessage($"{evt.Id}: {evt.Name} - {status}");
            }
        }
        #endregion
    }
}
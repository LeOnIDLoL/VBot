using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Events and Kits", "BULBARUST", "1.0.0")]
    [Description("Система китов и событий для сервера BULBARUST")]
    public class EventsAndKits : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableKits { get; set; } = true;
            public bool EnableEvents { get; set; } = true;
            public bool EnableDailyKits { get; set; } = true;
            public bool EnableVoteKits { get; set; } = true;
            public float KitCooldown { get; set; } = 3600f; // 1 час
            public float DailyKitCooldown { get; set; } = 86400f; // 24 часа
            public float VoteKitCooldown { get; set; } = 86400f; // 24 часа
            public List<Kit> Kits { get; set; } = new List<Kit>();
            public List<Event> Events { get; set; } = new List<Event>();
            public List<string> VoteRewards { get; set; } = new List<string>();
        }

        private class Kit
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public List<KitItem> Items { get; set; } = new List<KitItem>();
            public bool IsDaily { get; set; } = false;
            public bool IsVote { get; set; } = false;
            public bool RequirePermission { get; set; } = false;
            public string Permission { get; set; } = "";
            public int MaxUses { get; set; } = -1; // -1 = без ограничений
        }

        private class KitItem
        {
            public string ItemName { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public int SkinId { get; set; } = 0;
            public int Condition { get; set; } = 100;
        }

        private class Event
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public EventType Type { get; set; }
            public float Duration { get; set; } = 300f; // 5 минут
            public float Interval { get; set; } = 3600f; // 1 час
            public bool IsActive { get; set; } = false;
            public float StartTime { get; set; } = 0f;
            public List<EventReward> Rewards { get; set; } = new List<EventReward>();
        }

        private class EventReward
        {
            public string ItemName { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public float Chance { get; set; } = 100f; // Процент шанса
        }

        private enum EventType
        {
            Airdrop,
            ZombieHorde,
            ResourceBoost,
            PvP,
            Building,
            Mining
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем киты
            config.Kits.AddRange(new List<Kit>
            {
                new Kit
                {
                    Name = "Стартовый",
                    Description = "Базовый набор для новичков",
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "Камень", ItemId = -151838493, Amount = 1000 },
                        new KitItem { ItemName = "Дерево", ItemId = -151838493, Amount = 1000 },
                        new KitItem { ItemName = "Металл", ItemId = -151838493, Amount = 500 },
                        new KitItem { ItemName = "Топор", ItemId = -1251354797, Amount = 1, Condition = 100 }
                    }
                },
                new Kit
                {
                    Name = "PvP",
                    Description = "Набор для PvP",
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "AK-47", ItemId = -2069578888, Amount = 1, Condition = 100 },
                        new KitItem { ItemName = "Патроны", ItemId = -2069578888, Amount = 200 },
                        new KitItem { ItemName = "Броня", ItemId = -1251354797, Amount = 1, Condition = 100 },
                        new KitItem { ItemName = "Шлем", ItemId = -1251354797, Amount = 1, Condition = 100 }
                    }
                },
                new Kit
                {
                    Name = "Строитель",
                    Description = "Набор для строительства",
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "Камень", ItemId = -151838493, Amount = 5000 },
                        new KitItem { ItemName = "Дерево", ItemId = -151838493, Amount = 5000 },
                        new KitItem { ItemName = "Металл", ItemId = -151838493, Amount = 2000 },
                        new KitItem { ItemName = "Молоток", ItemId = -1251354797, Amount = 1, Condition = 100 }
                    }
                },
                new Kit
                {
                    Name = "Ежедневный",
                    Description = "Ежедневный набор",
                    IsDaily = true,
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "Монеты", ItemId = -151838493, Amount = 1000 },
                        new KitItem { ItemName = "Еда", ItemId = -1251354797, Amount = 10 },
                        new KitItem { ItemName = "Вода", ItemId = -1251354797, Amount = 10 }
                    }
                },
                new Kit
                {
                    Name = "Голосование",
                    Description = "Набор за голосование",
                    IsVote = true,
                    Items = new List<KitItem>
                    {
                        new KitItem { ItemName = "Редкий предмет", ItemId = -151838493, Amount = 1 },
                        new KitItem { ItemName = "Монеты", ItemId = -151838493, Amount = 500 }
                    }
                }
            });

            // Добавляем события
            config.Events.AddRange(new List<Event>
            {
                new Event
                {
                    Name = "Аирдроп",
                    Description = "Специальный аирдроп с редкими предметами",
                    Type = EventType.Airdrop,
                    Duration = 600f,
                    Interval = 7200f,
                    Rewards = new List<EventReward>
                    {
                        new EventReward { ItemName = "C4", ItemId = -1569454159, Amount = 1, Chance = 50f },
                        new EventReward { ItemName = "Ракетница", ItemId = -2069578888, Amount = 1, Chance = 30f },
                        new EventReward { ItemName = "Монеты", ItemId = -151838493, Amount = 1000, Chance = 100f }
                    }
                },
                new Event
                {
                    Name = "Зомби-апокалипсис",
                    Description = "Волна зомби атакует сервер",
                    Type = EventType.ZombieHorde,
                    Duration = 1800f,
                    Interval = 10800f,
                    Rewards = new List<EventReward>
                    {
                        new EventReward { ItemName = "Опыт", ItemId = -151838493, Amount = 1000, Chance = 100f },
                        new EventReward { ItemName = "Редкий лут", ItemId = -1251354797, Amount = 1, Chance = 25f }
                    }
                },
                new Event
                {
                    Name = "Буст ресурсов",
                    Description = "Удвоенная добыча ресурсов",
                    Type = EventType.ResourceBoost,
                    Duration = 3600f,
                    Interval = 14400f
                }
            });

            // Награды за голосование
            config.VoteRewards.AddRange(new List<string>
            {
                "Голосование",
                "VIP",
                "Премиум"
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
        private Dictionary<ulong, float> lastKitUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastDailyKit = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastVoteKit = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> kitUses = new Dictionary<ulong, int>();
        private Dictionary<string, float> eventLastRun = new Dictionary<string, float>();
        private List<BasePlayer> eventParticipants = new List<BasePlayer>();
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            Puts("Система китов и событий BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableEvents)
            {
                timer.Every(60f, CheckEvents);
                timer.Every(300f, ProcessEvents);
                timer.Every(60f, CheckKitCooldowns);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableKits)
            {
                // Даем стартовый кит новичкам
                timer.Once(5f, () => {
                    if (player.IsConnected)
                    {
                        GiveKit(player, "Стартовый", false);
                    }
                });
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

            // Проверяем события на убийство
            CheckKillEvents(killer, player);
        }

        void OnItemAdded(ItemContainer container, Item item)
        {
            if (!config.EnableEvents) return;

            var player = container.playerOwner;
            if (player == null) return;

            // Проверяем события на получение предметов
            CheckItemEvents(player, item);
        }
        #endregion

        #region Methods
        private void CheckEvents()
        {
            var currentTime = Time.time;

            foreach (var evt in config.Events)
            {
                if (evt.IsActive) continue;

                if (!eventLastRun.ContainsKey(evt.Name) || 
                    currentTime - eventLastRun[evt.Name] >= evt.Interval)
                {
                    StartEvent(evt);
                    eventLastRun[evt.Name] = currentTime;
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

        private void CheckKitCooldowns()
        {
            var currentTime = Time.time;
            var playersToRemove = new List<ulong>();

            foreach (var playerId in lastKitUse.Keys.ToList())
            {
                if (currentTime - lastKitUse[playerId] >= config.KitCooldown)
                {
                    playersToRemove.Add(playerId);
                }
            }

            foreach (var playerId in playersToRemove)
            {
                lastKitUse.Remove(playerId);
            }
        }

        private void CheckPlayerKitStatus(BasePlayer player)
        {
            if (!lastKitUse.ContainsKey(player.userID)) return;

            var timeLeft = config.KitCooldown - (Time.time - lastKitUse[player.userID]);
            if (timeLeft > 0)
            {
                player.ChatMessage($"<color=yellow>Кит доступен через {timeLeft:F0} секунд</color>");
            }
        }

        private void CheckKillEvents(BasePlayer killer, BasePlayer victim)
        {
            // Проверяем события на убийство
            foreach (var evt in config.Events.Where(e => e.IsActive && e.Type == EventType.PvP))
            {
                GiveEventReward(killer, evt);
            }
        }

        private void CheckItemEvents(BasePlayer player, Item item)
        {
            // Проверяем события на получение предметов
            foreach (var evt in config.Events.Where(e => e.IsActive && e.Type == EventType.ResourceBoost))
            {
                if (item.info.displayName.english.Contains("Metal") || 
                    item.info.displayName.english.Contains("Stone") || 
                    item.info.displayName.english.Contains("Wood"))
                {
                    GiveEventReward(player, evt);
                }
            }
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
            switch (evt.Type)
            {
                case EventType.Airdrop:
                    StartAirdropEvent(evt);
                    break;
                case EventType.ZombieHorde:
                    StartZombieHordeEvent(evt);
                    break;
                case EventType.ResourceBoost:
                    StartResourceBoostEvent(evt);
                    break;
            }

            // Завершаем событие через указанное время
            timer.Once(evt.Duration, () => EndEvent(evt));
        }

        private void StartAirdropEvent(Event evt)
        {
            // Создаем аирдроп в случайном месте
            var randomPos = GetRandomPosition();
            var airdrop = GameManager.server.CreateEntity("assets/prefabs/npc/cargo plane/cargo_plane.prefab", randomPos) as CargoPlane;
            if (airdrop != null)
            {
                airdrop.Spawn();
            }

            // Уведомляем игроков о местоположении
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=red>Аирдроп появился в координатах: {randomPos}</color>");
            }
        }

        private void StartZombieHordeEvent(Event evt)
        {
            // Создаем зомби в случайных местах
            for (int i = 0; i < 10; i++)
            {
                var zombiePos = GetRandomPosition();
                var zombie = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", zombiePos) as ScientistNPC;
                if (zombie != null)
                {
                    zombie.Spawn();
                    zombie.SetHealth(200f); // Увеличиваем здоровье
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage("<color=red>ВНИМАНИЕ! Зомби-апокалипсис начался!</color>");
            }
        }

        private void StartResourceBoostEvent(Event evt)
        {
            // Увеличиваем добычу ресурсов в 2 раза
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage("<color=green>Буст ресурсов активен! Добыча увеличена в 2 раза!</color>");
            }
        }

        private void EndEvent(Event evt)
        {
            evt.IsActive = false;

            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=yellow>Событие '{evt.Name}' завершено!</color>");
            }

            // Раздаем награды участникам
            foreach (var participant in eventParticipants)
            {
                GiveEventRewards(participant, evt);
            }

            eventParticipants.Clear();
        }

        private void GiveEventRewards(BasePlayer player, Event evt)
        {
            foreach (var reward in evt.Rewards)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= reward.Chance)
                {
                    var itemDef = ItemManager.FindItemDefinition(reward.ItemId);
                    if (itemDef != null)
                    {
                        var item = ItemManager.Create(itemDef, reward.Amount);
                        if (player.inventory.GiveItem(item))
                        {
                            player.ChatMessage($"<color=green>Получена награда: {reward.ItemName} x{reward.Amount}</color>");
                        }
                    }
                }
            }
        }

        private Vector3 GetRandomPosition()
        {
            var randomX = UnityEngine.Random.Range(-2000f, 2000f);
            var randomZ = UnityEngine.Random.Range(-2000f, 2000f);
            var height = TerrainMeta.HeightMap.GetHeight(randomX, randomZ) + 50f;
            return new Vector3(randomX, height, randomZ);
        }

        private bool CanUseKit(ulong playerId, string kitName)
        {
            var currentTime = Time.time;
            var kit = config.Kits.FirstOrDefault(k => k.Name.ToLower() == kitName.ToLower());
            if (kit == null) return false;

            // Проверяем кулдаун
            if (lastKitUse.ContainsKey(playerId))
            {
                var timeLeft = config.KitCooldown - (currentTime - lastKitUse[playerId]);
                if (timeLeft > 0) return false;
            }

            // Проверяем ежедневный кит
            if (kit.IsDaily)
            {
                if (lastDailyKit.ContainsKey(playerId))
                {
                    var timeLeft = config.DailyKitCooldown - (currentTime - lastDailyKit[playerId]);
                    if (timeLeft > 0) return false;
                }
            }

            // Проверяем кит за голосование
            if (kit.IsVote)
            {
                if (lastVoteKit.ContainsKey(playerId))
                {
                    var timeLeft = config.VoteKitCooldown - (currentTime - lastVoteKit[playerId]);
                    if (timeLeft > 0) return false;
                }
            }

            // Проверяем ограничения использования
            if (kit.MaxUses > 0)
            {
                var uses = kitUses.ContainsKey(playerId) ? kitUses[playerId] : 0;
                if (uses >= kit.MaxUses) return false;
            }

            return true;
        }

        private void GiveKit(BasePlayer player, string kitName, bool checkCooldown = true)
        {
            var kit = config.Kits.FirstOrDefault(k => k.Name.ToLower() == kitName.ToLower());
            if (kit == null)
            {
                player.ChatMessage($"Кит '{kitName}' не найден");
                return;
            }

            if (checkCooldown && !CanUseKit(player.userID, kitName))
            {
                player.ChatMessage("Кит еще не готов к использованию");
                return;
            }

            // Проверяем права доступа
            if (kit.RequirePermission && !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                player.ChatMessage("У вас нет прав на этот кит");
                return;
            }

            // Выдаем предметы
            var givenItems = 0;
            foreach (var kitItem in kit.Items)
            {
                var itemDef = ItemManager.FindItemDefinition(kitItem.ItemId);
                if (itemDef != null)
                {
                    var item = ItemManager.Create(itemDef, kitItem.Amount);
                    if (item != null)
                    {
                        item.condition = kitItem.Condition;
                        if (kitItem.SkinId > 0)
                        {
                            item.skin = kitItem.SkinId;
                        }

                        if (player.inventory.GiveItem(item))
                        {
                            givenItems++;
                        }
                    }
                }
            }

            if (givenItems > 0)
            {
                player.ChatMessage($"<color=green>Получен кит '{kit.Name}'! Предметов: {givenItems}</color>");
                
                // Обновляем данные
                lastKitUse[player.userID] = Time.time;
                if (kit.IsDaily)
                {
                    lastDailyKit[player.userID] = Time.time;
                }
                if (kit.IsVote)
                {
                    lastVoteKit[player.userID] = Time.time;
                }
                if (kit.MaxUses > 0)
                {
                    if (!kitUses.ContainsKey(player.userID))
                        kitUses[player.userID] = 0;
                    kitUses[player.userID]++;
                }
            }
            else
            {
                player.ChatMessage("Недостаточно места в инвентаре");
            }
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

            var kitName = string.Join(" ", args);
            GiveKit(player, kitName);
        }

        [ChatCommand("daily")]
        private void DailyKitCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableDailyKits)
            {
                player.ChatMessage("Ежедневные киты отключены");
                return;
            }

            GiveKit(player, "Ежедневный");
        }

        [ChatCommand("vote")]
        private void VoteKitCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableVoteKits)
            {
                player.ChatMessage("Киты за голосование отключены");
                return;
            }

            GiveKit(player, "Голосование");
        }

        [ChatCommand("event")]
        private void EventCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableEvents)
            {
                player.ChatMessage("Система событий отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowActiveEvents(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "join":
                    JoinEvent(player);
                    break;
                case "leave":
                    LeaveEvent(player);
                    break;
                case "list":
                    ShowAllEvents(player);
                    break;
            }
        }

        [ChatCommand("kits")]
        private void KitsCommand(BasePlayer player, string command, string[] args)
        {
            ShowAvailableKits(player);
        }
        #endregion

        #region Helper Methods
        private void ShowAvailableKits(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ КИТЫ ===</color>");
            
            foreach (var kit in config.Kits)
            {
                if (kit.RequirePermission && !permission.UserHasPermission(player.UserIDString, kit.Permission))
                    continue;

                var cooldownInfo = "";
                if (kit.IsDaily)
                {
                    cooldownInfo = " (Ежедневный)";
                }
                else if (kit.IsVote)
                {
                    cooldownInfo = " (За голосование)";
                }

                player.ChatMessage($"{kit.Name}{cooldownInfo} - {kit.Description}");
            }

            player.ChatMessage("Используйте: /kit <название>");
        }

        private void ShowActiveEvents(BasePlayer player)
        {
            var activeEvents = config.Events.Where(e => e.IsActive).ToList();
            
            if (activeEvents.Count == 0)
            {
                player.ChatMessage("Активных событий нет");
                return;
            }

            player.ChatMessage("<color=yellow>=== АКТИВНЫЕ СОБЫТИЯ ===</color>");
            foreach (var evt in activeEvents)
            {
                var timeLeft = evt.Duration - (Time.time - evt.StartTime);
                player.ChatMessage($"{evt.Name} - Осталось: {Mathf.CeilToInt(timeLeft / 60)} минут");
            }
        }

        private void ShowAllEvents(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ВСЕ СОБЫТИЯ ===</color>");
            foreach (var evt in config.Events)
            {
                var status = evt.IsActive ? "Активно" : "Неактивно";
                player.ChatMessage($"{evt.Name} - {status}");
                player.ChatMessage($"  {evt.Description}");
            }
        }

        private void JoinEvent(BasePlayer player)
        {
            if (!eventParticipants.Contains(player))
            {
                eventParticipants.Add(player);
                player.ChatMessage("<color=green>Вы присоединились к событию!</color>");
            }
            else
            {
                player.ChatMessage("Вы уже участвуете в событии");
            }
        }

        private void LeaveEvent(BasePlayer player)
        {
            if (eventParticipants.Contains(player))
            {
                eventParticipants.Remove(player);
                player.ChatMessage("<color=red>Вы покинули событие</color>");
            }
            else
            {
                player.ChatMessage("Вы не участвуете в событии");
            }
        }
        #endregion
    }
}
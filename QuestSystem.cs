using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Quest System", "BULBARUST", "1.0.0")]
    [Description("Система квестов для сервера BULBARUST")]
    public class QuestSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableQuests { get; set; } = true;
            public bool EnableDailyQuests { get; set; } = true;
            public bool EnableWeeklyQuests { get; set; } = true;
            public bool EnableEventQuests { get; set; } = true;
            public int MaxActiveQuests { get; set; } = 5;
            public int MaxDailyQuests { get; set; } = 3;
            public float QuestResetTime { get; set; } = 86400f; // 24 часа
            public List<Quest> DefaultQuests { get; set; } = new List<Quest>();
            public List<QuestReward> QuestRewards { get; set; } = new List<QuestReward>();
        }

        private class Quest
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Category { get; set; } = "Общее";
            public QuestType Type { get; set; }
            public QuestDifficulty Difficulty { get; set; }
            public int RequiredLevel { get; set; } = 1;
            public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
            public List<QuestReward> Rewards { get; set; } = new List<QuestReward>();
            public List<string> RequiredQuests { get; set; } = new List<string>();
            public bool IsRepeatable { get; set; } = false;
            public float Cooldown { get; set; } = 0f;
            public bool IsActive { get; set; } = true;
        }

        private class QuestObjective
        {
            public string Id { get; set; }
            public string Description { get; set; }
            public ObjectiveType Type { get; set; }
            public int RequiredAmount { get; set; } = 1;
            public int CurrentAmount { get; set; } = 0;
            public bool IsCompleted { get; set; } = false;
            public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        }

        private class QuestReward
        {
            public string Type { get; set; } // item, experience, reputation, currency
            public string ItemName { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public float Value { get; set; } = 0f;
            public string Description { get; set; }
        }

        private enum QuestType
        {
            Main,
            Side,
            Daily,
            Weekly,
            Event,
            Tutorial
        }

        private enum QuestDifficulty
        {
            Easy,
            Medium,
            Hard,
            Expert,
            Legendary
        }

        private enum ObjectiveType
        {
            Kill,
            Collect,
            Craft,
            Build,
            Travel,
            Trade,
            Survive,
            Explore
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем стандартные квесты
            config.DefaultQuests.AddRange(new List<Quest>
            {
                new Quest
                {
                    Id = "tutorial_start",
                    Name = "Начало пути",
                    Description = "Добро пожаловать на сервер BULBARUST! Выполните базовые задачи для начала игры.",
                    Category = "Обучение",
                    Type = QuestType.Tutorial,
                    Difficulty = QuestDifficulty.Easy,
                    RequiredLevel = 1,
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            Id = "gather_wood",
                            Description = "Соберите 100 дерева",
                            Type = ObjectiveType.Collect,
                            RequiredAmount = 100,
                            Parameters = new Dictionary<string, object> { { "item", "wood" } }
                        },
                        new QuestObjective
                        {
                            Id = "gather_stone",
                            Description = "Соберите 50 камня",
                            Type = ObjectiveType.Collect,
                            RequiredAmount = 50,
                            Parameters = new Dictionary<string, object> { { "item", "stone" } }
                        }
                    },
                    Rewards = new List<QuestReward>
                    {
                        new QuestReward { Type = "item", ItemName = "Топор", ItemId = -1251354797, Amount = 1 },
                        new QuestReward { Type = "currency", Value = 500f, Description = "500 монет" }
                    }
                },
                new Quest
                {
                    Id = "daily_gather",
                    Name = "Ежедневный сбор",
                    Description = "Соберите ресурсы для ежедневной награды",
                    Category = "Ежедневные",
                    Type = QuestType.Daily,
                    Difficulty = QuestDifficulty.Easy,
                    RequiredLevel = 1,
                    IsRepeatable = true,
                    Cooldown = 86400f,
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            Id = "gather_resources",
                            Description = "Соберите 500 дерева и 300 камня",
                            Type = ObjectiveType.Collect,
                            RequiredAmount = 1,
                            Parameters = new Dictionary<string, object> { { "wood", 500 }, { "stone", 300 } }
                        }
                    },
                    Rewards = new List<QuestReward>
                    {
                        new QuestReward { Type = "currency", Value = 1000f, Description = "1000 монет" },
                        new QuestReward { Type = "experience", Value = 100f, Description = "100 опыта" }
                    }
                },
                new Quest
                {
                    Id = "pvp_hunter",
                    Name = "Охотник",
                    Description = "Убейте 5 игроков в PvP",
                    Category = "PvP",
                    Type = QuestType.Side,
                    Difficulty = QuestDifficulty.Medium,
                    RequiredLevel = 5,
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            Id = "kill_players",
                            Description = "Убейте 5 игроков",
                            Type = ObjectiveType.Kill,
                            RequiredAmount = 5,
                            Parameters = new Dictionary<string, object> { { "target", "player" } }
                        }
                    },
                    Rewards = new List<QuestReward>
                    {
                        new QuestReward { Type = "item", ItemName = "AK-47", ItemId = -2069578888, Amount = 1 },
                        new QuestReward { Type = "reputation", Value = 50f, Description = "50 репутации" }
                    }
                },
                new Quest
                {
                    Id = "builder_master",
                    Name = "Мастер строительства",
                    Description = "Постройте 10 различных сооружений",
                    Category = "Строительство",
                    Type = QuestType.Side,
                    Difficulty = QuestDifficulty.Medium,
                    RequiredLevel = 3,
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            Id = "build_structures",
                            Description = "Постройте 10 сооружений",
                            Type = ObjectiveType.Build,
                            RequiredAmount = 10,
                            Parameters = new Dictionary<string, object> { { "structure_types", 5 } }
                        }
                    },
                    Rewards = new List<QuestReward>
                    {
                        new QuestReward { Type = "item", ItemName = "Молоток", ItemId = -1251354797, Amount = 1 },
                        new QuestReward { Type = "currency", Value = 2000f, Description = "2000 монет" }
                    }
                },
                new Quest
                {
                    Id = "explorer",
                    Name = "Исследователь",
                    Description = "Посетите 5 различных локаций на карте",
                    Category = "Исследование",
                    Type = QuestType.Side,
                    Difficulty = QuestDifficulty.Easy,
                    RequiredLevel = 2,
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            Id = "visit_locations",
                            Description = "Посетите 5 различных локаций",
                            Type = ObjectiveType.Explore,
                            RequiredAmount = 5,
                            Parameters = new Dictionary<string, object> { { "locations", new List<string> { "spawn", "trader", "arena", "mine", "city" } } }
                        }
                    },
                    Rewards = new List<QuestReward>
                    {
                        new QuestReward { Type = "currency", Value = 1500f, Description = "1500 монет" },
                        new QuestReward { Type = "experience", Value = 200f, Description = "200 опыта" }
                    }
                }
            });

            // Добавляем награды
            config.QuestRewards.AddRange(new List<QuestReward>
            {
                new QuestReward { Type = "currency", Value = 100f, Description = "100 монет" },
                new QuestReward { Type = "experience", Value = 50f, Description = "50 опыта" },
                new QuestReward { Type = "reputation", Value = 10f, Description = "10 репутации" }
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
        private Dictionary<ulong, PlayerQuestData> playerQuests = new Dictionary<ulong, PlayerQuestData>();
        private Dictionary<string, Quest> availableQuests = new Dictionary<string, Quest>();
        private Dictionary<ulong, float> lastDailyReset = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastWeeklyReset = new Dictionary<ulong, float>();

        private class PlayerQuestData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public List<ActiveQuest> ActiveQuests { get; set; } = new List<ActiveQuest>();
            public List<string> CompletedQuests { get; set; } = new List<string>();
            public Dictionary<string, float> QuestCooldowns { get; set; } = new Dictionary<string, float>();
            public int TotalQuestsCompleted { get; set; } = 0;
            public float TotalExperienceEarned { get; set; } = 0f;
            public float TotalCurrencyEarned { get; set; } = 0f;
        }

        private class ActiveQuest
        {
            public string QuestId { get; set; }
            public string QuestName { get; set; }
            public float StartTime { get; set; }
            public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
            public bool IsCompleted { get; set; } = false;
            public float CompletionTime { get; set; } = 0f;
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            InitializeQuests();
            Puts("Система квестов BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableQuests)
            {
                timer.Every(3600f, ProcessQuestResets); // Каждый час
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableQuests)
            {
                InitializePlayerQuests(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerQuestData(player.userID);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableQuests) return;

            var player = info.InitiatorPlayer;
            if (player == null) return;

            // Обрабатываем убийство для квестов
            ProcessKillObjective(player, entity);
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableQuests) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            // Обрабатываем строительство для квестов
            ProcessBuildObjective(player, go);
        }

        void OnItemAdded(ItemContainer container, Item item)
        {
            if (!config.EnableQuests) return;

            var player = container.playerOwner;
            if (player == null) return;

            // Обрабатываем сбор предметов для квестов
            ProcessCollectObjective(player, item);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerQuests = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerQuestData>>("player_quests") ?? new Dictionary<ulong, PlayerQuestData>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_quests", playerQuests);
        }

        private void SavePlayerQuestData(ulong playerId)
        {
            if (playerQuests.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"quests_{playerId}", playerQuests[playerId]);
            }
        }

        private void InitializeQuests()
        {
            foreach (var quest in config.DefaultQuests)
            {
                availableQuests[quest.Id] = quest;
            }
        }

        private void InitializePlayerQuests(BasePlayer player)
        {
            if (!playerQuests.ContainsKey(player.userID))
            {
                var questData = new PlayerQuestData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName
                };

                playerQuests[player.userID] = questData;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerQuests[player.userID].PlayerName = player.displayName;
            }

            // Даем стартовый квест новичкам
            if (playerQuests[player.userID].CompletedQuests.Count == 0)
            {
                GiveQuest(player, "tutorial_start");
            }
        }

        private void ProcessQuestResets()
        {
            var currentTime = Time.time;

            foreach (var playerId in playerQuests.Keys.ToList())
            {
                var questData = playerQuests[playerId];
                
                // Сброс ежедневных квестов
                if (config.EnableDailyQuests)
                {
                    if (!lastDailyReset.ContainsKey(playerId) || 
                        currentTime - lastDailyReset[playerId] >= config.QuestResetTime)
                    {
                        ResetDailyQuests(playerId);
                        lastDailyReset[playerId] = currentTime;
                    }
                }

                // Сброс еженедельных квестов
                if (config.EnableWeeklyQuests)
                {
                    if (!lastWeeklyReset.ContainsKey(playerId) || 
                        currentTime - lastWeeklyReset[playerId] >= (config.QuestResetTime * 7))
                    {
                        ResetWeeklyQuests(playerId);
                        lastWeeklyReset[playerId] = currentTime;
                    }
                }
            }

            SaveData();
        }

        private void ResetDailyQuests(ulong playerId)
        {
            var questData = playerQuests[playerId];
            
            // Удаляем завершенные ежедневные квесты
            var dailyQuests = questData.ActiveQuests.Where(q => 
                availableQuests.ContainsKey(q.QuestId) && 
                availableQuests[q.QuestId].Type == QuestType.Daily).ToList();

            foreach (var quest in dailyQuests)
            {
                questData.ActiveQuests.Remove(quest);
            }

            // Даем новые ежедневные квесты
            GiveRandomDailyQuests(playerId);
        }

        private void ResetWeeklyQuests(ulong playerId)
        {
            var questData = playerQuests[playerId];
            
            // Удаляем завершенные еженедельные квесты
            var weeklyQuests = questData.ActiveQuests.Where(q => 
                availableQuests.ContainsKey(q.QuestId) && 
                availableQuests[q.QuestId].Type == QuestType.Weekly).ToList();

            foreach (var quest in weeklyQuests)
            {
                questData.ActiveQuests.Remove(quest);
            }

            // Даем новые еженедельные квесты
            GiveRandomWeeklyQuests(playerId);
        }

        private void GiveRandomDailyQuests(ulong playerId)
        {
            var dailyQuests = availableQuests.Values
                .Where(q => q.Type == QuestType.Daily && q.IsActive)
                .Take(config.MaxDailyQuests)
                .ToList();

            foreach (var quest in dailyQuests)
            {
                GiveQuest(playerId, quest.Id);
            }
        }

        private void GiveRandomWeeklyQuests(ulong playerId)
        {
            var weeklyQuests = availableQuests.Values
                .Where(q => q.Type == QuestType.Weekly && q.IsActive)
                .Take(2)
                .ToList();

            foreach (var quest in weeklyQuests)
            {
                GiveQuest(playerId, quest.Id);
            }
        }

        private void GiveQuest(BasePlayer player, string questId)
        {
            GiveQuest(player.userID, questId);
        }

        private void GiveQuest(ulong playerId, string questId)
        {
            if (!availableQuests.ContainsKey(questId))
            {
                return;
            }

            var quest = availableQuests[questId];
            var questData = playerQuests[playerId];

            // Проверяем, можно ли дать квест
            if (!CanGiveQuest(playerId, quest))
            {
                return;
            }

            // Создаем активный квест
            var activeQuest = new ActiveQuest
            {
                QuestId = questId,
                QuestName = quest.Name,
                StartTime = Time.time,
                Objectives = quest.Objectives.Select(o => new QuestObjective
                {
                    Id = o.Id,
                    Description = o.Description,
                    Type = o.Type,
                    RequiredAmount = o.RequiredAmount,
                    CurrentAmount = 0,
                    IsCompleted = false,
                    Parameters = o.Parameters
                }).ToList()
            };

            questData.ActiveQuests.Add(activeQuest);

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=green>Получен новый квест: {quest.Name}</color>");
                player.ChatMessage($"<color=white>{quest.Description}</color>");
            }

            SaveData();
        }

        private bool CanGiveQuest(ulong playerId, Quest quest)
        {
            var questData = playerQuests[playerId];

            // Проверяем лимит активных квестов
            if (questData.ActiveQuests.Count >= config.MaxActiveQuests)
            {
                return false;
            }

            // Проверяем, не выполнен ли уже квест
            if (questData.CompletedQuests.Contains(quest.Id) && !quest.IsRepeatable)
            {
                return false;
            }

            // Проверяем кулдаун
            if (quest.Cooldown > 0 && questData.QuestCooldowns.ContainsKey(quest.Id))
            {
                var timeLeft = quest.Cooldown - (Time.time - questData.QuestCooldowns[quest.Id]);
                if (timeLeft > 0)
                {
                    return false;
                }
            }

            // Проверяем требуемые квесты
            foreach (var requiredQuest in quest.RequiredQuests)
            {
                if (!questData.CompletedQuests.Contains(requiredQuest))
                {
                    return false;
                }
            }

            return true;
        }

        private void ProcessKillObjective(BasePlayer player, BaseCombatEntity target)
        {
            if (!playerQuests.ContainsKey(player.userID)) return;

            var questData = playerQuests[player.userID];
            var activeQuests = questData.ActiveQuests.Where(q => !q.IsCompleted).ToList();

            foreach (var quest in activeQuests)
            {
                foreach (var objective in quest.Objectives.Where(o => !o.IsCompleted))
                {
                    if (objective.Type == ObjectiveType.Kill)
                    {
                        var targetType = objective.Parameters.ContainsKey("target") ? objective.Parameters["target"].ToString() : "";
                        
                        if (targetType == "player" && target is BasePlayer)
                        {
                            objective.CurrentAmount++;
                            CheckObjectiveCompletion(objective);
                        }
                        else if (targetType == "animal" && target is BaseNpc)
                        {
                            objective.CurrentAmount++;
                            CheckObjectiveCompletion(objective);
                        }
                    }
                }
            }

            CheckQuestCompletion(player.userID);
        }

        private void ProcessBuildObjective(BasePlayer player, GameObject builtObject)
        {
            if (!playerQuests.ContainsKey(player.userID)) return;

            var questData = playerQuests[player.userID];
            var activeQuests = questData.ActiveQuests.Where(q => !q.IsCompleted).ToList();

            foreach (var quest in activeQuests)
            {
                foreach (var objective in quest.Objectives.Where(o => !o.IsCompleted))
                {
                    if (objective.Type == ObjectiveType.Build)
                    {
                        objective.CurrentAmount++;
                        CheckObjectiveCompletion(objective);
                    }
                }
            }

            CheckQuestCompletion(player.userID);
        }

        private void ProcessCollectObjective(BasePlayer player, Item item)
        {
            if (!playerQuests.ContainsKey(player.userID)) return;

            var questData = playerQuests[player.userID];
            var activeQuests = questData.ActiveQuests.Where(q => !q.IsCompleted).ToList();

            foreach (var quest in activeQuests)
            {
                foreach (var objective in quest.Objectives.Where(o => !o.IsCompleted))
                {
                    if (objective.Type == ObjectiveType.Collect)
                    {
                        var requiredItem = objective.Parameters.ContainsKey("item") ? objective.Parameters["item"].ToString() : "";
                        
                        if (string.IsNullOrEmpty(requiredItem) || item.info.shortname.Contains(requiredItem))
                        {
                            objective.CurrentAmount += item.amount;
                            CheckObjectiveCompletion(objective);
                        }
                    }
                }
            }

            CheckQuestCompletion(player.userID);
        }

        private void CheckObjectiveCompletion(QuestObjective objective)
        {
            if (objective.CurrentAmount >= objective.RequiredAmount)
            {
                objective.IsCompleted = true;
            }
        }

        private void CheckQuestCompletion(ulong playerId)
        {
            if (!playerQuests.ContainsKey(playerId)) return;

            var questData = playerQuests[playerId];
            var completedQuests = questData.ActiveQuests.Where(q => 
                !q.IsCompleted && q.Objectives.All(o => o.IsCompleted)).ToList();

            foreach (var quest in completedQuests)
            {
                CompleteQuest(playerId, quest);
            }
        }

        private void CompleteQuest(ulong playerId, ActiveQuest quest)
        {
            quest.IsCompleted = true;
            quest.CompletionTime = Time.time;

            var questData = playerQuests[playerId];
            questData.CompletedQuests.Add(quest.QuestId);
            questData.TotalQuestsCompleted++;

            // Добавляем кулдаун
            var questTemplate = availableQuests[quest.QuestId];
            if (questTemplate.Cooldown > 0)
            {
                questData.QuestCooldowns[quest.QuestId] = Time.time;
            }

            // Выдаем награды
            GiveQuestRewards(playerId, questTemplate);

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=green>Квест '{quest.QuestName}' завершен!</color>");
                player.ChatMessage("<color=yellow>Получены награды:</color>");
                
                foreach (var reward in questTemplate.Rewards)
                {
                    player.ChatMessage($"<color=cyan>{reward.Description}</color>");
                }
            }

            SaveData();
        }

        private void GiveQuestRewards(ulong playerId, Quest quest)
        {
            var questData = playerQuests[playerId];

            foreach (var reward in quest.Rewards)
            {
                switch (reward.Type)
                {
                    case "currency":
                        questData.TotalCurrencyEarned += reward.Value;
                        // Здесь должна быть логика выдачи валюты
                        break;
                    case "experience":
                        questData.TotalExperienceEarned += reward.Value;
                        // Здесь должна быть логика выдачи опыта
                        break;
                    case "item":
                        // Здесь должна быть логика выдачи предметов
                        break;
                    case "reputation":
                        // Здесь должна быть логика выдачи репутации
                        break;
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("quest")]
        private void QuestCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableQuests)
            {
                player.ChatMessage("Система квестов отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowQuestHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowActiveQuests(player);
                    break;
                case "available":
                    ShowAvailableQuests(player);
                    break;
                case "completed":
                    ShowCompletedQuests(player);
                    break;
                case "start":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /quest start <id_квеста>");
                        return;
                    }
                    StartQuestCommand(player, args[1]);
                    break;
                case "abandon":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /quest abandon <id_квеста>");
                        return;
                    }
                    AbandonQuestCommand(player, args[1]);
                    break;
                case "info":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /quest info <id_квеста>");
                        return;
                    }
                    ShowQuestInfo(player, args[1]);
                    break;
                case "stats":
                    ShowQuestStats(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowQuestHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ КВЕСТОВ ===</color>");
            player.ChatMessage("/quest list - активные квесты");
            player.ChatMessage("/quest available - доступные квесты");
            player.ChatMessage("/quest completed - завершенные квесты");
            player.ChatMessage("/quest start <id> - начать квест");
            player.ChatMessage("/quest abandon <id> - бросить квест");
            player.ChatMessage("/quest info <id> - информация о квесте");
            player.ChatMessage("/quest stats - статистика");
        }

        private void ShowActiveQuests(BasePlayer player)
        {
            if (!playerQuests.ContainsKey(player.userID))
            {
                InitializePlayerQuests(player);
            }

            var questData = playerQuests[player.userID];
            var activeQuests = questData.ActiveQuests.Where(q => !q.IsCompleted).ToList();

            if (activeQuests.Count == 0)
            {
                player.ChatMessage("У вас нет активных квестов");
                return;
            }

            player.ChatMessage("<color=yellow>=== АКТИВНЫЕ КВЕСТЫ ===</color>");
            foreach (var quest in activeQuests)
            {
                player.ChatMessage($"<color=green>{quest.QuestName}</color>");
                
                foreach (var objective in quest.Objectives)
                {
                    var status = objective.IsCompleted ? "✓" : "○";
                    var color = objective.IsCompleted ? "green" : "white";
                    player.ChatMessage($"  <color={color}>{status} {objective.Description}</color>");
                    if (!objective.IsCompleted)
                    {
                        player.ChatMessage($"    Прогресс: {objective.CurrentAmount}/{objective.RequiredAmount}");
                    }
                }
            }
        }

        private void ShowAvailableQuests(BasePlayer player)
        {
            if (!playerQuests.ContainsKey(player.userID))
            {
                InitializePlayerQuests(player);
            }

            var questData = playerQuests[player.userID];
            var availableQuests = this.availableQuests.Values
                .Where(q => CanGiveQuest(player.userID, q))
                .ToList();

            if (availableQuests.Count == 0)
            {
                player.ChatMessage("Нет доступных квестов");
                return;
            }

            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ КВЕСТЫ ===</color>");
            foreach (var quest in availableQuests)
            {
                var difficultyColor = GetDifficultyColor(quest.Difficulty);
                player.ChatMessage($"<color={difficultyColor}>{quest.Name}</color> - {quest.Category}");
                player.ChatMessage($"  {quest.Description}");
                player.ChatMessage($"  Сложность: {quest.Difficulty}");
                player.ChatMessage($"  ID: {quest.Id}");
            }
        }

        private void ShowCompletedQuests(BasePlayer player)
        {
            if (!playerQuests.ContainsKey(player.userID))
            {
                InitializePlayerQuests(player);
            }

            var questData = playerQuests[player.userID];
            var completedQuests = questData.CompletedQuests;

            if (completedQuests.Count == 0)
            {
                player.ChatMessage("У вас нет завершенных квестов");
                return;
            }

            player.ChatMessage("<color=yellow>=== ЗАВЕРШЕННЫЕ КВЕСТЫ ===</color>");
            foreach (var questId in completedQuests)
            {
                if (availableQuests.ContainsKey(questId))
                {
                    var quest = availableQuests[questId];
                    player.ChatMessage($"<color=green>{quest.Name}</color> - {quest.Category}");
                }
            }
        }

        private void StartQuestCommand(BasePlayer player, string questId)
        {
            if (!availableQuests.ContainsKey(questId))
            {
                player.ChatMessage($"Квест '{questId}' не найден");
                return;
            }

            var quest = availableQuests[questId];
            if (!CanGiveQuest(player.userID, quest))
            {
                player.ChatMessage("Нельзя начать этот квест");
                return;
            }

            GiveQuest(player, questId);
        }

        private void AbandonQuestCommand(BasePlayer player, string questId)
        {
            if (!playerQuests.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет активных квестов");
                return;
            }

            var questData = playerQuests[player.userID];
            var quest = questData.ActiveQuests.FirstOrDefault(q => q.QuestId == questId);

            if (quest == null)
            {
                player.ChatMessage("Квест не найден");
                return;
            }

            questData.ActiveQuests.Remove(quest);
            player.ChatMessage($"<color=red>Квест '{quest.QuestName}' брошен</color>");
            SaveData();
        }

        private void ShowQuestInfo(BasePlayer player, string questId)
        {
            if (!availableQuests.ContainsKey(questId))
            {
                player.ChatMessage($"Квест '{questId}' не найден");
                return;
            }

            var quest = availableQuests[questId];
            var difficultyColor = GetDifficultyColor(quest.Difficulty);

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О КВЕСТЕ ===</color>");
            player.ChatMessage($"<color={difficultyColor}>{quest.Name}</color>");
            player.ChatMessage($"Категория: {quest.Category}");
            player.ChatMessage($"Сложность: {quest.Difficulty}");
            player.ChatMessage($"Тип: {quest.Type}");
            player.ChatMessage($"Описание: {quest.Description}");
            
            player.ChatMessage("<color=white>Задачи:</color>");
            foreach (var objective in quest.Objectives)
            {
                player.ChatMessage($"  • {objective.Description}");
            }

            player.ChatMessage("<color=white>Награды:</color>");
            foreach (var reward in quest.Rewards)
            {
                player.ChatMessage($"  • {reward.Description}");
            }
        }

        private void ShowQuestStats(BasePlayer player)
        {
            if (!playerQuests.ContainsKey(player.userID))
            {
                InitializePlayerQuests(player);
            }

            var questData = playerQuests[player.userID];

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА КВЕСТОВ ===</color>");
            player.ChatMessage($"Завершено квестов: {questData.TotalQuestsCompleted}");
            player.ChatMessage($"Заработано опыта: {questData.TotalExperienceEarned:F0}");
            player.ChatMessage($"Заработано валюты: {questData.TotalCurrencyEarned:F0}");
            player.ChatMessage($"Активных квестов: {questData.ActiveQuests.Count(q => !q.IsCompleted)}");
        }

        private string GetDifficultyColor(QuestDifficulty difficulty)
        {
            switch (difficulty)
            {
                case QuestDifficulty.Easy: return "green";
                case QuestDifficulty.Medium: return "yellow";
                case QuestDifficulty.Hard: return "orange";
                case QuestDifficulty.Expert: return "red";
                case QuestDifficulty.Legendary: return "purple";
                default: return "white";
            }
        }
        #endregion
    }
}
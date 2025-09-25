using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Statistics System", "BULBARUST", "1.0.0")]
    [Description("Система статистики для сервера BULBARUST")]
    public class StatisticsSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableStatistics { get; set; } = true;
            public bool EnablePlayerStats { get; set; } = true;
            public bool EnableServerStats { get; set; } = true;
            public bool EnableLeaderboards { get; set; } = true;
            public bool EnableAchievements { get; set; } = true;
            public float StatsUpdateInterval { get; set; } = 60f; // 1 минута
            public int LeaderboardSize { get; set; } = 10;
            public List<Statistic> Statistics { get; set; } = new List<Statistic>();
            public List<Achievement> Achievements { get; set; } = new List<Achievement>();
        }

        private class Statistic
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Category { get; set; } = "Общее";
            public StatisticType Type { get; set; }
            public bool IsPublic { get; set; } = true;
            public bool IsTracked { get; set; } = true;
            public float DefaultValue { get; set; } = 0f;
        }

        private class Achievement
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Category { get; set; } = "Общее";
            public List<AchievementRequirement> Requirements { get; set; } = new List<AchievementRequirement>();
            public List<string> Rewards { get; set; } = new List<string>();
            public bool IsHidden { get; set; } = false;
            public int Points { get; set; } = 10;
        }

        private class AchievementRequirement
        {
            public string StatisticId { get; set; }
            public float RequiredValue { get; set; }
            public ComparisonType Comparison { get; set; } = ComparisonType.GreaterOrEqual;
        }

        private enum StatisticType
        {
            Counter,
            Timer,
            Score,
            Percentage
        }

        private enum ComparisonType
        {
            Greater,
            GreaterOrEqual,
            Equal,
            Less,
            LessOrEqual
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем статистики
            config.Statistics.AddRange(new List<Statistic>
            {
                new Statistic
                {
                    Id = "playtime",
                    Name = "Время игры",
                    Description = "Общее время игры на сервере",
                    Category = "Основное",
                    Type = StatisticType.Timer
                },
                new Statistic
                {
                    Id = "kills",
                    Name = "Убийства",
                    Description = "Количество убийств игроков",
                    Category = "PvP",
                    Type = StatisticType.Counter
                },
                new Statistic
                {
                    Id = "deaths",
                    Name = "Смерти",
                    Description = "Количество смертей",
                    Category = "PvP",
                    Type = StatisticType.Counter
                },
                new Statistic
                {
                    Id = "resources_gathered",
                    Name = "Добыто ресурсов",
                    Description = "Общее количество добытых ресурсов",
                    Category = "Ресурсы",
                    Type = StatisticType.Counter
                },
                new Statistic
                {
                    Id = "buildings_built",
                    Name = "Построено зданий",
                    Description = "Количество построенных сооружений",
                    Category = "Строительство",
                    Type = StatisticType.Counter
                },
                new Statistic
                {
                    Id = "distance_traveled",
                    Name = "Пройдено расстояния",
                    Description = "Общее расстояние в метрах",
                    Category = "Исследование",
                    Type = StatisticType.Counter
                },
                new Statistic
                {
                    Id = "money_earned",
                    Name = "Заработано денег",
                    Description = "Общая сумма заработанных денег",
                    Category = "Экономика",
                    Type = StatisticType.Counter
                },
                new Statistic
                {
                    Id = "quests_completed",
                    Name = "Выполнено квестов",
                    Description = "Количество завершенных квестов",
                    Category = "Квесты",
                    Type = StatisticType.Counter
                }
            });

            // Добавляем достижения
            config.Achievements.AddRange(new List<Achievement>
            {
                new Achievement
                {
                    Id = "first_kill",
                    Name = "Первая кровь",
                    Description = "Убейте первого игрока",
                    Category = "PvP",
                    Requirements = new List<AchievementRequirement>
                    {
                        new AchievementRequirement { StatisticId = "kills", RequiredValue = 1, Comparison = ComparisonType.GreaterOrEqual }
                    },
                    Rewards = new List<string> { "title.first_blood", "currency.100" },
                    Points = 10
                },
                new Achievement
                {
                    Id = "veteran",
                    Name = "Ветеран",
                    Description = "Проведите 100 часов на сервере",
                    Category = "Время",
                    Requirements = new List<AchievementRequirement>
                    {
                        new AchievementRequirement { StatisticId = "playtime", RequiredValue = 360000f, Comparison = ComparisonType.GreaterOrEqual }
                    },
                    Rewards = new List<string> { "title.veteran", "currency.1000" },
                    Points = 50
                },
                new Achievement
                {
                    Id = "killer",
                    Name = "Убийца",
                    Description = "Убейте 100 игроков",
                    Category = "PvP",
                    Requirements = new List<AchievementRequirement>
                    {
                        new AchievementRequirement { StatisticId = "kills", RequiredValue = 100, Comparison = ComparisonType.GreaterOrEqual }
                    },
                    Rewards = new List<string> { "title.killer", "currency.500" },
                    Points = 25
                },
                new Achievement
                {
                    Id = "builder",
                    Name = "Строитель",
                    Description = "Постройте 1000 сооружений",
                    Category = "Строительство",
                    Requirements = new List<AchievementRequirement>
                    {
                        new AchievementRequirement { StatisticId = "buildings_built", RequiredValue = 1000, Comparison = ComparisonType.GreaterOrEqual }
                    },
                    Rewards = new List<string> { "title.builder", "currency.2000" },
                    Points = 30
                },
                new Achievement
                {
                    Id = "explorer",
                    Name = "Исследователь",
                    Description = "Пройдите 100 км",
                    Category = "Исследование",
                    Requirements = new List<AchievementRequirement>
                    {
                        new AchievementRequirement { StatisticId = "distance_traveled", RequiredValue = 100000f, Comparison = ComparisonType.GreaterOrEqual }
                    },
                    Rewards = new List<string> { "title.explorer", "currency.1500" },
                    Points = 20
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
        private Dictionary<ulong, PlayerStatistics> playerStats = new Dictionary<ulong, PlayerStatistics>();
        private Dictionary<ulong, List<string>> playerAchievements = new Dictionary<ulong, List<string>>();
        private Dictionary<string, float> serverStats = new Dictionary<string, float>();
        private Dictionary<ulong, float> lastStatsUpdate = new Dictionary<ulong, float>();

        private class PlayerStatistics
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public Dictionary<string, float> Statistics { get; set; } = new Dictionary<string, float>();
            public Dictionary<string, float> SessionStats { get; set; } = new Dictionary<string, float>();
            public float TotalPlayTime { get; set; } = 0f;
            public float SessionStartTime { get; set; } = 0f;
            public float LastUpdate { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система статистики BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableStatistics)
            {
                InitializeServerStats();
                timer.Every(config.StatsUpdateInterval, UpdateStatistics);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableStatistics)
            {
                InitializePlayerStats(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (config.EnableStatistics)
            {
                UpdatePlayerSessionStats(player);
                SavePlayerStats(player.userID);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableStatistics) return;

            var player = entity as BasePlayer;
            var killer = info.InitiatorPlayer;

            if (player != null && killer != null && player != killer)
            {
                // Обновляем статистику убийств и смертей
                UpdateStatistic(killer.userID, "kills", 1f);
                UpdateStatistic(player.userID, "deaths", 1f);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableStatistics) return;

            var player = plan.GetOwnerPlayer();
            if (player != null)
            {
                UpdateStatistic(player.userID, "buildings_built", 1f);
            }
        }

        void OnItemAdded(ItemContainer container, Item item)
        {
            if (!config.EnableStatistics) return;

            var player = container.playerOwner;
            if (player != null)
            {
                UpdateStatistic(player.userID, "resources_gathered", item.amount);
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerStats = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerStatistics>>("player_stats") ?? new Dictionary<ulong, PlayerStatistics>();
            playerAchievements = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("player_achievements") ?? new Dictionary<ulong, List<string>>();
            serverStats = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, float>>("server_stats") ?? new Dictionary<string, float>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_stats", playerStats);
            Interface.Oxide.DataFileSystem.WriteObject("player_achievements", playerAchievements);
            Interface.Oxide.DataFileSystem.WriteObject("server_stats", serverStats);
        }

        private void SavePlayerStats(ulong playerId)
        {
            if (playerStats.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"stats_{playerId}", playerStats[playerId]);
            }
        }

        private void InitializeServerStats()
        {
            // Инициализируем серверную статистику
            if (!serverStats.ContainsKey("total_players"))
            {
                serverStats["total_players"] = 0f;
            }
            if (!serverStats.ContainsKey("total_playtime"))
            {
                serverStats["total_playtime"] = 0f;
            }
            if (!serverStats.ContainsKey("total_kills"))
            {
                serverStats["total_kills"] = 0f;
            }
        }

        private void InitializePlayerStats(BasePlayer player)
        {
            if (!playerStats.ContainsKey(player.userID))
            {
                var stats = new PlayerStatistics
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    SessionStartTime = Time.time,
                    LastUpdate = Time.time
                };

                // Инициализируем все статистики
                foreach (var stat in config.Statistics.Where(s => s.IsTracked))
                {
                    stats.Statistics[stat.Id] = stat.DefaultValue;
                    stats.SessionStats[stat.Id] = 0f;
                }

                playerStats[player.userID] = stats;
                SaveData();
            }
            else
            {
                // Обновляем данные
                var stats = playerStats[player.userID];
                stats.PlayerName = player.displayName;
                stats.SessionStartTime = Time.time;
                stats.LastUpdate = Time.time;
            }
        }

        private void UpdateStatistics()
        {
            var currentTime = Time.time;

            // Обновляем время игры для всех онлайн игроков
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (playerStats.ContainsKey(player.userID))
                {
                    var stats = playerStats[player.userID];
                    var sessionTime = currentTime - stats.SessionStartTime;
                    stats.TotalPlayTime += sessionTime;
                    stats.SessionStartTime = currentTime;
                    stats.LastUpdate = currentTime;

                    // Обновляем статистику времени игры
                    UpdateStatistic(player.userID, "playtime", sessionTime);
                }
            }

            // Обновляем серверную статистику
            serverStats["total_players"] = BasePlayer.activePlayerList.Count;
            serverStats["total_playtime"] = playerStats.Values.Sum(s => s.TotalPlayTime);

            SaveData();
        }

        private void UpdatePlayerSessionStats(BasePlayer player)
        {
            if (!playerStats.ContainsKey(player.userID)) return;

            var stats = playerStats[player.userID];
            var sessionTime = Time.time - stats.SessionStartTime;
            stats.TotalPlayTime += sessionTime;

            // Обновляем статистику времени игры
            UpdateStatistic(player.userID, "playtime", sessionTime);
        }

        private void UpdateStatistic(ulong playerId, string statId, float value)
        {
            if (!config.EnableStatistics) return;

            var stat = config.Statistics.FirstOrDefault(s => s.Id == statId);
            if (stat == null || !stat.IsTracked) return;

            if (!playerStats.ContainsKey(playerId))
            {
                InitializePlayerStats(BasePlayer.FindByID(playerId));
            }

            var stats = playerStats[playerId];
            
            // Обновляем общую статистику
            if (!stats.Statistics.ContainsKey(statId))
            {
                stats.Statistics[statId] = stat.DefaultValue;
            }
            stats.Statistics[statId] += value;

            // Обновляем сессионную статистику
            if (!stats.SessionStats.ContainsKey(statId))
            {
                stats.SessionStats[statId] = 0f;
            }
            stats.SessionStats[statId] += value;

            // Обновляем серверную статистику
            if (serverStats.ContainsKey($"total_{statId}"))
            {
                serverStats[$"total_{statId}"] += value;
            }

            // Проверяем достижения
            CheckAchievements(playerId);

            SaveData();
        }

        private void CheckAchievements(ulong playerId)
        {
            if (!config.EnableAchievements) return;

            var stats = playerStats[playerId];
            var achievements = playerAchievements.ContainsKey(playerId) ? playerAchievements[playerId] : new List<string>();

            foreach (var achievement in config.Achievements)
            {
                if (achievements.Contains(achievement.Id)) continue;

                bool requirementsMet = true;
                foreach (var requirement in achievement.Requirements)
                {
                    var currentValue = stats.Statistics.ContainsKey(requirement.StatisticId) ? 
                        stats.Statistics[requirement.StatisticId] : 0f;

                    bool met = false;
                    switch (requirement.Comparison)
                    {
                        case ComparisonType.Greater:
                            met = currentValue > requirement.RequiredValue;
                            break;
                        case ComparisonType.GreaterOrEqual:
                            met = currentValue >= requirement.RequiredValue;
                            break;
                        case ComparisonType.Equal:
                            met = Mathf.Approximately(currentValue, requirement.RequiredValue);
                            break;
                        case ComparisonType.Less:
                            met = currentValue < requirement.RequiredValue;
                            break;
                        case ComparisonType.LessOrEqual:
                            met = currentValue <= requirement.RequiredValue;
                            break;
                    }

                    if (!met)
                    {
                        requirementsMet = false;
                        break;
                    }
                }

                if (requirementsMet)
                {
                    GiveAchievement(playerId, achievement);
                }
            }
        }

        private void GiveAchievement(ulong playerId, Achievement achievement)
        {
            if (!playerAchievements.ContainsKey(playerId))
            {
                playerAchievements[playerId] = new List<string>();
            }

            playerAchievements[playerId].Add(achievement.Id);

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=green>🎉 ДОСТИЖЕНИЕ РАЗБЛОКИРОВАНО! 🎉</color>");
                player.ChatMessage($"<color=yellow>{achievement.Name}</color>");
                player.ChatMessage($"<color=white>{achievement.Description}</color>");
                player.ChatMessage($"<color=cyan>Очки: {achievement.Points}</color>");
            }

            // Выдаем награды
            foreach (var reward in achievement.Rewards)
            {
                ProcessAchievementReward(playerId, reward);
            }

            SaveData();
        }

        private void ProcessAchievementReward(ulong playerId, string reward)
        {
            // Здесь должна быть логика выдачи наград за достижения
            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=cyan>Награда: {reward}</color>");
            }
        }

        private List<PlayerStatistics> GetLeaderboard(string statId, int count = 10)
        {
            return playerStats.Values
                .Where(s => s.Statistics.ContainsKey(statId))
                .OrderByDescending(s => s.Statistics[statId])
                .Take(count)
                .ToList();
        }

        private int GetPlayerRank(ulong playerId, string statId)
        {
            var playerValue = playerStats.ContainsKey(playerId) && playerStats[playerId].Statistics.ContainsKey(statId) ?
                playerStats[playerId].Statistics[statId] : 0f;

            var rank = 1;
            foreach (var stats in playerStats.Values)
            {
                if (stats.PlayerId == playerId) continue;
                if (stats.Statistics.ContainsKey(statId) && stats.Statistics[statId] > playerValue)
                {
                    rank++;
                }
            }

            return rank;
        }
        #endregion

        #region Commands
        [ChatCommand("stats")]
        private void StatsCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableStatistics)
            {
                player.ChatMessage("Система статистики отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowPlayerStats(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "leaderboard":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /stats leaderboard <статистика>");
                        return;
                    }
                    ShowLeaderboard(player, args[1]);
                    break;
                case "achievements":
                    ShowPlayerAchievements(player);
                    break;
                case "server":
                    ShowServerStats(player);
                    break;
                case "compare":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /stats compare <игрок>");
                        return;
                    }
                    CompareStats(player, args[1]);
                    break;
                case "session":
                    ShowSessionStats(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowPlayerStats(BasePlayer player)
        {
            if (!playerStats.ContainsKey(player.userID))
            {
                InitializePlayerStats(player);
            }

            var stats = playerStats[player.userID];

            player.ChatMessage("<color=yellow>=== ВАША СТАТИСТИКА ===</color>");
            player.ChatMessage($"<color=white>Игрок: {stats.PlayerName}</color>");
            player.ChatMessage($"<color=white>Время игры: {TimeSpan.FromSeconds(stats.TotalPlayTime).TotalHours:F1} часов</color>");

            // Показываем основные статистики
            var mainStats = new[] { "kills", "deaths", "resources_gathered", "buildings_built", "money_earned", "quests_completed" };
            foreach (var statId in mainStats)
            {
                if (stats.Statistics.ContainsKey(statId))
                {
                    var stat = config.Statistics.FirstOrDefault(s => s.Id == statId);
                    var value = stats.Statistics[statId];
                    var rank = GetPlayerRank(player.userID, statId);
                    
                    player.ChatMessage($"<color=cyan>{stat?.Name}: {value:F0}</color> (место: {rank})");
                }
            }
        }

        private void ShowLeaderboard(BasePlayer player, string statId)
        {
            var stat = config.Statistics.FirstOrDefault(s => s.Id == statId);
            if (stat == null)
            {
                player.ChatMessage($"Статистика '{statId}' не найдена");
                return;
            }

            var leaderboard = GetLeaderboard(statId, config.LeaderboardSize);

            if (leaderboard.Count == 0)
            {
                player.ChatMessage("Нет данных для этой статистики");
                return;
            }

            player.ChatMessage($"<color=yellow>=== ТОП {config.LeaderboardSize} ПО {stat.Name.ToUpper()} ===</color>");
            for (int i = 0; i < leaderboard.Count; i++)
            {
                var stats = leaderboard[i];
                var value = stats.Statistics[statId];
                var position = i + 1;
                var color = position <= 3 ? (position == 1 ? "gold" : position == 2 ? "silver" : "bronze") : "white";
                
                player.ChatMessage($"<color={color}>{position}. {stats.PlayerName}: {value:F0}</color>");
            }
        }

        private void ShowPlayerAchievements(BasePlayer player)
        {
            if (!playerAchievements.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет достижений");
                return;
            }

            var achievements = playerAchievements[player.userID];
            var totalPoints = achievements.Sum(a => config.Achievements.FirstOrDefault(ach => ach.Id == a)?.Points ?? 0);

            player.ChatMessage("<color=yellow>=== ВАШИ ДОСТИЖЕНИЯ ===</color>");
            player.ChatMessage($"<color=white>Всего достижений: {achievements.Count}</color>");
            player.ChatMessage($"<color=white>Общие очки: {totalPoints}</color>");

            foreach (var achievementId in achievements)
            {
                var achievement = config.Achievements.FirstOrDefault(a => a.Id == achievementId);
                if (achievement != null)
                {
                    player.ChatMessage($"<color=green>✓ {achievement.Name}</color>");
                    player.ChatMessage($"  {achievement.Description}");
                    player.ChatMessage($"  Очки: {achievement.Points}");
                }
            }
        }

        private void ShowServerStats(BasePlayer player)
        {
            if (!config.EnableServerStats)
            {
                player.ChatMessage("Серверная статистика отключена");
                return;
            }

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА СЕРВЕРА ===</color>");
            player.ChatMessage($"<color=white>Игроков онлайн: {BasePlayer.activePlayerList.Count}</color>");
            player.ChatMessage($"<color=white>Всего игроков: {playerStats.Count}</color>");
            player.ChatMessage($"<color=white>Общее время игры: {TimeSpan.FromSeconds(serverStats.GetValueOrDefault("total_playtime", 0f)).TotalHours:F1} часов</color>");
            player.ChatMessage($"<color=white>Всего убийств: {serverStats.GetValueOrDefault("total_kills", 0f):F0}</color>");
        }

        private void CompareStats(BasePlayer player, string targetName)
        {
            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (!playerStats.ContainsKey(target.userID))
            {
                player.ChatMessage($"У игрока '{targetName}' нет статистики");
                return;
            }

            var playerStatsData = playerStats[player.userID];
            var targetStatsData = playerStats[target.userID];

            player.ChatMessage($"<color=yellow>=== СРАВНЕНИЕ С {target.displayName} ===</color>");

            var compareStats = new[] { "kills", "deaths", "resources_gathered", "buildings_built", "money_earned" };
            foreach (var statId in compareStats)
            {
                var stat = config.Statistics.FirstOrDefault(s => s.Id == statId);
                if (stat == null) continue;

                var playerValue = playerStatsData.Statistics.GetValueOrDefault(statId, 0f);
                var targetValue = targetStatsData.Statistics.GetValueOrDefault(statId, 0f);

                var color = playerValue > targetValue ? "green" : playerValue < targetValue ? "red" : "white";
                var symbol = playerValue > targetValue ? ">" : playerValue < targetValue ? "<" : "=";

                player.ChatMessage($"<color={color}>{stat.Name}: {playerValue:F0} {symbol} {targetValue:F0}</color>");
            }
        }

        private void ShowSessionStats(BasePlayer player)
        {
            if (!playerStats.ContainsKey(player.userID))
            {
                InitializePlayerStats(player);
            }

            var stats = playerStats[player.userID];
            var sessionTime = Time.time - stats.SessionStartTime;

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА СЕССИИ ===</color>");
            player.ChatMessage($"<color=white>Время сессии: {TimeSpan.FromSeconds(sessionTime).TotalHours:F1} часов</color>");

            foreach (var sessionStat in stats.SessionStats)
            {
                if (sessionStat.Value > 0)
                {
                    var stat = config.Statistics.FirstOrDefault(s => s.Id == sessionStat.Key);
                    if (stat != null)
                    {
                        player.ChatMessage($"<color=cyan>{stat.Name}: {sessionStat.Value:F0}</color>");
                    }
                }
            }
        }
        #endregion
    }
}
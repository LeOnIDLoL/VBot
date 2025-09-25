using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Reputation System", "BULBARUST", "1.0.0")]
    [Description("Система репутации для сервера BULBARUST")]
    public class ReputationSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableReputation { get; set; } = true;
            public bool EnableVoting { get; set; } = true;
            public bool EnableReputationRewards { get; set; } = true;
            public bool EnableReputationDecay { get; set; } = true;
            public float StartingReputation { get; set; } = 0f;
            public float MaxReputation { get; set; } = 1000f;
            public float MinReputation { get; set; } = -1000f;
            public float VoteCooldown { get; set; } = 3600f; // 1 час
            public float DecayRate { get; set; } = 1f; // 1 репутация в день
            public float DecayInterval { get; set; } = 86400f; // 24 часа
            public List<ReputationAction> Actions { get; set; } = new List<ReputationAction>();
            public List<ReputationReward> Rewards { get; set; } = new List<ReputationReward>();
            public List<ReputationTitle> Titles { get; set; } = new List<ReputationTitle>();
        }

        private class ReputationAction
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float ReputationChange { get; set; }
            public string Category { get; set; } = "Общее";
            public bool RequiresTarget { get; set; } = false;
            public List<string> RequiredItems { get; set; } = new List<string>();
        }

        private class ReputationReward
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float RequiredReputation { get; set; }
            public List<string> Rewards { get; set; } = new List<string>();
            public bool IsOneTime { get; set; } = false;
        }

        private class ReputationTitle
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public float RequiredReputation { get; set; }
            public string Color { get; set; } = "white";
            public List<string> Permissions { get; set; } = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем действия репутации
            config.Actions.AddRange(new List<ReputationAction>
            {
                new ReputationAction
                {
                    Name = "Убийство игрока",
                    Description = "Убийство другого игрока",
                    ReputationChange = -10f,
                    Category = "PvP",
                    RequiresTarget = true
                },
                new ReputationAction
                {
                    Name = "Помощь новичку",
                    Description = "Помощь новому игроку",
                    ReputationChange = 5f,
                    Category = "Помощь",
                    RequiresTarget = true
                },
                new ReputationAction
                {
                    Name = "Строительство",
                    Description = "Строительство полезных сооружений",
                    ReputationChange = 2f,
                    Category = "Строительство"
                },
                new ReputationAction
                {
                    Name = "Торговля",
                    Description = "Честная торговля",
                    ReputationChange = 3f,
                    Category = "Торговля"
                },
                new ReputationAction
                {
                    Name = "Участие в событиях",
                    Description = "Активное участие в серверных событиях",
                    ReputationChange = 8f,
                    Category = "События"
                },
                new ReputationAction
                {
                    Name = "Нарушение правил",
                    Description = "Нарушение правил сервера",
                    ReputationChange = -20f,
                    Category = "Нарушения"
                }
            });

            // Добавляем награды
            config.Rewards.AddRange(new List<ReputationReward>
            {
                new ReputationReward
                {
                    Name = "Новичок",
                    Description = "Базовые привилегии",
                    RequiredReputation = 50f,
                    Rewards = new List<string> { "kit.starter", "teleport.basic" }
                },
                new ReputationReward
                {
                    Name = "Друг",
                    Description = "Дополнительные возможности",
                    RequiredReputation = 150f,
                    Rewards = new List<string> { "kit.friend", "teleport.advanced", "building.extra" }
                },
                new ReputationReward
                {
                    Name = "Ветеран",
                    Description = "VIP привилегии",
                    RequiredReputation = 300f,
                    Rewards = new List<string> { "kit.veteran", "teleport.all", "building.unlimited", "economy.bonus" }
                },
                new ReputationReward
                {
                    Name = "Легенда",
                    Description = "Максимальные привилегии",
                    RequiredReputation = 500f,
                    Rewards = new List<string> { "kit.legend", "admin.helper", "economy.vip" }
                }
            });

            // Добавляем титулы
            config.Titles.AddRange(new List<ReputationTitle>
            {
                new ReputationTitle
                {
                    Name = "Новичок",
                    DisplayName = "Новичок",
                    RequiredReputation = 0f,
                    Color = "white"
                },
                new ReputationTitle
                {
                    Name = "Друг",
                    DisplayName = "Друг",
                    RequiredReputation = 100f,
                    Color = "green"
                },
                new ReputationTitle
                {
                    Name = "Ветеран",
                    DisplayName = "Ветеран",
                    RequiredReputation = 250f,
                    Color = "blue"
                },
                new ReputationTitle
                {
                    Name = "Эксперт",
                    DisplayName = "Эксперт",
                    RequiredReputation = 400f,
                    Color = "purple"
                },
                new ReputationTitle
                {
                    Name = "Легенда",
                    DisplayName = "Легенда",
                    RequiredReputation = 600f,
                    Color = "gold"
                },
                new ReputationTitle
                {
                    Name = "Мастер",
                    DisplayName = "Мастер",
                    RequiredReputation = 800f,
                    Color = "red"
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
        private Dictionary<ulong, PlayerReputation> playerReputation = new Dictionary<ulong, PlayerReputation>();
        private Dictionary<ulong, float> lastVote = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastDecay = new Dictionary<ulong, float>();
        private Dictionary<ulong, List<string>> claimedRewards = new Dictionary<ulong, List<string>>();

        private class PlayerReputation
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public float Reputation { get; set; } = 0f;
            public string CurrentTitle { get; set; } = "Новичок";
            public float TotalEarned { get; set; } = 0f;
            public float TotalLost { get; set; } = 0f;
            public List<ReputationHistory> History { get; set; } = new List<ReputationHistory>();
            public Dictionary<string, int> ActionCounts { get; set; } = new Dictionary<string, int>();
            public float LastUpdate { get; set; }
        }

        private class ReputationHistory
        {
            public string Action { get; set; }
            public float Change { get; set; }
            public string Reason { get; set; }
            public float Timestamp { get; set; }
            public ulong? SourcePlayer { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система репутации BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableReputation)
            {
                timer.Every(3600f, ProcessReputationDecay); // Каждый час
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableReputation)
            {
                InitializePlayerReputation(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Сохраняем данные игрока
            SavePlayerReputation(player.userID);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableReputation) return;

            var player = entity as BasePlayer;
            var killer = info.InitiatorPlayer;
            
            if (player != null && killer != null && player != killer)
            {
                // Обрабатываем убийство игрока
                ProcessReputationAction(killer, "Убийство игрока", player.userID, player.displayName);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableReputation) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            // Обрабатываем строительство
            ProcessReputationAction(player, "Строительство");
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerReputation = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerReputation>>("player_reputation") ?? new Dictionary<ulong, PlayerReputation>();
            claimedRewards = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("claimed_rewards") ?? new Dictionary<ulong, List<string>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_reputation", playerReputation);
            Interface.Oxide.DataFileSystem.WriteObject("claimed_rewards", claimedRewards);
        }

        private void SavePlayerReputation(ulong playerId)
        {
            if (playerReputation.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"reputation_{playerId}", playerReputation[playerId]);
            }
        }

        private void InitializePlayerReputation(BasePlayer player)
        {
            if (!playerReputation.ContainsKey(player.userID))
            {
                var reputation = new PlayerReputation
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    Reputation = config.StartingReputation,
                    CurrentTitle = "Новичок",
                    LastUpdate = Time.time
                };

                playerReputation[player.userID] = reputation;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerReputation[player.userID].PlayerName = player.displayName;
            }

            // Проверяем и обновляем титул
            UpdatePlayerTitle(player.userID);
        }

        private void ProcessReputationAction(BasePlayer player, string actionName, ulong? targetPlayerId = null, string targetPlayerName = null)
        {
            var action = config.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null) return;

            if (!playerReputation.ContainsKey(player.userID))
            {
                InitializePlayerReputation(player);
            }

            var reputation = playerReputation[player.userID];
            var oldReputation = reputation.Reputation;
            var change = action.ReputationChange;

            // Применяем изменение репутации
            reputation.Reputation = Mathf.Clamp(reputation.Reputation + change, config.MinReputation, config.MaxReputation);
            reputation.LastUpdate = Time.time;

            // Обновляем статистику
            if (change > 0)
            {
                reputation.TotalEarned += change;
            }
            else
            {
                reputation.TotalLost += Mathf.Abs(change);
            }

            // Добавляем в историю
            var historyEntry = new ReputationHistory
            {
                Action = actionName,
                Change = change,
                Reason = action.Description,
                Timestamp = Time.time,
                SourcePlayer = targetPlayerId
            };

            reputation.History.Add(historyEntry);

            // Обновляем счетчик действий
            if (!reputation.ActionCounts.ContainsKey(actionName))
            {
                reputation.ActionCounts[actionName] = 0;
            }
            reputation.ActionCounts[actionName]++;

            // Проверяем изменение титула
            var oldTitle = reputation.CurrentTitle;
            UpdatePlayerTitle(player.userID);
            var newTitle = reputation.CurrentTitle;

            // Уведомляем игрока
            var changeText = change > 0 ? $"+{change:F1}" : change.ToString("F1");
            player.ChatMessage($"<color=yellow>Репутация: {changeText} ({actionName})</color>");
            player.ChatMessage($"<color=white>Текущая репутация: {reputation.Reputation:F1}</color>");

            if (oldTitle != newTitle)
            {
                player.ChatMessage($"<color=green>Новый титул: {newTitle}</color>");
            }

            // Проверяем награды
            CheckReputationRewards(player);

            SaveData();
        }

        private void UpdatePlayerTitle(ulong playerId)
        {
            if (!playerReputation.ContainsKey(playerId)) return;

            var reputation = playerReputation[playerId];
            var newTitle = GetPlayerTitle(reputation.Reputation);

            if (reputation.CurrentTitle != newTitle)
            {
                reputation.CurrentTitle = newTitle;
                
                var player = BasePlayer.FindByID(playerId);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage($"<color=green>Поздравляем! Вы получили титул: {newTitle}</color>");
                }
            }
        }

        private string GetPlayerTitle(float reputation)
        {
            var title = config.Titles
                .Where(t => reputation >= t.RequiredReputation)
                .OrderByDescending(t => t.RequiredReputation)
                .FirstOrDefault();

            return title?.Name ?? "Новичок";
        }

        private void CheckReputationRewards(BasePlayer player)
        {
            if (!config.EnableReputationRewards) return;

            var reputation = playerReputation[player.userID];
            var availableRewards = config.Rewards
                .Where(r => reputation.Reputation >= r.RequiredReputation)
                .Where(r => !HasClaimedReward(player.userID, r.Name))
                .ToList();

            foreach (var reward in availableRewards)
            {
                GiveReputationReward(player, reward);
            }
        }

        private bool HasClaimedReward(ulong playerId, string rewardName)
        {
            if (!claimedRewards.ContainsKey(playerId))
            {
                claimedRewards[playerId] = new List<string>();
            }
            return claimedRewards[playerId].Contains(rewardName);
        }

        private void GiveReputationReward(BasePlayer player, ReputationReward reward)
        {
            if (!claimedRewards.ContainsKey(player.userID))
            {
                claimedRewards[player.userID] = new List<string>();
            }

            claimedRewards[player.userID].Add(reward.Name);

            player.ChatMessage($"<color=green>Получена награда: {reward.Name}</color>");
            player.ChatMessage($"<color=white>{reward.Description}</color>");

            // Здесь должна быть логика выдачи наград
            foreach (var rewardItem in reward.Rewards)
            {
                // Обрабатываем различные типы наград
                ProcessRewardItem(player, rewardItem);
            }

            SaveData();
        }

        private void ProcessRewardItem(BasePlayer player, string rewardItem)
        {
            // Здесь должна быть логика обработки различных типов наград
            // Например, киты, права доступа, предметы и т.д.
            player.ChatMessage($"<color=cyan>Награда: {rewardItem}</color>");
        }

        private void ProcessReputationDecay()
        {
            if (!config.EnableReputationDecay) return;

            var currentTime = Time.time;

            foreach (var reputation in playerReputation.Values)
            {
                if (currentTime - reputation.LastUpdate >= config.DecayInterval)
                {
                    var decayAmount = config.DecayRate;
                    reputation.Reputation = Mathf.Clamp(reputation.Reputation - decayAmount, config.MinReputation, config.MaxReputation);
                    reputation.LastUpdate = currentTime;

                    var player = BasePlayer.FindByID(reputation.PlayerId);
                    if (player != null && player.IsConnected)
                    {
                        player.ChatMessage($"<color=yellow>Репутация уменьшена на {decayAmount:F1} (деградация)</color>");
                    }

                    UpdatePlayerTitle(reputation.PlayerId);
                }
            }

            SaveData();
        }

        private bool CanVote(ulong playerId)
        {
            if (!config.EnableVoting) return false;
            if (!lastVote.ContainsKey(playerId)) return true;

            var currentTime = Time.time;
            return currentTime - lastVote[playerId] >= config.VoteCooldown;
        }

        private void ProcessVote(BasePlayer voter, BasePlayer target, bool isPositive)
        {
            if (!CanVote(voter.userID))
            {
                var timeLeft = config.VoteCooldown - (Time.time - lastVote[voter.userID]);
                voter.ChatMessage($"Осталось ждать: {Mathf.CeilToInt(timeLeft / 60)} минут");
                return;
            }

            if (voter.userID == target.userID)
            {
                voter.ChatMessage("Нельзя голосовать за себя");
                return;
            }

            var change = isPositive ? 5f : -5f;
            var actionName = isPositive ? "Положительный голос" : "Отрицательный голос";

            ProcessReputationAction(target, actionName, voter.userID, voter.displayName);

            lastVote[voter.userID] = Time.time;
            voter.ChatMessage($"<color=green>Голос за {target.displayName} учтен!</color>");
        }
        #endregion

        #region Commands
        [ChatCommand("rep")]
        private void RepCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableReputation)
            {
                player.ChatMessage("Система репутации отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowReputationInfo(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "info":
                    ShowReputationInfo(player, args.Length > 1 ? args[1] : null);
                    break;
                case "top":
                    ShowTopReputation(player);
                    break;
                case "history":
                    ShowReputationHistory(player);
                    break;
                case "vote":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /rep vote <игрок> <+/- >");
                        return;
                    }
                    VoteCommand(player, args[1], args[2]);
                    break;
                case "rewards":
                    ShowReputationRewards(player);
                    break;
                case "title":
                    ShowPlayerTitle(player);
                    break;
            }
        }

        [ChatCommand("vote")]
        private void VoteCommand(BasePlayer player, string targetName, string voteType)
        {
            if (!config.EnableVoting)
            {
                player.ChatMessage("Система голосования отключена");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            bool isPositive = voteType == "+" || voteType.ToLower() == "plus";
            ProcessVote(player, target, isPositive);
        }
        #endregion

        #region Command Methods
        private void ShowReputationInfo(BasePlayer player, string targetName = null)
        {
            PlayerReputation reputation = null;

            if (string.IsNullOrEmpty(targetName))
            {
                if (!playerReputation.ContainsKey(player.userID))
                {
                    InitializePlayerReputation(player);
                }
                reputation = playerReputation[player.userID];
            }
            else
            {
                var target = BasePlayer.Find(targetName);
                if (target == null)
                {
                    player.ChatMessage($"Игрок '{targetName}' не найден");
                    return;
                }

                if (!playerReputation.ContainsKey(target.userID))
                {
                    InitializePlayerReputation(target);
                }
                reputation = playerReputation[target.userID];
            }

            var title = config.Titles.FirstOrDefault(t => t.Name == reputation.CurrentTitle);
            var titleColor = title?.Color ?? "white";

            player.ChatMessage($"<color=yellow>=== РЕПУТАЦИЯ ===</color>");
            player.ChatMessage($"Игрок: {reputation.PlayerName}");
            player.ChatMessage($"<color={titleColor}>Титул: {reputation.CurrentTitle}</color>");
            player.ChatMessage($"Репутация: {reputation.Reputation:F1}");
            player.ChatMessage($"Заработано: {reputation.TotalEarned:F1}");
            player.ChatMessage($"Потеряно: {reputation.TotalLost:F1}");
            player.ChatMessage($"Последнее обновление: {TimeSpan.FromSeconds(Time.time - reputation.LastUpdate).TotalHours:F1} часов назад");
        }

        private void ShowTopReputation(BasePlayer player)
        {
            var topPlayers = playerReputation.Values
                .OrderByDescending(r => r.Reputation)
                .Take(10)
                .ToList();

            if (topPlayers.Count == 0)
            {
                player.ChatMessage("Нет данных о репутации");
                return;
            }

            player.ChatMessage("<color=yellow>=== ТОП РЕПУТАЦИИ ===</color>");
            for (int i = 0; i < topPlayers.Count; i++)
            {
                var rep = topPlayers[i];
                var title = config.Titles.FirstOrDefault(t => t.Name == rep.CurrentTitle);
                var titleColor = title?.Color ?? "white";
                
                player.ChatMessage($"{i + 1}. <color={titleColor}>{rep.PlayerName}</color> - {rep.Reputation:F1}");
            }
        }

        private void ShowReputationHistory(BasePlayer player)
        {
            if (!playerReputation.ContainsKey(player.userID))
            {
                InitializePlayerReputation(player);
            }

            var reputation = playerReputation[player.userID];
            var recentHistory = reputation.History
                .OrderByDescending(h => h.Timestamp)
                .Take(10)
                .ToList();

            if (recentHistory.Count == 0)
            {
                player.ChatMessage("История репутации пуста");
                return;
            }

            player.ChatMessage("<color=yellow>=== ИСТОРИЯ РЕПУТАЦИИ ===</color>");
            foreach (var entry in recentHistory)
            {
                var changeText = entry.Change > 0 ? $"+{entry.Change:F1}" : entry.Change.ToString("F1");
                var timeAgo = TimeSpan.FromSeconds(Time.time - entry.Timestamp).TotalHours;
                player.ChatMessage($"{entry.Action}: {changeText} ({timeAgo:F1}ч назад)");
            }
        }

        private void ShowReputationRewards(BasePlayer player)
        {
            if (!playerReputation.ContainsKey(player.userID))
            {
                InitializePlayerReputation(player);
            }

            var reputation = playerReputation[player.userID];
            var availableRewards = config.Rewards
                .Where(r => reputation.Reputation >= r.RequiredReputation)
                .ToList();

            if (availableRewards.Count == 0)
            {
                player.ChatMessage("Нет доступных наград");
                return;
            }

            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ НАГРАДЫ ===</color>");
            foreach (var reward in availableRewards)
            {
                var claimed = HasClaimedReward(player.userID, reward.Name);
                var status = claimed ? "Получена" : "Доступна";
                var color = claimed ? "green" : "white";
                
                player.ChatMessage($"<color={color}>{reward.Name} - {status}</color>");
                player.ChatMessage($"  {reward.Description}");
                player.ChatMessage($"  Требуется: {reward.RequiredReputation:F1} репутации");
            }
        }

        private void ShowPlayerTitle(BasePlayer player)
        {
            if (!playerReputation.ContainsKey(player.userID))
            {
                InitializePlayerReputation(player);
            }

            var reputation = playerReputation[player.userID];
            var title = config.Titles.FirstOrDefault(t => t.Name == reputation.CurrentTitle);
            
            if (title != null)
            {
                var titleColor = title.Color;
                player.ChatMessage($"<color={titleColor}>Ваш титул: {title.DisplayName}</color>");
                player.ChatMessage($"Текущая репутация: {reputation.Reputation:F1}");
                
                // Показываем следующий титул
                var nextTitle = config.Titles
                    .Where(t => t.RequiredReputation > reputation.Reputation)
                    .OrderBy(t => t.RequiredReputation)
                    .FirstOrDefault();
                
                if (nextTitle != null)
                {
                    var needed = nextTitle.RequiredReputation - reputation.Reputation;
                    player.ChatMessage($"До следующего титула '{nextTitle.DisplayName}': {needed:F1} репутации");
                }
            }
        }
        #endregion
    }
}
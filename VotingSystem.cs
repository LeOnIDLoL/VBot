using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Voting System", "BULBARUST", "1.0.0")]
    [Description("Система голосования для сервера BULBARUST")]
    public class VotingSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableVoting { get; set; } = true;
            public bool EnablePlayerVoting { get; set; } = true;
            public bool EnableAdminVoting { get; set; } = true;
            public bool EnableVoteRewards { get; set; } = true;
            public float VoteCooldown { get; set; } = 3600f; // 1 час
            public float VoteDuration { get; set; } = 300f; // 5 минут
            public int MinVotesToPass { get; set; } = 3;
            public float MinVotePercentage { get; set; } = 0.6f; // 60%
            public List<VoteReward> VoteRewards { get; set; } = new List<VoteReward>();
            public List<string> VoteSites { get; set; } = new List<string>();
        }

        private class VoteReward
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int RequiredVotes { get; set; } = 1;
            public List<string> Rewards { get; set; } = new List<string>();
            public bool IsOneTime { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем награды за голосование
            config.VoteRewards.AddRange(new List<VoteReward>
            {
                new VoteReward
                {
                    Name = "Ежедневное голосование",
                    Description = "Награда за ежедневное голосование",
                    RequiredVotes = 1,
                    Rewards = new List<string> { "kit.vote", "currency.500" },
                    IsOneTime = false
                },
                new VoteReward
                {
                    Name = "Недельное голосование",
                    Description = "Награда за неделю голосования",
                    RequiredVotes = 7,
                    Rewards = new List<string> { "kit.weekly", "currency.2000", "reputation.100" },
                    IsOneTime = false
                },
                new VoteReward
                {
                    Name = "Месячное голосование",
                    Description = "Награда за месяц голосования",
                    RequiredVotes = 30,
                    Rewards = new List<string> { "kit.monthly", "currency.10000", "reputation.500", "title.voter" },
                    IsOneTime = false
                }
            });

            // Добавляем сайты для голосования
            config.VoteSites.AddRange(new List<string>
            {
                "https://topg.org/rust-servers/",
                "https://rust-servers.net/",
                "https://rust-servers.org/",
                "https://rust-servers.com/"
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
        private Dictionary<ulong, PlayerVoteData> playerVotes = new Dictionary<ulong, PlayerVoteData>();
        private Dictionary<string, ActiveVote> activeVotes = new Dictionary<string, ActiveVote>();
        private Dictionary<ulong, float> lastVoteTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, List<string>> claimedRewards = new Dictionary<ulong, List<string>>();

        private class PlayerVoteData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public int TotalVotes { get; set; } = 0;
            public int DailyVotes { get; set; } = 0;
            public int WeeklyVotes { get; set; } = 0;
            public int MonthlyVotes { get; set; } = 0;
            public float LastVoteTime { get; set; } = 0f;
            public float LastDailyReset { get; set; } = 0f;
            public float LastWeeklyReset { get; set; } = 0f;
            public float LastMonthlyReset { get; set; } = 0f;
            public List<VoteHistory> VoteHistory { get; set; } = new List<VoteHistory>();
        }

        private class VoteHistory
        {
            public string VoteId { get; set; }
            public string VoteName { get; set; }
            public string VoteOption { get; set; }
            public float VoteTime { get; set; }
            public bool WasSuccessful { get; set; } = false;
        }

        private class ActiveVote
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Creator { get; set; }
            public ulong CreatorId { get; set; }
            public float StartTime { get; set; }
            public float Duration { get; set; }
            public Dictionary<string, int> Options { get; set; } = new Dictionary<string, int>();
            public Dictionary<ulong, string> Voters { get; set; } = new Dictionary<ulong, string>();
            public bool IsActive { get; set; } = true;
            public string Result { get; set; } = "";
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система голосования BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableVoting)
            {
                timer.Every(60f, ProcessVoteResets);
                timer.Every(10f, CheckActiveVotes);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableVoting)
            {
                InitializePlayerVotes(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerVoteData(player.userID);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerVotes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerVoteData>>("player_votes") ?? new Dictionary<ulong, PlayerVoteData>();
            activeVotes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ActiveVote>>("active_votes") ?? new Dictionary<string, ActiveVote>();
            claimedRewards = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("claimed_rewards") ?? new Dictionary<ulong, List<string>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_votes", playerVotes);
            Interface.Oxide.DataFileSystem.WriteObject("active_votes", activeVotes);
            Interface.Oxide.DataFileSystem.WriteObject("claimed_rewards", claimedRewards);
        }

        private void SavePlayerVoteData(ulong playerId)
        {
            if (playerVotes.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"votes_{playerId}", playerVotes[playerId]);
            }
        }

        private void InitializePlayerVotes(BasePlayer player)
        {
            if (!playerVotes.ContainsKey(player.userID))
            {
                var voteData = new PlayerVoteData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName
                };

                playerVotes[player.userID] = voteData;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerVotes[player.userID].PlayerName = player.displayName;
            }
        }

        private void ProcessVoteResets()
        {
            var currentTime = Time.time;

            foreach (var playerId in playerVotes.Keys.ToList())
            {
                var voteData = playerVotes[playerId];

                // Сброс ежедневных голосов
                if (currentTime - voteData.LastDailyReset >= 86400f) // 24 часа
                {
                    voteData.DailyVotes = 0;
                    voteData.LastDailyReset = currentTime;
                }

                // Сброс еженедельных голосов
                if (currentTime - voteData.LastWeeklyReset >= 604800f) // 7 дней
                {
                    voteData.WeeklyVotes = 0;
                    voteData.LastWeeklyReset = currentTime;
                }

                // Сброс месячных голосов
                if (currentTime - voteData.LastMonthlyReset >= 2592000f) // 30 дней
                {
                    voteData.MonthlyVotes = 0;
                    voteData.LastMonthlyReset = currentTime;
                }
            }

            SaveData();
        }

        private void CheckActiveVotes()
        {
            var currentTime = Time.time;
            var expiredVotes = new List<string>();

            foreach (var vote in activeVotes.Values)
            {
                if (vote.IsActive && currentTime - vote.StartTime >= vote.Duration)
                {
                    EndVote(vote);
                    expiredVotes.Add(vote.Id);
                }
            }

            foreach (var voteId in expiredVotes)
            {
                activeVotes.Remove(voteId);
            }

            if (expiredVotes.Count > 0)
            {
                SaveData();
            }
        }

        private void EndVote(ActiveVote vote)
        {
            vote.IsActive = false;

            // Определяем победителя
            var totalVotes = vote.Voters.Count;
            var winner = vote.Options.OrderByDescending(o => o.Value).FirstOrDefault();

            if (totalVotes >= config.MinVotesToPass && 
                (float)winner.Value / totalVotes >= config.MinVotePercentage)
            {
                vote.Result = winner.Key;
                
                // Уведомляем всех игроков
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage($"<color=green>Голосование '{vote.Name}' завершено!</color>");
                    player.ChatMessage($"<color=white>Победитель: {winner.Key} ({winner.Value} голосов)</color>");
                }

                // Выполняем действие голосования
                ExecuteVoteAction(vote);
            }
            else
            {
                vote.Result = "Не прошло";
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage($"<color=red>Голосование '{vote.Name}' не прошло</color>");
                    player.ChatMessage($"<color=white>Недостаточно голосов или процентов</color>");
                }
            }
        }

        private void ExecuteVoteAction(ActiveVote vote)
        {
            // Здесь должна быть логика выполнения действий голосования
            // Например, изменение настроек сервера, выдача наград и т.д.
            Puts($"Выполняется действие голосования: {vote.Name} - {vote.Result}");
        }

        private bool CanVote(ulong playerId)
        {
            if (!config.EnableVoting) return false;
            if (!lastVoteTime.ContainsKey(playerId)) return true;

            var currentTime = Time.time;
            return currentTime - lastVoteTime[playerId] >= config.VoteCooldown;
        }

        private void ProcessVote(ulong playerId, string voteId, string option)
        {
            if (!activeVotes.ContainsKey(voteId))
            {
                return;
            }

            var vote = activeVotes[voteId];
            if (!vote.IsActive)
            {
                return;
            }

            // Проверяем, не голосовал ли уже игрок
            if (vote.Voters.ContainsKey(playerId))
            {
                return;
            }

            // Добавляем голос
            vote.Voters[playerId] = option;
            if (vote.Options.ContainsKey(option))
            {
                vote.Options[option]++;
            }
            else
            {
                vote.Options[option] = 1;
            }

            // Обновляем статистику игрока
            if (playerVotes.ContainsKey(playerId))
            {
                var voteData = playerVotes[playerId];
                voteData.TotalVotes++;
                voteData.DailyVotes++;
                voteData.WeeklyVotes++;
                voteData.MonthlyVotes++;
                voteData.LastVoteTime = Time.time;

                // Добавляем в историю
                var historyEntry = new VoteHistory
                {
                    VoteId = voteId,
                    VoteName = vote.Name,
                    VoteOption = option,
                    VoteTime = Time.time
                };
                voteData.VoteHistory.Add(historyEntry);
            }

            lastVoteTime[playerId] = Time.time;

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=green>Ваш голос '{option}' учтен!</color>");
            }

            SaveData();
        }

        private void CreateVote(string voteId, string name, string description, ulong creatorId, string creatorName, float duration, List<string> options)
        {
            var vote = new ActiveVote
            {
                Id = voteId,
                Name = name,
                Description = description,
                Creator = creatorName,
                CreatorId = creatorId,
                StartTime = Time.time,
                Duration = duration,
                IsActive = true
            };

            // Инициализируем опции
            foreach (var option in options)
            {
                vote.Options[option] = 0;
            }

            activeVotes[voteId] = vote;

            // Уведомляем всех игроков
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=yellow>=== НОВОЕ ГОЛОСОВАНИЕ ===</color>");
                player.ChatMessage($"<color=white>{name}</color>");
                player.ChatMessage($"<color=white>{description}</color>");
                player.ChatMessage($"<color=white>Создатель: {creatorName}</color>");
                player.ChatMessage($"<color=white>Длительность: {duration / 60} минут</color>");
                player.ChatMessage($"<color=white>Используйте: /vote {voteId} <вариант></color>");
            }

            SaveData();
        }

        private void GiveVoteRewards(ulong playerId)
        {
            if (!config.EnableVoteRewards) return;

            var voteData = playerVotes[playerId];
            var availableRewards = config.VoteRewards
                .Where(r => voteData.TotalVotes >= r.RequiredVotes)
                .Where(r => !HasClaimedReward(playerId, r.Name))
                .ToList();

            foreach (var reward in availableRewards)
            {
                GiveVoteReward(playerId, reward);
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

        private void GiveVoteReward(ulong playerId, VoteReward reward)
        {
            if (!claimedRewards.ContainsKey(playerId))
            {
                claimedRewards[playerId] = new List<string>();
            }

            claimedRewards[playerId].Add(reward.Name);

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=green>Получена награда за голосование: {reward.Name}</color>");
                player.ChatMessage($"<color=white>{reward.Description}</color>");
            }

            // Здесь должна быть логика выдачи наград
            foreach (var rewardItem in reward.Rewards)
            {
                ProcessRewardItem(playerId, rewardItem);
            }

            SaveData();
        }

        private void ProcessRewardItem(ulong playerId, string rewardItem)
        {
            // Здесь должна быть логика обработки различных типов наград
            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=cyan>Награда: {rewardItem}</color>");
            }
        }
        #endregion

        #region Commands
        [ChatCommand("vote")]
        private void VoteCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableVoting)
            {
                player.ChatMessage("Система голосования отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowVoteHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowActiveVotes(player);
                    break;
                case "create":
                    if (args.Length < 4)
                    {
                        player.ChatMessage("Использование: /vote create <название> <описание> <варианты через |>");
                        return;
                    }
                    CreateVoteCommand(player, args);
                    break;
                case "info":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /vote info <id_голосования>");
                        return;
                    }
                    ShowVoteInfo(player, args[1]);
                    break;
                case "stats":
                    ShowVoteStats(player);
                    break;
                case "rewards":
                    ShowVoteRewards(player);
                    break;
                case "claim":
                    ClaimVoteRewards(player);
                    break;
                case "sites":
                    ShowVoteSites(player);
                    break;
                default:
                    // Голосование за вариант
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /vote <id_голосования> <вариант>");
                        return;
                    }
                    CastVoteCommand(player, args[0], args[1]);
                    break;
            }
        }

        [ChatCommand("votereward")]
        private void VoteRewardCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableVoteRewards)
            {
                player.ChatMessage("Награды за голосование отключены");
                return;
            }

            GiveVoteRewards(player.userID);
        }
        #endregion

        #region Command Methods
        private void ShowVoteHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ ГОЛОСОВАНИЯ ===</color>");
            player.ChatMessage("/vote list - активные голосования");
            player.ChatMessage("/vote create <название> <описание> <варианты> - создать голосование");
            player.ChatMessage("/vote <id> <вариант> - проголосовать");
            player.ChatMessage("/vote info <id> - информация о голосовании");
            player.ChatMessage("/vote stats - статистика голосования");
            player.ChatMessage("/vote rewards - доступные награды");
            player.ChatMessage("/vote claim - получить награды");
            player.ChatMessage("/vote sites - сайты для голосования");
            player.ChatMessage("/votereward - получить награду за голосование");
        }

        private void ShowActiveVotes(BasePlayer player)
        {
            var activeVotesList = activeVotes.Values.Where(v => v.IsActive).ToList();

            if (activeVotesList.Count == 0)
            {
                player.ChatMessage("Нет активных голосований");
                return;
            }

            player.ChatMessage("<color=yellow>=== АКТИВНЫЕ ГОЛОСОВАНИЯ ===</color>");
            foreach (var vote in activeVotesList)
            {
                var timeLeft = vote.Duration - (Time.time - vote.StartTime);
                var minutesLeft = Mathf.CeilToInt(timeLeft / 60f);
                
                player.ChatMessage($"<color=green>{vote.Name}</color>");
                player.ChatMessage($"  ID: {vote.Id}");
                player.ChatMessage($"  Описание: {vote.Description}");
                player.ChatMessage($"  Создатель: {vote.Creator}");
                player.ChatMessage($"  Осталось: {minutesLeft} минут");
                player.ChatMessage($"  Голосов: {vote.Voters.Count}");
                
                foreach (var option in vote.Options)
                {
                    player.ChatMessage($"    {option.Key}: {option.Value} голосов");
                }
            }
        }

        private void CreateVoteCommand(BasePlayer player, string[] args)
        {
            if (!config.EnablePlayerVoting && !player.IsAdmin)
            {
                player.ChatMessage("Создание голосований игроками отключено");
                return;
            }

            var name = args[1];
            var description = args[2];
            var optionsString = string.Join(" ", args.Skip(3));
            var options = optionsString.Split('|').Select(o => o.Trim()).ToList();

            if (options.Count < 2)
            {
                player.ChatMessage("Нужно минимум 2 варианта для голосования");
                return;
            }

            var voteId = Guid.NewGuid().ToString();
            CreateVote(voteId, name, description, player.userID, player.displayName, config.VoteDuration, options);
            
            player.ChatMessage($"<color=green>Голосование '{name}' создано!</color>");
            player.ChatMessage($"<color=white>ID: {voteId}</color>");
        }

        private void ShowVoteInfo(BasePlayer player, string voteId)
        {
            if (!activeVotes.ContainsKey(voteId))
            {
                player.ChatMessage($"Голосование '{voteId}' не найдено");
                return;
            }

            var vote = activeVotes[voteId];
            var timeLeft = vote.Duration - (Time.time - vote.StartTime);
            var minutesLeft = Mathf.CeilToInt(timeLeft / 60f);

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О ГОЛОСОВАНИИ ===</color>");
            player.ChatMessage($"Название: {vote.Name}");
            player.ChatMessage($"Описание: {vote.Description}");
            player.ChatMessage($"Создатель: {vote.Creator}");
            player.ChatMessage($"Статус: {(vote.IsActive ? "Активно" : "Завершено")}");
            player.ChatMessage($"Осталось: {minutesLeft} минут");
            player.ChatMessage($"Всего голосов: {vote.Voters.Count}");
            
            player.ChatMessage("<color=white>Результаты:</color>");
            foreach (var option in vote.Options.OrderByDescending(o => o.Value))
            {
                var percentage = vote.Voters.Count > 0 ? (float)option.Value / vote.Voters.Count * 100f : 0f;
                player.ChatMessage($"  {option.Key}: {option.Value} голосов ({percentage:F1}%)");
            }

            if (!string.IsNullOrEmpty(vote.Result))
            {
                player.ChatMessage($"<color=green>Результат: {vote.Result}</color>");
            }
        }

        private void CastVoteCommand(BasePlayer player, string voteId, string option)
        {
            if (!CanVote(player.userID))
            {
                var timeLeft = config.VoteCooldown - (Time.time - lastVoteTime[player.userID]);
                player.ChatMessage($"Осталось ждать: {Mathf.CeilToInt(timeLeft / 60)} минут");
                return;
            }

            ProcessVote(player.userID, voteId, option);
        }

        private void ShowVoteStats(BasePlayer player)
        {
            if (!playerVotes.ContainsKey(player.userID))
            {
                InitializePlayerVotes(player);
            }

            var voteData = playerVotes[player.userID];

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА ГОЛОСОВАНИЯ ===</color>");
            player.ChatMessage($"Всего голосов: {voteData.TotalVotes}");
            player.ChatMessage($"Голосов сегодня: {voteData.DailyVotes}");
            player.ChatMessage($"Голосов на неделе: {voteData.WeeklyVotes}");
            player.ChatMessage($"Голосов в месяце: {voteData.MonthlyVotes}");
            player.ChatMessage($"Последний голос: {TimeSpan.FromSeconds(Time.time - voteData.LastVoteTime).TotalHours:F1} часов назад");
        }

        private void ShowVoteRewards(BasePlayer player)
        {
            if (!config.EnableVoteRewards)
            {
                player.ChatMessage("Награды за голосование отключены");
                return;
            }

            if (!playerVotes.ContainsKey(player.userID))
            {
                InitializePlayerVotes(player);
            }

            var voteData = playerVotes[player.userID];
            var availableRewards = config.VoteRewards
                .Where(r => voteData.TotalVotes >= r.RequiredVotes)
                .ToList();

            if (availableRewards.Count == 0)
            {
                player.ChatMessage("Нет доступных наград");
                return;
            }

            player.ChatMessage("<color=yellow>=== НАГРАДЫ ЗА ГОЛОСОВАНИЕ ===</color>");
            foreach (var reward in availableRewards)
            {
                var claimed = HasClaimedReward(player.userID, reward.Name);
                var status = claimed ? "Получена" : "Доступна";
                var color = claimed ? "green" : "white";
                
                player.ChatMessage($"<color={color}>{reward.Name} - {status}</color>");
                player.ChatMessage($"  {reward.Description}");
                player.ChatMessage($"  Требуется: {reward.RequiredVotes} голосов");
            }
        }

        private void ClaimVoteRewards(BasePlayer player)
        {
            if (!config.EnableVoteRewards)
            {
                player.ChatMessage("Награды за голосование отключены");
                return;
            }

            GiveVoteRewards(player.userID);
        }

        private void ShowVoteSites(BasePlayer player)
        {
            if (config.VoteSites.Count == 0)
            {
                player.ChatMessage("Сайты для голосования не настроены");
                return;
            }

            player.ChatMessage("<color=yellow>=== САЙТЫ ДЛЯ ГОЛОСОВАНИЯ ===</color>");
            for (int i = 0; i < config.VoteSites.Count; i++)
            {
                player.ChatMessage($"{i + 1}. {config.VoteSites[i]}");
            }
            player.ChatMessage("<color=white>Голосуйте за наш сервер и получайте награды!</color>");
        }
        #endregion
    }
}
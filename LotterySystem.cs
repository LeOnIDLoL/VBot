using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Lottery System", "BULBARUST", "1.0.0")]
    [Description("Система лотереи для сервера BULBARUST")]
    public class LotterySystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableLottery { get; set; } = true;
            public bool EnableDailyLottery { get; set; } = true;
            public bool EnableWeeklyLottery { get; set; } = true;
            public bool EnableMonthlyLottery { get; set; } = true;
            public float TicketPrice { get; set; } = 100f;
            public int MaxTicketsPerPlayer { get; set; } = 10;
            public float DailyLotteryTime { get; set; } = 86400f; // 24 часа
            public float WeeklyLotteryTime { get; set; } = 604800f; // 7 дней
            public float MonthlyLotteryTime { get; set; } = 2592000f; // 30 дней
            public float JackpotPercentage { get; set; } = 0.7f; // 70% от общего фонда
            public float SecondPlacePercentage { get; set; } = 0.2f; // 20% от общего фонда
            public float ThirdPlacePercentage { get; set; } = 0.1f; // 10% от общего фонда
            public List<LotteryReward> Rewards { get; set; } = new List<LotteryReward>();
            public List<LotteryPrize> Prizes { get; set; } = new List<LotteryPrize>();
        }

        private class LotteryReward
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float Chance { get; set; } = 1f; // Процент шанса
            public List<string> Rewards { get; set; } = new List<string>();
            public bool IsSpecial { get; set; } = false;
        }

        private class LotteryPrize
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float MinAmount { get; set; } = 1000f;
            public float MaxAmount { get; set; } = 10000f;
            public int RequiredTickets { get; set; } = 1;
            public bool IsGuaranteed { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем награды лотереи
            config.Rewards.AddRange(new List<LotteryReward>
            {
                new LotteryReward
                {
                    Name = "Малый приз",
                    Description = "Небольшая награда за участие",
                    Chance = 50f,
                    Rewards = new List<string> { "currency.500", "kit.small" }
                },
                new LotteryReward
                {
                    Name = "Средний приз",
                    Description = "Хорошая награда",
                    Chance = 25f,
                    Rewards = new List<string> { "currency.2000", "kit.medium", "reputation.50" }
                },
                new LotteryReward
                {
                    Name = "Большой приз",
                    Description = "Отличная награда",
                    Chance = 10f,
                    Rewards = new List<string> { "currency.5000", "kit.large", "reputation.100" }
                },
                new LotteryReward
                {
                    Name = "Джекпот",
                    Description = "Максимальная награда",
                    Chance = 1f,
                    Rewards = new List<string> { "currency.50000", "kit.jackpot", "reputation.500", "title.lucky" },
                    IsSpecial = true
                }
            });

            // Добавляем призы
            config.Prizes.AddRange(new List<LotteryPrize>
            {
                new LotteryPrize
                {
                    Name = "Утешительный приз",
                    Description = "Приз за участие",
                    MinAmount = 100f,
                    MaxAmount = 500f,
                    RequiredTickets = 1
                },
                new LotteryPrize
                {
                    Name = "Третий приз",
                    Description = "Бронзовая награда",
                    MinAmount = 1000f,
                    MaxAmount = 5000f,
                    RequiredTickets = 3
                },
                new LotteryPrize
                {
                    Name = "Второй приз",
                    Description = "Серебряная награда",
                    MinAmount = 5000f,
                    MaxAmount = 15000f,
                    RequiredTickets = 5
                },
                new LotteryPrize
                {
                    Name = "Первый приз",
                    Description = "Золотая награда",
                    MinAmount = 15000f,
                    MaxAmount = 50000f,
                    RequiredTickets = 10
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
        private Dictionary<ulong, PlayerLotteryData> playerLottery = new Dictionary<ulong, PlayerLotteryData>();
        private Dictionary<string, LotteryDraw> activeLotteries = new Dictionary<string, LotteryDraw>();
        private Dictionary<string, float> lastLotteryDraw = new Dictionary<string, float>();
        private Dictionary<string, List<LotteryTicket>> lotteryTickets = new Dictionary<string, List<LotteryTicket>>();

        private class PlayerLotteryData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public int TotalTicketsBought { get; set; } = 0;
            public int TotalWins { get; set; } = 0;
            public float TotalWinnings { get; set; } = 0f;
            public float TotalSpent { get; set; } = 0f;
            public List<LotteryWin> WinHistory { get; set; } = new List<LotteryWin>();
            public Dictionary<string, int> TicketsByType { get; set; } = new Dictionary<string, int>();
        }

        private class LotteryWin
        {
            public string LotteryType { get; set; }
            public string Prize { get; set; }
            public float Amount { get; set; }
            public float Timestamp { get; set; }
        }

        private class LotteryDraw
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Type { get; set; } // daily, weekly, monthly
            public float StartTime { get; set; }
            public float EndTime { get; set; }
            public float TicketPrice { get; set; }
            public float TotalFund { get; set; } = 0f;
            public int TotalTickets { get; set; } = 0;
            public bool IsActive { get; set; } = true;
            public List<LotteryWinner> Winners { get; set; } = new List<LotteryWinner>();
        }

        private class LotteryTicket
        {
            public string Id { get; set; }
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public string LotteryId { get; set; }
            public float PurchaseTime { get; set; }
            public bool IsWinner { get; set; } = false;
            public string Prize { get; set; } = "";
        }

        private class LotteryWinner
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public string Prize { get; set; }
            public float Amount { get; set; }
            public int TicketCount { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система лотереи BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableLottery)
            {
                InitializeLotteries();
                timer.Every(60f, CheckLotteries); // Каждую минуту
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableLottery)
            {
                InitializePlayerLottery(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerLotteryData(player.userID);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerLottery = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerLotteryData>>("player_lottery") ?? new Dictionary<ulong, PlayerLotteryData>();
            activeLotteries = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, LotteryDraw>>("active_lotteries") ?? new Dictionary<string, LotteryDraw>();
            lotteryTickets = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<LotteryTicket>>>("lottery_tickets") ?? new Dictionary<string, List<LotteryTicket>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_lottery", playerLottery);
            Interface.Oxide.DataFileSystem.WriteObject("active_lotteries", activeLotteries);
            Interface.Oxide.DataFileSystem.WriteObject("lottery_tickets", lotteryTickets);
        }

        private void SavePlayerLotteryData(ulong playerId)
        {
            if (playerLottery.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"lottery_{playerId}", playerLottery[playerId]);
            }
        }

        private void InitializePlayerLottery(BasePlayer player)
        {
            if (!playerLottery.ContainsKey(player.userID))
            {
                var lotteryData = new PlayerLotteryData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName
                };

                playerLottery[player.userID] = lotteryData;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerLottery[player.userID].PlayerName = player.displayName;
            }
        }

        private void InitializeLotteries()
        {
            var currentTime = Time.time;

            // Инициализируем ежедневную лотерею
            if (config.EnableDailyLottery)
            {
                InitializeLottery("daily", "Ежедневная лотерея", "daily", config.DailyLotteryTime, config.TicketPrice);
            }

            // Инициализируем еженедельную лотерею
            if (config.EnableWeeklyLottery)
            {
                InitializeLottery("weekly", "Еженедельная лотерея", "weekly", config.WeeklyLotteryTime, config.TicketPrice * 2);
            }

            // Инициализируем месячную лотерею
            if (config.EnableMonthlyLottery)
            {
                InitializeLottery("monthly", "Месячная лотерея", "monthly", config.MonthlyLotteryTime, config.TicketPrice * 5);
            }
        }

        private void InitializeLottery(string id, string name, string type, float duration, float ticketPrice)
        {
            if (!activeLotteries.ContainsKey(id))
            {
                var lottery = new LotteryDraw
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    StartTime = Time.time,
                    EndTime = Time.time + duration,
                    TicketPrice = ticketPrice,
                    IsActive = true
                };

                activeLotteries[id] = lottery;
                lotteryTickets[id] = new List<LotteryTicket>();
            }
        }

        private void CheckLotteries()
        {
            var currentTime = Time.time;
            var expiredLotteries = new List<string>();

            foreach (var lottery in activeLotteries.Values)
            {
                if (lottery.IsActive && currentTime >= lottery.EndTime)
                {
                    DrawLottery(lottery);
                    expiredLotteries.Add(lottery.Id);
                }
            }

            // Удаляем завершенные лотереи и создаем новые
            foreach (var lotteryId in expiredLotteries)
            {
                activeLotteries.Remove(lotteryId);
                lotteryTickets.Remove(lotteryId);

                // Создаем новую лотерею того же типа
                switch (lotteryId)
                {
                    case "daily":
                        if (config.EnableDailyLottery)
                        {
                            InitializeLottery("daily", "Ежедневная лотерея", "daily", config.DailyLotteryTime, config.TicketPrice);
                        }
                        break;
                    case "weekly":
                        if (config.EnableWeeklyLottery)
                        {
                            InitializeLottery("weekly", "Еженедельная лотерея", "weekly", config.WeeklyLotteryTime, config.TicketPrice * 2);
                        }
                        break;
                    case "monthly":
                        if (config.EnableMonthlyLottery)
                        {
                            InitializeLottery("monthly", "Месячная лотерея", "monthly", config.MonthlyLotteryTime, config.TicketPrice * 5);
                        }
                        break;
                }
            }

            if (expiredLotteries.Count > 0)
            {
                SaveData();
            }
        }

        private void DrawLottery(LotteryDraw lottery)
        {
            lottery.IsActive = false;

            if (!lotteryTickets.ContainsKey(lottery.Id) || lotteryTickets[lottery.Id].Count == 0)
            {
                // Нет билетов - лотерея отменяется
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage($"<color=yellow>Лотерея '{lottery.Name}' отменена - нет участников</color>");
                }
                return;
            }

            var tickets = lotteryTickets[lottery.Id];
            var totalTickets = tickets.Count;

            // Определяем победителей
            var winners = DetermineWinners(lottery, tickets);
            lottery.Winners = winners;

            // Уведомляем всех игроков о результатах
            AnnounceWinners(lottery, winners);

            // Выдаем призы победителям
            GivePrizes(winners);

            // Обновляем статистику игроков
            UpdatePlayerStatistics(winners);
        }

        private List<LotteryWinner> DetermineWinners(LotteryDraw lottery, List<LotteryTicket> tickets)
        {
            var winners = new List<LotteryWinner>();
            var totalFund = lottery.TotalFund;

            if (totalFund <= 0) return winners;

            // Группируем билеты по игрокам
            var playerTickets = tickets.GroupBy(t => t.PlayerId).ToDictionary(g => g.Key, g => g.ToList());

            // Определяем количество победителей
            var winnerCount = Mathf.Min(3, playerTickets.Count);

            // Выбираем победителей случайным образом
            var allTickets = tickets.ToList();
            var selectedWinners = new List<LotteryTicket>();

            for (int i = 0; i < winnerCount; i++)
            {
                if (allTickets.Count == 0) break;

                var randomIndex = UnityEngine.Random.Range(0, allTickets.Count);
                var winnerTicket = allTickets[randomIndex];
                selectedWinners.Add(winnerTicket);
                allTickets.RemoveAt(randomIndex);
            }

            // Распределяем призы
            var prizes = new List<float>();
            if (selectedWinners.Count >= 1)
            {
                prizes.Add(totalFund * config.JackpotPercentage);
            }
            if (selectedWinners.Count >= 2)
            {
                prizes.Add(totalFund * config.SecondPlacePercentage);
            }
            if (selectedWinners.Count >= 3)
            {
                prizes.Add(totalFund * config.ThirdPlacePercentage);
            }

            for (int i = 0; i < selectedWinners.Count; i++)
            {
                var winnerTicket = selectedWinners[i];
                var prizeAmount = i < prizes.Count ? prizes[i] : 0f;

                var winner = new LotteryWinner
                {
                    PlayerId = winnerTicket.PlayerId,
                    PlayerName = winnerTicket.PlayerName,
                    Prize = GetPrizeName(i + 1),
                    Amount = prizeAmount,
                    TicketCount = playerTickets[winnerTicket.PlayerId].Count
                };

                winners.Add(winner);
            }

            return winners;
        }

        private string GetPrizeName(int place)
        {
            switch (place)
            {
                case 1: return "Джекпот";
                case 2: return "Второе место";
                case 3: return "Третье место";
                default: return "Утешительный приз";
            }
        }

        private void AnnounceWinners(LotteryDraw lottery, List<LotteryWinner> winners)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage($"<color=yellow>=== РЕЗУЛЬТАТЫ ЛОТЕРЕИ '{lottery.Name}' ===</color>");
                player.ChatMessage($"<color=white>Общий фонд: {lottery.TotalFund:F2}</color>");
                player.ChatMessage($"<color=white>Всего билетов: {lottery.TotalTickets}</color>");

                if (winners.Count == 0)
                {
                    player.ChatMessage("<color=red>Победителей нет</color>");
                }
                else
                {
                    for (int i = 0; i < winners.Count; i++)
                    {
                        var winner = winners[i];
                        var place = i + 1;
                        var color = place == 1 ? "gold" : place == 2 ? "silver" : "bronze";
                        
                        player.ChatMessage($"<color={color}>{place} место: {winner.PlayerName}</color>");
                        player.ChatMessage($"  Приз: {winner.Prize}");
                        player.ChatMessage($"  Сумма: {winner.Amount:F2}");
                        player.ChatMessage($"  Билетов: {winner.TicketCount}");
                    }
                }
            }
        }

        private void GivePrizes(List<LotteryWinner> winners)
        {
            foreach (var winner in winners)
            {
                if (winner.Amount > 0)
                {
                    // Здесь должна быть логика выдачи призов
                    var player = BasePlayer.FindByID(winner.PlayerId);
                    if (player != null && player.IsConnected)
                    {
                        player.ChatMessage($"<color=green>Поздравляем! Вы выиграли {winner.Amount:F2} в лотерее!</color>");
                    }
                }
            }
        }

        private void UpdatePlayerStatistics(List<LotteryWinner> winners)
        {
            foreach (var winner in winners)
            {
                if (playerLottery.ContainsKey(winner.PlayerId))
                {
                    var lotteryData = playerLottery[winner.PlayerId];
                    lotteryData.TotalWins++;
                    lotteryData.TotalWinnings += winner.Amount;

                    var winEntry = new LotteryWin
                    {
                        LotteryType = "lottery",
                        Prize = winner.Prize,
                        Amount = winner.Amount,
                        Timestamp = Time.time
                    };

                    lotteryData.WinHistory.Add(winEntry);
                }
            }

            SaveData();
        }

        private bool BuyTicket(ulong playerId, string lotteryId, int ticketCount = 1)
        {
            if (!activeLotteries.ContainsKey(lotteryId))
            {
                return false;
            }

            var lottery = activeLotteries[lotteryId];
            if (!lottery.IsActive)
            {
                return false;
            }

            var totalCost = lottery.TicketPrice * ticketCount;

            // Проверяем лимит билетов на игрока
            var playerTickets = lotteryTickets[lotteryId].Count(t => t.PlayerId == playerId);
            if (playerTickets + ticketCount > config.MaxTicketsPerPlayer)
            {
                return false;
            }

            // Здесь должна быть логика списания средств
            // Для примера просто добавляем билеты

            for (int i = 0; i < ticketCount; i++)
            {
                var ticket = new LotteryTicket
                {
                    Id = Guid.NewGuid().ToString(),
                    PlayerId = playerId,
                    PlayerName = playerLottery.ContainsKey(playerId) ? playerLottery[playerId].PlayerName : "Unknown",
                    LotteryId = lotteryId,
                    PurchaseTime = Time.time
                };

                lotteryTickets[lotteryId].Add(ticket);
            }

            // Обновляем статистику лотереи
            lottery.TotalTickets += ticketCount;
            lottery.TotalFund += totalCost;

            // Обновляем статистику игрока
            if (playerLottery.ContainsKey(playerId))
            {
                var lotteryData = playerLottery[playerId];
                lotteryData.TotalTicketsBought += ticketCount;
                lotteryData.TotalSpent += totalCost;

                if (!lotteryData.TicketsByType.ContainsKey(lotteryId))
                {
                    lotteryData.TicketsByType[lotteryId] = 0;
                }
                lotteryData.TicketsByType[lotteryId] += ticketCount;
            }

            return true;
        }
        #endregion

        #region Commands
        [ChatCommand("lottery")]
        private void LotteryCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableLottery)
            {
                player.ChatMessage("Система лотереи отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowLotteryHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowActiveLotteries(player);
                    break;
                case "buy":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /lottery buy <тип> <количество>");
                        return;
                    }
                    BuyTicketCommand(player, args[1], args[2]);
                    break;
                case "tickets":
                    ShowPlayerTickets(player, args.Length > 1 ? args[1] : null);
                    break;
                case "stats":
                    ShowLotteryStats(player);
                    break;
                case "winners":
                    ShowRecentWinners(player);
                    break;
                case "info":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /lottery info <тип>");
                        return;
                    }
                    ShowLotteryInfo(player, args[1]);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowLotteryHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ ЛОТЕРЕИ ===</color>");
            player.ChatMessage("/lottery list - активные лотереи");
            player.ChatMessage("/lottery buy <тип> <количество> - купить билеты");
            player.ChatMessage("/lottery tickets [тип] - мои билеты");
            player.ChatMessage("/lottery stats - статистика");
            player.ChatMessage("/lottery winners - последние победители");
            player.ChatMessage("/lottery info <тип> - информация о лотерее");
        }

        private void ShowActiveLotteries(BasePlayer player)
        {
            var activeLotteriesList = activeLotteries.Values.Where(l => l.IsActive).ToList();

            if (activeLotteriesList.Count == 0)
            {
                player.ChatMessage("Нет активных лотерей");
                return;
            }

            player.ChatMessage("<color=yellow>=== АКТИВНЫЕ ЛОТЕРЕИ ===</color>");
            foreach (var lottery in activeLotteriesList)
            {
                var timeLeft = lottery.EndTime - Time.time;
                var hoursLeft = Mathf.CeilToInt(timeLeft / 3600f);
                var playerTickets = lotteryTickets.ContainsKey(lottery.Id) ? 
                    lotteryTickets[lottery.Id].Count(t => t.PlayerId == player.userID) : 0;

                player.ChatMessage($"<color=green>{lottery.Name}</color>");
                player.ChatMessage($"  Тип: {lottery.Type}");
                player.ChatMessage($"  Цена билета: {lottery.TicketPrice:F2}");
                player.ChatMessage($"  Осталось: {hoursLeft} часов");
                player.ChatMessage($"  Общий фонд: {lottery.TotalFund:F2}");
                player.ChatMessage($"  Всего билетов: {lottery.TotalTickets}");
                player.ChatMessage($"  Ваших билетов: {playerTickets}");
            }
        }

        private void BuyTicketCommand(BasePlayer player, string lotteryType, string countStr)
        {
            if (!int.TryParse(countStr, out int count) || count <= 0)
            {
                player.ChatMessage("Неверное количество билетов");
                return;
            }

            if (count > config.MaxTicketsPerPlayer)
            {
                player.ChatMessage($"Максимальное количество билетов: {config.MaxTicketsPerPlayer}");
                return;
            }

            if (BuyTicket(player.userID, lotteryType, count))
            {
                var lottery = activeLotteries[lotteryType];
                var totalCost = lottery.TicketPrice * count;
                player.ChatMessage($"<color=green>Куплено {count} билетов за {totalCost:F2}!</color>");
                player.ChatMessage($"<color=white>Удачи в лотерее!</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не удалось купить билеты</color>");
            }
        }

        private void ShowPlayerTickets(BasePlayer player, string lotteryType = null)
        {
            if (string.IsNullOrEmpty(lotteryType))
            {
                player.ChatMessage("<color=yellow>=== МОИ БИЛЕТЫ ===</color>");
                foreach (var lottery in activeLotteries.Values.Where(l => l.IsActive))
                {
                    var tickets = lotteryTickets.ContainsKey(lottery.Id) ? 
                        lotteryTickets[lottery.Id].Where(t => t.PlayerId == player.userID).ToList() : new List<LotteryTicket>();

                    player.ChatMessage($"{lottery.Name}: {tickets.Count} билетов");
                }
            }
            else
            {
                if (!lotteryTickets.ContainsKey(lotteryType))
                {
                    player.ChatMessage("Лотерея не найдена");
                    return;
                }

                var tickets = lotteryTickets[lotteryType].Where(t => t.PlayerId == player.userID).ToList();
                player.ChatMessage($"<color=yellow>Билеты в лотерее '{lotteryType}': {tickets.Count}</color>");
                
                foreach (var ticket in tickets.Take(10)) // Показываем только первые 10
                {
                    var timeAgo = TimeSpan.FromSeconds(Time.time - ticket.PurchaseTime).TotalHours;
                    player.ChatMessage($"  Билет {ticket.Id.Substring(0, 8)}... ({timeAgo:F1}ч назад)");
                }
            }
        }

        private void ShowLotteryStats(BasePlayer player)
        {
            if (!playerLottery.ContainsKey(player.userID))
            {
                InitializePlayerLottery(player);
            }

            var lotteryData = playerLottery[player.userID];

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА ЛОТЕРЕИ ===</color>");
            player.ChatMessage($"Куплено билетов: {lotteryData.TotalTicketsBought}");
            player.ChatMessage($"Потрачено: {lotteryData.TotalSpent:F2}");
            player.ChatMessage($"Выигрышей: {lotteryData.TotalWins}");
            player.ChatMessage($"Выиграно: {lotteryData.TotalWinnings:F2}");
            player.ChatMessage($"Прибыль: {lotteryData.TotalWinnings - lotteryData.TotalSpent:F2}");

            if (lotteryData.WinHistory.Count > 0)
            {
                player.ChatMessage("<color=white>Последние выигрыши:</color>");
                foreach (var win in lotteryData.WinHistory.OrderByDescending(w => w.Timestamp).Take(5))
                {
                    var timeAgo = TimeSpan.FromSeconds(Time.time - win.Timestamp).TotalDays;
                    player.ChatMessage($"  {win.Prize}: {win.Amount:F2} ({timeAgo:F0} дней назад)");
                }
            }
        }

        private void ShowRecentWinners(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ПОСЛЕДНИЕ ПОБЕДИТЕЛИ ===</color>");
            
            var recentWinners = new List<LotteryWinner>();
            foreach (var lottery in activeLotteries.Values)
            {
                recentWinners.AddRange(lottery.Winners);
            }

            if (recentWinners.Count == 0)
            {
                player.ChatMessage("Победителей пока нет");
                return;
            }

            var topWinners = recentWinners.OrderByDescending(w => w.Amount).Take(10);
            foreach (var winner in topWinners)
            {
                player.ChatMessage($"{winner.PlayerName} - {winner.Prize} ({winner.Amount:F2})");
            }
        }

        private void ShowLotteryInfo(BasePlayer player, string lotteryType)
        {
            if (!activeLotteries.ContainsKey(lotteryType))
            {
                player.ChatMessage("Лотерея не найдена");
                return;
            }

            var lottery = activeLotteries[lotteryType];
            var timeLeft = lottery.EndTime - Time.time;
            var hoursLeft = Mathf.CeilToInt(timeLeft / 3600f);

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О ЛОТЕРЕЕ ===</color>");
            player.ChatMessage($"Название: {lottery.Name}");
            player.ChatMessage($"Тип: {lottery.Type}");
            player.ChatMessage($"Цена билета: {lottery.TicketPrice:F2}");
            player.ChatMessage($"Осталось времени: {hoursLeft} часов");
            player.ChatMessage($"Общий фонд: {lottery.TotalFund:F2}");
            player.ChatMessage($"Всего билетов: {lottery.TotalTickets}");
            player.ChatMessage($"Максимум билетов на игрока: {config.MaxTicketsPerPlayer}");

            if (lottery.Winners.Count > 0)
            {
                player.ChatMessage("<color=white>Последние победители:</color>");
                foreach (var winner in lottery.Winners.Take(3))
                {
                    player.ChatMessage($"  {winner.PlayerName} - {winner.Prize} ({winner.Amount:F2})");
                }
            }
        }
        #endregion
    }
}
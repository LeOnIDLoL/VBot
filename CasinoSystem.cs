using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Casino System", "BULBARUST", "1.0.0")]
    [Description("Система казино для сервера BULBARUST")]
    public class CasinoSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableCasino { get; set; } = true;
            public bool EnableSlotMachines { get; set; } = true;
            public bool EnableRoulette { get; set; } = true;
            public bool EnableBlackjack { get; set; } = true;
            public bool EnablePoker { get; set; } = true;
            public bool EnableDice { get; set; } = true;
            public float MinBet { get; set; } = 10f;
            public float MaxBet { get; set; } = 10000f;
            public float HouseEdge { get; set; } = 0.05f; // 5% преимущество казино
            public List<SlotMachine> SlotMachines { get; set; } = new List<SlotMachine>();
            public List<CasinoGame> Games { get; set; } = new List<CasinoGame>();
            public List<CasinoReward> Rewards { get; set; } = new List<CasinoReward>();
        }

        private class SlotMachine
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public float MinBet { get; set; } = 10f;
            public float MaxBet { get; set; } = 1000f;
            public List<SlotSymbol> Symbols { get; set; } = new List<SlotSymbol>();
            public List<SlotCombination> Combinations { get; set; } = new List<SlotCombination>();
        }

        private class SlotSymbol
        {
            public string Name { get; set; }
            public string Display { get; set; }
            public float Weight { get; set; } = 1f;
            public string Color { get; set; } = "white";
        }

        private class SlotCombination
        {
            public List<string> Symbols { get; set; } = new List<string>();
            public float Multiplier { get; set; } = 1f;
            public float Chance { get; set; } = 1f;
            public string Name { get; set; }
        }

        private class CasinoGame
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public float MinBet { get; set; } = 10f;
            public float MaxBet { get; set; } = 1000f;
            public float HouseEdge { get; set; } = 0.05f;
            public bool IsActive { get; set; } = true;
        }

        private class CasinoReward
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float RequiredWinnings { get; set; } = 1000f;
            public List<string> Rewards { get; set; } = new List<string>();
            public bool IsOneTime { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем игровые автоматы
            config.SlotMachines.AddRange(new List<SlotMachine>
            {
                new SlotMachine
                {
                    Id = "classic",
                    Name = "Классический автомат",
                    MinBet = 10f,
                    MaxBet = 1000f,
                    Symbols = new List<SlotSymbol>
                    {
                        new SlotSymbol { Name = "cherry", Display = "🍒", Weight = 4f, Color = "red" },
                        new SlotSymbol { Name = "lemon", Display = "🍋", Weight = 3f, Color = "yellow" },
                        new SlotSymbol { Name = "orange", Display = "🍊", Weight = 3f, Color = "orange" },
                        new SlotSymbol { Name = "plum", Display = "🍇", Weight = 2f, Color = "purple" },
                        new SlotSymbol { Name = "bell", Display = "🔔", Weight = 1f, Color = "gold" },
                        new SlotSymbol { Name = "seven", Display = "7️⃣", Weight = 0.5f, Color = "gold" }
                    },
                    Combinations = new List<SlotCombination>
                    {
                        new SlotCombination { Symbols = new List<string> { "seven", "seven", "seven" }, Multiplier = 100f, Chance = 0.001f, Name = "Три семерки" },
                        new SlotCombination { Symbols = new List<string> { "bell", "bell", "bell" }, Multiplier = 20f, Chance = 0.01f, Name = "Три колокола" },
                        new SlotCombination { Symbols = new List<string> { "cherry", "cherry", "cherry" }, Multiplier = 10f, Chance = 0.05f, Name = "Три вишни" },
                        new SlotCombination { Symbols = new List<string> { "lemon", "lemon", "lemon" }, Multiplier = 8f, Chance = 0.05f, Name = "Три лимона" },
                        new SlotCombination { Symbols = new List<string> { "orange", "orange", "orange" }, Multiplier = 8f, Chance = 0.05f, Name = "Три апельсина" },
                        new SlotCombination { Symbols = new List<string> { "plum", "plum", "plum" }, Multiplier = 15f, Chance = 0.03f, Name = "Три сливы" }
                    }
                }
            });

            // Добавляем игры
            config.Games.AddRange(new List<CasinoGame>
            {
                new CasinoGame
                {
                    Id = "roulette",
                    Name = "Рулетка",
                    Description = "Классическая рулетка с числами от 0 до 36",
                    MinBet = 10f,
                    MaxBet = 5000f,
                    HouseEdge = 0.027f
                },
                new CasinoGame
                {
                    Id = "blackjack",
                    Name = "Блэкджек",
                    Description = "Классический блэкджек против дилера",
                    MinBet = 20f,
                    MaxBet = 2000f,
                    HouseEdge = 0.02f
                },
                new CasinoGame
                {
                    Id = "poker",
                    Name = "Покер",
                    Description = "Техасский холдем",
                    MinBet = 50f,
                    MaxBet = 10000f,
                    HouseEdge = 0.05f
                },
                new CasinoGame
                {
                    Id = "dice",
                    Name = "Кости",
                    Description = "Угадайте число на кости",
                    MinBet = 5f,
                    MaxBet = 1000f,
                    HouseEdge = 0.1f
                }
            });

            // Добавляем награды
            config.Rewards.AddRange(new List<CasinoReward>
            {
                new CasinoReward
                {
                    Name = "Новичок казино",
                    Description = "Первая крупная победа",
                    RequiredWinnings = 1000f,
                    Rewards = new List<string> { "currency.500", "title.gambler" }
                },
                new CasinoReward
                {
                    Name = "Удачливый игрок",
                    Description = "Опытный игрок",
                    RequiredWinnings = 10000f,
                    Rewards = new List<string> { "currency.2000", "title.lucky" }
                },
                new CasinoReward
                {
                    Name = "Мастер казино",
                    Description = "Профессиональный игрок",
                    RequiredWinnings = 50000f,
                    Rewards = new List<string> { "currency.10000", "title.casino_master" }
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
        private Dictionary<ulong, PlayerCasinoData> playerCasino = new Dictionary<ulong, PlayerCasinoData>();
        private Dictionary<ulong, List<ActiveGame>> activeGames = new Dictionary<ulong, List<ActiveGame>>();
        private Dictionary<ulong, List<string>> claimedRewards = new Dictionary<ulong, List<string>>();

        private class PlayerCasinoData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public float TotalBets { get; set; } = 0f;
            public float TotalWinnings { get; set; } = 0f;
            public float TotalLosses { get; set; } = 0f;
            public int TotalGamesPlayed { get; set; } = 0;
            public int TotalWins { get; set; } = 0;
            public List<CasinoTransaction> Transactions { get; set; } = new List<CasinoTransaction>();
            public Dictionary<string, float> GameStats { get; set; } = new Dictionary<string, float>();
            public float LastActivity { get; set; }
        }

        private class CasinoTransaction
        {
            public string Id { get; set; }
            public string GameType { get; set; }
            public float Amount { get; set; }
            public string Result { get; set; }
            public float Timestamp { get; set; }
            public string Details { get; set; }
        }

        private class ActiveGame
        {
            public string Id { get; set; }
            public string GameType { get; set; }
            public float BetAmount { get; set; }
            public float StartTime { get; set; }
            public Dictionary<string, object> GameData { get; set; } = new Dictionary<string, object>();
            public bool IsCompleted { get; set; } = false;
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система казино BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableCasino)
            {
                timer.Every(300f, ProcessCasinoRewards); // Каждые 5 минут
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableCasino)
            {
                InitializePlayerCasino(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerCasinoData(player.userID);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerCasino = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerCasinoData>>("player_casino") ?? new Dictionary<ulong, PlayerCasinoData>();
            activeGames = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ActiveGame>>>("active_games") ?? new Dictionary<ulong, List<ActiveGame>>();
            claimedRewards = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("claimed_casino_rewards") ?? new Dictionary<ulong, List<string>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_casino", playerCasino);
            Interface.Oxide.DataFileSystem.WriteObject("active_games", activeGames);
            Interface.Oxide.DataFileSystem.WriteObject("claimed_casino_rewards", claimedRewards);
        }

        private void SavePlayerCasinoData(ulong playerId)
        {
            if (playerCasino.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"casino_{playerId}", playerCasino[playerId]);
            }
        }

        private void InitializePlayerCasino(BasePlayer player)
        {
            if (!playerCasino.ContainsKey(player.userID))
            {
                var casinoData = new PlayerCasinoData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    LastActivity = Time.time
                };

                playerCasino[player.userID] = casinoData;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerCasino[player.userID].PlayerName = player.displayName;
            }
        }

        private void ProcessCasinoRewards()
        {
            if (!config.EnableCasino) return;

            foreach (var playerId in playerCasino.Keys.ToList())
            {
                CheckCasinoRewards(playerId);
            }
        }

        private void CheckCasinoRewards(ulong playerId)
        {
            if (!playerCasino.ContainsKey(playerId)) return;

            var casinoData = playerCasino[playerId];
            var availableRewards = config.Rewards
                .Where(r => casinoData.TotalWinnings >= r.RequiredWinnings)
                .Where(r => !HasClaimedReward(playerId, r.Name))
                .ToList();

            foreach (var reward in availableRewards)
            {
                GiveCasinoReward(playerId, reward);
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

        private void GiveCasinoReward(ulong playerId, CasinoReward reward)
        {
            if (!claimedRewards.ContainsKey(playerId))
            {
                claimedRewards[playerId] = new List<string>();
            }

            claimedRewards[playerId].Add(reward.Name);

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=green>Получена награда казино: {reward.Name}</color>");
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

        private bool PlaceBet(ulong playerId, string gameType, float amount)
        {
            if (!config.EnableCasino) return false;

            var game = config.Games.FirstOrDefault(g => g.Id == gameType);
            if (game == null || !game.IsActive) return false;

            if (amount < game.MinBet || amount > game.MaxBet) return false;

            // Здесь должна быть логика проверки баланса игрока и списания средств
            // Для примера просто регистрируем ставку

            if (!playerCasino.ContainsKey(playerId))
            {
                InitializePlayerCasino(BasePlayer.FindByID(playerId));
            }

            var casinoData = playerCasino[playerId];
            casinoData.TotalBets += amount;
            casinoData.TotalGamesPlayed++;
            casinoData.LastActivity = Time.time;

            // Добавляем транзакцию
            var transaction = new CasinoTransaction
            {
                Id = Guid.NewGuid().ToString(),
                GameType = gameType,
                Amount = amount,
                Result = "bet",
                Timestamp = Time.time,
                Details = $"Ставка в {game.Name}"
            };

            casinoData.Transactions.Add(transaction);

            return true;
        }

        private void ProcessGameResult(ulong playerId, string gameType, bool won, float amount, string details = "")
        {
            if (!playerCasino.ContainsKey(playerId)) return;

            var casinoData = playerCasino[playerId];
            
            if (won)
            {
                casinoData.TotalWinnings += amount;
                casinoData.TotalWins++;
            }
            else
            {
                casinoData.TotalLosses += amount;
            }

            // Обновляем статистику по играм
            if (!casinoData.GameStats.ContainsKey(gameType))
            {
                casinoData.GameStats[gameType] = 0f;
            }
            casinoData.GameStats[gameType] += won ? amount : -amount;

            // Добавляем транзакцию
            var transaction = new CasinoTransaction
            {
                Id = Guid.NewGuid().ToString(),
                GameType = gameType,
                Amount = amount,
                Result = won ? "win" : "loss",
                Timestamp = Time.time,
                Details = details
            };

            casinoData.Transactions.Add(transaction);

            // Здесь должна быть логика выдачи/списания средств
            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                var resultText = won ? "Победа" : "Поражение";
                var color = won ? "green" : "red";
                player.ChatMessage($"<color={color}>{resultText}: {amount:F2}</color>");
                if (!string.IsNullOrEmpty(details))
                {
                    player.ChatMessage($"<color=white>{details}</color>");
                }
            }

            SaveData();
        }

        private List<string> SpinSlotMachine(string machineId, int betAmount)
        {
            var machine = config.SlotMachines.FirstOrDefault(m => m.Id == machineId);
            if (machine == null) return new List<string>();

            var result = new List<string>();
            var totalWeight = machine.Symbols.Sum(s => s.Weight);

            for (int i = 0; i < 3; i++)
            {
                var random = UnityEngine.Random.Range(0f, totalWeight);
                var currentWeight = 0f;

                foreach (var symbol in machine.Symbols)
                {
                    currentWeight += symbol.Weight;
                    if (random <= currentWeight)
                    {
                        result.Add(symbol.Name);
                        break;
                    }
                }
            }

            return result;
        }

        private float CalculateSlotWin(List<string> result, string machineId, float betAmount)
        {
            var machine = config.SlotMachines.FirstOrDefault(m => m.Id == machineId);
            if (machine == null) return 0f;

            foreach (var combination in machine.Combinations)
            {
                if (result.SequenceEqual(combination.Symbols))
                {
                    return betAmount * combination.Multiplier;
                }
            }

            return 0f;
        }

        private int RollDice()
        {
            return UnityEngine.Random.Range(1, 7);
        }

        private int SpinRoulette()
        {
            return UnityEngine.Random.Range(0, 37);
        }

        private List<string> DealBlackjack()
        {
            var cards = new List<string>();
            var suits = new[] { "♠", "♥", "♦", "♣" };
            var ranks = new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

            for (int i = 0; i < 2; i++)
            {
                var suit = suits[UnityEngine.Random.Range(0, suits.Length)];
                var rank = ranks[UnityEngine.Random.Range(0, ranks.Length)];
                cards.Add($"{rank}{suit}");
            }

            return cards;
        }

        private int CalculateBlackjackValue(List<string> cards)
        {
            int value = 0;
            int aces = 0;

            foreach (var card in cards)
            {
                var rank = card.Substring(0, card.Length - 1);
                switch (rank)
                {
                    case "A":
                        aces++;
                        value += 11;
                        break;
                    case "J":
                    case "Q":
                    case "K":
                        value += 10;
                        break;
                    default:
                        value += int.Parse(rank);
                        break;
                }
            }

            // Обрабатываем тузы
            while (value > 21 && aces > 0)
            {
                value -= 10;
                aces--;
            }

            return value;
        }
        #endregion

        #region Commands
        [ChatCommand("casino")]
        private void CasinoCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableCasino)
            {
                player.ChatMessage("Казино отключено");
                return;
            }

            if (args.Length == 0)
            {
                ShowCasinoHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "games":
                    ShowAvailableGames(player);
                    break;
                case "slots":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /casino slots <автомат> <ставка>");
                        return;
                    }
                    PlaySlotsCommand(player, args[1], args[2]);
                    break;
                case "roulette":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /casino roulette <ставка> <число>");
                        return;
                    }
                    PlayRouletteCommand(player, args[1], args[2]);
                    break;
                case "blackjack":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /casino blackjack <ставка>");
                        return;
                    }
                    PlayBlackjackCommand(player, args[1]);
                    break;
                case "dice":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /casino dice <ставка> <число>");
                        return;
                    }
                    PlayDiceCommand(player, args[1], args[2]);
                    break;
                case "stats":
                    ShowCasinoStats(player);
                    break;
                case "history":
                    ShowGameHistory(player);
                    break;
                case "rewards":
                    ShowCasinoRewards(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowCasinoHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ КАЗИНО ===</color>");
            player.ChatMessage("/casino games - доступные игры");
            player.ChatMessage("/casino slots <автомат> <ставка> - игровые автоматы");
            player.ChatMessage("/casino roulette <ставка> <число> - рулетка");
            player.ChatMessage("/casino blackjack <ставка> - блэкджек");
            player.ChatMessage("/casino dice <ставка> <число> - кости");
            player.ChatMessage("/casino stats - статистика");
            player.ChatMessage("/casino history - история игр");
            player.ChatMessage("/casino rewards - награды");
        }

        private void ShowAvailableGames(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ ИГРЫ ===</color>");
            
            if (config.EnableSlotMachines)
            {
                player.ChatMessage("<color=green>Игровые автоматы:</color>");
                foreach (var machine in config.SlotMachines)
                {
                    player.ChatMessage($"  {machine.Name} (ID: {machine.Id})");
                    player.ChatMessage($"    Ставка: {machine.MinBet:F0} - {machine.MaxBet:F0}");
                }
            }

            if (config.EnableRoulette)
            {
                player.ChatMessage("<color=green>Рулетка:</color>");
                player.ChatMessage("  Угадайте число от 0 до 36");
                player.ChatMessage("  Ставка: 10 - 5000");
            }

            if (config.EnableBlackjack)
            {
                player.ChatMessage("<color=green>Блэкджек:</color>");
                player.ChatMessage("  Наберите 21 очко или ближе к нему");
                player.ChatMessage("  Ставка: 20 - 2000");
            }

            if (config.EnableDice)
            {
                player.ChatMessage("<color=green>Кости:</color>");
                player.ChatMessage("  Угадайте число от 1 до 6");
                player.ChatMessage("  Ставка: 5 - 1000");
            }
        }

        private void PlaySlotsCommand(BasePlayer player, string machineId, string betStr)
        {
            if (!config.EnableSlotMachines)
            {
                player.ChatMessage("Игровые автоматы отключены");
                return;
            }

            if (!float.TryParse(betStr, out float bet) || bet <= 0)
            {
                player.ChatMessage("Неверная ставка");
                return;
            }

            if (!PlaceBet(player.userID, "slots", bet))
            {
                player.ChatMessage("Не удалось сделать ставку");
                return;
            }

            var result = SpinSlotMachine(machineId, (int)bet);
            var winAmount = CalculateSlotWin(result, machineId, bet);
            var won = winAmount > 0;

            ProcessGameResult(player.userID, "slots", won, winAmount, $"Результат: {string.Join(" ", result)}");

            // Показываем результат
            player.ChatMessage($"<color=yellow>=== РЕЗУЛЬТАТ АВТОМАТА ===</color>");
            player.ChatMessage($"<color=white>{string.Join(" | ", result)}</color>");
            if (won)
            {
                player.ChatMessage($"<color=green>Выигрыш: {winAmount:F2}!</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Попробуйте еще раз!</color>");
            }
        }

        private void PlayRouletteCommand(BasePlayer player, string betStr, string numberStr)
        {
            if (!config.EnableRoulette)
            {
                player.ChatMessage("Рулетка отключена");
                return;
            }

            if (!float.TryParse(betStr, out float bet) || !int.TryParse(numberStr, out int number))
            {
                player.ChatMessage("Неверные параметры");
                return;
            }

            if (number < 0 || number > 36)
            {
                player.ChatMessage("Число должно быть от 0 до 36");
                return;
            }

            if (!PlaceBet(player.userID, "roulette", bet))
            {
                player.ChatMessage("Не удалось сделать ставку");
                return;
            }

            var result = SpinRoulette();
            var won = result == number;
            var winAmount = won ? bet * 35f : 0f; // 35:1 коэффициент

            ProcessGameResult(player.userID, "roulette", won, winAmount, $"Загадали: {number}, Выпало: {result}");

            player.ChatMessage($"<color=yellow>=== РЕЗУЛЬТАТ РУЛЕТКИ ===</color>");
            player.ChatMessage($"<color=white>Выпало число: {result}</color>");
            if (won)
            {
                player.ChatMessage($"<color=green>Поздравляем! Выигрыш: {winAmount:F2}!</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не повезло в этот раз!</color>");
            }
        }

        private void PlayBlackjackCommand(BasePlayer player, string betStr)
        {
            if (!config.EnableBlackjack)
            {
                player.ChatMessage("Блэкджек отключен");
                return;
            }

            if (!float.TryParse(betStr, out float bet))
            return;

            if (!PlaceBet(player.userID, "blackjack", bet))
            {
                player.ChatMessage("Не удалось сделать ставку");
                return;
            }

            var playerCards = DealBlackjack();
            var dealerCards = DealBlackjack();
            var playerValue = CalculateBlackjackValue(playerCards);
            var dealerValue = CalculateBlackjackValue(dealerCards);

            var won = playerValue > dealerValue && playerValue <= 21;
            var winAmount = won ? bet * 2f : 0f;

            ProcessGameResult(player.userID, "blackjack", won, winAmount, 
                $"Ваши карты: {string.Join(", ", playerCards)} ({playerValue}), Дилер: {string.Join(", ", dealerCards)} ({dealerValue})");

            player.ChatMessage($"<color=yellow>=== РЕЗУЛЬТАТ БЛЭКДЖЕКА ===</color>");
            player.ChatMessage($"<color=white>Ваши карты: {string.Join(", ", playerCards)} ({playerValue})</color>");
            player.ChatMessage($"<color=white>Карты дилера: {string.Join(", ", dealerCards)} ({dealerValue})</color>");
            
            if (won)
            {
                player.ChatMessage($"<color=green>Победа! Выигрыш: {winAmount:F2}!</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Поражение!</color>");
            }
        }

        private void PlayDiceCommand(BasePlayer player, string betStr, string numberStr)
        {
            if (!config.EnableDice)
            {
                player.ChatMessage("Кости отключены");
                return;
            }

            if (!float.TryParse(betStr, out float bet) || !int.TryParse(numberStr, out int number))
            {
                player.ChatMessage("Неверные параметры");
                return;
            }

            if (number < 1 || number > 6)
            {
                player.ChatMessage("Число должно быть от 1 до 6");
                return;
            }

            if (!PlaceBet(player.userID, "dice", bet))
            {
                player.ChatMessage("Не удалось сделать ставку");
                return;
            }

            var result = RollDice();
            var won = result == number;
            var winAmount = won ? bet * 5f : 0f; // 5:1 коэффициент

            ProcessGameResult(player.userID, "dice", won, winAmount, $"Загадали: {number}, Выпало: {result}");

            player.ChatMessage($"<color=yellow>=== РЕЗУЛЬТАТ КОСТЕЙ ===</color>");
            player.ChatMessage($"<color=white>Выпало: {result}</color>");
            if (won)
            {
                player.ChatMessage($"<color=green>Поздравляем! Выигрыш: {winAmount:F2}!</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не повезло!</color>");
            }
        }

        private void ShowCasinoStats(BasePlayer player)
        {
            if (!playerCasino.ContainsKey(player.userID))
            {
                InitializePlayerCasino(player);
            }

            var casinoData = playerCasino[player.userID];

            player.ChatMessage("<color=yellow>=== СТАТИСТИКА КАЗИНО ===</color>");
            player.ChatMessage($"Всего ставок: {casinoData.TotalBets:F2}");
            player.ChatMessage($"Выиграно: {casinoData.TotalWinnings:F2}");
            player.ChatMessage($"Проиграно: {casinoData.TotalLosses:F2}");
            player.ChatMessage($"Игр сыграно: {casinoData.TotalGamesPlayed}");
            player.ChatMessage($"Побед: {casinoData.TotalWins}");
            player.ChatMessage($"Процент побед: {(casinoData.TotalGamesPlayed > 0 ? (float)casinoData.TotalWins / casinoData.TotalGamesPlayed * 100f : 0f):F1}%");
            player.ChatMessage($"Прибыль: {casinoData.TotalWinnings - casinoData.TotalLosses:F2}");
        }

        private void ShowGameHistory(BasePlayer player)
        {
            if (!playerCasino.ContainsKey(player.userID))
            {
                InitializePlayerCasino(player);
            }

            var casinoData = playerCasino[player.userID];
            var recentTransactions = casinoData.Transactions
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .ToList();

            if (recentTransactions.Count == 0)
            {
                player.ChatMessage("История игр пуста");
                return;
            }

            player.ChatMessage("<color=yellow>=== ИСТОРИЯ ИГР ===</color>");
            foreach (var transaction in recentTransactions)
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - transaction.Timestamp).TotalHours;
                var result = transaction.Result == "win" ? "Победа" : transaction.Result == "loss" ? "Поражение" : "Ставка";
                var color = transaction.Result == "win" ? "green" : transaction.Result == "loss" ? "red" : "yellow";
                
                player.ChatMessage($"<color={color}>{result}: {transaction.Amount:F2} ({transaction.GameType})</color>");
                player.ChatMessage($"  {transaction.Details} ({timeAgo:F1}ч назад)");
            }
        }

        private void ShowCasinoRewards(BasePlayer player)
        {
            if (!playerCasino.ContainsKey(player.userID))
            {
                InitializePlayerCasino(player);
            }

            var casinoData = playerCasino[player.userID];
            var availableRewards = config.Rewards
                .Where(r => casinoData.TotalWinnings >= r.RequiredWinnings)
                .ToList();

            if (availableRewards.Count == 0)
            {
                player.ChatMessage("Нет доступных наград");
                return;
            }

            player.ChatMessage("<color=yellow>=== НАГРАДЫ КАЗИНО ===</color>");
            foreach (var reward in availableRewards)
            {
                var claimed = HasClaimedReward(player.userID, reward.Name);
                var status = claimed ? "Получена" : "Доступна";
                var color = claimed ? "green" : "white";
                
                player.ChatMessage($"<color={color}>{reward.Name} - {status}</color>");
                player.ChatMessage($"  {reward.Description}");
                player.ChatMessage($"  Требуется выигрышей: {reward.RequiredWinnings:F0}");
            }
        }
        #endregion
    }
}
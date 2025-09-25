using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Economy Shop Complete", "BULBARUST", "2.0.0")]
    [Description("Полная система экономики и магазина для BULBARUST")]
    public class EconomyShopComplete : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableEconomy { get; set; } = true;
            public bool EnableShop { get; set; } = true;
            public bool EnableJobs { get; set; } = true;
            public bool EnableBanking { get; set; } = true;
            public bool EnableTrading { get; set; } = true;
            public bool EnableAuction { get; set; } = true;
            public string CurrencyName { get; set; } = "Рубли";
            public string CurrencySymbol { get; set; } = "₽";
            public float StartingBalance { get; set; } = 1000f;
            public float DailyReward { get; set; } = 500f;
            public float KillReward { get; set; } = 100f;
            public float DeathPenalty { get; set; } = 50f;
            public float MaxBalance { get; set; } = 1000000f;
            public float TaxRate { get; set; } = 0.05f; // 5%
            public List<ShopItem> ShopItems { get; set; } = new List<ShopItem>();
            public List<Job> Jobs { get; set; } = new List<Job>();
            public List<AuctionItem> AuctionItems { get; set; } = new List<AuctionItem>();
            public Dictionary<string, float> ItemPrices { get; set; } = new Dictionary<string, float>();
        }

        private class ShopItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public float Price { get; set; }
            public string Category { get; set; } = "Общее";
            public bool IsLimited { get; set; } = false;
            public int MaxStock { get; set; } = 0;
            public int CurrentStock { get; set; } = 0;
            public bool RequirePermission { get; set; } = false;
            public string Permission { get; set; } = "";
            public List<string> RequiredItems { get; set; } = new List<string>();
        }

        private class Job
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public float PayPerHour { get; set; }
            public float PayPerAction { get; set; }
            public List<string> Requirements { get; set; } = new List<string>();
            public List<string> Actions { get; set; } = new List<string>();
            public bool IsActive { get; set; } = true;
            public int MaxWorkers { get; set; } = -1;
        }

        private class AuctionItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; }
            public ulong SellerId { get; set; }
            public string SellerName { get; set; }
            public float StartingPrice { get; set; }
            public float CurrentPrice { get; set; }
            public ulong CurrentBidder { get; set; }
            public string CurrentBidderName { get; set; }
            public float EndTime { get; set; }
            public bool IsActive { get; set; } = true;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем товары в магазин
            config.ShopItems.AddRange(new List<ShopItem>
            {
                new ShopItem
                {
                    Id = "wood_1000",
                    Name = "Дерево (1000)",
                    Description = "1000 единиц дерева",
                    ItemId = -151838493,
                    Amount = 1000,
                    Price = 50f,
                    Category = "Ресурсы"
                },
                new ShopItem
                {
                    Id = "stone_1000",
                    Name = "Камень (1000)",
                    Description = "1000 единиц камня",
                    ItemId = -151838493,
                    Amount = 1000,
                    Price = 75f,
                    Category = "Ресурсы"
                },
                new ShopItem
                {
                    Id = "metal_500",
                    Name = "Металл (500)",
                    Description = "500 единиц металла",
                    ItemId = -151838493,
                    Amount = 500,
                    Price = 100f,
                    Category = "Ресурсы"
                },
                new ShopItem
                {
                    Id = "ak47",
                    Name = "AK-47",
                    Description = "Автомат Калашникова",
                    ItemId = -2069578888,
                    Amount = 1,
                    Price = 5000f,
                    Category = "Оружие",
                    RequirePermission = true,
                    Permission = "economy.weapons"
                },
                new ShopItem
                {
                    Id = "armor",
                    Name = "Броня",
                    Description = "Защитная броня",
                    ItemId = -1251354797,
                    Amount = 1,
                    Price = 3000f,
                    Category = "Броня"
                },
                new ShopItem
                {
                    Id = "c4",
                    Name = "C4",
                    Description = "Взрывчатка C4",
                    ItemId = -1569454159,
                    Amount = 1,
                    Price = 10000f,
                    Category = "Взрывчатка",
                    RequirePermission = true,
                    Permission = "economy.explosives"
                }
            });

            // Добавляем работы
            config.Jobs.AddRange(new List<Job>
            {
                new Job
                {
                    Id = "miner",
                    Name = "Шахтер",
                    Description = "Добыча ресурсов",
                    PayPerHour = 200f,
                    PayPerAction = 10f,
                    Actions = new List<string> { "mine_stone", "mine_wood", "mine_metal" },
                    MaxWorkers = 10
                },
                new Job
                {
                    Id = "guard",
                    Name = "Охранник",
                    Description = "Защита территории",
                    PayPerHour = 300f,
                    PayPerAction = 15f,
                    Actions = new List<string> { "patrol", "defend" },
                    MaxWorkers = 5
                },
                new Job
                {
                    Id = "trader",
                    Name = "Торговец",
                    Description = "Продажа товаров",
                    PayPerHour = 250f,
                    PayPerAction = 5f,
                    Actions = new List<string> { "sell_items", "buy_items" },
                    MaxWorkers = 3
                }
            });

            // Настройки цен предметов
            config.ItemPrices.Add("wood", 0.05f);
            config.ItemPrices.Add("stone", 0.08f);
            config.ItemPrices.Add("metal.fragments", 0.2f);
            config.ItemPrices.Add("scrap", 1f);
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
        private Dictionary<ulong, PlayerEconomy> playerEconomy = new Dictionary<ulong, PlayerEconomy>();
        private Dictionary<ulong, string> playerJobs = new Dictionary<ulong, string>();
        private Dictionary<ulong, float> lastDailyReward = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastJobPayment = new Dictionary<ulong, float>();
        private Dictionary<ulong, List<Transaction>> playerTransactions = new Dictionary<ulong, List<Transaction>>();
        private Dictionary<string, AuctionItem> auctionItems = new Dictionary<string, AuctionItem>();
        private Dictionary<ulong, List<string>> playerAuctions = new Dictionary<ulong, List<string>>();

        private class PlayerEconomy
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public float Balance { get; set; } = 0f;
            public float TotalEarned { get; set; } = 0f;
            public float TotalSpent { get; set; } = 0f;
            public float BankBalance { get; set; } = 0f;
            public float TotalDeposits { get; set; } = 0f;
            public float TotalWithdrawals { get; set; } = 0f;
            public float LastActivity { get; set; }
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }

        private class Transaction
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public float Amount { get; set; }
            public string Description { get; set; }
            public float Timestamp { get; set; }
            public string FromPlayer { get; set; }
            public string ToPlayer { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Полная система экономики BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableEconomy)
            {
                timer.Every(300f, ProcessEconomyUpdates);
                timer.Every(3600f, ProcessJobPayments);
                timer.Every(86400f, ProcessDailyRewards);
                timer.Every(60f, ProcessAuctions);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableEconomy)
            {
                InitializePlayerEconomy(player);
                CheckPlayerEconomyStatus(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerEconomyData(player.userID);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableEconomy) return;

            var player = entity as BasePlayer;
            if (player == null) return;

            var killer = info.InitiatorPlayer;
            if (killer == null || killer == player) return;

            // Выдаем награду за убийство
            GiveKillReward(killer, player);
            
            // Штраф за смерть
            ApplyDeathPenalty(player);
        }

        void OnItemAdded(ItemContainer container, Item item)
        {
            if (!config.EnableEconomy) return;

            var player = container.playerOwner;
            if (player == null) return;

            // Проверяем, можно ли продать предмет
            CheckItemForSale(player, item);
        }

        void OnItemRemoved(ItemContainer container, Item item)
        {
            if (!config.EnableEconomy) return;

            var player = container.playerOwner;
            if (player == null) return;

            // Проверяем, не продается ли предмет
            CheckItemSale(player, item);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerEconomy = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerEconomy>>("player_economy") ?? new Dictionary<ulong, PlayerEconomy>();
            playerJobs = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("player_jobs") ?? new Dictionary<ulong, string>();
            lastDailyReward = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("daily_rewards") ?? new Dictionary<ulong, float>();
            lastJobPayment = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("job_payments") ?? new Dictionary<ulong, float>();
            playerTransactions = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<Transaction>>>("player_transactions") ?? new Dictionary<ulong, List<Transaction>>();
            auctionItems = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, AuctionItem>>("auction_items") ?? new Dictionary<string, AuctionItem>();
            playerAuctions = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("player_auctions") ?? new Dictionary<ulong, List<string>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_economy", playerEconomy);
            Interface.Oxide.DataFileSystem.WriteObject("player_jobs", playerJobs);
            Interface.Oxide.DataFileSystem.WriteObject("daily_rewards", lastDailyReward);
            Interface.Oxide.DataFileSystem.WriteObject("job_payments", lastJobPayment);
            Interface.Oxide.DataFileSystem.WriteObject("player_transactions", playerTransactions);
            Interface.Oxide.DataFileSystem.WriteObject("auction_items", auctionItems);
            Interface.Oxide.DataFileSystem.WriteObject("player_auctions", playerAuctions);
        }

        private void SavePlayerEconomyData(ulong playerId)
        {
            if (playerEconomy.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"economy_{playerId}", playerEconomy[playerId]);
            }
            if (playerTransactions.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"transactions_{playerId}", playerTransactions[playerId]);
            }
        }

        private void InitializePlayerEconomy(BasePlayer player)
        {
            if (!playerEconomy.ContainsKey(player.userID))
            {
                var economy = new PlayerEconomy
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    Balance = config.StartingBalance,
                    TotalEarned = 0f,
                    TotalSpent = 0f,
                    BankBalance = 0f,
                    LastActivity = Time.time
                };

                playerEconomy[player.userID] = economy;

                // Добавляем стартовую транзакцию
                AddTransaction(player.userID, "starting_balance", config.StartingBalance, "Стартовый баланс");
            }
            else
            {
                var economy = playerEconomy[player.userID];
                economy.PlayerName = player.displayName;
                economy.LastActivity = Time.time;
            }

            if (!playerTransactions.ContainsKey(player.userID))
            {
                playerTransactions[player.userID] = new List<Transaction>();
            }
        }

        private void CheckPlayerEconomyStatus(BasePlayer player)
        {
            if (!playerEconomy.ContainsKey(player.userID)) return;

            var economy = playerEconomy[player.userID];
            player.ChatMessage($"<color=yellow>Баланс: {economy.Balance:F0} {config.CurrencySymbol}</color>");
            player.ChatMessage($"<color=white>Банк: {economy.BankBalance:F0} {config.CurrencySymbol}</color>");
        }

        private void ProcessEconomyUpdates()
        {
            // Обновляем экономику каждые 5 минут
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (playerEconomy.ContainsKey(player.userID))
                {
                    var economy = playerEconomy[player.userID];
                    economy.LastActivity = Time.time;
                }
            }
        }

        private void ProcessJobPayments()
        {
            var currentTime = Time.time;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!playerJobs.ContainsKey(player.userID)) continue;

                var jobId = playerJobs[player.userID];
                var job = config.Jobs.FirstOrDefault(j => j.Id == jobId);
                if (job == null || !job.IsActive) continue;

                var lastPayment = lastJobPayment.ContainsKey(player.userID) ? lastJobPayment[player.userID] : 0f;
                if (currentTime - lastPayment >= 3600f) // 1 час
                {
                    PayJobSalary(player, job);
                    lastJobPayment[player.userID] = currentTime;
                }
            }
        }

        private void ProcessDailyRewards()
        {
            var currentTime = Time.time;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!playerEconomy.ContainsKey(player.userID)) continue;

                var lastReward = lastDailyReward.ContainsKey(player.userID) ? lastDailyReward[player.userID] : 0f;
                if (currentTime - lastReward >= 86400f) // 24 часа
                {
                    GiveDailyReward(player);
                    lastDailyReward[player.userID] = currentTime;
                }
            }
        }

        private void ProcessAuctions()
        {
            var currentTime = Time.time;
            var expiredAuctions = new List<string>();

            foreach (var auction in auctionItems.Values)
            {
                if (auction.IsActive && currentTime >= auction.EndTime)
                {
                    expiredAuctions.Add(auction.Id);
                    EndAuction(auction);
                }
            }

            foreach (var auctionId in expiredAuctions)
            {
                auctionItems.Remove(auctionId);
            }

            if (expiredAuctions.Count > 0)
            {
                SaveData();
            }
        }

        private void GiveDailyReward(BasePlayer player)
        {
            if (!playerEconomy.ContainsKey(player.userID)) return;

            var economy = playerEconomy[player.userID];
            economy.Balance += config.DailyReward;
            economy.TotalEarned += config.DailyReward;

            AddTransaction(player.userID, "daily_reward", config.DailyReward, "Ежедневная награда");
            player.ChatMessage($"<color=green>Ежедневная награда: {config.DailyReward:F0} {config.CurrencySymbol}</color>");
            SaveData();
        }

        private void GiveKillReward(BasePlayer killer, BasePlayer victim)
        {
            if (!playerEconomy.ContainsKey(killer.userID)) return;

            var economy = playerEconomy[killer.userID];
            economy.Balance += config.KillReward;
            economy.TotalEarned += config.KillReward;

            AddTransaction(killer.userID, "kill_reward", config.KillReward, $"Награда за убийство {victim.displayName}");
            killer.ChatMessage($"<color=green>Награда за убийство: {config.KillReward:F0} {config.CurrencySymbol}</color>");
            SaveData();
        }

        private void ApplyDeathPenalty(BasePlayer player)
        {
            if (!playerEconomy.ContainsKey(player.userID)) return;

            var economy = playerEconomy[player.userID];
            var penalty = Math.Min(config.DeathPenalty, economy.Balance * 0.1f); // Максимум 10% от баланса
            
            economy.Balance -= penalty;
            economy.TotalSpent += penalty;

            AddTransaction(player.userID, "death_penalty", -penalty, "Штраф за смерть");
            player.ChatMessage($"<color=red>Штраф за смерть: {penalty:F0} {config.CurrencySymbol}</color>");
            SaveData();
        }

        private void PayJobSalary(BasePlayer player, Job job)
        {
            if (!playerEconomy.ContainsKey(player.userID)) return;

            var economy = playerEconomy[player.userID];
            economy.Balance += job.PayPerHour;
            economy.TotalEarned += job.PayPerHour;

            AddTransaction(player.userID, "job_salary", job.PayPerHour, $"Зарплата за работу: {job.Name}");
            player.ChatMessage($"<color=green>Зарплата: {job.PayPerHour:F0} {config.CurrencySymbol}</color>");
            SaveData();
        }

        private void CheckItemForSale(BasePlayer player, Item item)
        {
            var sellPrice = GetItemSellPrice(item);
            if (sellPrice > 0)
            {
                player.ChatMessage($"<color=yellow>Предмет {item.info.displayName.english} можно продать за {sellPrice:F0} {config.CurrencySymbol}</color>");
            }
        }

        private void CheckItemSale(BasePlayer player, Item item)
        {
            // Здесь можно добавить логику автоматической продажи предметов
        }

        private float GetItemSellPrice(Item item)
        {
            if (item == null || item.info == null) return 0f;

            var shortname = item.info.shortname;
            if (config.ItemPrices.ContainsKey(shortname))
            {
                return config.ItemPrices[shortname] * item.amount;
            }

            // Базовая цена продажи предмета
            return item.info.stackable ? item.amount * 0.1f : 1f;
        }

        private void AddTransaction(ulong playerId, string type, float amount, string description)
        {
            if (!playerTransactions.ContainsKey(playerId))
            {
                playerTransactions[playerId] = new List<Transaction>();
            }

            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Amount = amount,
                Description = description,
                Timestamp = Time.time
            };

            playerTransactions[playerId].Add(transaction);

            // Ограничиваем количество транзакций
            if (playerTransactions[playerId].Count > 1000)
            {
                playerTransactions[playerId] = playerTransactions[playerId].OrderByDescending(t => t.Timestamp).Take(500).ToList();
            }
        }

        private bool CanAfford(ulong playerId, float amount)
        {
            if (!playerEconomy.ContainsKey(playerId)) return false;
            return playerEconomy[playerId].Balance >= amount;
        }

        private void AddMoney(ulong playerId, float amount, string reason = "")
        {
            if (!playerEconomy.ContainsKey(playerId)) return;

            var economy = playerEconomy[playerId];
            economy.Balance = Math.Min(economy.Balance + amount, config.MaxBalance);
            economy.TotalEarned += amount;

            if (!string.IsNullOrEmpty(reason))
            {
                AddTransaction(playerId, "money_add", amount, reason);
            }
        }

        private bool RemoveMoney(ulong playerId, float amount, string reason = "")
        {
            if (!playerEconomy.ContainsKey(playerId)) return false;
            if (!CanAfford(playerId, amount)) return false;

            var economy = playerEconomy[playerId];
            economy.Balance -= amount;
            economy.TotalSpent += amount;

            if (!string.IsNullOrEmpty(reason))
            {
                AddTransaction(playerId, "money_remove", -amount, reason);
            }

            return true;
        }

        private void EndAuction(AuctionItem auction)
        {
            if (auction.CurrentBidder != 0)
            {
                // Аукцион завершен с победителем
                var winner = BasePlayer.FindByID(auction.CurrentBidder);
                if (winner != null)
                {
                    GiveItemToPlayer(winner, auction.ItemId, auction.Amount);
                    winner.ChatMessage($"<color=green>Вы выиграли аукцион: {auction.Name}</color>");
                }

                // Переводим деньги продавцу
                if (playerEconomy.ContainsKey(auction.SellerId))
                {
                    AddMoney(auction.SellerId, auction.CurrentPrice, $"Продажа на аукционе: {auction.Name}");
                }
            }
            else
            {
                // Аукцион завершен без победителя - возвращаем предмет продавцу
                var seller = BasePlayer.FindByID(auction.SellerId);
                if (seller != null)
                {
                    GiveItemToPlayer(seller, auction.ItemId, auction.Amount);
                    seller.ChatMessage($"<color=yellow>Аукцион {auction.Name} завершен без победителя</color>");
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
        #endregion

        #region Commands
        [ChatCommand("balance")]
        private void BalanceCommand(BasePlayer player, string command, string[] args)
        {
            if (!playerEconomy.ContainsKey(player.userID))
            {
                InitializePlayerEconomy(player);
            }

            var economy = playerEconomy[player.userID];
            player.ChatMessage($"<color=yellow>=== ВАШ БАЛАНС ===</color>");
            player.ChatMessage($"Наличные: {economy.Balance:F0} {config.CurrencySymbol}");
            player.ChatMessage($"Банк: {economy.BankBalance:F0} {config.CurrencySymbol}");
            player.ChatMessage($"Заработано: {economy.TotalEarned:F0} {config.CurrencySymbol}");
            player.ChatMessage($"Потрачено: {economy.TotalSpent:F0} {config.CurrencySymbol}");
        }

        [ChatCommand("shop")]
        private void ShopCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableShop)
            {
                player.ChatMessage("Магазин отключен");
                return;
            }

            if (args.Length == 0)
            {
                ShowShopCategories(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /shop list <категория>");
                        return;
                    }
                    ShowShopItems(player, args[1]);
                    break;
                case "buy":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /shop buy <id> <количество>");
                        return;
                    }
                    BuyItem(player, args[1], int.Parse(args[2]));
                    break;
                case "sell":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /shop sell <предмет>");
                        return;
                    }
                    SellItem(player, args[1]);
                    break;
            }
        }

        [ChatCommand("job")]
        private void JobCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableJobs)
            {
                player.ChatMessage("Система работ отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowAvailableJobs(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "join":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /job join <работа>");
                        return;
                    }
                    JoinJob(player, args[1]);
                    break;
                case "leave":
                    LeaveJob(player);
                    break;
                case "info":
                    ShowJobInfo(player);
                    break;
            }
        }

        [ChatCommand("bank")]
        private void BankCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableBanking)
            {
                player.ChatMessage("Банковская система отключена");
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /bank <deposit|withdraw|balance> <сумма>");
                return;
            }

            var action = args[0].ToLower();
            var amount = float.Parse(args[1]);

            switch (action)
            {
                case "deposit":
                    DepositMoney(player, amount);
                    break;
                case "withdraw":
                    WithdrawMoney(player, amount);
                    break;
                case "balance":
                    ShowBankBalance(player);
                    break;
            }
        }

        [ChatCommand("auction")]
        private void AuctionCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableAuction)
            {
                player.ChatMessage("Система аукционов отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowAuctionHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowAuctionList(player);
                    break;
                case "bid":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /auction bid <id> <сумма>");
                        return;
                    }
                    BidOnAuction(player, args[1], float.Parse(args[2]));
                    break;
                case "create":
                    if (args.Length < 4)
                    {
                        player.ChatMessage("Использование: /auction create <предмет> <количество> <цена>");
                        return;
                    }
                    CreateAuction(player, args[1], int.Parse(args[2]), float.Parse(args[3]));
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowShopCategories(BasePlayer player)
        {
            var categories = config.ShopItems.Select(item => item.Category).Distinct().ToList();
            
            player.ChatMessage("<color=yellow>=== КАТЕГОРИИ МАГАЗИНА ===</color>");
            foreach (var category in categories)
            {
                player.ChatMessage($"<color=cyan>{category}</color>");
            }
            player.ChatMessage("Используйте: /shop list <категория>");
        }

        private void ShowShopItems(BasePlayer player, string category)
        {
            var items = config.ShopItems.Where(item => item.Category.ToLower() == category.ToLower()).ToList();
            
            if (items.Count == 0)
            {
                player.ChatMessage($"Категория {category} не найдена");
                return;
            }

            player.ChatMessage($"<color=yellow>=== ТОВАРЫ: {category.ToUpper()} ===</color>");
            foreach (var item in items)
            {
                var stock = item.IsLimited ? $" (Остаток: {item.CurrentStock})" : "";
                player.ChatMessage($"<color=cyan>{item.Id}</color> - {item.Name} - {item.Price:F0} {config.CurrencySymbol}{stock}");
                player.ChatMessage($"  {item.Description}");
            }
        }

        private void BuyItem(BasePlayer player, string itemId, int amount)
        {
            var item = config.ShopItems.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                player.ChatMessage($"Товар {itemId} не найден");
                return;
            }

            if (item.RequirePermission && !string.IsNullOrEmpty(item.Permission))
            {
                if (!permission.UserHasPermission(player.UserIDString, item.Permission))
                {
                    player.ChatMessage("У вас нет прав на покупку этого товара");
                    return;
                }
            }

            if (item.IsLimited && item.CurrentStock < amount)
            {
                player.ChatMessage($"Недостаточно товара на складе. Доступно: {item.CurrentStock}");
                return;
            }

            var totalPrice = item.Price * amount;
            if (!CanAfford(player.userID, totalPrice))
            {
                player.ChatMessage($"Недостаточно средств. Нужно: {totalPrice:F0} {config.CurrencySymbol}");
                return;
            }

            // Проверяем требования
            foreach (var requiredItem in item.RequiredItems)
            {
                var parts = requiredItem.Split(':');
                if (parts.Length != 2) continue;

                var requiredItemName = parts[0];
                var requiredAmount = int.Parse(parts[1]);

                var playerItem = player.inventory.FindItemByName(requiredItemName);
                if (playerItem == null || playerItem.amount < requiredAmount)
                {
                    player.ChatMessage($"Требуется: {requiredAmount} {requiredItemName}");
                    return;
                }
            }

            // Покупаем товар
            if (RemoveMoney(player.userID, totalPrice, $"Покупка {item.Name} x{amount}"))
            {
                GiveItemToPlayer(player, item.ItemId, item.Amount * amount);
                player.ChatMessage($"<color=green>Куплено: {item.Name} x{amount}</color>");

                if (item.IsLimited)
                {
                    item.CurrentStock -= amount;
                }

                SaveData();
            }
        }

        private void SellItem(BasePlayer player, string itemName)
        {
            var item = player.inventory.FindItemByName(itemName);
            if (item == null)
            {
                player.ChatMessage($"Предмет {itemName} не найден в инвентаре");
                return;
            }

            var sellPrice = GetItemSellPrice(item);
            if (sellPrice <= 0)
            {
                player.ChatMessage("Этот предмет нельзя продать");
                return;
            }

            item.RemoveFromContainer();
            AddMoney(player.userID, sellPrice, $"Продажа {item.info.displayName.english}");
            player.ChatMessage($"<color=green>Продано: {item.info.displayName.english} за {sellPrice:F0} {config.CurrencySymbol}</color>");
        }

        private void ShowAvailableJobs(BasePlayer player)
        {
            var availableJobs = config.Jobs.Where(job => job.IsActive).ToList();
            
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ РАБОТЫ ===</color>");
            foreach (var job in availableJobs)
            {
                player.ChatMessage($"<color=cyan>{job.Id}</color> - {job.Name}");
                player.ChatMessage($"  {job.Description}");
                player.ChatMessage($"  Зарплата: {job.PayPerHour:F0} {config.CurrencySymbol}/час");
            }
        }

        private void JoinJob(BasePlayer player, string jobId)
        {
            var job = config.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                player.ChatMessage($"Работа {jobId} не найдена");
                return;
            }

            if (!job.IsActive)
            {
                player.ChatMessage("Эта работа недоступна");
                return;
            }

            if (job.MaxWorkers > 0)
            {
                var currentWorkers = playerJobs.Values.Count(j => j == jobId);
                if (currentWorkers >= job.MaxWorkers)
                {
                    player.ChatMessage("На эту работу уже набрано достаточно работников");
                    return;
                }
            }

            playerJobs[player.userID] = jobId;
            player.ChatMessage($"<color=green>Вы устроились на работу: {job.Name}</color>");
            SaveData();
        }

        private void LeaveJob(BasePlayer player)
        {
            if (!playerJobs.ContainsKey(player.userID))
            {
                player.ChatMessage("Вы не работаете");
                return;
            }

            var jobId = playerJobs[player.userID];
            playerJobs.Remove(player.userID);
            player.ChatMessage($"<color=yellow>Вы уволились с работы: {jobId}</color>");
            SaveData();
        }

        private void ShowJobInfo(BasePlayer player)
        {
            if (!playerJobs.ContainsKey(player.userID))
            {
                player.ChatMessage("Вы не работаете");
                return;
            }

            var jobId = playerJobs[player.userID];
            var job = config.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                player.ChatMessage("Информация о вашей работе не найдена");
                return;
            }

            player.ChatMessage($"<color=yellow>=== ВАША РАБОТА ===</color>");
            player.ChatMessage($"Название: {job.Name}");
            player.ChatMessage($"Описание: {job.Description}");
            player.ChatMessage($"Зарплата: {job.PayPerHour:F0} {config.CurrencySymbol}/час");
        }

        private void DepositMoney(BasePlayer player, float amount)
        {
            if (amount <= 0)
            {
                player.ChatMessage("Сумма должна быть больше 0");
                return;
            }

            if (!CanAfford(player.userID, amount))
            {
                player.ChatMessage("Недостаточно средств");
                return;
            }

            if (RemoveMoney(player.userID, amount, "Депозит в банк"))
            {
                var economy = playerEconomy[player.userID];
                economy.BankBalance += amount;
                economy.TotalDeposits += amount;
                player.ChatMessage($"<color=green>Депозит: {amount:F0} {config.CurrencySymbol}</color>");
                SaveData();
            }
        }

        private void WithdrawMoney(BasePlayer player, float amount)
        {
            if (amount <= 0)
            {
                player.ChatMessage("Сумма должна быть больше 0");
                return;
            }

            var economy = playerEconomy[player.userID];
            if (economy.BankBalance < amount)
            {
                player.ChatMessage("Недостаточно средств в банке");
                return;
            }

            economy.BankBalance -= amount;
            economy.TotalWithdrawals += amount;
            AddMoney(player.userID, amount, "Снятие с банка");
            player.ChatMessage($"<color=green>Снято: {amount:F0} {config.CurrencySymbol}</color>");
            SaveData();
        }

        private void ShowBankBalance(BasePlayer player)
        {
            var economy = playerEconomy[player.userID];
            player.ChatMessage($"<color=yellow>Банковский баланс: {economy.BankBalance:F0} {config.CurrencySymbol}</color>");
        }

        private void ShowAuctionHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ АУКЦИОНА ===</color>");
            player.ChatMessage("/auction list - список аукционов");
            player.ChatMessage("/auction bid <id> <сумма> - сделать ставку");
            player.ChatMessage("/auction create <предмет> <количество> <цена> - создать аукцион");
        }

        private void ShowAuctionList(BasePlayer player)
        {
            var activeAuctions = auctionItems.Values.Where(a => a.IsActive).ToList();
            
            if (activeAuctions.Count == 0)
            {
                player.ChatMessage("Нет активных аукционов");
                return;
            }

            player.ChatMessage("<color=yellow>=== АКТИВНЫЕ АУКЦИОНЫ ===</color>");
            foreach (var auction in activeAuctions)
            {
                var timeLeft = auction.EndTime - Time.time;
                player.ChatMessage($"<color=cyan>{auction.Id}</color> - {auction.Name} x{auction.Amount}");
                player.ChatMessage($"  Текущая цена: {auction.CurrentPrice:F0} {config.CurrencySymbol}");
                player.ChatMessage($"  Осталось: {timeLeft:F0} секунд");
            }
        }

        private void BidOnAuction(BasePlayer player, string auctionId, float amount)
        {
            if (!auctionItems.ContainsKey(auctionId))
            {
                player.ChatMessage($"Аукцион {auctionId} не найден");
                return;
            }

            var auction = auctionItems[auctionId];
            if (!auction.IsActive)
            {
                player.ChatMessage("Аукцион завершен");
                return;
            }

            if (auction.SellerId == player.userID)
            {
                player.ChatMessage("Нельзя делать ставки на собственный аукцион");
                return;
            }

            if (amount <= auction.CurrentPrice)
            {
                player.ChatMessage($"Ставка должна быть больше текущей цены ({auction.CurrentPrice:F0})");
                return;
            }

            if (!CanAfford(player.userID, amount))
            {
                player.ChatMessage("Недостаточно средств");
                return;
            }

            // Возвращаем деньги предыдущему участнику
            if (auction.CurrentBidder != 0 && playerEconomy.ContainsKey(auction.CurrentBidder))
            {
                AddMoney(auction.CurrentBidder, auction.CurrentPrice, $"Возврат ставки: {auction.Name}");
            }

            // Блокируем деньги нового участника
            if (RemoveMoney(player.userID, amount, $"Ставка на аукцион: {auction.Name}"))
            {
                auction.CurrentPrice = amount;
                auction.CurrentBidder = player.userID;
                auction.CurrentBidderName = player.displayName;

                player.ChatMessage($"<color=green>Ставка принята: {amount:F0} {config.CurrencySymbol}</color>");
                SaveData();
            }
        }

        private void CreateAuction(BasePlayer player, string itemName, int amount, float startingPrice)
        {
            var item = player.inventory.FindItemByName(itemName);
            if (item == null)
            {
                player.ChatMessage($"Предмет {itemName} не найден в инвентаре");
                return;
            }

            if (item.amount < amount)
            {
                player.ChatMessage($"Недостаточно предметов. У вас: {item.amount}, нужно: {amount}");
                return;
            }

            if (startingPrice <= 0)
            {
                player.ChatMessage("Начальная цена должна быть больше 0");
                return;
            }

            var auctionId = Guid.NewGuid().ToString();
            var auction = new AuctionItem
            {
                Id = auctionId,
                Name = item.info.displayName.english,
                ItemId = item.info.itemid,
                Amount = amount,
                SellerId = player.userID,
                SellerName = player.displayName,
                StartingPrice = startingPrice,
                CurrentPrice = startingPrice,
                EndTime = Time.time + 3600f, // 1 час
                IsActive = true
            };

            auctionItems[auctionId] = auction;

            if (!playerAuctions.ContainsKey(player.userID))
            {
                playerAuctions[player.userID] = new List<string>();
            }
            playerAuctions[player.userID].Add(auctionId);

            item.amount -= amount;
            if (item.amount <= 0)
            {
                item.RemoveFromContainer();
            }

            player.ChatMessage($"<color=green>Аукцион создан: {auction.Name} x{amount}</color>");
            SaveData();
        }
        #endregion
    }
}
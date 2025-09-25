using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Economy Shop", "BULBARUST", "1.0.0")]
    [Description("Система экономики и магазина для сервера BULBARUST")]
    public class EconomyShop : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public string CurrencyName { get; set; } = "Рубли";
            public string CurrencySymbol { get; set; } = "₽";
            public float StartingBalance { get; set; } = 1000f;
            public float DailyReward { get; set; } = 500f;
            public bool EnableShop { get; set; } = true;
            public bool EnableJobs { get; set; } = true;
            public bool EnableBanking { get; set; } = true;
            public List<ShopItem> ShopItems { get; set; } = new List<ShopItem>();
            public List<Job> Jobs { get; set; } = new List<Job>();
        }

        private class ShopItem
        {
            public string ItemName { get; set; }
            public int ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public float Price { get; set; }
            public string Category { get; set; } = "Общее";
            public bool IsLimited { get; set; } = false;
            public int MaxStock { get; set; } = 0;
        }

        private class Job
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float PayPerHour { get; set; }
            public List<string> Requirements { get; set; } = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем товары в магазин
            config.ShopItems.AddRange(new List<ShopItem>
            {
                new ShopItem { ItemName = "Дерево", ItemId = -151838493, Amount = 1000, Price = 50f, Category = "Ресурсы" },
                new ShopItem { ItemName = "Камень", ItemId = -151838493, Amount = 1000, Price = 75f, Category = "Ресурсы" },
                new ShopItem { ItemName = "Металл", ItemId = -151838493, Amount = 500, Price = 100f, Category = "Ресурсы" },
                new ShopItem { ItemName = "AK-47", ItemId = -2069578888, Amount = 1, Price = 5000f, Category = "Оружие" },
                new ShopItem { ItemName = "Броня", ItemId = -1251354797, Amount = 1, Price = 3000f, Category = "Броня" },
                new ShopItem { ItemName = "C4", ItemId = -1569454159, Amount = 1, Price = 10000f, Category = "Взрывчатка" }
            });

            // Добавляем работы
            config.Jobs.AddRange(new List<Job>
            {
                new Job { Name = "Шахтер", Description = "Добыча ресурсов", PayPerHour = 200f },
                new Job { Name = "Охранник", Description = "Защита территории", PayPerHour = 300f },
                new Job { Name = "Торговец", Description = "Продажа товаров", PayPerHour = 250f }
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
        private Dictionary<ulong, PlayerEconomy> playerEconomy = new Dictionary<ulong, PlayerEconomy>();
        private Dictionary<ulong, string> playerJobs = new Dictionary<ulong, string>();
        private Dictionary<ulong, float> lastDailyReward = new Dictionary<ulong, float>();

        private class PlayerEconomy
        {
            public float Balance { get; set; } = 0f;
            public float TotalEarned { get; set; } = 0f;
            public float TotalSpent { get; set; } = 0f;
            public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        }

        private class Transaction
        {
            public string Type { get; set; }
            public float Amount { get; set; }
            public string Description { get; set; }
            public float Timestamp { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            Puts("Система экономики BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableJobs)
            {
                timer.Every(3600f, PayJobSalaries); // Каждый час
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!playerEconomy.ContainsKey(player.userID))
            {
                InitializePlayerEconomy(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerData(player.userID);
        }
        #endregion

        #region Methods
        private void InitializePlayerEconomy(BasePlayer player)
        {
            var economy = new PlayerEconomy
            {
                Balance = config.StartingBalance
            };
            playerEconomy[player.userID] = economy;
            
            player.ChatMessage($"<color=green>Добро пожаловать в экономику BULBARUST!</color>");
            player.ChatMessage($"Ваш стартовый баланс: {config.CurrencySymbol}{config.StartingBalance}");
            player.ChatMessage("Используйте /shop для открытия магазина");
        }

        private void AddTransaction(ulong playerId, string type, float amount, string description)
        {
            if (!playerEconomy.ContainsKey(playerId)) return;

            var transaction = new Transaction
            {
                Type = type,
                Amount = amount,
                Description = description,
                Timestamp = Time.time
            };

            playerEconomy[playerId].Transactions.Add(transaction);
            
            if (type == "income")
            {
                playerEconomy[playerId].TotalEarned += amount;
            }
            else if (type == "expense")
            {
                playerEconomy[playerId].TotalSpent += amount;
            }
        }

        private bool HasEnoughMoney(ulong playerId, float amount)
        {
            return playerEconomy.ContainsKey(playerId) && playerEconomy[playerId].Balance >= amount;
        }

        private void AddMoney(ulong playerId, float amount, string reason = "")
        {
            if (!playerEconomy.ContainsKey(playerId)) return;

            playerEconomy[playerId].Balance += amount;
            AddTransaction(playerId, "income", amount, reason);
        }

        private bool RemoveMoney(ulong playerId, float amount, string reason = "")
        {
            if (!HasEnoughMoney(playerId, amount)) return false;

            playerEconomy[playerId].Balance -= amount;
            AddTransaction(playerId, "expense", amount, reason);
            return true;
        }

        private void PayJobSalaries()
        {
            foreach (var job in playerJobs)
            {
                var player = BasePlayer.FindByID(job.Key);
                if (player != null && player.IsConnected)
                {
                    var jobData = config.Jobs.FirstOrDefault(j => j.Name == job.Value);
                    if (jobData != null)
                    {
                        AddMoney(job.Key, jobData.PayPerHour, $"Зарплата за работу: {jobData.Name}");
                        player.ChatMessage($"<color=green>Получена зарплата: {config.CurrencySymbol}{jobData.PayPerHour}</color>");
                    }
                }
            }
        }

        private void GiveDailyReward(BasePlayer player)
        {
            var playerId = player.userID;
            var currentTime = Time.time;
            
            if (!lastDailyReward.ContainsKey(playerId) || 
                currentTime - lastDailyReward[playerId] >= 86400f) // 24 часа
            {
                AddMoney(playerId, config.DailyReward, "Ежедневная награда");
                lastDailyReward[playerId] = currentTime;
                player.ChatMessage($"<color=yellow>Ежедневная награда: {config.CurrencySymbol}{config.DailyReward}</color>");
            }
            else
            {
                var timeLeft = 86400f - (currentTime - lastDailyReward[playerId]);
                var hoursLeft = Mathf.CeilToInt(timeLeft / 3600f);
                player.ChatMessage($"<color=red>Следующая ежедневная награда через {hoursLeft} часов</color>");
            }
        }

        private void SavePlayerData(ulong playerId)
        {
            if (playerEconomy.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"economy_{playerId}", playerEconomy[playerId]);
            }
        }

        private void LoadPlayerData(ulong playerId)
        {
            var data = Interface.Oxide.DataFileSystem.ReadObject<PlayerEconomy>($"economy_{playerId}");
            if (data != null)
            {
                playerEconomy[playerId] = data;
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
                return;
            }

            var economy = playerEconomy[player.userID];
            player.ChatMessage($"<color=yellow>=== БАЛАНС ===</color>");
            player.ChatMessage($"Текущий баланс: {config.CurrencySymbol}{economy.Balance:F2}");
            player.ChatMessage($"Всего заработано: {config.CurrencySymbol}{economy.TotalEarned:F2}");
            player.ChatMessage($"Всего потрачено: {config.CurrencySymbol}{economy.TotalSpent:F2}");
        }

        [ChatCommand("shop")]
        private void ShopCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableShop)
            {
                player.ChatMessage("Магазин временно недоступен");
                return;
            }

            if (args.Length == 0)
            {
                ShowShopCategories(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "buy":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /shop buy <номер_товара> <количество>");
                        return;
                    }
                    BuyItem(player, args);
                    break;
                case "list":
                    ShowShopItems(player, args.Length > 1 ? args[1] : "Все");
                    break;
                default:
                    ShowShopCategories(player);
                    break;
            }
        }

        [ChatCommand("job")]
        private void JobCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableJobs)
            {
                player.ChatMessage("Система работ недоступна");
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
                        player.ChatMessage("Использование: /job join <название_работы>");
                        return;
                    }
                    JoinJob(player, string.Join(" ", args.Skip(1)));
                    break;
                case "leave":
                    LeaveJob(player);
                    break;
                case "status":
                    ShowJobStatus(player);
                    break;
            }
        }

        [ChatCommand("daily")]
        private void DailyCommand(BasePlayer player, string command, string[] args)
        {
            GiveDailyReward(player);
        }
        #endregion

        #region Shop Methods
        private void ShowShopCategories(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== МАГАЗИН BULBARUST ===</color>");
            player.ChatMessage("Доступные команды:");
            player.ChatMessage("/shop list - показать все товары");
            player.ChatMessage("/shop list <категория> - товары по категории");
            player.ChatMessage("/shop buy <номер> <количество> - купить товар");
            player.ChatMessage("/balance - проверить баланс");
        }

        private void ShowShopItems(BasePlayer player, string category)
        {
            player.ChatMessage($"<color=yellow>=== ТОВАРЫ ({category}) ===</color>");
            
            var items = config.ShopItems;
            if (category != "Все")
            {
                items = items.Where(x => x.Category == category).ToList();
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var stockInfo = item.IsLimited ? $" (Остаток: {item.MaxStock})" : "";
                player.ChatMessage($"{i + 1}. {item.ItemName} x{item.Amount} - {config.CurrencySymbol}{item.Price:F2}{stockInfo}");
            }
        }

        private void BuyItem(BasePlayer player, string[] args)
        {
            if (!int.TryParse(args[1], out int itemIndex) || !int.TryParse(args[2], out int quantity))
            {
                player.ChatMessage("Неверные параметры");
                return;
            }

            itemIndex--; // Пользователь вводит с 1, а не с 0

            if (itemIndex < 0 || itemIndex >= config.ShopItems.Count)
            {
                player.ChatMessage("Товар не найден");
                return;
            }

            var item = config.ShopItems[itemIndex];
            var totalPrice = item.Price * quantity;

            if (!HasEnoughMoney(player.userID, totalPrice))
            {
                player.ChatMessage($"Недостаточно средств. Нужно: {config.CurrencySymbol}{totalPrice:F2}");
                return;
            }

            if (item.IsLimited && item.MaxStock < quantity)
            {
                player.ChatMessage($"Недостаточно товара на складе. Доступно: {item.MaxStock}");
                return;
            }

            // Создаем предмет
            var itemDef = ItemManager.FindItemDefinition(item.ItemId);
            if (itemDef != null)
            {
                var createdItem = ItemManager.Create(itemDef, item.Amount * quantity);
                if (player.inventory.GiveItem(createdItem))
                {
                    RemoveMoney(player.userID, totalPrice, $"Покупка: {item.ItemName} x{quantity}");
                    player.ChatMessage($"<color=green>Успешно куплено: {item.ItemName} x{quantity}</color>");
                    
                    if (item.IsLimited)
                    {
                        item.MaxStock -= quantity;
                    }
                }
                else
                {
                    player.ChatMessage("Недостаточно места в инвентаре");
                }
            }
        }
        #endregion

        #region Job Methods
        private void ShowAvailableJobs(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ РАБОТЫ ===</color>");
            for (int i = 0; i < config.Jobs.Count; i++)
            {
                var job = config.Jobs[i];
                player.ChatMessage($"{i + 1}. {job.Name} - {job.Description}");
                player.ChatMessage($"   Зарплата: {config.CurrencySymbol}{job.PayPerHour}/час");
            }
            player.ChatMessage("Используйте: /job join <название_работы>");
        }

        private void JoinJob(BasePlayer player, string jobName)
        {
            var job = config.Jobs.FirstOrDefault(j => j.Name.ToLower() == jobName.ToLower());
            if (job == null)
            {
                player.ChatMessage("Работа не найдена");
                return;
            }

            playerJobs[player.userID] = job.Name;
            player.ChatMessage($"<color=green>Вы устроились на работу: {job.Name}</color>");
            player.ChatMessage($"Зарплата: {config.CurrencySymbol}{job.PayPerHour}/час");
        }

        private void LeaveJob(BasePlayer player)
        {
            if (playerJobs.ContainsKey(player.userID))
            {
                var jobName = playerJobs[player.userID];
                playerJobs.Remove(player.userID);
                player.ChatMessage($"<color=red>Вы уволились с работы: {jobName}</color>");
            }
            else
            {
                player.ChatMessage("Вы не работаете");
            }
        }

        private void ShowJobStatus(BasePlayer player)
        {
            if (playerJobs.ContainsKey(player.userID))
            {
                var jobName = playerJobs[player.userID];
                var job = config.Jobs.FirstOrDefault(j => j.Name == jobName);
                if (job != null)
                {
                    player.ChatMessage($"<color=yellow>Текущая работа: {job.Name}</color>");
                    player.ChatMessage($"Описание: {job.Description}");
                    player.ChatMessage($"Зарплата: {config.CurrencySymbol}{job.PayPerHour}/час");
                }
            }
            else
            {
                player.ChatMessage("Вы не работаете. Используйте /job для просмотра доступных работ");
            }
        }
        #endregion
    }
}
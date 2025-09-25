using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Banking System", "BULBARUST", "1.0.0")]
    [Description("Система банков для сервера BULBARUST")]
    public class BankingSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableBanking { get; set; } = true;
            public bool EnableInterest { get; set; } = true;
            public bool EnableLoans { get; set; } = true;
            public bool EnableInvestments { get; set; } = true;
            public bool EnableBankTransfers { get; set; } = true;
            public float InterestRate { get; set; } = 0.05f; // 5% в день
            public float InterestInterval { get; set; } = 86400f; // 24 часа
            public float MaxLoanAmount { get; set; } = 10000f;
            public float LoanInterestRate { get; set; } = 0.1f; // 10% в день
            public float MaxLoanDuration { get; set; } = 604800f; // 7 дней
            public float TransferFee { get; set; } = 0.02f; // 2% комиссия
            public float MinTransferAmount { get; set; } = 100f;
            public List<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
            public List<Investment> Investments { get; set; } = new List<Investment>();
        }

        private class BankAccount
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public float InterestRate { get; set; } = 0.05f;
            public float MinDeposit { get; set; } = 1000f;
            public float MaxDeposit { get; set; } = 100000f;
            public bool IsPremium { get; set; } = false;
            public List<string> RequiredPermissions { get; set; } = new List<string>();
        }

        private class Investment
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public float MinAmount { get; set; } = 5000f;
            public float MaxAmount { get; set; } = 50000f;
            public float ReturnRate { get; set; } = 0.15f; // 15% в день
            public float Risk { get; set; } = 0.3f; // 30% риск
            public float Duration { get; set; } = 2592000f; // 30 дней
            public bool IsActive { get; set; } = true;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем типы банковских счетов
            config.BankAccounts.AddRange(new List<BankAccount>
            {
                new BankAccount
                {
                    Id = "basic",
                    Name = "Базовый счет",
                    Description = "Стандартный банковский счет",
                    InterestRate = 0.03f,
                    MinDeposit = 1000f,
                    MaxDeposit = 10000f,
                    IsPremium = false
                },
                new BankAccount
                {
                    Id = "premium",
                    Name = "Премиум счет",
                    Description = "Премиум счет с повышенными процентами",
                    InterestRate = 0.08f,
                    MinDeposit = 10000f,
                    MaxDeposit = 100000f,
                    IsPremium = true,
                    RequiredPermissions = new List<string> { "banking.premium" }
                },
                new BankAccount
                {
                    Id = "vip",
                    Name = "VIP счет",
                    Description = "VIP счет для особых клиентов",
                    InterestRate = 0.12f,
                    MinDeposit = 50000f,
                    MaxDeposit = 500000f,
                    IsPremium = true,
                    RequiredPermissions = new List<string> { "banking.vip" }
                }
            });

            // Добавляем инвестиции
            config.Investments.AddRange(new List<Investment>
            {
                new Investment
                {
                    Id = "safe",
                    Name = "Безопасные инвестиции",
                    Description = "Низкий риск, стабильный доход",
                    MinAmount = 5000f,
                    MaxAmount = 50000f,
                    ReturnRate = 0.08f,
                    Risk = 0.1f,
                    Duration = 2592000f
                },
                new Investment
                {
                    Id = "moderate",
                    Name = "Умеренные инвестиции",
                    Description = "Средний риск, хороший доход",
                    MinAmount = 10000f,
                    MaxAmount = 100000f,
                    ReturnRate = 0.15f,
                    Risk = 0.3f,
                    Duration = 2592000f
                },
                new Investment
                {
                    Id = "risky",
                    Name = "Рискованные инвестиции",
                    Description = "Высокий риск, высокий доход",
                    MinAmount = 20000f,
                    MaxAmount = 200000f,
                    ReturnRate = 0.25f,
                    Risk = 0.6f,
                    Duration = 2592000f
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
        private Dictionary<ulong, PlayerBankData> playerBanks = new Dictionary<ulong, PlayerBankData>();
        private Dictionary<ulong, List<Loan>> playerLoans = new Dictionary<ulong, List<Loan>>();
        private Dictionary<ulong, List<InvestmentData>> playerInvestments = new Dictionary<ulong, List<InvestmentData>>();
        private Dictionary<ulong, float> lastInterestPayment = new Dictionary<ulong, float>();

        private class PlayerBankData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public Dictionary<string, float> AccountBalances { get; set; } = new Dictionary<string, float>();
            public float TotalDeposits { get; set; } = 0f;
            public float TotalWithdrawals { get; set; } = 0f;
            public float TotalInterestEarned { get; set; } = 0f;
            public List<Transaction> Transactions { get; set; } = new List<Transaction>();
            public float LastActivity { get; set; }
        }

        private class Transaction
        {
            public string Id { get; set; }
            public string Type { get; set; } // deposit, withdrawal, transfer, interest, loan
            public float Amount { get; set; }
            public string Description { get; set; }
            public float Timestamp { get; set; }
            public string FromAccount { get; set; } = "";
            public string ToAccount { get; set; } = "";
            public ulong? ToPlayer { get; set; }
        }

        private class Loan
        {
            public string Id { get; set; }
            public float Amount { get; set; }
            public float InterestRate { get; set; }
            public float Duration { get; set; }
            public float StartTime { get; set; }
            public float DueTime { get; set; }
            public float PaidAmount { get; set; } = 0f;
            public bool IsPaid { get; set; } = false;
            public bool IsOverdue { get; set; } = false;
        }

        private class InvestmentData
        {
            public string Id { get; set; }
            public string InvestmentType { get; set; }
            public float Amount { get; set; }
            public float StartTime { get; set; }
            public float EndTime { get; set; }
            public float ExpectedReturn { get; set; }
            public bool IsCompleted { get; set; } = false;
            public bool IsSuccessful { get; set; } = false;
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система банков BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableBanking)
            {
                timer.Every(3600f, ProcessInterestPayments); // Каждый час
                timer.Every(1800f, ProcessLoans); // Каждые 30 минут
                timer.Every(7200f, ProcessInvestments); // Каждые 2 часа
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableBanking)
            {
                InitializePlayerBank(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerBankData(player.userID);
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerBanks = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerBankData>>("player_banks") ?? new Dictionary<ulong, PlayerBankData>();
            playerLoans = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<Loan>>>("player_loans") ?? new Dictionary<ulong, List<Loan>>();
            playerInvestments = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<InvestmentData>>>("player_investments") ?? new Dictionary<ulong, List<InvestmentData>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_banks", playerBanks);
            Interface.Oxide.DataFileSystem.WriteObject("player_loans", playerLoans);
            Interface.Oxide.DataFileSystem.WriteObject("player_investments", playerInvestments);
        }

        private void SavePlayerBankData(ulong playerId)
        {
            if (playerBanks.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"bank_{playerId}", playerBanks[playerId]);
            }
        }

        private void InitializePlayerBank(BasePlayer player)
        {
            if (!playerBanks.ContainsKey(player.userID))
            {
                var bankData = new PlayerBankData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    LastActivity = Time.time
                };

                // Инициализируем балансы для всех типов счетов
                foreach (var account in config.BankAccounts)
                {
                    bankData.AccountBalances[account.Id] = 0f;
                }

                playerBanks[player.userID] = bankData;
                SaveData();
            }
            else
            {
                // Обновляем имя игрока
                playerBanks[player.userID].PlayerName = player.displayName;
            }
        }

        private void ProcessInterestPayments()
        {
            if (!config.EnableInterest) return;

            var currentTime = Time.time;

            foreach (var playerId in playerBanks.Keys.ToList())
            {
                var bankData = playerBanks[playerId];
                
                if (currentTime - bankData.LastActivity >= config.InterestInterval)
                {
                    foreach (var account in config.BankAccounts)
                    {
                        if (bankData.AccountBalances.ContainsKey(account.Id))
                        {
                            var balance = bankData.AccountBalances[account.Id];
                            if (balance > 0)
                            {
                                var interest = balance * account.InterestRate;
                                bankData.AccountBalances[account.Id] += interest;
                                bankData.TotalInterestEarned += interest;

                                // Добавляем транзакцию
                                AddTransaction(playerId, "interest", interest, $"Проценты по счету {account.Name}");

                                var player = BasePlayer.FindByID(playerId);
                                if (player != null && player.IsConnected)
                                {
                                    player.ChatMessage($"<color=green>Получены проценты: {interest:F2} ({account.Name})</color>");
                                }
                            }
                        }
                    }

                    bankData.LastActivity = currentTime;
                }
            }

            SaveData();
        }

        private void ProcessLoans()
        {
            if (!config.EnableLoans) return;

            var currentTime = Time.time;

            foreach (var playerId in playerLoans.Keys.ToList())
            {
                var loans = playerLoans[playerId];
                var overdueLoans = new List<Loan>();

                foreach (var loan in loans.Where(l => !l.IsPaid))
                {
                    if (currentTime >= loan.DueTime)
                    {
                        loan.IsOverdue = true;
                        overdueLoans.Add(loan);
                    }
                }

                // Обрабатываем просроченные кредиты
                foreach (var loan in overdueLoans)
                {
                    ProcessOverdueLoan(playerId, loan);
                }
            }

            SaveData();
        }

        private void ProcessInvestments()
        {
            if (!config.EnableInvestments) return;

            var currentTime = Time.time;

            foreach (var playerId in playerInvestments.Keys.ToList())
            {
                var investments = playerInvestments[playerId];
                var completedInvestments = new List<InvestmentData>();

                foreach (var investment in investments.Where(i => !i.IsCompleted))
                {
                    if (currentTime >= investment.EndTime)
                    {
                        CompleteInvestment(playerId, investment);
                        completedInvestments.Add(investment);
                    }
                }

                // Удаляем завершенные инвестиции
                foreach (var investment in completedInvestments)
                {
                    investments.Remove(investment);
                }
            }

            SaveData();
        }

        private void ProcessOverdueLoan(ulong playerId, Loan loan)
        {
            // Здесь должна быть логика обработки просроченных кредитов
            // Например, штрафы, блокировка счетов и т.д.
            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                player.ChatMessage($"<color=red>ВНИМАНИЕ! Просрочен кредит на сумму {loan.Amount:F2}</color>");
            }
        }

        private void CompleteInvestment(ulong playerId, InvestmentData investment)
        {
            var investmentTemplate = config.Investments.FirstOrDefault(i => i.Id == investment.InvestmentType);
            if (investmentTemplate == null) return;

            // Определяем успешность инвестиции
            var success = UnityEngine.Random.Range(0f, 1f) > investmentTemplate.Risk;
            investment.IsCompleted = true;
            investment.IsSuccessful = success;

            var bankData = playerBanks[playerId];
            var returnAmount = success ? investment.ExpectedReturn : investment.Amount * 0.5f; // 50% возврата при неудаче

            // Добавляем средства на основной счет
            if (!bankData.AccountBalances.ContainsKey("basic"))
            {
                bankData.AccountBalances["basic"] = 0f;
            }
            bankData.AccountBalances["basic"] += returnAmount;

            // Добавляем транзакцию
            AddTransaction(playerId, "investment", returnAmount, 
                $"Инвестиция {investmentTemplate.Name} - {(success ? "Успешно" : "Неудачно")}");

            var player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
            {
                var status = success ? "успешной" : "неудачной";
                player.ChatMessage($"<color=yellow>Инвестиция завершена ({status})</color>");
                player.ChatMessage($"<color=white>Получено: {returnAmount:F2}</color>");
            }
        }

        private void AddTransaction(ulong playerId, string type, float amount, string description)
        {
            if (!playerBanks.ContainsKey(playerId)) return;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Amount = amount,
                Description = description,
                Timestamp = Time.time
            };

            playerBanks[playerId].Transactions.Add(transaction);
        }

        private bool CanAccessAccount(ulong playerId, string accountId)
        {
            var account = config.BankAccounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null) return false;

            if (account.RequiredPermissions.Count == 0) return true;

            var player = BasePlayer.FindByID(playerId);
            if (player == null) return false;

            return account.RequiredPermissions.All(p => permission.UserHasPermission(player.UserIDString, p));
        }

        private bool Deposit(ulong playerId, string accountId, float amount)
        {
            if (!playerBanks.ContainsKey(playerId)) return false;
            if (!CanAccessAccount(playerId, accountId)) return false;

            var account = config.BankAccounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null) return false;

            if (amount < account.MinDeposit) return false;

            var bankData = playerBanks[playerId];
            if (!bankData.AccountBalances.ContainsKey(accountId))
            {
                bankData.AccountBalances[accountId] = 0f;
            }

            var newBalance = bankData.AccountBalances[accountId] + amount;
            if (newBalance > account.MaxDeposit) return false;

            bankData.AccountBalances[accountId] = newBalance;
            bankData.TotalDeposits += amount;
            bankData.LastActivity = Time.time;

            AddTransaction(playerId, "deposit", amount, $"Пополнение счета {account.Name}");

            return true;
        }

        private bool Withdraw(ulong playerId, string accountId, float amount)
        {
            if (!playerBanks.ContainsKey(playerId)) return false;
            if (!CanAccessAccount(playerId, accountId)) return false;

            var bankData = playerBanks[playerId];
            if (!bankData.AccountBalances.ContainsKey(accountId)) return false;

            if (bankData.AccountBalances[accountId] < amount) return false;

            bankData.AccountBalances[accountId] -= amount;
            bankData.TotalWithdrawals += amount;
            bankData.LastActivity = Time.time;

            var account = config.BankAccounts.FirstOrDefault(a => a.Id == accountId);
            AddTransaction(playerId, "withdrawal", amount, $"Снятие со счета {account?.Name}");

            return true;
        }

        private bool Transfer(ulong fromPlayerId, ulong toPlayerId, string fromAccount, string toAccount, float amount)
        {
            if (!config.EnableBankTransfers) return false;
            if (!playerBanks.ContainsKey(fromPlayerId) || !playerBanks.ContainsKey(toPlayerId)) return false;

            var transferFee = amount * config.TransferFee;
            var totalAmount = amount + transferFee;

            if (!Withdraw(fromPlayerId, fromAccount, totalAmount)) return false;

            if (!Deposit(toPlayerId, toAccount, amount)) return false;

            // Добавляем транзакции
            AddTransaction(fromPlayerId, "transfer", -totalAmount, $"Перевод игроку {playerBanks[toPlayerId].PlayerName}");
            AddTransaction(toPlayerId, "transfer", amount, $"Перевод от игрока {playerBanks[fromPlayerId].PlayerName}");

            return true;
        }

        private bool TakeLoan(ulong playerId, float amount, float duration)
        {
            if (!config.EnableLoans) return false;
            if (amount > config.MaxLoanAmount) return false;
            if (duration > config.MaxLoanDuration) return false;

            if (!playerLoans.ContainsKey(playerId))
            {
                playerLoans[playerId] = new List<Loan>();
            }

            var loan = new Loan
            {
                Id = Guid.NewGuid().ToString(),
                Amount = amount,
                InterestRate = config.LoanInterestRate,
                Duration = duration,
                StartTime = Time.time,
                DueTime = Time.time + duration
            };

            playerLoans[playerId].Add(loan);

            // Добавляем средства на основной счет
            if (!playerBanks.ContainsKey(playerId))
            {
                InitializePlayerBank(BasePlayer.FindByID(playerId));
            }

            if (!playerBanks[playerId].AccountBalances.ContainsKey("basic"))
            {
                playerBanks[playerId].AccountBalances["basic"] = 0f;
            }

            playerBanks[playerId].AccountBalances["basic"] += amount;
            AddTransaction(playerId, "loan", amount, $"Получен кредит на {amount:F2}");

            return true;
        }

        private bool MakeInvestment(ulong playerId, string investmentType, float amount)
        {
            if (!config.EnableInvestments) return false;

            var investment = config.Investments.FirstOrDefault(i => i.Id == investmentType);
            if (investment == null || !investment.IsActive) return false;

            if (amount < investment.MinAmount || amount > investment.MaxAmount) return false;

            if (!playerBanks.ContainsKey(playerId)) return false;

            var bankData = playerBanks[playerId];
            if (!bankData.AccountBalances.ContainsKey("basic") || bankData.AccountBalances["basic"] < amount)
            {
                return false;
            }

            // Снимаем средства
            bankData.AccountBalances["basic"] -= amount;

            // Создаем инвестицию
            if (!playerInvestments.ContainsKey(playerId))
            {
                playerInvestments[playerId] = new List<InvestmentData>();
            }

            var investmentData = new InvestmentData
            {
                Id = Guid.NewGuid().ToString(),
                InvestmentType = investmentType,
                Amount = amount,
                StartTime = Time.time,
                EndTime = Time.time + investment.Duration,
                ExpectedReturn = amount * (1 + investment.ReturnRate)
            };

            playerInvestments[playerId].Add(investmentData);
            AddTransaction(playerId, "investment", -amount, $"Инвестиция в {investment.Name}");

            return true;
        }
        #endregion

        #region Commands
        [ChatCommand("bank")]
        private void BankCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableBanking)
            {
                player.ChatMessage("Банковская система отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowBankHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "balance":
                    ShowBalance(player, args.Length > 1 ? args[1] : null);
                    break;
                case "deposit":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /bank deposit <счет> <сумма>");
                        return;
                    }
                    DepositCommand(player, args[1], args[2]);
                    break;
                case "withdraw":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /bank withdraw <счет> <сумма>");
                        return;
                    }
                    WithdrawCommand(player, args[1], args[2]);
                    break;
                case "transfer":
                    if (args.Length < 4)
                    {
                        player.ChatMessage("Использование: /bank transfer <игрок> <счет> <сумма>");
                        return;
                    }
                    TransferCommand(player, args[1], args[2], args[3]);
                    break;
                case "history":
                    ShowTransactionHistory(player);
                    break;
                case "accounts":
                    ShowAvailableAccounts(player);
                    break;
                case "loan":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /bank loan <сумма> <дни>");
                        return;
                    }
                    LoanCommand(player, args[1], args[2]);
                    break;
                case "loans":
                    ShowLoans(player);
                    break;
                case "invest":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /bank invest <тип> <сумма>");
                        return;
                    }
                    InvestCommand(player, args[1], args[2]);
                    break;
                case "investments":
                    ShowInvestments(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowBankHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== БАНКОВСКИЕ КОМАНДЫ ===</color>");
            player.ChatMessage("/bank balance [счет] - баланс счетов");
            player.ChatMessage("/bank deposit <счет> <сумма> - пополнить счет");
            player.ChatMessage("/bank withdraw <счет> <сумма> - снять со счета");
            player.ChatMessage("/bank transfer <игрок> <счет> <сумма> - перевод");
            player.ChatMessage("/bank history - история транзакций");
            player.ChatMessage("/bank accounts - доступные счета");
            player.ChatMessage("/bank loan <сумма> <дни> - взять кредит");
            player.ChatMessage("/bank loans - мои кредиты");
            player.ChatMessage("/bank invest <тип> <сумма> - инвестировать");
            player.ChatMessage("/bank investments - мои инвестиции");
        }

        private void ShowBalance(BasePlayer player, string accountId = null)
        {
            if (!playerBanks.ContainsKey(player.userID))
            {
                InitializePlayerBank(player);
            }

            var bankData = playerBanks[player.userID];

            if (string.IsNullOrEmpty(accountId))
            {
                player.ChatMessage("<color=yellow>=== БАЛАНС СЧЕТОВ ===</color>");
                foreach (var account in config.BankAccounts)
                {
                    if (CanAccessAccount(player.userID, account.Id))
                    {
                        var balance = bankData.AccountBalances.ContainsKey(account.Id) ? bankData.AccountBalances[account.Id] : 0f;
                        player.ChatMessage($"{account.Name}: {balance:F2}");
                    }
                }
            }
            else
            {
                if (!CanAccessAccount(player.userID, accountId))
                {
                    player.ChatMessage("У вас нет доступа к этому счету");
                    return;
                }

                var balance = bankData.AccountBalances.ContainsKey(accountId) ? bankData.AccountBalances[accountId] : 0f;
                var account = config.BankAccounts.FirstOrDefault(a => a.Id == accountId);
                player.ChatMessage($"<color=yellow>Баланс счета '{account?.Name}': {balance:F2}</color>");
            }
        }

        private void DepositCommand(BasePlayer player, string accountId, string amountStr)
        {
            if (!float.TryParse(amountStr, out float amount) || amount <= 0)
            {
                player.ChatMessage("Неверная сумма");
                return;
            }

            if (Deposit(player.userID, accountId, amount))
            {
                player.ChatMessage($"<color=green>Средства зачислены на счет {accountId}</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не удалось пополнить счет</color>");
            }
        }

        private void WithdrawCommand(BasePlayer player, string accountId, string amountStr)
        {
            if (!float.TryParse(amountStr, out float amount) || amount <= 0)
            {
                player.ChatMessage("Неверная сумма");
                return;
            }

            if (Withdraw(player.userID, accountId, amount))
            {
                player.ChatMessage($"<color=green>Средства сняты со счета {accountId}</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не удалось снять средства</color>");
            }
        }

        private void TransferCommand(BasePlayer player, string targetName, string accountId, string amountStr)
        {
            if (!float.TryParse(amountStr, out float amount) || amount <= 0)
            {
                player.ChatMessage("Неверная сумма");
                return;
            }

            if (amount < config.MinTransferAmount)
            {
                player.ChatMessage($"Минимальная сумма перевода: {config.MinTransferAmount}");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (Transfer(player.userID, target.userID, "basic", accountId, amount))
            {
                var transferFee = amount * config.TransferFee;
                player.ChatMessage($"<color=green>Перевод выполнен! Комиссия: {transferFee:F2}</color>");
                target.ChatMessage($"<color=green>Получен перевод от {player.displayName}: {amount:F2}</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не удалось выполнить перевод</color>");
            }
        }

        private void ShowTransactionHistory(BasePlayer player)
        {
            if (!playerBanks.ContainsKey(player.userID))
            {
                InitializePlayerBank(player);
            }

            var bankData = playerBanks[player.userID];
            var recentTransactions = bankData.Transactions
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .ToList();

            if (recentTransactions.Count == 0)
            {
                player.ChatMessage("История транзакций пуста");
                return;
            }

            player.ChatMessage("<color=yellow>=== ИСТОРИЯ ТРАНЗАКЦИЙ ===</color>");
            foreach (var transaction in recentTransactions)
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - transaction.Timestamp).TotalHours;
                var amountText = transaction.Amount > 0 ? $"+{transaction.Amount:F2}" : transaction.Amount.ToString("F2");
                var color = transaction.Amount > 0 ? "green" : "red";
                
                player.ChatMessage($"<color={color}>{amountText}</color> - {transaction.Description}");
                player.ChatMessage($"  {timeAgo:F1} часов назад");
            }
        }

        private void ShowAvailableAccounts(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ СЧЕТА ===</color>");
            foreach (var account in config.BankAccounts)
            {
                if (CanAccessAccount(player.userID, account.Id))
                {
                    player.ChatMessage($"{account.Name} (ID: {account.Id})");
                    player.ChatMessage($"  {account.Description}");
                    player.ChatMessage($"  Процент: {account.InterestRate * 100:F1}% в день");
                    player.ChatMessage($"  Лимит: {account.MinDeposit:F0} - {account.MaxDeposit:F0}");
                }
            }
        }

        private void LoanCommand(BasePlayer player, string amountStr, string daysStr)
        {
            if (!config.EnableLoans)
            {
                player.ChatMessage("Кредиты отключены");
                return;
            }

            if (!float.TryParse(amountStr, out float amount) || !int.TryParse(daysStr, out int days))
            {
                player.ChatMessage("Неверные параметры");
                return;
            }

            var duration = days * 86400f; // Конвертируем дни в секунды

            if (TakeLoan(player.userID, amount, duration))
            {
                var interest = amount * config.LoanInterestRate * days;
                player.ChatMessage($"<color=green>Кредит получен!</color>");
                player.ChatMessage($"<color=white>Сумма: {amount:F2}</color>");
                player.ChatMessage($"<color=white>Процент: {config.LoanInterestRate * 100:F1}% в день</color>");
                player.ChatMessage($"<color=white>К возврату: {amount + interest:F2}</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не удалось получить кредит</color>");
            }
        }

        private void ShowLoans(BasePlayer player)
        {
            if (!playerLoans.ContainsKey(player.userID) || playerLoans[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет кредитов");
                return;
            }

            player.ChatMessage("<color=yellow>=== МОИ КРЕДИТЫ ===</color>");
            foreach (var loan in playerLoans[player.userID].Where(l => !l.IsPaid))
            {
                var timeLeft = loan.DueTime - Time.time;
                var daysLeft = Mathf.CeilToInt(timeLeft / 86400f);
                var status = loan.IsOverdue ? "ПРОСРОЧЕН" : "Активен";
                var color = loan.IsOverdue ? "red" : "white";

                player.ChatMessage($"<color={color}>Сумма: {loan.Amount:F2} - {status}</color>");
                player.ChatMessage($"  Осталось: {daysLeft} дней");
                player.ChatMessage($"  Процент: {loan.InterestRate * 100:F1}% в день");
            }
        }

        private void InvestCommand(BasePlayer player, string investmentType, string amountStr)
        {
            if (!config.EnableInvestments)
            {
                player.ChatMessage("Инвестиции отключены");
                return;
            }

            if (!float.TryParse(amountStr, out float amount))
            {
                player.ChatMessage("Неверная сумма");
                return;
            }

            if (MakeInvestment(player.userID, investmentType, amount))
            {
                var investment = config.Investments.FirstOrDefault(i => i.Id == investmentType);
                player.ChatMessage($"<color=green>Инвестиция создана!</color>");
                player.ChatMessage($"<color=white>Тип: {investment?.Name}</color>");
                player.ChatMessage($"<color=white>Сумма: {amount:F2}</color>");
                player.ChatMessage($"<color=white>Ожидаемая доходность: {investment?.ReturnRate * 100:F1}%</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Не удалось создать инвестицию</color>");
            }
        }

        private void ShowInvestments(BasePlayer player)
        {
            if (!playerInvestments.ContainsKey(player.userID) || playerInvestments[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет инвестиций");
                return;
            }

            player.ChatMessage("<color=yellow>=== МОИ ИНВЕСТИЦИИ ===</color>");
            foreach (var investment in playerInvestments[player.userID].Where(i => !i.IsCompleted))
            {
                var timeLeft = investment.EndTime - Time.time;
                var daysLeft = Mathf.CeilToInt(timeLeft / 86400f);
                var investmentTemplate = config.Investments.FirstOrDefault(i => i.Id == investment.InvestmentType);

                player.ChatMessage($"{investmentTemplate?.Name} - {investment.Amount:F2}");
                player.ChatMessage($"  Осталось: {daysLeft} дней");
                player.ChatMessage($"  Ожидаемый доход: {investment.ExpectedReturn:F2}");
            }
        }
        #endregion
    }
}
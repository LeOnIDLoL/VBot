using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Clan System", "BULBARUST", "1.0.0")]
    [Description("Система кланов для сервера BULBARUST")]
    public class ClanSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableClans { get; set; } = true;
            public bool EnableClanWars { get; set; } = true;
            public bool EnableClanChat { get; set; } = true;
            public bool EnableClanBank { get; set; } = true;
            public int MaxClanMembers { get; set; } = 10;
            public int MaxClanNameLength { get; set; } = 20;
            public int MaxClanTagLength { get; set; } = 6;
            public float ClanWarDuration { get; set; } = 1800f; // 30 минут
            public float ClanWarCooldown { get; set; } = 3600f; // 1 час
            public List<ClanRank> DefaultRanks { get; set; } = new List<ClanRank>();
            public Dictionary<string, float> ClanWarRewards { get; set; } = new Dictionary<string, float>();
        }

        private class ClanRank
        {
            public string Name { get; set; }
            public int Level { get; set; }
            public List<string> Permissions { get; set; } = new List<string>();
            public string Color { get; set; } = "white";
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем ранги клана
            config.DefaultRanks.AddRange(new List<ClanRank>
            {
                new ClanRank
                {
                    Name = "Лидер",
                    Level = 5,
                    Permissions = new List<string> { "invite", "kick", "promote", "demote", "disband", "war", "bank" },
                    Color = "red"
                },
                new ClanRank
                {
                    Name = "Заместитель",
                    Level = 4,
                    Permissions = new List<string> { "invite", "kick", "promote", "war", "bank" },
                    Color = "orange"
                },
                new ClanRank
                {
                    Name = "Офицер",
                    Level = 3,
                    Permissions = new List<string> { "invite", "kick", "war" },
                    Color = "yellow"
                },
                new ClanRank
                {
                    Name = "Ветеран",
                    Level = 2,
                    Permissions = new List<string> { "invite" },
                    Color = "green"
                },
                new ClanRank
                {
                    Name = "Участник",
                    Level = 1,
                    Permissions = new List<string>(),
                    Color = "white"
                }
            });

            // Награды за войну кланов
            config.ClanWarRewards.Add("Победа", 1000f);
            config.ClanWarRewards.Add("Поражение", 100f);
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
        private Dictionary<string, Clan> clans = new Dictionary<string, Clan>();
        private Dictionary<ulong, string> playerClans = new Dictionary<ulong, string>();
        private Dictionary<string, ClanWar> activeWars = new Dictionary<string, ClanWar>();
        private Dictionary<ulong, float> lastWarTime = new Dictionary<ulong, float>();

        private class Clan
        {
            public string Name { get; set; }
            public string Tag { get; set; }
            public string Description { get; set; } = "";
            public ulong Leader { get; set; }
            public List<ClanMember> Members { get; set; } = new List<ClanMember>();
            public float Bank { get; set; } = 0f;
            public int Level { get; set; } = 1;
            public int Experience { get; set; } = 0;
            public float Created { get; set; }
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }

        private class ClanMember
        {
            public ulong PlayerId { get; set; }
            public string Name { get; set; }
            public string Rank { get; set; } = "Участник";
            public float Joined { get; set; }
            public bool IsOnline { get; set; } = false;
        }

        private class ClanWar
        {
            public string Clan1 { get; set; }
            public string Clan2 { get; set; }
            public float StartTime { get; set; }
            public float Duration { get; set; }
            public Dictionary<string, int> Scores { get; set; } = new Dictionary<string, int>();
            public bool IsActive { get; set; } = true;
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система кланов BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableClanWars)
            {
                timer.Every(60f, CheckClanWars);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            UpdatePlayerOnlineStatus(player.userID, true);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            UpdatePlayerOnlineStatus(player.userID, false);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableClanWars) return;

            var player = entity as BasePlayer;
            if (player == null) return;

            var killer = info.InitiatorPlayer;
            if (killer == null) return;

            // Проверяем, участвуют ли игроки в войне кланов
            var playerClan = GetPlayerClan(player.userID);
            var killerClan = GetPlayerClan(killer.userID);

            if (playerClan != null && killerClan != null && playerClan != killerClan)
            {
                var war = GetActiveWar(playerClan, killerClan);
                if (war != null)
                {
                    UpdateWarScore(war, killerClan);
                }
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            clans = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Clan>>("clans") ?? new Dictionary<string, Clan>();
            playerClans = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("player_clans") ?? new Dictionary<ulong, string>();
            activeWars = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ClanWar>>("clan_wars") ?? new Dictionary<string, ClanWar>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("clans", clans);
            Interface.Oxide.DataFileSystem.WriteObject("player_clans", playerClans);
            Interface.Oxide.DataFileSystem.WriteObject("clan_wars", activeWars);
        }

        private string GetPlayerClan(ulong playerId)
        {
            return playerClans.ContainsKey(playerId) ? playerClans[playerId] : null;
        }

        private Clan GetClan(string clanName)
        {
            return clans.ContainsKey(clanName.ToLower()) ? clans[clanName.ToLower()] : null;
        }

        private ClanMember GetClanMember(ulong playerId, string clanName)
        {
            var clan = GetClan(clanName);
            if (clan == null) return null;
            return clan.Members.FirstOrDefault(m => m.PlayerId == playerId);
        }

        private bool HasClanPermission(ulong playerId, string permission)
        {
            var clanName = GetPlayerClan(playerId);
            if (clanName == null) return false;

            var member = GetClanMember(playerId, clanName);
            if (member == null) return false;

            var rank = config.DefaultRanks.FirstOrDefault(r => r.Name == member.Rank);
            if (rank == null) return false;

            return rank.Permissions.Contains(permission);
        }

        private void UpdatePlayerOnlineStatus(ulong playerId, bool isOnline)
        {
            var clanName = GetPlayerClan(playerId);
            if (clanName == null) return;

            var clan = GetClan(clanName);
            if (clan == null) return;

            var member = clan.Members.FirstOrDefault(m => m.PlayerId == playerId);
            if (member != null)
            {
                member.IsOnline = isOnline;
            }
        }

        private void CreateClan(string clanName, string tag, ulong leaderId, string leaderName)
        {
            var clan = new Clan
            {
                Name = clanName,
                Tag = tag,
                Leader = leaderId,
                Created = Time.time
            };

            clan.Members.Add(new ClanMember
            {
                PlayerId = leaderId,
                Name = leaderName,
                Rank = "Лидер",
                Joined = Time.time,
                IsOnline = true
            });

            clans[clanName.ToLower()] = clan;
            playerClans[leaderId] = clanName;

            SaveData();
        }

        private void DisbandClan(string clanName)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            // Уведомляем всех участников
            foreach (var member in clan.Members)
            {
                var player = BasePlayer.FindByID(member.PlayerId);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage($"<color=red>Клан '{clanName}' распущен!</color>");
                }
                playerClans.Remove(member.PlayerId);
            }

            clans.Remove(clanName.ToLower());
            SaveData();
        }

        private void AddClanMember(string clanName, ulong playerId, string playerName)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            var member = new ClanMember
            {
                PlayerId = playerId,
                Name = playerName,
                Rank = "Участник",
                Joined = Time.time,
                IsOnline = true
            };

            clan.Members.Add(member);
            playerClans[playerId] = clanName;
            SaveData();
        }

        private void RemoveClanMember(string clanName, ulong playerId)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            clan.Members.RemoveAll(m => m.PlayerId == playerId);
            playerClans.Remove(playerId);
            SaveData();
        }

        private void PromoteClanMember(string clanName, ulong playerId, string newRank)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            var member = clan.Members.FirstOrDefault(m => m.PlayerId == playerId);
            if (member != null)
            {
                member.Rank = newRank;
                SaveData();
            }
        }

        private ClanWar GetActiveWar(string clan1, string clan2)
        {
            var warKey1 = $"{clan1}_{clan2}";
            var warKey2 = $"{clan2}_{clan1}";

            if (activeWars.ContainsKey(warKey1))
                return activeWars[warKey1];
            if (activeWars.ContainsKey(warKey2))
                return activeWars[warKey2];

            return null;
        }

        private void StartClanWar(string clan1, string clan2)
        {
            var war = new ClanWar
            {
                Clan1 = clan1,
                Clan2 = clan2,
                StartTime = Time.time,
                Duration = config.ClanWarDuration,
                Scores = new Dictionary<string, int> { { clan1, 0 }, { clan2, 0 } }
            };

            var warKey = $"{clan1}_{clan2}";
            activeWars[warKey] = war;

            // Уведомляем участников
            NotifyClanMembers(clan1, $"<color=red>Война с кланом '{clan2}' началась!</color>");
            NotifyClanMembers(clan2, $"<color=red>Война с кланом '{clan1}' началась!</color>");

            SaveData();
        }

        private void EndClanWar(ClanWar war)
        {
            war.IsActive = false;
            var clan1Score = war.Scores[war.Clan1];
            var clan2Score = war.Scores[war.Clan2];

            string winner, loser;
            if (clan1Score > clan2Score)
            {
                winner = war.Clan1;
                loser = war.Clan2;
            }
            else if (clan2Score > clan1Score)
            {
                winner = war.Clan2;
                loser = war.Clan1;
            }
            else
            {
                // Ничья
                NotifyClanMembers(war.Clan1, "<color=yellow>Война закончилась ничьей!</color>");
                NotifyClanMembers(war.Clan2, "<color=yellow>Война закончилась ничьей!</color>");
                return;
            }

            // Раздаем награды
            GiveWarRewards(winner, config.ClanWarRewards["Победа"]);
            GiveWarRewards(loser, config.ClanWarRewards["Поражение"]);

            NotifyClanMembers(winner, $"<color=green>Победа в войне! Счет: {clan1Score}:{clan2Score}</color>");
            NotifyClanMembers(loser, $"<color=red>Поражение в войне. Счет: {clan1Score}:{clan2Score}</color>");

            activeWars.Remove($"{war.Clan1}_{war.Clan2}");
            SaveData();
        }

        private void UpdateWarScore(ClanWar war, string killerClan)
        {
            if (war.Scores.ContainsKey(killerClan))
            {
                war.Scores[killerClan]++;
            }
        }

        private void CheckClanWars()
        {
            var currentTime = Time.time;
            var warsToEnd = new List<ClanWar>();

            foreach (var war in activeWars.Values)
            {
                if (currentTime - war.StartTime >= war.Duration)
                {
                    warsToEnd.Add(war);
                }
            }

            foreach (var war in warsToEnd)
            {
                EndClanWar(war);
            }
        }

        private void GiveWarRewards(string clanName, float amount)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            clan.Bank += amount;
            SaveData();
        }

        private void NotifyClanMembers(string clanName, string message)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            foreach (var member in clan.Members)
            {
                var player = BasePlayer.FindByID(member.PlayerId);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage($"[{clan.Tag}] {message}");
                }
            }
        }

        private void SendClanChat(string clanName, ulong senderId, string message)
        {
            var clan = GetClan(clanName);
            if (clan == null) return;

            var sender = clan.Members.FirstOrDefault(m => m.PlayerId == senderId);
            if (sender == null) return;

            var rankColor = config.DefaultRanks.FirstOrDefault(r => r.Name == sender.Rank)?.Color ?? "white";
            var chatMessage = $"<color={rankColor}>[{clan.Tag}] {sender.Name}: {message}</color>";

            foreach (var member in clan.Members)
            {
                var player = BasePlayer.FindByID(member.PlayerId);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage(chatMessage);
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("clan")]
        private void ClanCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableClans)
            {
                player.ChatMessage("Система кланов отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowClanHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "create":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /clan create <название> <тег>");
                        return;
                    }
                    CreateClanCommand(player, args);
                    break;
                case "disband":
                    DisbandClanCommand(player);
                    break;
                case "invite":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /clan invite <игрок>");
                        return;
                    }
                    InvitePlayerCommand(player, args[1]);
                    break;
                case "kick":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /clan kick <игрок>");
                        return;
                    }
                    KickPlayerCommand(player, args[1]);
                    break;
                case "leave":
                    LeaveClanCommand(player);
                    break;
                case "info":
                    ShowClanInfo(player, args.Length > 1 ? args[1] : null);
                    break;
                case "members":
                    ShowClanMembers(player);
                    break;
                case "promote":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /clan promote <игрок> <ранг>");
                        return;
                    }
                    PromotePlayerCommand(player, args[1], args[2]);
                    break;
                case "war":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /clan war <клан>");
                        return;
                    }
                    StartWarCommand(player, args[1]);
                    break;
                case "bank":
                    ShowClanBank(player);
                    break;
                case "deposit":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /clan deposit <сумма>");
                        return;
                    }
                    DepositToBankCommand(player, args[1]);
                    break;
                case "withdraw":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /clan withdraw <сумма>");
                        return;
                    }
                    WithdrawFromBankCommand(player, args[1]);
                    break;
            }
        }

        [ChatCommand("c")]
        private void ClanChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableClanChat)
            {
                player.ChatMessage("Клан-чат отключен");
                return;
            }

            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /c <сообщение>");
                return;
            }

            var message = string.Join(" ", args);
            SendClanChat(clanName, player.userID, message);
        }
        #endregion

        #region Command Methods
        private void ShowClanHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ КЛАНА ===</color>");
            player.ChatMessage("/clan create <название> <тег> - создать клан");
            player.ChatMessage("/clan invite <игрок> - пригласить игрока");
            player.ChatMessage("/clan kick <игрок> - исключить игрока");
            player.ChatMessage("/clan leave - покинуть клан");
            player.ChatMessage("/clan info - информация о клане");
            player.ChatMessage("/clan members - список участников");
            player.ChatMessage("/clan promote <игрок> <ранг> - повысить игрока");
            player.ChatMessage("/clan war <клан> - объявить войну");
            player.ChatMessage("/clan bank - банк клана");
            player.ChatMessage("/c <сообщение> - клан-чат");
        }

        private void CreateClanCommand(BasePlayer player, string[] args)
        {
            var clanName = args[1];
            var tag = args[2];

            if (GetPlayerClan(player.userID) != null)
            {
                player.ChatMessage("Вы уже состоите в клане");
                return;
            }

            if (GetClan(clanName) != null)
            {
                player.ChatMessage("Клан с таким названием уже существует");
                return;
            }

            if (clanName.Length > config.MaxClanNameLength)
            {
                player.ChatMessage($"Название клана слишком длинное (макс. {config.MaxClanNameLength} символов)");
                return;
            }

            if (tag.Length > config.MaxClanTagLength)
            {
                player.ChatMessage($"Тег клана слишком длинный (макс. {config.MaxClanTagLength} символов)");
                return;
            }

            CreateClan(clanName, tag, player.userID, player.displayName);
            player.ChatMessage($"<color=green>Клан '{clanName}' создан!</color>");
        }

        private void DisbandClanCommand(BasePlayer player)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (!HasClanPermission(player.userID, "disband"))
            {
                player.ChatMessage("У вас нет прав на роспуск клана");
                return;
            }

            DisbandClan(clanName);
            player.ChatMessage($"<color=red>Клан '{clanName}' распущен!</color>");
        }

        private void InvitePlayerCommand(BasePlayer player, string targetName)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (!HasClanPermission(player.userID, "invite"))
            {
                player.ChatMessage("У вас нет прав на приглашение игроков");
                return;
            }

            var targetPlayer = BasePlayer.Find(targetName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (GetPlayerClan(targetPlayer.userID) != null)
            {
                player.ChatMessage("Игрок уже состоит в клане");
                return;
            }

            var clan = GetClan(clanName);
            if (clan.Members.Count >= config.MaxClanMembers)
            {
                player.ChatMessage($"Клан полный (макс. {config.MaxClanMembers} участников)");
                return;
            }

            AddClanMember(clanName, targetPlayer.userID, targetPlayer.displayName);
            player.ChatMessage($"<color=green>{targetPlayer.displayName} принят в клан!</color>");
            targetPlayer.ChatMessage($"<color=green>Вы приняты в клан '{clanName}'!</color>");
        }

        private void KickPlayerCommand(BasePlayer player, string targetName)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (!HasClanPermission(player.userID, "kick"))
            {
                player.ChatMessage("У вас нет прав на исключение игроков");
                return;
            }

            var targetPlayer = BasePlayer.Find(targetName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (GetPlayerClan(targetPlayer.userID) != clanName)
            {
                player.ChatMessage("Игрок не состоит в вашем клане");
                return;
            }

            RemoveClanMember(clanName, targetPlayer.userID);
            player.ChatMessage($"<color=red>{targetPlayer.displayName} исключен из клана!</color>");
            targetPlayer.ChatMessage($"<color=red>Вы исключены из клана '{clanName}'!</color>");
        }

        private void LeaveClanCommand(BasePlayer player)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            var clan = GetClan(clanName);
            if (clan.Leader == player.userID)
            {
                player.ChatMessage("Лидер не может покинуть клан. Используйте /clan disband для роспуска");
                return;
            }

            RemoveClanMember(clanName, player.userID);
            player.ChatMessage($"<color=red>Вы покинули клан '{clanName}'!</color>");
        }

        private void ShowClanInfo(BasePlayer player, string targetClan = null)
        {
            var clanName = targetClan ?? GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Укажите название клана или вступите в клан");
                return;
            }

            var clan = GetClan(clanName);
            if (clan == null)
            {
                player.ChatMessage($"Клан '{clanName}' не найден");
                return;
            }

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О КЛАНЕ ===</color>");
            player.ChatMessage($"Название: {clan.Name}");
            player.ChatMessage($"Тег: {clan.Tag}");
            player.ChatMessage($"Участников: {clan.Members.Count}/{config.MaxClanMembers}");
            player.ChatMessage($"Уровень: {clan.Level}");
            player.ChatMessage($"Опыт: {clan.Experience}");
            player.ChatMessage($"Банк: {clan.Bank:F2}");
            player.ChatMessage($"Создан: {TimeSpan.FromSeconds(Time.time - clan.Created).TotalDays:F1} дней назад");
        }

        private void ShowClanMembers(BasePlayer player)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            var clan = GetClan(clanName);
            player.ChatMessage($"<color=yellow>=== УЧАСТНИКИ КЛАНА '{clan.Name}' ===</color>");
            
            foreach (var member in clan.Members.OrderByDescending(m => config.DefaultRanks.FirstOrDefault(r => r.Name == m.Rank)?.Level ?? 0))
            {
                var status = member.IsOnline ? "Онлайн" : "Оффлайн";
                var rankColor = config.DefaultRanks.FirstOrDefault(r => r.Name == member.Rank)?.Color ?? "white";
                player.ChatMessage($"<color={rankColor}>{member.Rank}</color> {member.Name} ({status})");
            }
        }

        private void PromotePlayerCommand(BasePlayer player, string targetName, string newRank)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (!HasClanPermission(player.userID, "promote"))
            {
                player.ChatMessage("У вас нет прав на повышение игроков");
                return;
            }

            var targetPlayer = BasePlayer.Find(targetName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (GetPlayerClan(targetPlayer.userID) != clanName)
            {
                player.ChatMessage("Игрок не состоит в вашем клане");
                return;
            }

            PromoteClanMember(clanName, targetPlayer.userID, newRank);
            player.ChatMessage($"<color=green>{targetPlayer.displayName} повышен до ранга '{newRank}'!</color>");
            targetPlayer.ChatMessage($"<color=green>Вы повышен до ранга '{newRank}'!</color>");
        }

        private void StartWarCommand(BasePlayer player, string targetClan)
        {
            if (!config.EnableClanWars)
            {
                player.ChatMessage("Войны кланов отключены");
                return;
            }

            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (!HasClanPermission(player.userID, "war"))
            {
                player.ChatMessage("У вас нет прав на объявление войны");
                return;
            }

            if (GetClan(targetClan) == null)
            {
                player.ChatMessage($"Клан '{targetClan}' не найден");
                return;
            }

            if (GetActiveWar(clanName, targetClan) != null)
            {
                player.ChatMessage("Война с этим кланом уже идет");
                return;
            }

            StartClanWar(clanName, targetClan);
            player.ChatMessage($"<color=red>Война с кланом '{targetClan}' объявлена!</color>");
        }

        private void ShowClanBank(BasePlayer player)
        {
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            var clan = GetClan(clanName);
            player.ChatMessage($"<color=yellow>Банк клана '{clan.Name}': {clan.Bank:F2}</color>");
        }

        private void DepositToBankCommand(BasePlayer player, string amountStr)
        {
            if (!config.EnableClanBank)
            {
                player.ChatMessage("Банк клана отключен");
                return;
            }

            if (!float.TryParse(amountStr, out float amount) || amount <= 0)
            {
                player.ChatMessage("Неверная сумма");
                return;
            }

            // Здесь должна быть логика проверки баланса игрока и списания средств
            // Для примера просто добавляем в банк клана
            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            var clan = GetClan(clanName);
            clan.Bank += amount;
            SaveData();

            player.ChatMessage($"<color=green>В банк клана внесено: {amount:F2}</color>");
        }

        private void WithdrawFromBankCommand(BasePlayer player, string amountStr)
        {
            if (!config.EnableClanBank)
            {
                player.ChatMessage("Банк клана отключен");
                return;
            }

            if (!float.TryParse(amountStr, out float amount) || amount <= 0)
            {
                player.ChatMessage("Неверная сумма");
                return;
            }

            var clanName = GetPlayerClan(player.userID);
            if (clanName == null)
            {
                player.ChatMessage("Вы не состоите в клане");
                return;
            }

            if (!HasClanPermission(player.userID, "bank"))
            {
                player.ChatMessage("У вас нет прав на управление банком");
                return;
            }

            var clan = GetClan(clanName);
            if (clan.Bank < amount)
            {
                player.ChatMessage("Недостаточно средств в банке клана");
                return;
            }

            clan.Bank -= amount;
            SaveData();

            // Здесь должна быть логика выдачи средств игроку
            player.ChatMessage($"<color=green>Из банка клана снято: {amount:F2}</color>");
        }
        #endregion
    }
}
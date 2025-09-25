using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Raid Protection Complete", "BULBARUST", "2.0.0")]
    [Description("Полная система автозащиты от рейдов для BULBARUST")]
    public class AutoRaidProtectionComplete : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableAutoProtection { get; set; } = true;
            public bool EnablePlayerProtection { get; set; } = true;
            public bool EnableBuildingProtection { get; set; } = true;
            public bool EnableNotificationSystem { get; set; } = true;
            public bool EnableAdminAlerts { get; set; } = true;
            public bool EnableAutoKick { get; set; } = false;
            public bool EnableAutoBan { get; set; } = false;
            public float ProtectionRadius { get; set; } = 50f;
            public float ProtectionDuration { get; set; } = 300f; // 5 минут
            public int MaxRaidAttempts { get; set; } = 3;
            public float RaidDetectionTime { get; set; } = 10f; // 10 секунд
            public float NotificationInterval { get; set; } = 30f; // 30 секунд
            public List<string> ProtectedBlocks { get; set; } = new List<string>();
            public List<string> RaidWeapons { get; set; } = new List<string>();
            public Dictionary<string, float> ProtectionSettings { get; set; } = new Dictionary<string, float>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Защищенные блоки
            config.ProtectedBlocks.AddRange(new List<string>
            {
                "foundation", "wall", "door", "chest", "furnace", "campfire", "sleepingbag", "bed"
            });

            // Оружие для рейдов
            config.RaidWeapons.AddRange(new List<string>
            {
                "explosive.timed", "explosive.satchel", "rocket.launcher", "ammo.rocket.basic", "c4"
            });

            // Настройки защиты
            config.ProtectionSettings.Add("damage_threshold", 100f);
            config.ProtectionSettings.Add("structure_health_threshold", 0.3f);
            config.ProtectionSettings.Add("player_health_threshold", 0.5f);
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
        private Dictionary<ulong, PlayerProtectionData> playerProtectionData = new Dictionary<ulong, PlayerProtectionData>();
        private Dictionary<ulong, List<RaidAttempt>> raidAttempts = new Dictionary<ulong, List<RaidAttempt>>();
        private Dictionary<ulong, float> protectionEndTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastNotificationTime = new Dictionary<ulong, float>();
        private Dictionary<string, BuildingProtectionData> buildingProtection = new Dictionary<string, BuildingProtectionData>();
        private Dictionary<ulong, float> lastDamageTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastStructureDamage = new Dictionary<string, float>();

        private class PlayerProtectionData
        {
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public bool IsProtected { get; set; } = false;
            public float ProtectionStartTime { get; set; }
            public float ProtectionDuration { get; set; }
            public int RaidAttempts { get; set; } = 0;
            public float LastRaidAttempt { get; set; }
            public Vector3 LastPosition { get; set; }
            public List<string> ViolationTypes { get; set; } = new List<string>();
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }

        private class RaidAttempt
        {
            public string Id { get; set; }
            public ulong PlayerId { get; set; }
            public string PlayerName { get; set; }
            public Vector3 Position { get; set; }
            public float Timestamp { get; set; }
            public string Weapon { get; set; }
            public string Target { get; set; }
            public float Damage { get; set; }
            public string Severity { get; set; } = "medium";
        }

        private class BuildingProtectionData
        {
            public string BuildingId { get; set; }
            public ulong OwnerId { get; set; }
            public Vector3 Position { get; set; }
            public float Health { get; set; } = 1f;
            public bool IsProtected { get; set; } = false;
            public float ProtectionStartTime { get; set; }
            public float LastDamageTime { get; set; }
            public List<ulong> Attackers { get; set; } = new List<ulong>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Полная система автозащиты от рейдов BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableAutoProtection)
            {
                timer.Every(1f, ProcessProtectionSystem);
                timer.Every(config.NotificationInterval, SendProtectionNotifications);
                timer.Every(60f, CleanupOldData);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableAutoProtection)
            {
                InitializePlayerProtection(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerProtectionData(player.userID);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableAutoProtection) return null;

            var player = entity as BasePlayer;
            if (player != null)
            {
                return ProcessPlayerDamage(player, info);
            }

            var building = entity as BuildingBlock;
            if (building != null)
            {
                return ProcessBuildingDamage(building, info);
            }

            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableAutoProtection) return;

            var player = entity as BasePlayer;
            if (player == null) return;

            var killer = info.InitiatorPlayer;
            if (killer == null || killer == player) return;

            ProcessKillEvent(killer, player);
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!config.EnableAutoProtection) return;

            ProcessExplosiveUsage(player, entity);
        }

        void OnItemUse(Item item, BasePlayer player)
        {
            if (!config.EnableAutoProtection) return;

            if (config.RaidWeapons.Contains(item.info.shortname))
            {
                ProcessRaidWeaponUsage(player, item);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableAutoProtection) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var building = go.GetComponent<BuildingBlock>();
            if (building != null)
            {
                RegisterBuildingProtection(building, player);
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerProtectionData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerProtectionData>>("raid_protection_data") ?? new Dictionary<ulong, PlayerProtectionData>();
            raidAttempts = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<RaidAttempt>>>("raid_attempts") ?? new Dictionary<ulong, List<RaidAttempt>>();
            protectionEndTime = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("protection_end_times") ?? new Dictionary<ulong, float>();
            buildingProtection = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, BuildingProtectionData>>("building_protection") ?? new Dictionary<string, BuildingProtectionData>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("raid_protection_data", playerProtectionData);
            Interface.Oxide.DataFileSystem.WriteObject("raid_attempts", raidAttempts);
            Interface.Oxide.DataFileSystem.WriteObject("protection_end_times", protectionEndTime);
            Interface.Oxide.DataFileSystem.WriteObject("building_protection", buildingProtection);
        }

        private void SavePlayerProtectionData(ulong playerId)
        {
            if (playerProtectionData.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"raid_protection_{playerId}", playerProtectionData[playerId]);
            }
        }

        private void InitializePlayerProtection(BasePlayer player)
        {
            if (!playerProtectionData.ContainsKey(player.userID))
            {
                var protectionData = new PlayerProtectionData
                {
                    PlayerId = player.userID,
                    PlayerName = player.displayName,
                    IsProtected = false,
                    RaidAttempts = 0,
                    LastRaidAttempt = 0f,
                    LastPosition = player.transform.position
                };

                playerProtectionData[player.userID] = protectionData;
            }
            else
            {
                var protectionData = playerProtectionData[player.userID];
                protectionData.PlayerName = player.displayName;
                protectionData.LastPosition = player.transform.position;
            }

            if (!raidAttempts.ContainsKey(player.userID))
            {
                raidAttempts[player.userID] = new List<RaidAttempt>();
            }
        }

        private object ProcessPlayerDamage(BasePlayer player, HitInfo info)
        {
            var attacker = info.InitiatorPlayer;
            if (attacker == null || attacker == player) return null;

            // Проверяем защиту игрока
            if (IsPlayerProtected(player))
            {
                attacker.ChatMessage($"<color=red>Игрок {player.displayName} защищен от атак</color>");
                return true; // Блокируем урон
            }

            // Проверяем на рейдовую активность
            if (IsRaidWeapon(info.InitiatorWeapon))
            {
                RecordRaidAttempt(attacker, player, info);
            }

            return null;
        }

        private object ProcessBuildingDamage(BuildingBlock building, HitInfo info)
        {
            var attacker = info.InitiatorPlayer;
            if (attacker == null) return null;

            var buildingId = GetBuildingId(building);
            if (string.IsNullOrEmpty(buildingId)) return null;

            // Проверяем защиту здания
            if (IsBuildingProtected(buildingId))
            {
                if (!CanAccessBuilding(attacker.userID, buildingId))
                {
                    attacker.ChatMessage("<color=red>Здание защищено от повреждений</color>");
                    return true; // Блокируем урон
                }
            }

            // Проверяем на рейдовую активность
            if (IsRaidWeapon(info.InitiatorWeapon))
            {
                RecordBuildingRaidAttempt(attacker, building, info);
            }

            return null;
        }

        private void ProcessKillEvent(BasePlayer killer, BasePlayer victim)
        {
            if (!playerProtectionData.ContainsKey(killer.userID)) return;

            var protectionData = playerProtectionData[killer.userID];
            protectionData.RaidAttempts++;
            protectionData.LastRaidAttempt = Time.time;

            // Проверяем превышение лимита попыток рейда
            if (protectionData.RaidAttempts >= config.MaxRaidAttempts)
            {
                ActivateAutoProtection(killer);
            }

            SaveData();
        }

        private void ProcessExplosiveUsage(BasePlayer player, BaseEntity entity)
        {
            if (!playerProtectionData.ContainsKey(player.userID)) return;

            var protectionData = playerProtectionData[player.userID];
            protectionData.RaidAttempts++;
            protectionData.LastRaidAttempt = Time.time;

            RecordRaidAttempt(player, null, new HitInfo
            {
                InitiatorPlayer = player,
                InitiatorWeapon = entity,
                damageTypes = new DamageTypeList()
            });

            if (protectionData.RaidAttempts >= config.MaxRaidAttempts)
            {
                ActivateAutoProtection(player);
            }
        }

        private void ProcessRaidWeaponUsage(BasePlayer player, Item weapon)
        {
            if (!playerProtectionData.ContainsKey(player.userID)) return;

            var protectionData = playerProtectionData[player.userID];
            protectionData.RaidAttempts++;
            protectionData.LastRaidAttempt = Time.time;

            RecordRaidAttempt(player, null, new HitInfo
            {
                InitiatorPlayer = player,
                InitiatorWeapon = weapon.GetHeldEntity(),
                damageTypes = new DamageTypeList()
            });

            if (protectionData.RaidAttempts >= config.MaxRaidAttempts)
            {
                ActivateAutoProtection(player);
            }
        }

        private void RecordRaidAttempt(BasePlayer attacker, BasePlayer victim, HitInfo info)
        {
            if (!raidAttempts.ContainsKey(attacker.userID))
            {
                raidAttempts[attacker.userID] = new List<RaidAttempt>();
            }

            var attempt = new RaidAttempt
            {
                Id = Guid.NewGuid().ToString(),
                PlayerId = attacker.userID,
                PlayerName = attacker.displayName,
                Position = attacker.transform.position,
                Timestamp = Time.time,
                Weapon = info.InitiatorWeapon?.ShortPrefabName ?? "unknown",
                Target = victim?.displayName ?? "building",
                Damage = info.damageTypes.Total(),
                Severity = CalculateSeverity(info)
            };

            raidAttempts[attacker.userID].Add(attempt);

            // Ограничиваем количество записей
            if (raidAttempts[attacker.userID].Count > 100)
            {
                raidAttempts[attacker.userID] = raidAttempts[attacker.userID].OrderByDescending(a => a.Timestamp).Take(50).ToList();
            }

            SaveData();
        }

        private void RecordBuildingRaidAttempt(BasePlayer attacker, BuildingBlock building, HitInfo info)
        {
            var buildingId = GetBuildingId(building);
            if (string.IsNullOrEmpty(buildingId)) return;

            if (!buildingProtection.ContainsKey(buildingId))
            {
                RegisterBuildingProtection(building, null);
            }

            var buildingData = buildingProtection[buildingId];
            if (!buildingData.Attackers.Contains(attacker.userID))
            {
                buildingData.Attackers.Add(attacker.userID);
            }

            buildingData.LastDamageTime = Time.time;
            buildingData.Health = building.health / building.MaxHealth();

            // Активируем защиту здания при критическом уроне
            if (buildingData.Health < config.ProtectionSettings["structure_health_threshold"])
            {
                ActivateBuildingProtection(buildingId);
            }
        }

        private void ActivateAutoProtection(BasePlayer player)
        {
            if (!playerProtectionData.ContainsKey(player.userID)) return;

            var protectionData = playerProtectionData[player.userID];
            protectionData.IsProtected = true;
            protectionData.ProtectionStartTime = Time.time;
            protectionData.ProtectionDuration = config.ProtectionDuration;

            protectionEndTime[player.userID] = Time.time + config.ProtectionDuration;

            // Уведомляем игрока
            player.ChatMessage($"<color=red>ВНИМАНИЕ! Активирована автозащита от рейдов на {config.ProtectionDuration} секунд!</color>");
            player.ChatMessage($"<color=yellow>Попыток рейда: {protectionData.RaidAttempts}/{config.MaxRaidAttempts}</color>");

            // Уведомляем админов
            if (config.EnableAdminAlerts)
            {
                NotifyAdmins($"Автозащита активирована для игрока {player.displayName} в позиции {player.transform.position}");
            }

            // Автокик при необходимости
            if (config.EnableAutoKick)
            {
                timer.Once(5f, () =>
                {
                    if (player.IsConnected)
                    {
                        player.Kick("Автозащита: превышено количество попыток рейда");
                    }
                });
            }

            // Автобан при необходимости
            if (config.EnableAutoBan)
            {
                timer.Once(10f, () =>
                {
                    if (player.IsConnected)
                    {
                        player.Ban("Автозащита: превышено количество попыток рейда");
                    }
                });
            }

            SaveData();
        }

        private void ActivateBuildingProtection(string buildingId)
        {
            if (!buildingProtection.ContainsKey(buildingId)) return;

            var buildingData = buildingProtection[buildingId];
            buildingData.IsProtected = true;
            buildingData.ProtectionStartTime = Time.time;

            // Уведомляем владельца
            var owner = BasePlayer.FindByID(buildingData.OwnerId);
            if (owner != null)
            {
                owner.ChatMessage($"<color=red>Здание защищено от рейдов!</color>");
            }

            SaveData();
        }

        private void RegisterBuildingProtection(BuildingBlock building, BasePlayer player)
        {
            var buildingId = GetBuildingId(building);
            if (string.IsNullOrEmpty(buildingId)) return;

            var buildingData = new BuildingProtectionData
            {
                BuildingId = buildingId,
                OwnerId = player?.userID ?? 0,
                Position = building.transform.position,
                Health = building.health / building.MaxHealth(),
                IsProtected = false,
                ProtectionStartTime = 0f,
                LastDamageTime = 0f
            };

            buildingProtection[buildingId] = buildingData;
        }

        private void ProcessProtectionSystem()
        {
            var currentTime = Time.time;
            var playersToUnprotect = new List<ulong>();

            // Проверяем окончание защиты игроков
            foreach (var playerId in protectionEndTime.Keys.ToList())
            {
                if (currentTime >= protectionEndTime[playerId])
                {
                    playersToUnprotect.Add(playerId);
                }
            }

            foreach (var playerId in playersToUnprotect)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player != null)
                {
                    player.ChatMessage("<color=green>Автозащита от рейдов деактивирована</color>");
                }

                if (playerProtectionData.ContainsKey(playerId))
                {
                    playerProtectionData[playerId].IsProtected = false;
                }

                protectionEndTime.Remove(playerId);
            }

            // Проверяем окончание защиты зданий
            var buildingsToUnprotect = new List<string>();
            foreach (var building in buildingProtection.Values)
            {
                if (building.IsProtected && currentTime - building.ProtectionStartTime >= config.ProtectionDuration)
                {
                    buildingsToUnprotect.Add(building.BuildingId);
                }
            }

            foreach (var buildingId in buildingsToUnprotect)
            {
                buildingProtection[buildingId].IsProtected = false;
            }

            if (playersToUnprotect.Count > 0 || buildingsToUnprotect.Count > 0)
            {
                SaveData();
            }
        }

        private void SendProtectionNotifications()
        {
            var currentTime = Time.time;

            foreach (var playerId in protectionEndTime.Keys.ToList())
            {
                if (!lastNotificationTime.ContainsKey(playerId) || 
                    currentTime - lastNotificationTime[playerId] >= config.NotificationInterval)
                {
                    var player = BasePlayer.FindByID(playerId);
                    if (player != null)
                    {
                        var timeLeft = protectionEndTime[playerId] - currentTime;
                        if (timeLeft > 0)
                        {
                            player.ChatMessage($"<color=yellow>Автозащита активна: {timeLeft:F0} секунд</color>");
                            lastNotificationTime[playerId] = currentTime;
                        }
                    }
                }
            }
        }

        private void CleanupOldData()
        {
            var currentTime = Time.time;
            var cutoffTime = currentTime - 86400f; // 24 часа

            // Очищаем старые попытки рейдов
            foreach (var playerId in raidAttempts.Keys.ToList())
            {
                raidAttempts[playerId] = raidAttempts[playerId].Where(a => currentTime - a.Timestamp < cutoffTime).ToList();
            }

            // Очищаем старые данные защиты
            var oldProtectionData = playerProtectionData.Where(kvp => 
                currentTime - kvp.Value.LastRaidAttempt > cutoffTime && !kvp.Value.IsProtected).ToList();

            foreach (var data in oldProtectionData)
            {
                playerProtectionData.Remove(data.Key);
            }

            SaveData();
        }

        private bool IsPlayerProtected(BasePlayer player)
        {
            if (!playerProtectionData.ContainsKey(player.userID)) return false;
            if (!playerProtectionData[player.userID].IsProtected) return false;

            return Time.time < protectionEndTime.GetValueOrDefault(player.userID, 0f);
        }

        private bool IsBuildingProtected(string buildingId)
        {
            if (!buildingProtection.ContainsKey(buildingId)) return false;
            return buildingProtection[buildingId].IsProtected;
        }

        private bool CanAccessBuilding(ulong playerId, string buildingId)
        {
            if (!buildingProtection.ContainsKey(buildingId)) return true;
            return buildingProtection[buildingId].OwnerId == playerId;
        }

        private bool IsRaidWeapon(BaseEntity weapon)
        {
            if (weapon == null) return false;
            return config.RaidWeapons.Contains(weapon.ShortPrefabName);
        }

        private string GetBuildingId(BuildingBlock building)
        {
            if (building == null) return null;
            return $"{building.transform.position.x}_{building.transform.position.y}_{building.transform.position.z}";
        }

        private string CalculateSeverity(HitInfo info)
        {
            var damage = info.damageTypes.Total();
            if (damage >= 100f) return "high";
            if (damage >= 50f) return "medium";
            return "low";
        }

        private void NotifyAdmins(string message)
        {
            foreach (var admin in BasePlayer.activePlayerList.Where(p => p.IsAdmin))
            {
                admin.ChatMessage($"<color=red>[АВТОЗАЩИТА] {message}</color>");
            }
        }
        #endregion

        #region Commands
        [ChatCommand("raidprotect")]
        private void RaidProtectCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав на использование этой команды");
                return;
            }

            if (args.Length == 0)
            {
                ShowRaidProtectHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "status":
                    ShowProtectionStatus(player);
                    break;
                case "protect":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /raidprotect protect <игрок>");
                        return;
                    }
                    ProtectPlayerCommand(player, args[1]);
                    break;
                case "unprotect":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /raidprotect unprotect <игрок>");
                        return;
                    }
                    UnprotectPlayerCommand(player, args[1]);
                    break;
                case "attempts":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /raidprotect attempts <игрок>");
                        return;
                    }
                    ShowPlayerAttempts(player, args[1]);
                    break;
                case "reset":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /raidprotect reset <игрок>");
                        return;
                    }
                    ResetPlayerAttempts(player, args[1]);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowRaidProtectHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ АВТОЗАЩИТЫ ===</color>");
            player.ChatMessage("/raidprotect status - статус системы");
            player.ChatMessage("/raidprotect protect <игрок> - защитить игрока");
            player.ChatMessage("/raidprotect unprotect <игрок> - снять защиту");
            player.ChatMessage("/raidprotect attempts <игрок> - попытки рейдов");
            player.ChatMessage("/raidprotect reset <игрок> - сбросить попытки");
        }

        private void ShowProtectionStatus(BasePlayer player)
        {
            var protectedPlayers = protectionEndTime.Count;
            var totalAttempts = raidAttempts.Values.Sum(a => a.Count);
            var activeProtections = playerProtectionData.Values.Count(p => p.IsProtected);

            player.ChatMessage("<color=yellow>=== СТАТУС АВТОЗАЩИТЫ ===</color>");
            player.ChatMessage($"Защищенных игроков: {protectedPlayers}");
            player.ChatMessage($"Активных защит: {activeProtections}");
            player.ChatMessage($"Всего попыток рейдов: {totalAttempts}");
            player.ChatMessage($"Система: {(config.EnableAutoProtection ? "Включена" : "Выключена")}");
        }

        private void ProtectPlayerCommand(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!playerProtectionData.ContainsKey(targetPlayer.userID))
            {
                InitializePlayerProtection(targetPlayer);
            }

            var protectionData = playerProtectionData[targetPlayer.userID];
            protectionData.IsProtected = true;
            protectionData.ProtectionStartTime = Time.time;
            protectionData.ProtectionDuration = config.ProtectionDuration;

            protectionEndTime[targetPlayer.userID] = Time.time + config.ProtectionDuration;

            admin.ChatMessage($"<color=green>Игрок {playerName} защищен</color>");
            targetPlayer.ChatMessage("<color=green>Вы защищены от рейдов администратором</color>");
            SaveData();
        }

        private void UnprotectPlayerCommand(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (protectionEndTime.ContainsKey(targetPlayer.userID))
            {
                protectionEndTime.Remove(targetPlayer.userID);
            }

            if (playerProtectionData.ContainsKey(targetPlayer.userID))
            {
                playerProtectionData[targetPlayer.userID].IsProtected = false;
            }

            admin.ChatMessage($"<color=green>Защита с игрока {playerName} снята</color>");
            targetPlayer.ChatMessage("<color=yellow>Защита от рейдов снята</color>");
            SaveData();
        }

        private void ShowPlayerAttempts(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (!raidAttempts.ContainsKey(targetPlayer.userID))
            {
                admin.ChatMessage($"У игрока {playerName} нет попыток рейдов");
                return;
            }

            var attempts = raidAttempts[targetPlayer.userID].OrderByDescending(a => a.Timestamp).Take(10);

            admin.ChatMessage($"<color=yellow>=== ПОПЫТКИ РЕЙДОВ {playerName.ToUpper()} ===</color>");
            foreach (var attempt in attempts)
            {
                var timeAgo = TimeSpan.FromSeconds(Time.time - attempt.Timestamp).TotalMinutes;
                admin.ChatMessage($"{attempt.Weapon} -> {attempt.Target} ({attempt.Damage} урона) - {timeAgo:F1} мин назад");
            }
        }

        private void ResetPlayerAttempts(BasePlayer admin, string playerName)
        {
            var targetPlayer = BasePlayer.Find(playerName);
            if (targetPlayer == null)
            {
                admin.ChatMessage($"Игрок {playerName} не найден");
                return;
            }

            if (raidAttempts.ContainsKey(targetPlayer.userID))
            {
                raidAttempts[targetPlayer.userID].Clear();
            }

            if (playerProtectionData.ContainsKey(targetPlayer.userID))
            {
                playerProtectionData[targetPlayer.userID].RaidAttempts = 0;
            }

            admin.ChatMessage($"<color=green>Попытки рейдов для {playerName} сброшены</color>");
            SaveData();
        }
        #endregion
    }
}
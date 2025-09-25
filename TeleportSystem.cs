using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Teleport System", "BULBARUST", "1.0.0")]
    [Description("Система телепортации для сервера BULBARUST")]
    public class TeleportSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableTeleport { get; set; } = true;
            public bool EnableHome { get; set; } = true;
            public bool EnableTPA { get; set; } = true;
            public bool EnableRandomTP { get; set; } = true;
            public float TeleportDelay { get; set; } = 5f;
            public float HomeCooldown { get; set; } = 30f;
            public float TPACooldown { get; set; } = 60f;
            public int MaxHomes { get; set; } = 3;
            public bool RequirePermission { get; set; } = false;
            public List<TeleportLocation> PublicLocations { get; set; } = new List<TeleportLocation>();
        }

        private class TeleportLocation
        {
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public string Description { get; set; }
            public bool RequirePermission { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем публичные локации
            config.PublicLocations.AddRange(new List<TeleportLocation>
            {
                new TeleportLocation 
                { 
                    Name = "Спавн", 
                    Position = new Vector3(0, 100, 0), 
                    Description = "Главная точка спавна" 
                },
                new TeleportLocation 
                { 
                    Name = "Торговец", 
                    Position = new Vector3(100, 100, 100), 
                    Description = "Торговая зона" 
                },
                new TeleportLocation 
                { 
                    Name = "Арена", 
                    Position = new Vector3(-100, 100, -100), 
                    Description = "PvP арена" 
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
        private Dictionary<ulong, List<HomeLocation>> playerHomes = new Dictionary<ulong, List<HomeLocation>>();
        private Dictionary<ulong, float> lastHomeUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastTPAUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, TeleportRequest> pendingRequests = new Dictionary<ulong, TeleportRequest>();
        private Dictionary<ulong, float> teleportingPlayers = new Dictionary<ulong, float>();

        private class HomeLocation
        {
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public float Created { get; set; }
        }

        private class TeleportRequest
        {
            public ulong FromPlayer { get; set; }
            public ulong ToPlayer { get; set; }
            public float RequestTime { get; set; }
            public bool IsTPAHere { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            Puts("Система телепортации BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            LoadData();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Отменяем все запросы телепортации для этого игрока
            var requestsToRemove = pendingRequests.Where(x => x.Value.FromPlayer == player.userID || x.Value.ToPlayer == player.userID).ToList();
            foreach (var request in requestsToRemove)
            {
                pendingRequests.Remove(request.Key);
            }
            
            if (teleportingPlayers.ContainsKey(player.userID))
            {
                teleportingPlayers.Remove(player.userID);
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerHomes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<HomeLocation>>>("teleport_homes") ?? new Dictionary<ulong, List<HomeLocation>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("teleport_homes", playerHomes);
        }

        private bool CanUseTeleport(ulong playerId, string type)
        {
            var currentTime = Time.time;
            
            switch (type)
            {
                case "home":
                    if (lastHomeUse.ContainsKey(playerId))
                    {
                        return currentTime - lastHomeUse[playerId] >= config.HomeCooldown;
                    }
                    return true;
                case "tpa":
                    if (lastTPAUse.ContainsKey(playerId))
                    {
                        return currentTime - lastTPAUse[playerId] >= config.TPACooldown;
                    }
                    return true;
                default:
                    return true;
            }
        }

        private void StartTeleport(BasePlayer player, Vector3 destination, string reason = "")
        {
            if (teleportingPlayers.ContainsKey(player.userID))
            {
                player.ChatMessage("Вы уже телепортируетесь!");
                return;
            }

            teleportingPlayers[player.userID] = Time.time + config.TeleportDelay;
            
            player.ChatMessage($"<color=yellow>Телепортация через {config.TeleportDelay} секунд...</color>");
            player.ChatMessage("<color=red>НЕ ДВИГАЙТЕСЬ!</color>");
            
            if (!string.IsNullOrEmpty(reason))
            {
                player.ChatMessage($"Причина: {reason}");
            }

            timer.Once(config.TeleportDelay, () => {
                if (teleportingPlayers.ContainsKey(player.userID) && player.IsConnected)
                {
                    // Проверяем, что игрок не двигался
                    var distance = Vector3.Distance(player.transform.position, player.transform.position);
                    if (distance < 5f) // Допустимое расстояние движения
                    {
                        player.Teleport(destination);
                        player.ChatMessage("<color=green>Телепортация завершена!</color>");
                    }
                    else
                    {
                        player.ChatMessage("<color=red>Телепортация отменена - вы двигались!</color>");
                    }
                    
                    teleportingPlayers.Remove(player.userID);
                }
            });
        }

        private void CancelTeleport(ulong playerId)
        {
            if (teleportingPlayers.ContainsKey(playerId))
            {
                teleportingPlayers.Remove(playerId);
                var player = BasePlayer.FindByID(playerId);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage("<color=red>Телепортация отменена!</color>");
                }
            }
        }

        private bool HasPermission(BasePlayer player, string permission)
        {
            if (!config.RequirePermission) return true;
            return permission.HasPermission(player.UserIDString);
        }
        #endregion

        #region Commands
        [ChatCommand("home")]
        private void HomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableHome)
            {
                player.ChatMessage("Система домов отключена");
                return;
            }

            if (!HasPermission(player, "teleport.home"))
            {
                player.ChatMessage("У вас нет прав на использование домов");
                return;
            }

            if (!CanUseTeleport(player.userID, "home"))
            {
                var timeLeft = config.HomeCooldown - (Time.time - lastHomeUse[player.userID]);
                player.ChatMessage($"Осталось ждать: {Mathf.CeilToInt(timeLeft)} секунд");
                return;
            }

            if (args.Length == 0)
            {
                ShowHomes(player);
                return;
            }

            var homeName = string.Join(" ", args);
            var homes = playerHomes.ContainsKey(player.userID) ? playerHomes[player.userID] : new List<HomeLocation>();
            var home = homes.FirstOrDefault(h => h.Name.ToLower() == homeName.ToLower());

            if (home == null)
            {
                player.ChatMessage($"Дом '{homeName}' не найден");
                return;
            }

            lastHomeUse[player.userID] = Time.time;
            StartTeleport(player, home.Position, $"Дом: {home.Name}");
        }

        [ChatCommand("sethome")]
        private void SetHomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableHome)
            {
                player.ChatMessage("Система домов отключена");
                return;
            }

            if (!HasPermission(player, "teleport.sethome"))
            {
                player.ChatMessage("У вас нет прав на создание домов");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /sethome <название>");
                return;
            }

            var homeName = string.Join(" ", args);
            
            if (!playerHomes.ContainsKey(player.userID))
            {
                playerHomes[player.userID] = new List<HomeLocation>();
            }

            var homes = playerHomes[player.userID];
            
            if (homes.Count >= config.MaxHomes)
            {
                player.ChatMessage($"Максимальное количество домов: {config.MaxHomes}");
                return;
            }

            if (homes.Any(h => h.Name.ToLower() == homeName.ToLower()))
            {
                player.ChatMessage($"Дом '{homeName}' уже существует");
                return;
            }

            var newHome = new HomeLocation
            {
                Name = homeName,
                Position = player.transform.position,
                Created = Time.time
            };

            homes.Add(newHome);
            SaveData();
            
            player.ChatMessage($"<color=green>Дом '{homeName}' создан!</color>");
        }

        [ChatCommand("delhome")]
        private void DelHomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableHome)
            {
                player.ChatMessage("Система домов отключена");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /delhome <название>");
                return;
            }

            var homeName = string.Join(" ", args);
            
            if (!playerHomes.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет домов");
                return;
            }

            var homes = playerHomes[player.userID];
            var home = homes.FirstOrDefault(h => h.Name.ToLower() == homeName.ToLower());

            if (home == null)
            {
                player.ChatMessage($"Дом '{homeName}' не найден");
                return;
            }

            homes.Remove(home);
            SaveData();
            
            player.ChatMessage($"<color=red>Дом '{homeName}' удален!</color>");
        }

        [ChatCommand("tpa")]
        private void TPACommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableTPA)
            {
                player.ChatMessage("Система TPA отключена");
                return;
            }

            if (!HasPermission(player, "teleport.tpa"))
            {
                player.ChatMessage("У вас нет прав на TPA");
                return;
            }

            if (!CanUseTeleport(player.userID, "tpa"))
            {
                var timeLeft = config.TPACooldown - (Time.time - lastTPAUse[player.userID]);
                player.ChatMessage($"Осталось ждать: {Mathf.CeilToInt(timeLeft)} секунд");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /tpa <игрок>");
                return;
            }

            var targetName = string.Join(" ", args);
            var targetPlayer = BasePlayer.Find(targetName);

            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (targetPlayer == player)
            {
                player.ChatMessage("Нельзя телепортироваться к себе");
                return;
            }

            var request = new TeleportRequest
            {
                FromPlayer = player.userID,
                ToPlayer = targetPlayer.userID,
                RequestTime = Time.time,
                IsTPAHere = false
            };

            pendingRequests[targetPlayer.userID] = request;
            lastTPAUse[player.userID] = Time.time;

            player.ChatMessage($"<color=yellow>Запрос телепортации отправлен игроку {targetPlayer.displayName}</color>");
            targetPlayer.ChatMessage($"<color=yellow>{player.displayName} хочет телепортироваться к вам</color>");
            targetPlayer.ChatMessage("Используйте /tpaccept или /tpdeny");
        }

        [ChatCommand("tphere")]
        private void TPHereCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableTPA)
            {
                player.ChatMessage("Система TPA отключена");
                return;
            }

            if (!HasPermission(player, "teleport.tpa"))
            {
                player.ChatMessage("У вас нет прав на TPA");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /tphere <игрок>");
                return;
            }

            var targetName = string.Join(" ", args);
            var targetPlayer = BasePlayer.Find(targetName);

            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            if (targetPlayer == player)
            {
                player.ChatMessage("Нельзя телепортировать себя");
                return;
            }

            var request = new TeleportRequest
            {
                FromPlayer = player.userID,
                ToPlayer = targetPlayer.userID,
                RequestTime = Time.time,
                IsTPAHere = true
            };

            pendingRequests[targetPlayer.userID] = request;

            player.ChatMessage($"<color=yellow>Запрос телепортации отправлен игроку {targetPlayer.displayName}</color>");
            targetPlayer.ChatMessage($"<color=yellow>{player.displayName} хочет телепортировать вас к себе</color>");
            targetPlayer.ChatMessage("Используйте /tpaccept или /tpdeny");
        }

        [ChatCommand("tpaccept")]
        private void TPAcceptCommand(BasePlayer player, string command, string[] args)
        {
            if (!pendingRequests.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет активных запросов телепортации");
                return;
            }

            var request = pendingRequests[player.userID];
            var fromPlayer = BasePlayer.FindByID(request.FromPlayer);

            if (fromPlayer == null || !fromPlayer.IsConnected)
            {
                player.ChatMessage("Игрок, отправивший запрос, больше не в сети");
                pendingRequests.Remove(player.userID);
                return;
            }

            if (request.IsTPAHere)
            {
                StartTeleport(fromPlayer, player.transform.position, $"TPA к {player.displayName}");
                player.ChatMessage($"<color=green>Запрос принят! {fromPlayer.displayName} телепортируется к вам</color>");
            }
            else
            {
                StartTeleport(fromPlayer, player.transform.position, $"TPA к {player.displayName}");
                player.ChatMessage($"<color=green>Запрос принят! {fromPlayer.displayName} телепортируется к вам</color>");
            }

            pendingRequests.Remove(player.userID);
        }

        [ChatCommand("tpdeny")]
        private void TPDenyCommand(BasePlayer player, string command, string[] args)
        {
            if (!pendingRequests.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет активных запросов телепортации");
                return;
            }

            var request = pendingRequests[player.userID];
            var fromPlayer = BasePlayer.FindByID(request.FromPlayer);

            if (fromPlayer != null && fromPlayer.IsConnected)
            {
                fromPlayer.ChatMessage($"<color=red>Запрос телепортации отклонен игроком {player.displayName}</color>");
            }

            player.ChatMessage("<color=red>Запрос телепортации отклонен</color>");
            pendingRequests.Remove(player.userID);
        }

        [ChatCommand("tp")]
        private void TPCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableTeleport)
            {
                player.ChatMessage("Система телепортации отключена");
                return;
            }

            if (!HasPermission(player, "teleport.admin"))
            {
                player.ChatMessage("У вас нет прав на админскую телепортацию");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /tp <игрок> или /tp <x> <y> <z>");
                return;
            }

            if (args.Length == 1)
            {
                // Телепорт к игроку
                var targetName = args[0];
                var targetPlayer = BasePlayer.Find(targetName);

                if (targetPlayer == null)
                {
                    player.ChatMessage($"Игрок '{targetName}' не найден");
                    return;
                }

                player.Teleport(targetPlayer.transform.position);
                player.ChatMessage($"<color=green>Телепортирован к игроку {targetPlayer.displayName}</color>");
            }
            else if (args.Length == 3)
            {
                // Телепорт по координатам
                if (float.TryParse(args[0], out float x) && 
                    float.TryParse(args[1], out float y) && 
                    float.TryParse(args[2], out float z))
                {
                    var destination = new Vector3(x, y, z);
                    player.Teleport(destination);
                    player.ChatMessage($"<color=green>Телепортирован к координатам {x}, {y}, {z}</color>");
                }
                else
                {
                    player.ChatMessage("Неверные координаты");
                }
            }
        }

        [ChatCommand("warp")]
        private void WarpCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                ShowWarps(player);
                return;
            }

            var warpName = args[0];
            var warp = config.PublicLocations.FirstOrDefault(w => w.Name.ToLower() == warpName.ToLower());

            if (warp == null)
            {
                player.ChatMessage($"Варп '{warpName}' не найден");
                return;
            }

            if (warp.RequirePermission && !HasPermission(player, "teleport.warp." + warpName.ToLower()))
            {
                player.ChatMessage("У вас нет прав на этот варп");
                return;
            }

            StartTeleport(player, warp.Position, $"Варп: {warp.Name}");
        }

        [ChatCommand("rtp")]
        private void RandomTPCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableRandomTP)
            {
                player.ChatMessage("Случайная телепортация отключена");
                return;
            }

            if (!HasPermission(player, "teleport.random"))
            {
                player.ChatMessage("У вас нет прав на случайную телепортацию");
                return;
            }

            // Генерируем случайные координаты в безопасной зоне
            var randomX = UnityEngine.Random.Range(-2000f, 2000f);
            var randomZ = UnityEngine.Random.Range(-2000f, 2000f);
            var randomY = TerrainMeta.HeightMap.GetHeight(randomX, randomZ) + 10f;

            var destination = new Vector3(randomX, randomY, randomZ);
            StartTeleport(player, destination, "Случайная телепортация");
        }

        [ChatCommand("tpcancel")]
        private void TPCancelCommand(BasePlayer player, string command, string[] args)
        {
            CancelTeleport(player.userID);
        }
        #endregion

        #region Helper Methods
        private void ShowHomes(BasePlayer player)
        {
            if (!playerHomes.ContainsKey(player.userID) || playerHomes[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет домов. Используйте /sethome <название>");
                return;
            }

            player.ChatMessage("<color=yellow>=== ВАШИ ДОМА ===</color>");
            var homes = playerHomes[player.userID];
            for (int i = 0; i < homes.Count; i++)
            {
                var home = homes[i];
                player.ChatMessage($"{i + 1}. {home.Name} (создан {TimeSpan.FromSeconds(Time.time - home.Created).TotalDays:F1} дней назад)");
            }
            player.ChatMessage("Используйте: /home <название>");
        }

        private void ShowWarps(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ ВАРПЫ ===</color>");
            foreach (var warp in config.PublicLocations)
            {
                if (!warp.RequirePermission || HasPermission(player, "teleport.warp." + warp.Name.ToLower()))
                {
                    player.ChatMessage($"{warp.Name} - {warp.Description}");
                }
            }
            player.ChatMessage("Используйте: /warp <название>");
        }
        #endregion
    }
}
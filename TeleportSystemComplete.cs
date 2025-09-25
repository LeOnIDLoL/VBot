using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Teleport System Complete", "BULBARUST", "2.0.0")]
    [Description("Полная система телепортации для BULBARUST")]
    public class TeleportSystemComplete : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableTeleport { get; set; } = true;
            public bool EnableHome { get; set; } = true;
            public bool EnableTPA { get; set; } = true;
            public bool EnableRandomTP { get; set; } = true;
            public bool EnableBack { get; set; } = true;
            public bool EnableAdminTP { get; set; } = true;
            public float TeleportDelay { get; set; } = 5f;
            public float HomeCooldown { get; set; } = 30f;
            public float TPACooldown { get; set; } = 60f;
            public float RandomTPCooldown { get; set; } = 300f;
            public float BackCooldown { get; set; } = 10f;
            public int MaxHomes { get; set; } = 3;
            public float TeleportCost { get; set; } = 0f;
            public float HomeCost { get; set; } = 0f;
            public float TPACost { get; set; } = 0f;
            public bool RequirePermission { get; set; } = false;
            public bool CancelOnDamage { get; set; } = true;
            public bool CancelOnMovement { get; set; } = true;
            public float MovementThreshold { get; set; } = 2f;
            public List<TeleportLocation> PublicLocations { get; set; } = new List<TeleportLocation>();
            public List<TeleportZone> TeleportZones { get; set; } = new List<TeleportZone>();
        }

        private class TeleportLocation
        {
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public string Description { get; set; }
            public bool RequirePermission { get; set; } = false;
            public string Permission { get; set; } = "";
            public float Cost { get; set; } = 0f;
            public bool IsActive { get; set; } = true;
        }

        private class TeleportZone
        {
            public string Name { get; set; }
            public Vector3 Center { get; set; }
            public float Radius { get; set; }
            public string Destination { get; set; }
            public bool IsActive { get; set; } = true;
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
                    Description = "Главная точка спавна",
                    Cost = 0f
                },
                new TeleportLocation 
                { 
                    Name = "Торговец", 
                    Position = new Vector3(100, 100, 100), 
                    Description = "Торговая зона",
                    Cost = 50f
                },
                new TeleportLocation 
                { 
                    Name = "Арена", 
                    Position = new Vector3(-100, 100, -100), 
                    Description = "PvP арена",
                    Cost = 100f,
                    RequirePermission = true,
                    Permission = "teleport.arena"
                },
                new TeleportLocation 
                { 
                    Name = "Шахта", 
                    Position = new Vector3(500, 50, 500), 
                    Description = "Зона добычи ресурсов",
                    Cost = 25f
                }
            });

            // Добавляем телепорт зоны
            config.TeleportZones.AddRange(new List<TeleportZone>
            {
                new TeleportZone
                {
                    Name = "Спавн зона",
                    Center = new Vector3(0, 100, 0),
                    Radius = 50f,
                    Destination = "Спавн"
                },
                new TeleportZone
                {
                    Name = "Торговая зона",
                    Center = new Vector3(100, 100, 100),
                    Radius = 30f,
                    Destination = "Торговец"
                }
            });
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
        private Dictionary<ulong, List<HomeLocation>> playerHomes = new Dictionary<ulong, List<HomeLocation>>();
        private Dictionary<ulong, float> lastHomeUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastTPAUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastRandomTPUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastBackUse = new Dictionary<ulong, float>();
        private Dictionary<ulong, TeleportRequest> pendingRequests = new Dictionary<ulong, TeleportRequest>();
        private Dictionary<ulong, TeleportSession> teleportingPlayers = new Dictionary<ulong, TeleportSession>();
        private Dictionary<ulong, Vector3> lastPositions = new Dictionary<ulong, Vector3>();
        private Dictionary<ulong, List<Vector3>> playerBackHistory = new Dictionary<ulong, List<Vector3>>();

        private class HomeLocation
        {
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public float Created { get; set; }
            public bool IsPublic { get; set; } = false;
            public List<ulong> SharedWith { get; set; } = new List<ulong>();
        }

        private class TeleportRequest
        {
            public ulong FromPlayer { get; set; }
            public ulong ToPlayer { get; set; }
            public float RequestTime { get; set; }
            public bool IsTPAHere { get; set; }
            public string RequestType { get; set; } = "tpa";
        }

        private class TeleportSession
        {
            public ulong PlayerId { get; set; }
            public Vector3 StartPosition { get; set; }
            public Vector3 Destination { get; set; }
            public float StartTime { get; set; }
            public float Duration { get; set; }
            public string TeleportType { get; set; }
            public bool IsMoving { get; set; } = false;
            public float LastMovementCheck { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Полная система телепортации BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableTeleport)
            {
                timer.Every(1f, ProcessTeleportSessions);
                timer.Every(60f, CleanupOldData);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableTeleport)
            {
                InitializePlayerTeleportation(player);
                CheckPlayerTeleportationStatus(player);
            }
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

            SavePlayerTeleportationData(player.userID);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableTeleport || !config.CancelOnDamage) return;

            var player = entity as BasePlayer;
            if (player == null) return;

            CancelTeleportation(player, "получения урона");
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (!config.EnableTeleport || !config.CancelOnDamage) return;

            CancelTeleportation(attacker, "атаки");
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableTeleport) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            CancelTeleportation(player, "строительства");
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!config.EnableTeleport || !config.CancelOnMovement) return;

            if (teleportingPlayers.ContainsKey(player.userID))
            {
                var session = teleportingPlayers[player.userID];
                var currentTime = Time.time;

                if (currentTime - session.LastMovementCheck >= 0.5f) // Проверяем каждые 0.5 секунды
                {
                    var currentPos = player.transform.position;
                    var distance = Vector3.Distance(session.StartPosition, currentPos);

                    if (distance > config.MovementThreshold)
                    {
                        CancelTeleportation(player, "движения");
                    }

                    session.LastMovementCheck = currentTime;
                }
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerHomes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<HomeLocation>>>("teleport_homes") ?? new Dictionary<ulong, List<HomeLocation>>();
            lastHomeUse = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_home_use") ?? new Dictionary<ulong, float>();
            lastTPAUse = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_tpa_use") ?? new Dictionary<ulong, float>();
            lastRandomTPUse = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_random_tp_use") ?? new Dictionary<ulong, float>();
            lastBackUse = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("last_back_use") ?? new Dictionary<ulong, float>();
            lastPositions = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Vector3>>("last_positions") ?? new Dictionary<ulong, Vector3>();
            playerBackHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<Vector3>>>("back_history") ?? new Dictionary<ulong, List<Vector3>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("teleport_homes", playerHomes);
            Interface.Oxide.DataFileSystem.WriteObject("last_home_use", lastHomeUse);
            Interface.Oxide.DataFileSystem.WriteObject("last_tpa_use", lastTPAUse);
            Interface.Oxide.DataFileSystem.WriteObject("last_random_tp_use", lastRandomTPUse);
            Interface.Oxide.DataFileSystem.WriteObject("last_back_use", lastBackUse);
            Interface.Oxide.DataFileSystem.WriteObject("last_positions", lastPositions);
            Interface.Oxide.DataFileSystem.WriteObject("back_history", playerBackHistory);
        }

        private void SavePlayerTeleportationData(ulong playerId)
        {
            if (playerHomes.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"teleport_homes_{playerId}", playerHomes[playerId]);
            }
            if (playerBackHistory.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"back_history_{playerId}", playerBackHistory[playerId]);
            }
        }

        private void InitializePlayerTeleportation(BasePlayer player)
        {
            if (!playerHomes.ContainsKey(player.userID))
            {
                playerHomes[player.userID] = new List<HomeLocation>();
            }

            if (!playerBackHistory.ContainsKey(player.userID))
            {
                playerBackHistory[player.userID] = new List<Vector3>();
            }

            // Сохраняем текущую позицию
            lastPositions[player.userID] = player.transform.position;
        }

        private void CheckPlayerTeleportationStatus(BasePlayer player)
        {
            if (!playerHomes.ContainsKey(player.userID)) return;

            var homes = playerHomes[player.userID];
            player.ChatMessage($"<color=yellow>Дома: {homes.Count}/{config.MaxHomes}</color>");
            
            // Проверяем кулдауны
            var currentTime = Time.time;
            var homeCooldown = lastHomeUse.ContainsKey(player.userID) ? config.HomeCooldown - (currentTime - lastHomeUse[player.userID]) : 0f;
            var tpaCooldown = lastTPAUse.ContainsKey(player.userID) ? config.TPACooldown - (currentTime - lastTPAUse[player.userID]) : 0f;
            var randomCooldown = lastRandomTPUse.ContainsKey(player.userID) ? config.RandomTPCooldown - (currentTime - lastRandomTPUse[player.userID]) : 0f;
            var backCooldown = lastBackUse.ContainsKey(player.userID) ? config.BackCooldown - (currentTime - lastBackUse[player.userID]) : 0f;

            if (homeCooldown > 0) player.ChatMessage($"<color=yellow>Дом доступен через {homeCooldown:F0} секунд</color>");
            if (tpaCooldown > 0) player.ChatMessage($"<color=yellow>TPA доступен через {tpaCooldown:F0} секунд</color>");
            if (randomCooldown > 0) player.ChatMessage($"<color=yellow>Случайная телепортация доступна через {randomCooldown:F0} секунд</color>");
            if (backCooldown > 0) player.ChatMessage($"<color=yellow>Назад доступен через {backCooldown:F0} секунд</color>");
        }

        private void ProcessTeleportSessions()
        {
            var currentTime = Time.time;
            var sessionsToComplete = new List<ulong>();

            foreach (var session in teleportingPlayers.Values)
            {
                if (currentTime - session.StartTime >= session.Duration)
                {
                    sessionsToComplete.Add(session.PlayerId);
                }
            }

            foreach (var playerId in sessionsToComplete)
            {
                CompleteTeleportation(playerId);
            }
        }

        private void CleanupOldData()
        {
            var currentTime = Time.time;
            var cutoffTime = currentTime - 86400f; // 24 часа

            // Очищаем старые позиции
            var oldPositions = lastPositions.Where(kvp => currentTime - kvp.Value.magnitude > cutoffTime).ToList();
            foreach (var pos in oldPositions)
            {
                lastPositions.Remove(pos.Key);
            }

            // Ограничиваем историю позиций
            foreach (var playerId in playerBackHistory.Keys.ToList())
            {
                if (playerBackHistory[playerId].Count > 10)
                {
                    playerBackHistory[playerId] = playerBackHistory[playerId].TakeLast(10).ToList();
                }
            }

            SaveData();
        }

        private bool CanUseTeleport(ulong playerId, string type)
        {
            var currentTime = Time.time;

            switch (type.ToLower())
            {
                case "home":
                    return !lastHomeUse.ContainsKey(playerId) || currentTime - lastHomeUse[playerId] >= config.HomeCooldown;
                case "tpa":
                    return !lastTPAUse.ContainsKey(playerId) || currentTime - lastTPAUse[playerId] >= config.TPACooldown;
                case "random":
                    return !lastRandomTPUse.ContainsKey(playerId) || currentTime - lastRandomTPUse[playerId] >= config.RandomTPCooldown;
                case "back":
                    return !lastBackUse.ContainsKey(playerId) || currentTime - lastBackUse[playerId] >= config.BackCooldown;
                default:
                    return true;
            }
        }

        private void StartTeleportation(BasePlayer player, Vector3 destination, string type, float cost = 0f)
        {
            if (teleportingPlayers.ContainsKey(player.userID))
            {
                player.ChatMessage("<color=red>Вы уже телепортируетесь</color>");
                return;
            }

            if (cost > 0)
            {
                // Здесь должна быть проверка баланса игрока
                // if (!CanAfford(player.userID, cost)) return;
            }

            var session = new TeleportSession
            {
                PlayerId = player.userID,
                StartPosition = player.transform.position,
                Destination = destination,
                StartTime = Time.time,
                Duration = config.TeleportDelay,
                TeleportType = type,
                LastMovementCheck = Time.time
            };

            teleportingPlayers[player.userID] = session;

            player.ChatMessage($"<color=yellow>Телепортация через {config.TeleportDelay} секунд...</color>");
            player.ChatMessage("<color=red>Не двигайтесь и не атакуйте!</color>");

            // Обновляем кулдауны
            switch (type.ToLower())
            {
                case "home":
                    lastHomeUse[player.userID] = Time.time;
                    break;
                case "tpa":
                    lastTPAUse[player.userID] = Time.time;
                    break;
                case "random":
                    lastRandomTPUse[player.userID] = Time.time;
                    break;
                case "back":
                    lastBackUse[player.userID] = Time.time;
                    break;
            }
        }

        private void CompleteTeleportation(ulong playerId)
        {
            if (!teleportingPlayers.ContainsKey(playerId)) return;

            var session = teleportingPlayers[playerId];
            var player = BasePlayer.FindByID(playerId);

            if (player != null && player.IsConnected)
            {
                // Сохраняем текущую позицию в историю
                if (!playerBackHistory.ContainsKey(playerId))
                {
                    playerBackHistory[playerId] = new List<Vector3>();
                }

                playerBackHistory[playerId].Add(player.transform.position);
                if (playerBackHistory[playerId].Count > 10)
                {
                    playerBackHistory[playerId].RemoveAt(0);
                }

                // Телепортируем игрока
                player.Teleport(session.Destination);
                player.ChatMessage($"<color=green>Телепортация завершена</color>");

                // Обновляем последнюю позицию
                lastPositions[playerId] = session.Destination;
            }

            teleportingPlayers.Remove(playerId);
            SaveData();
        }

        private void CancelTeleportation(BasePlayer player, string reason)
        {
            if (!teleportingPlayers.ContainsKey(player.userID)) return;

            teleportingPlayers.Remove(player.userID);
            player.ChatMessage($"<color=red>Телепортация отменена из-за {reason}</color>");
        }

        private Vector3 GetRandomTeleportLocation()
        {
            // Генерируем случайную позицию в безопасной зоне
            var random = new System.Random();
            var x = random.Next(-1000, 1000);
            var z = random.Next(-1000, 1000);
            var y = 100f; // Высота над землей

            return new Vector3(x, y, z);
        }

        private bool IsInTeleportZone(Vector3 position)
        {
            foreach (var zone in config.TeleportZones)
            {
                if (!zone.IsActive) continue;

                if (Vector3.Distance(position, zone.Center) <= zone.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetTeleportZoneDestination(Vector3 position)
        {
            foreach (var zone in config.TeleportZones)
            {
                if (!zone.IsActive) continue;

                if (Vector3.Distance(position, zone.Center) <= zone.Radius)
                {
                    return zone.Destination;
                }
            }

            return null;
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

            if (args.Length == 0)
            {
                ShowHomeList(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "set":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /home set <название>");
                        return;
                    }
                    SetHomeCommand(player, args[1]);
                    break;
                case "del":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /home del <название>");
                        return;
                    }
                    DeleteHomeCommand(player, args[1]);
                    break;
                case "list":
                    ShowHomeList(player);
                    break;
                case "share":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /home share <название> <игрок>");
                        return;
                    }
                    ShareHomeCommand(player, args[1], args[2]);
                    break;
                default:
                    TeleportToHomeCommand(player, args[0]);
                    break;
            }
        }

        [ChatCommand("tpa")]
        private void TPACommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableTPA)
            {
                player.ChatMessage("Система TPA отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowTPAHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "to":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /tpa to <игрок>");
                        return;
                    }
                    SendTPARequest(player, args[1], false);
                    break;
                case "here":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /tpa here <игрок>");
                        return;
                    }
                    SendTPARequest(player, args[1], true);
                    break;
                case "accept":
                    AcceptTPARequest(player);
                    break;
                case "deny":
                    DenyTPARequest(player);
                    break;
                case "cancel":
                    CancelTPARequest(player);
                    break;
            }
        }

        [ChatCommand("back")]
        private void BackCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableBack)
            {
                player.ChatMessage("Команда /back отключена");
                return;
            }

            if (!CanUseTeleport(player.userID, "back"))
            {
                var timeLeft = config.BackCooldown - (Time.time - lastBackUse[player.userID]);
                player.ChatMessage($"<color=red>Команда доступна через {timeLeft:F0} секунд</color>");
                return;
            }

            if (!playerBackHistory.ContainsKey(player.userID) || playerBackHistory[player.userID].Count == 0)
            {
                player.ChatMessage("<color=red>Нет предыдущих позиций</color>");
                return;
            }

            var lastPosition = playerBackHistory[player.userID].Last();
            StartTeleportation(player, lastPosition, "back", config.TeleportCost);
        }

        [ChatCommand("rtp")]
        private void RandomTPCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableRandomTP)
            {
                player.ChatMessage("Случайная телепортация отключена");
                return;
            }

            if (!CanUseTeleport(player.userID, "random"))
            {
                var timeLeft = config.RandomTPCooldown - (Time.time - lastRandomTPUse[player.userID]);
                player.ChatMessage($"<color=red>Команда доступна через {timeLeft:F0} секунд</color>");
                return;
            }

            var randomLocation = GetRandomTeleportLocation();
            StartTeleportation(player, randomLocation, "random", config.TeleportCost);
        }

        [ChatCommand("tp")]
        private void TeleportCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав на использование этой команды");
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /tp <игрок1> <игрок2> или /tp <игрок> <x> <y> <z>");
                return;
            }

            if (args.Length == 2)
            {
                // Телепорт игрока к игроку
                var player1 = BasePlayer.Find(args[0]);
                var player2 = BasePlayer.Find(args[1]);

                if (player1 == null || player2 == null)
                {
                    player.ChatMessage("Один из игроков не найден");
                    return;
                }

                player1.Teleport(player2.transform.position);
                player.ChatMessage($"<color=green>{player1.displayName} телепортирован к {player2.displayName}</color>");
            }
            else if (args.Length >= 4)
            {
                // Телепорт игрока к координатам
                var targetPlayer = BasePlayer.Find(args[0]);
                if (targetPlayer == null)
                {
                    player.ChatMessage("Игрок не найден");
                    return;
                }

                var x = float.Parse(args[1]);
                var y = float.Parse(args[2]);
                var z = float.Parse(args[3]);

                targetPlayer.Teleport(new Vector3(x, y, z));
                player.ChatMessage($"<color=green>{targetPlayer.displayName} телепортирован к координатам {x}, {y}, {z}</color>");
            }
        }
        #endregion

        #region Command Methods
        private void ShowHomeList(BasePlayer player)
        {
            if (!playerHomes.ContainsKey(player.userID) || playerHomes[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет домов. Используйте /home set <название>");
                return;
            }

            player.ChatMessage("<color=yellow>=== ВАШИ ДОМА ===</color>");
            foreach (var home in playerHomes[player.userID])
            {
                var sharedInfo = home.SharedWith.Count > 0 ? $" (Поделен с {home.SharedWith.Count} игроками)" : "";
                player.ChatMessage($"<color=cyan>{home.Name}</color> - {home.Position}{sharedInfo}");
            }
        }

        private void SetHomeCommand(BasePlayer player, string homeName)
        {
            if (!playerHomes.ContainsKey(player.userID))
            {
                playerHomes[player.userID] = new List<HomeLocation>();
            }

            var homes = playerHomes[player.userID];
            if (homes.Count >= config.MaxHomes)
            {
                player.ChatMessage($"<color=red>Максимум домов: {config.MaxHomes}</color>");
                return;
            }

            if (homes.Any(h => h.Name.ToLower() == homeName.ToLower()))
            {
                player.ChatMessage($"<color=red>Дом с названием '{homeName}' уже существует</color>");
                return;
            }

            var home = new HomeLocation
            {
                Name = homeName,
                Position = player.transform.position,
                Created = Time.time
            };

            homes.Add(home);
            player.ChatMessage($"<color=green>Дом '{homeName}' создан</color>");
            SaveData();
        }

        private void DeleteHomeCommand(BasePlayer player, string homeName)
        {
            if (!playerHomes.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет домов");
                return;
            }

            var home = playerHomes[player.userID].FirstOrDefault(h => h.Name.ToLower() == homeName.ToLower());
            if (home == null)
            {
                player.ChatMessage($"<color=red>Дом '{homeName}' не найден</color>");
                return;
            }

            playerHomes[player.userID].Remove(home);
            player.ChatMessage($"<color=green>Дом '{homeName}' удален</color>");
            SaveData();
        }

        private void TeleportToHomeCommand(BasePlayer player, string homeName)
        {
            if (!CanUseTeleport(player.userID, "home"))
            {
                var timeLeft = config.HomeCooldown - (Time.time - lastHomeUse[player.userID]);
                player.ChatMessage($"<color=red>Команда доступна через {timeLeft:F0} секунд</color>");
                return;
            }

            if (!playerHomes.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет домов");
                return;
            }

            var home = playerHomes[player.userID].FirstOrDefault(h => h.Name.ToLower() == homeName.ToLower());
            if (home == null)
            {
                player.ChatMessage($"<color=red>Дом '{homeName}' не найден</color>");
                return;
            }

            StartTeleportation(player, home.Position, "home", config.HomeCost);
        }

        private void ShareHomeCommand(BasePlayer player, string homeName, string targetName)
        {
            if (!playerHomes.ContainsKey(player.userID))
            {
                player.ChatMessage("У вас нет домов");
                return;
            }

            var home = playerHomes[player.userID].FirstOrDefault(h => h.Name.ToLower() == homeName.ToLower());
            if (home == null)
            {
                player.ChatMessage($"<color=red>Дом '{homeName}' не найден</color>");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            if (home.SharedWith.Contains(target.userID))
            {
                player.ChatMessage($"<color=red>Дом уже поделен с {target.displayName}</color>");
                return;
            }

            home.SharedWith.Add(target.userID);
            player.ChatMessage($"<color=green>Дом '{homeName}' поделен с {target.displayName}</color>");
            target.ChatMessage($"<color=green>{player.displayName} поделился домом '{homeName}' с вами</color>");
            SaveData();
        }

        private void ShowTPAHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ TPA ===</color>");
            player.ChatMessage("/tpa to <игрок> - телепорт к игроку");
            player.ChatMessage("/tpa here <игрок> - телепорт игрока к вам");
            player.ChatMessage("/tpa accept - принять запрос");
            player.ChatMessage("/tpa deny - отклонить запрос");
            player.ChatMessage("/tpa cancel - отменить запрос");
        }

        private void SendTPARequest(BasePlayer player, string targetName, bool isTPAHere)
        {
            if (!CanUseTeleport(player.userID, "tpa"))
            {
                var timeLeft = config.TPACooldown - (Time.time - lastTPAUse[player.userID]);
                player.ChatMessage($"<color=red>Команда доступна через {timeLeft:F0} секунд</color>");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"<color=red>Игрок '{targetName}' не найден</color>");
                return;
            }

            if (target == player)
            {
                player.ChatMessage("<color=red>Нельзя телепортироваться к себе</color>");
                return;
            }

            if (pendingRequests.ContainsKey(target.userID))
            {
                player.ChatMessage($"<color=red>У {target.displayName} уже есть активный запрос</color>");
                return;
            }

            var request = new TeleportRequest
            {
                FromPlayer = player.userID,
                ToPlayer = target.userID,
                RequestTime = Time.time,
                IsTPAHere = isTPAHere,
                RequestType = isTPAHere ? "tpahere" : "tpa"
            };

            pendingRequests[target.userID] = request;

            var requestType = isTPAHere ? "телепортировать к вам" : "телепортироваться к";
            player.ChatMessage($"<color=green>Запрос отправлен {target.displayName}</color>");
            target.ChatMessage($"<color=yellow>{player.displayName} хочет {requestType}</color>");
            target.ChatMessage("<color=white>Используйте /tpa accept или /tpa deny</color>");

            // Автоматически отменяем запрос через 30 секунд
            timer.Once(30f, () =>
            {
                if (pendingRequests.ContainsKey(target.userID) && pendingRequests[target.userID].FromPlayer == player.userID)
                {
                    pendingRequests.Remove(target.userID);
                    player.ChatMessage($"<color=red>Запрос к {target.displayName} истек</color>");
                }
            });
        }

        private void AcceptTPARequest(BasePlayer player)
        {
            if (!pendingRequests.ContainsKey(player.userID))
            {
                player.ChatMessage("<color=red>У вас нет активных запросов</color>");
                return;
            }

            var request = pendingRequests[player.userID];
            var fromPlayer = BasePlayer.FindByID(request.FromPlayer);

            if (fromPlayer == null || !fromPlayer.IsConnected)
            {
                player.ChatMessage("<color=red>Игрок, отправивший запрос, не в сети</color>");
                pendingRequests.Remove(player.userID);
                return;
            }

            pendingRequests.Remove(player.userID);

            if (request.IsTPAHere)
            {
                // Телепортируем игрока к нам
                StartTeleportation(fromPlayer, player.transform.position, "tpa", config.TPACost);
                player.ChatMessage($"<color=green>Запрос принят. {fromPlayer.displayName} телепортируется к вам</color>");
            }
            else
            {
                // Телепортируем нас к игроку
                StartTeleportation(player, fromPlayer.transform.position, "tpa", config.TPACost);
                player.ChatMessage($"<color=green>Запрос принят. Вы телепортируетесь к {fromPlayer.displayName}</color>");
            }
        }

        private void DenyTPARequest(BasePlayer player)
        {
            if (!pendingRequests.ContainsKey(player.userID))
            {
                player.ChatMessage("<color=red>У вас нет активных запросов</color>");
                return;
            }

            var request = pendingRequests[player.userID];
            var fromPlayer = BasePlayer.FindByID(request.FromPlayer);

            pendingRequests.Remove(player.userID);

            if (fromPlayer != null && fromPlayer.IsConnected)
            {
                fromPlayer.ChatMessage($"<color=red>{player.displayName} отклонил ваш запрос</color>");
            }

            player.ChatMessage("<color=green>Запрос отклонен</color>");
        }

        private void CancelTPARequest(BasePlayer player)
        {
            var request = pendingRequests.FirstOrDefault(kvp => kvp.Value.FromPlayer == player.userID);
            if (request.Value == null)
            {
                player.ChatMessage("<color=red>У вас нет активных запросов</color>");
                return;
            }

            var target = BasePlayer.FindByID(request.Value.ToPlayer);
            if (target != null && target.IsConnected)
            {
                target.ChatMessage($"<color=red>{player.displayName} отменил запрос</color>");
            }

            pendingRequests.Remove(request.Key);
            player.ChatMessage("<color=green>Запрос отменен</color>");
        }
        #endregion
    }
}
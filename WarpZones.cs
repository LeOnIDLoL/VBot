using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Warp Zones", "BULBARUST", "1.0.0")]
    [Description("Система варп-зон для сервера BULBARUST")]
    public class WarpZones : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableWarpZones { get; set; } = true;
            public bool EnablePrivateZones { get; set; } = true;
            public bool EnablePublicZones { get; set; } = true;
            public bool EnableZoneProtection { get; set; } = true;
            public float DefaultZoneRadius { get; set; } = 20f;
            public float MaxZoneRadius { get; set; } = 100f;
            public float MinZoneDistance { get; set; } = 50f;
            public float TeleportDelay { get; set; } = 3f;
            public bool RequirePermission { get; set; } = false;
            public List<WarpZone> DefaultZones { get; set; } = new List<WarpZone>();
            public List<ZoneEffect> ZoneEffects { get; set; } = new List<ZoneEffect>();
        }

        private class WarpZone
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public Vector3 Position { get; set; }
            public float Radius { get; set; }
            public string Category { get; set; } = "Общее";
            public bool IsPublic { get; set; } = true;
            public bool IsProtected { get; set; } = true;
            public bool AllowPvP { get; set; } = true;
            public bool AllowBuilding { get; set; } = false;
            public bool AllowMining { get; set; } = true;
            public ulong Owner { get; set; } = 0;
            public string OwnerName { get; set; } = "";
            public List<ulong> AllowedPlayers { get; set; } = new List<ulong>();
            public List<string> RequiredPermissions { get; set; } = new List<string>();
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
            public float Created { get; set; }
            public float LastUsed { get; set; }
            public int UsageCount { get; set; } = 0;
        }

        private class ZoneEffect
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float Duration { get; set; } = 60f;
            public List<string> Effects { get; set; } = new List<string>();
            public float Chance { get; set; } = 100f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем стандартные зоны
            config.DefaultZones.AddRange(new List<WarpZone>
            {
                new WarpZone
                {
                    Name = "Спавн",
                    Description = "Главная точка спавна",
                    Position = new Vector3(0, 100, 0),
                    Radius = 50f,
                    Category = "Основные",
                    IsPublic = true,
                    IsProtected = true,
                    AllowPvP = false,
                    AllowBuilding = false,
                    AllowMining = false
                },
                new WarpZone
                {
                    Name = "Торговец",
                    Description = "Торговая зона",
                    Position = new Vector3(100, 100, 100),
                    Radius = 30f,
                    Category = "Торговля",
                    IsPublic = true,
                    IsProtected = true,
                    AllowPvP = false,
                    AllowBuilding = false,
                    AllowMining = false
                },
                new WarpZone
                {
                    Name = "PvP Арена",
                    Description = "Зона для PvP боев",
                    Position = new Vector3(-100, 100, -100),
                    Radius = 40f,
                    Category = "PvP",
                    IsPublic = true,
                    IsProtected = false,
                    AllowPvP = true,
                    AllowBuilding = false,
                    AllowMining = false
                },
                new WarpZone
                {
                    Name = "Шахта",
                    Description = "Зона для добычи ресурсов",
                    Position = new Vector3(200, 100, 200),
                    Radius = 60f,
                    Category = "Ресурсы",
                    IsPublic = true,
                    IsProtected = false,
                    AllowPvP = true,
                    AllowBuilding = false,
                    AllowMining = true
                },
                new WarpZone
                {
                    Name = "Стройка",
                    Description = "Зона для строительства",
                    Position = new Vector3(-200, 100, -200),
                    Radius = 80f,
                    Category = "Строительство",
                    IsPublic = true,
                    IsProtected = false,
                    AllowPvP = false,
                    AllowBuilding = true,
                    AllowMining = true
                }
            });

            // Добавляем эффекты зон
            config.ZoneEffects.AddRange(new List<ZoneEffect>
            {
                new ZoneEffect
                {
                    Name = "Ускорение",
                    Description = "Увеличивает скорость передвижения",
                    Duration = 300f,
                    Effects = new List<string> { "speed_boost" },
                    Chance = 50f
                },
                new ZoneEffect
                {
                    Name = "Регенерация",
                    Description = "Восстанавливает здоровье",
                    Duration = 180f,
                    Effects = new List<string> { "health_regen" },
                    Chance = 30f
                },
                new ZoneEffect
                {
                    Name = "Буст ресурсов",
                    Description = "Увеличивает добычу ресурсов",
                    Duration = 600f,
                    Effects = new List<string> { "resource_boost" },
                    Chance = 25f
                },
                new ZoneEffect
                {
                    Name = "Невидимость",
                    Description = "Делает игрока невидимым",
                    Duration = 120f,
                    Effects = new List<string> { "invisibility" },
                    Chance = 10f
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
        private Dictionary<string, WarpZone> warpZones = new Dictionary<string, WarpZone>();
        private Dictionary<ulong, float> lastTeleport = new Dictionary<ulong, float>();
        private Dictionary<ulong, string> playerCurrentZone = new Dictionary<ulong, string>();
        private Dictionary<ulong, float> zoneEffectStart = new Dictionary<ulong, float>();
        private Dictionary<ulong, List<string>> activeEffects = new Dictionary<ulong, List<string>>();
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система варп-зон BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableWarpZones)
            {
                InitializeDefaultZones();
                timer.Every(5f, ProcessZoneEffects);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableWarpZones)
            {
                // Проверяем, в какой зоне находится игрок
                CheckPlayerZone(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Очищаем данные игрока
            if (playerCurrentZone.ContainsKey(player.userID))
            {
                playerCurrentZone.Remove(player.userID);
            }
            if (activeEffects.ContainsKey(player.userID))
            {
                activeEffects.Remove(player.userID);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableZoneProtection) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var entity = go.GetComponent<BaseEntity>();
            if (entity == null) return;

            var zone = GetZoneAtPosition(entity.transform.position);
            if (zone != null && !CanBuildInZone(player, zone))
            {
                player.ChatMessage($"<color=red>Строительство запрещено в зоне '{zone.Name}'!</color>");
                entity.Kill();
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableZoneProtection) return;

            var player = info.InitiatorPlayer;
            if (player == null) return;

            var zone = GetZoneAtPosition(entity.transform.position);
            if (zone != null && !CanPvPInZone(player, zone))
            {
                player.ChatMessage($"<color=red>PvP запрещен в зоне '{zone.Name}'!</color>");
                // Восстанавливаем здоровье
                entity.health = entity.MaxHealth();
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            warpZones = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, WarpZone>>("warp_zones") ?? new Dictionary<string, WarpZone>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("warp_zones", warpZones);
        }

        private void InitializeDefaultZones()
        {
            foreach (var defaultZone in config.DefaultZones)
            {
                if (!warpZones.ContainsKey(defaultZone.Name.ToLower()))
                {
                    defaultZone.Created = Time.time;
                    warpZones[defaultZone.Name.ToLower()] = defaultZone;
                }
            }
            SaveData();
        }

        private WarpZone GetZoneAtPosition(Vector3 position)
        {
            foreach (var zone in warpZones.Values)
            {
                if (Vector3.Distance(position, zone.Position) <= zone.Radius)
                {
                    return zone;
                }
            }
            return null;
        }

        private bool CanBuildInZone(BasePlayer player, WarpZone zone)
        {
            if (zone.AllowBuilding) return true;
            if (zone.Owner == player.userID) return true;
            if (zone.AllowedPlayers.Contains(player.userID)) return true;
            return false;
        }

        private bool CanPvPInZone(BasePlayer player, WarpZone zone)
        {
            return zone.AllowPvP;
        }

        private bool CanAccessZone(BasePlayer player, WarpZone zone)
        {
            if (zone.IsPublic) return true;
            if (zone.Owner == player.userID) return true;
            if (zone.AllowedPlayers.Contains(player.userID)) return true;
            
            // Проверяем права доступа
            if (zone.RequiredPermissions.Count > 0)
            {
                foreach (var permission in zone.RequiredPermissions)
                {
                    if (permission.UserHasPermission(player.UserIDString, permission))
                    {
                        return true;
                    }
                }
                return false;
            }

            return false;
        }

        private void CheckPlayerZone(BasePlayer player)
        {
            var zone = GetZoneAtPosition(player.transform.position);
            var currentZone = playerCurrentZone.ContainsKey(player.userID) ? playerCurrentZone[player.userID] : null;

            if (zone != null && zone.Name != currentZone)
            {
                // Игрок вошел в новую зону
                EnterZone(player, zone);
            }
            else if (zone == null && currentZone != null)
            {
                // Игрок покинул зону
                ExitZone(player, currentZone);
            }
        }

        private void EnterZone(BasePlayer player, WarpZone zone)
        {
            playerCurrentZone[player.userID] = zone.Name;
            zone.LastUsed = Time.time;
            zone.UsageCount++;

            player.ChatMessage($"<color=green>Вы вошли в зону: {zone.Name}</color>");
            if (!string.IsNullOrEmpty(zone.Description))
            {
                player.ChatMessage($"<color=white>{zone.Description}</color>");
            }

            // Применяем эффекты зоны
            ApplyZoneEffects(player, zone);
        }

        private void ExitZone(BasePlayer player, string zoneName)
        {
            playerCurrentZone.Remove(player.userID);
            player.ChatMessage($"<color=yellow>Вы покинули зону: {zoneName}</color>");

            // Убираем эффекты
            RemoveZoneEffects(player);
        }

        private void ApplyZoneEffects(BasePlayer player, WarpZone zone)
        {
            if (activeEffects.ContainsKey(player.userID))
            {
                activeEffects[player.userID].Clear();
            }
            else
            {
                activeEffects[player.userID] = new List<string>();
            }

            zoneEffectStart[player.userID] = Time.time;

            // Применяем случайные эффекты
            foreach (var effect in config.ZoneEffects)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= effect.Chance)
                {
                    ApplyEffect(player, effect);
                }
            }
        }

        private void ApplyEffect(BasePlayer player, ZoneEffect effect)
        {
            foreach (var effectName in effect.Effects)
            {
                switch (effectName)
                {
                    case "speed_boost":
                        player.movementSpeed = 2f; // Увеличиваем скорость
                        break;
                    case "health_regen":
                        // Восстанавливаем здоровье
                        timer.Every(1f, () => {
                            if (player.IsConnected && player.health < player.MaxHealth())
                            {
                                player.health = Mathf.Min(player.MaxHealth(), player.health + 5f);
                            }
                        });
                        break;
                    case "resource_boost":
                        // Буст ресурсов (логика должна быть в других плагинах)
                        break;
                    case "invisibility":
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        break;
                }

                activeEffects[player.userID].Add(effectName);
                player.ChatMessage($"<color=cyan>Получен эффект: {effect.Name}</color>");
            }
        }

        private void RemoveZoneEffects(BasePlayer player)
        {
            if (!activeEffects.ContainsKey(player.userID)) return;

            foreach (var effect in activeEffects[player.userID])
            {
                switch (effect)
                {
                    case "speed_boost":
                        player.movementSpeed = 1f; // Возвращаем обычную скорость
                        break;
                    case "invisibility":
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        break;
                }
            }

            activeEffects[player.userID].Clear();
        }

        private void ProcessZoneEffects()
        {
            var currentTime = Time.time;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!activeEffects.ContainsKey(player.userID)) continue;

                var zone = GetZoneAtPosition(player.transform.position);
                if (zone == null)
                {
                    RemoveZoneEffects(player);
                    continue;
                }

                // Проверяем, не истекли ли эффекты
                var effectDuration = currentTime - zoneEffectStart[player.userID];
                foreach (var effect in config.ZoneEffects)
                {
                    if (effectDuration >= effect.Duration)
                    {
                        RemoveZoneEffects(player);
                        break;
                    }
                }
            }
        }

        private void TeleportToZone(BasePlayer player, WarpZone zone)
        {
            if (!CanAccessZone(player, zone))
            {
                player.ChatMessage($"<color=red>У вас нет доступа к зоне '{zone.Name}'</color>");
                return;
            }

            var currentTime = Time.time;
            if (lastTeleport.ContainsKey(player.userID))
            {
                var timeLeft = config.TeleportDelay - (currentTime - lastTeleport[player.userID]);
                if (timeLeft > 0)
                {
                    player.ChatMessage($"<color=red>Осталось ждать: {Mathf.CeilToInt(timeLeft)} секунд</color>");
                    return;
                }
            }

            lastTeleport[player.userID] = currentTime;
            zone.LastUsed = currentTime;
            zone.UsageCount++;

            player.ChatMessage($"<color=yellow>Телепортация в зону '{zone.Name}' через {config.TeleportDelay} секунд...</color>");
            player.ChatMessage("<color=red>НЕ ДВИГАЙТЕСЬ!</color>");

            timer.Once(config.TeleportDelay, () => {
                if (player.IsConnected)
                {
                    player.Teleport(zone.Position);
                    player.ChatMessage($"<color=green>Телепортация завершена!</color>");
                    EnterZone(player, zone);
                }
            });
        }

        private void CreateZone(ulong playerId, string playerName, string zoneName, Vector3 position, float radius)
        {
            var zone = new WarpZone
            {
                Name = zoneName,
                Description = $"Зона создана игроком {playerName}",
                Position = position,
                Radius = radius,
                Owner = playerId,
                OwnerName = playerName,
                IsPublic = false,
                IsProtected = true,
                AllowPvP = true,
                AllowBuilding = true,
                AllowMining = true,
                Created = Time.time
            };

            warpZones[zoneName.ToLower()] = zone;
            SaveData();
        }

        private void DeleteZone(string zoneName)
        {
            if (warpZones.ContainsKey(zoneName.ToLower()))
            {
                warpZones.Remove(zoneName.ToLower());
                SaveData();
            }
        }
        #endregion

        #region Commands
        [ChatCommand("warp")]
        private void WarpCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableWarpZones)
            {
                player.ChatMessage("Система варп-зон отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowWarpHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowWarpList(player, args.Length > 1 ? args[1] : null);
                    break;
                case "go":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /warp go <название_зоны>");
                        return;
                    }
                    TeleportToZoneCommand(player, args[1]);
                    break;
                case "create":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /warp create <название> <радиус>");
                        return;
                    }
                    CreateZoneCommand(player, args);
                    break;
                case "delete":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /warp delete <название>");
                        return;
                    }
                    DeleteZoneCommand(player, args[1]);
                    break;
                case "info":
                    ShowZoneInfo(player, args.Length > 1 ? args[1] : null);
                    break;
                case "near":
                    ShowNearbyZones(player);
                    break;
                case "set":
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Использование: /warp set <название> <параметр> <значение>");
                        return;
                    }
                    SetZonePropertyCommand(player, args);
                    break;
            }
        }

        [ChatCommand("zone")]
        private void ZoneCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                ShowCurrentZone(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "info":
                    ShowCurrentZoneInfo(player);
                    break;
                case "effects":
                    ShowZoneEffects(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowWarpHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ ВАРП-ЗОН ===</color>");
            player.ChatMessage("/warp list [категория] - список зон");
            player.ChatMessage("/warp go <название> - телепорт в зону");
            player.ChatMessage("/warp create <название> <радиус> - создать зону");
            player.ChatMessage("/warp delete <название> - удалить зону");
            player.ChatMessage("/warp info [название] - информация о зоне");
            player.ChatMessage("/warp near - зоны рядом");
            player.ChatMessage("/warp set <название> <параметр> <значение> - настройки зоны");
            player.ChatMessage("/zone - текущая зона");
            player.ChatMessage("/zone effects - эффекты зоны");
        }

        private void ShowWarpList(BasePlayer player, string category = null)
        {
            var zones = warpZones.Values.Where(z => CanAccessZone(player, z));
            
            if (!string.IsNullOrEmpty(category))
            {
                zones = zones.Where(z => z.Category.ToLower() == category.ToLower());
            }

            if (zones.Count() == 0)
            {
                player.ChatMessage("Нет доступных зон");
                return;
            }

            player.ChatMessage($"<color=yellow>=== ВАРП-ЗОНЫ {(category != null ? $"({category})" : "")} ===</color>");
            foreach (var zone in zones.OrderBy(z => z.Name))
            {
                var distance = Vector3.Distance(player.transform.position, zone.Position);
                var status = zone.IsPublic ? "Публичная" : "Приватная";
                player.ChatMessage($"{zone.Name} - {distance:F1}м - {status}");
                if (!string.IsNullOrEmpty(zone.Description))
                {
                    player.ChatMessage($"  {zone.Description}");
                }
            }
        }

        private void TeleportToZoneCommand(BasePlayer player, string zoneName)
        {
            var zone = warpZones.Values.FirstOrDefault(z => z.Name.ToLower() == zoneName.ToLower());
            if (zone == null)
            {
                player.ChatMessage($"Зона '{zoneName}' не найдена");
                return;
            }

            TeleportToZone(player, zone);
        }

        private void CreateZoneCommand(BasePlayer player, string[] args)
        {
            if (!config.EnablePrivateZones)
            {
                player.ChatMessage("Создание приватных зон отключено");
                return;
            }

            var zoneName = args[1];
            if (!float.TryParse(args[2], out float radius))
            {
                player.ChatMessage("Неверный радиус");
                return;
            }

            if (radius < 5f || radius > config.MaxZoneRadius)
            {
                player.ChatMessage($"Радиус должен быть от 5 до {config.MaxZoneRadius}");
                return;
            }

            if (warpZones.ContainsKey(zoneName.ToLower()))
            {
                player.ChatMessage("Зона с таким названием уже существует");
                return;
            }

            // Проверяем расстояние до других зон
            var tooClose = warpZones.Values.Any(z => Vector3.Distance(player.transform.position, z.Position) < config.MinZoneDistance);
            if (tooClose)
            {
                player.ChatMessage($"Слишком близко к другой зоне! Минимальное расстояние: {config.MinZoneDistance}м");
                return;
            }

            CreateZone(player.userID, player.displayName, zoneName, player.transform.position, radius);
            player.ChatMessage($"<color=green>Зона '{zoneName}' создана!</color>");
        }

        private void DeleteZoneCommand(BasePlayer player, string zoneName)
        {
            if (!warpZones.ContainsKey(zoneName.ToLower()))
            {
                player.ChatMessage($"Зона '{zoneName}' не найдена");
                return;
            }

            var zone = warpZones[zoneName.ToLower()];
            if (zone.Owner != player.userID && !player.IsAdmin)
            {
                player.ChatMessage("Вы не можете удалить эту зону");
                return;
            }

            DeleteZone(zoneName);
            player.ChatMessage($"<color=red>Зона '{zoneName}' удалена!</color>");
        }

        private void ShowZoneInfo(BasePlayer player, string zoneName = null)
        {
            WarpZone zone = null;

            if (string.IsNullOrEmpty(zoneName))
            {
                zone = GetZoneAtPosition(player.transform.position);
                if (zone == null)
                {
                    player.ChatMessage("Рядом нет зон");
                    return;
                }
            }
            else
            {
                zone = warpZones.Values.FirstOrDefault(z => z.Name.ToLower() == zoneName.ToLower());
                if (zone == null)
                {
                    player.ChatMessage($"Зона '{zoneName}' не найдена");
                    return;
                }
            }

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О ЗОНЕ ===</color>");
            player.ChatMessage($"Название: {zone.Name}");
            player.ChatMessage($"Описание: {zone.Description}");
            player.ChatMessage($"Категория: {zone.Category}");
            player.ChatMessage($"Позиция: {zone.Position}");
            player.ChatMessage($"Радиус: {zone.Radius}м");
            player.ChatMessage($"Владелец: {zone.OwnerName}");
            player.ChatMessage($"Тип: {(zone.IsPublic ? "Публичная" : "Приватная")}");
            player.ChatMessage($"Защита: {(zone.IsProtected ? "Включена" : "Отключена")}");
            player.ChatMessage($"PvP: {(zone.AllowPvP ? "Разрешен" : "Запрещен")}");
            player.ChatMessage($"Строительство: {(zone.AllowBuilding ? "Разрешено" : "Запрещено")}");
            player.ChatMessage($"Добыча: {(zone.AllowMining ? "Разрешена" : "Запрещена")}");
            player.ChatMessage($"Использований: {zone.UsageCount}");
            player.ChatMessage($"Создана: {TimeSpan.FromSeconds(Time.time - zone.Created).TotalDays:F1} дней назад");
        }

        private void ShowNearbyZones(BasePlayer player)
        {
            var nearbyZones = warpZones.Values
                .Where(z => Vector3.Distance(player.transform.position, z.Position) <= 200f)
                .OrderBy(z => Vector3.Distance(player.transform.position, z.Position))
                .Take(10)
                .ToList();

            if (nearbyZones.Count == 0)
            {
                player.ChatMessage("Рядом нет зон");
                return;
            }

            player.ChatMessage("<color=yellow>=== ЗОНЫ РЯДОМ ===</color>");
            foreach (var zone in nearbyZones)
            {
                var distance = Vector3.Distance(player.transform.position, zone.Position);
                var access = CanAccessZone(player, zone) ? "Доступна" : "Недоступна";
                player.ChatMessage($"{zone.Name} - {distance:F1}м - {access}");
            }
        }

        private void SetZonePropertyCommand(BasePlayer player, string[] args)
        {
            var zoneName = args[1];
            var property = args[2];
            var value = args.Length > 3 ? args[3] : "";

            if (!warpZones.ContainsKey(zoneName.ToLower()))
            {
                player.ChatMessage($"Зона '{zoneName}' не найдена");
                return;
            }

            var zone = warpZones[zoneName.ToLower()];
            if (zone.Owner != player.userID && !player.IsAdmin)
            {
                player.ChatMessage("Вы не можете изменять эту зону");
                return;
            }

            switch (property.ToLower())
            {
                case "description":
                    zone.Description = value;
                    player.ChatMessage($"<color=green>Описание зоны изменено</color>");
                    break;
                case "pvp":
                    if (bool.TryParse(value, out bool pvp))
                    {
                        zone.AllowPvP = pvp;
                        player.ChatMessage($"<color=green>PvP в зоне: {(pvp ? "разрешен" : "запрещен")}</color>");
                    }
                    break;
                case "building":
                    if (bool.TryParse(value, out bool building))
                    {
                        zone.AllowBuilding = building;
                        player.ChatMessage($"<color=green>Строительство в зоне: {(building ? "разрешено" : "запрещено")}</color>");
                    }
                    break;
                case "mining":
                    if (bool.TryParse(value, out bool mining))
                    {
                        zone.AllowMining = mining;
                        player.ChatMessage($"<color=green>Добыча в зоне: {(mining ? "разрешена" : "запрещена")}</color>");
                    }
                    break;
                case "protected":
                    if (bool.TryParse(value, out bool protected_))
                    {
                        zone.IsProtected = protected_;
                        player.ChatMessage($"<color=green>Защита зоны: {(protected_ ? "включена" : "отключена")}</color>");
                    }
                    break;
                default:
                    player.ChatMessage("Неизвестный параметр");
                    return;
            }

            SaveData();
        }

        private void ShowCurrentZone(BasePlayer player)
        {
            var zone = GetZoneAtPosition(player.transform.position);
            if (zone == null)
            {
                player.ChatMessage("Вы не находитесь в зоне");
                return;
            }

            player.ChatMessage($"<color=green>Текущая зона: {zone.Name}</color>");
            if (!string.IsNullOrEmpty(zone.Description))
            {
                player.ChatMessage($"<color=white>{zone.Description}</color>");
            }
        }

        private void ShowCurrentZoneInfo(BasePlayer player)
        {
            var zone = GetZoneAtPosition(player.transform.position);
            if (zone == null)
            {
                player.ChatMessage("Вы не находитесь в зоне");
                return;
            }

            ShowZoneInfo(player, zone.Name);
        }

        private void ShowZoneEffects(BasePlayer player)
        {
            if (!activeEffects.ContainsKey(player.userID) || activeEffects[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет активных эффектов");
                return;
            }

            player.ChatMessage("<color=yellow>=== АКТИВНЫЕ ЭФФЕКТЫ ===</color>");
            foreach (var effect in activeEffects[player.userID])
            {
                player.ChatMessage($"<color=cyan>{effect}</color>");
            }
        }
        #endregion
    }
}
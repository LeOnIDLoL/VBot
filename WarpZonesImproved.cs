using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Warp Zones Improved", "BULBARUST", "2.0.0")]
    [Description("Улучшенная система варп-зон для BULBARUST")]
    public class WarpZonesImproved : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableWarpZones { get; set; } = true;
            public bool EnablePvPZones { get; set; } = true;
            public bool EnableSafeZones { get; set; } = true;
            public bool EnableRadiationZones { get; set; } = true;
            public bool EnableEventZones { get; set; } = true;
            public float DefaultZoneRadius { get; set; } = 50f;
            public float WarpDelay { get; set; } = 3f;
            public float WarpCooldown { get; set; } = 60f;
            public List<WarpZone> DefaultZones { get; set; } = new List<WarpZone>();
        }

        private class WarpZone
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public Vector3 Position { get; set; }
            public float Radius { get; set; }
            public ZoneType Type { get; set; }
            public bool IsActive { get; set; } = true;
            public bool RequirePermission { get; set; } = false;
            public string Permission { get; set; } = "";
            public float Cost { get; set; } = 0f;
            public List<ZoneEffect> Effects { get; set; } = new List<ZoneEffect>();
            public List<string> AllowedCommands { get; set; } = new List<string>();
            public List<string> DisallowedCommands { get; set; } = new List<string>();
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }

        private class ZoneEffect
        {
            public string Type { get; set; }
            public float Value { get; set; }
            public float Duration { get; set; }
        }

        private enum ZoneType
        {
            Safe,
            PvP,
            NoTPE,
            NoBuild,
            NoPickup,
            Radiation,
            Event,
            Shop,
            Arena
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            config.DefaultZones.AddRange(new List<WarpZone>
            {
                new WarpZone
                {
                    Id = "spawn",
                    Name = "Спавн",
                    Description = "Безопасная зона спавна",
                    Position = new Vector3(0, 100, 0),
                    Radius = 100f,
                    Type = ZoneType.Safe,
                    Effects = new List<ZoneEffect>
                    {
                        new ZoneEffect { Type = "heal", Value = 10f, Duration = 5f },
                        new ZoneEffect { Type = "god", Value = 1f, Duration = 0f }
                    },
                    DisallowedCommands = new List<string> { "raid", "attack" }
                },
                new WarpZone
                {
                    Id = "pvp_arena",
                    Name = "PvP Арена",
                    Description = "Зона для сражений",
                    Position = new Vector3(500, 100, 500),
                    Radius = 75f,
                    Type = ZoneType.Arena,
                    Effects = new List<ZoneEffect>
                    {
                        new ZoneEffect { Type = "damage_multiplier", Value = 1.5f, Duration = 0f }
                    }
                },
                new WarpZone
                {
                    Id = "shop",
                    Name = "Торговая зона",
                    Description = "Безопасная торговая зона",
                    Position = new Vector3(-500, 100, -500),
                    Radius = 50f,
                    Type = ZoneType.Shop,
                    Effects = new List<ZoneEffect>
                    {
                        new ZoneEffect { Type = "god", Value = 1f, Duration = 0f }
                    },
                    AllowedCommands = new List<string> { "shop", "trade", "buy", "sell" }
                },
                new WarpZone
                {
                    Id = "radiation",
                    Name = "Радиационная зона",
                    Description = "Опасная радиационная зона",
                    Position = new Vector3(1000, 100, 1000),
                    Radius = 100f,
                    Type = ZoneType.Radiation,
                    Effects = new List<ZoneEffect>
                    {
                        new ZoneEffect { Type = "radiation", Value = 5f, Duration = 1f }
                    }
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
        private Dictionary<string, WarpZone> warpZones = new Dictionary<string, WarpZone>();
        private Dictionary<ulong, string> playerCurrentZone = new Dictionary<ulong, string>();
        private Dictionary<ulong, float> lastWarpTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> warpingPlayers = new Dictionary<ulong, float>();

        private class PlayerZoneData
        {
            public ulong PlayerId { get; set; }
            public string CurrentZone { get; set; }
            public float LastWarp { get; set; }
            public List<string> VisitedZones { get; set; } = new List<string>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            InitializeZones();
            Puts("Улучшенная система варп-зон BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableWarpZones)
            {
                timer.Every(1f, CheckPlayerZones);
                timer.Every(5f, ProcessZoneEffects);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableWarpZones)
            {
                CheckPlayerZone(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (playerCurrentZone.ContainsKey(player.userID))
            {
                playerCurrentZone.Remove(player.userID);
            }
            if (warpingPlayers.ContainsKey(player.userID))
            {
                warpingPlayers.Remove(player.userID);
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableWarpZones) return;

            var player = entity as BasePlayer;
            if (player == null) return;

            var zone = GetPlayerZone(player);
            if (zone != null && zone.Type == ZoneType.Safe)
            {
                info.damageTypes.ScaleAll(0f); // Блокируем урон в безопасной зоне
            }
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (!config.EnableWarpZones) return;

            var zone = GetPlayerZone(attacker);
            if (zone != null && zone.Type == ZoneType.Safe)
            {
                attacker.ChatMessage("<color=red>Нельзя атаковать в безопасной зоне!</color>");
                info.damageTypes.ScaleAll(0f);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableWarpZones) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var zone = GetPlayerZone(player);
            if (zone != null && zone.Type == ZoneType.NoBuild)
            {
                player.ChatMessage("<color=red>Строительство запрещено в этой зоне!</color>");
                go.Kill();
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            warpZones = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, WarpZone>>("warp_zones") ?? new Dictionary<string, WarpZone>();
            playerCurrentZone = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("player_zones") ?? new Dictionary<ulong, string>();
            lastWarpTime = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("warp_times") ?? new Dictionary<ulong, float>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("warp_zones", warpZones);
            Interface.Oxide.DataFileSystem.WriteObject("player_zones", playerCurrentZone);
            Interface.Oxide.DataFileSystem.WriteObject("warp_times", lastWarpTime);
        }

        private void InitializeZones()
        {
            foreach (var zone in config.DefaultZones)
            {
                if (!warpZones.ContainsKey(zone.Id))
                {
                    warpZones[zone.Id] = zone;
                }
            }
            SaveData();
        }

        private void CheckPlayerZones()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckPlayerZone(player);
            }
        }

        private void CheckPlayerZone(BasePlayer player)
        {
            var currentZoneId = playerCurrentZone.ContainsKey(player.userID) ? playerCurrentZone[player.userID] : null;
            var newZone = GetZoneAtPosition(player.transform.position);

            if (newZone?.Id != currentZoneId)
            {
                // Игрок покинул старую зону
                if (currentZoneId != null && warpZones.ContainsKey(currentZoneId))
                {
                    OnPlayerExitZone(player, warpZones[currentZoneId]);
                }

                // Игрок вошел в новую зону
                if (newZone != null)
                {
                    OnPlayerEnterZone(player, newZone);
                    playerCurrentZone[player.userID] = newZone.Id;
                }
                else
                {
                    playerCurrentZone.Remove(player.userID);
                }
            }
        }

        private WarpZone GetZoneAtPosition(Vector3 position)
        {
            return warpZones.Values.FirstOrDefault(z => z.IsActive && Vector3.Distance(z.Position, position) <= z.Radius);
        }

        private WarpZone GetPlayerZone(BasePlayer player)
        {
            if (!playerCurrentZone.ContainsKey(player.userID)) return null;
            var zoneId = playerCurrentZone[player.userID];
            return warpZones.ContainsKey(zoneId) ? warpZones[zoneId] : null;
        }

        private void OnPlayerEnterZone(BasePlayer player, WarpZone zone)
        {
            player.ChatMessage($"<color=green>Вы вошли в зону: {zone.Name}</color>");
            player.ChatMessage($"<color=white>{zone.Description}</color>");

            // Применяем эффекты зоны
            ApplyZoneEffects(player, zone);
        }

        private void OnPlayerExitZone(BasePlayer player, WarpZone zone)
        {
            player.ChatMessage($"<color=yellow>Вы покинули зону: {zone.Name}</color>");

            // Убираем эффекты зоны
            RemoveZoneEffects(player, zone);
        }

        private void ApplyZoneEffects(BasePlayer player, WarpZone zone)
        {
            foreach (var effect in zone.Effects)
            {
                switch (effect.Type.ToLower())
                {
                    case "heal":
                        player.Heal(effect.Value);
                        break;
                    case "god":
                        // Применяем режим бога (нужна интеграция с другими плагинами)
                        break;
                    case "radiation":
                        player.metabolism.radiation_poison.Add(effect.Value);
                        break;
                    case "speed":
                        // Увеличиваем скорость (нужна дополнительная логика)
                        break;
                }
            }
        }

        private void RemoveZoneEffects(BasePlayer player, WarpZone zone)
        {
            foreach (var effect in zone.Effects)
            {
                switch (effect.Type.ToLower())
                {
                    case "god":
                        // Убираем режим бога
                        break;
                    case "speed":
                        // Возвращаем обычную скорость
                        break;
                }
            }
        }

        private void ProcessZoneEffects()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                var zone = GetPlayerZone(player);
                if (zone != null)
                {
                    foreach (var effect in zone.Effects.Where(e => e.Duration > 0))
                    {
                        ApplyTimedEffect(player, effect);
                    }
                }
            }
        }

        private void ApplyTimedEffect(BasePlayer player, ZoneEffect effect)
        {
            switch (effect.Type.ToLower())
            {
                case "heal":
                    player.Heal(effect.Value);
                    break;
                case "radiation":
                    player.metabolism.radiation_poison.Add(effect.Value);
                    break;
            }
        }

        private void WarpToZone(BasePlayer player, string zoneId)
        {
            if (!warpZones.ContainsKey(zoneId))
            {
                player.ChatMessage($"<color=red>Зона {zoneId} не найдена</color>");
                return;
            }

            var zone = warpZones[zoneId];
            
            // Проверяем права доступа
            if (zone.RequirePermission && !string.IsNullOrEmpty(zone.Permission))
            {
                if (!permission.UserHasPermission(player.UserIDString, zone.Permission))
                {
                    player.ChatMessage("<color=red>У вас нет прав для телепортации в эту зону</color>");
                    return;
                }
            }

            // Проверяем кулдаун
            if (lastWarpTime.ContainsKey(player.userID))
            {
                var timeSinceLastWarp = Time.time - lastWarpTime[player.userID];
                if (timeSinceLastWarp < config.WarpCooldown)
                {
                    var timeLeft = config.WarpCooldown - timeSinceLastWarp;
                    player.ChatMessage($"<color=red>Телепортация доступна через {timeLeft:F0} секунд</color>");
                    return;
                }
            }

            // Проверяем стоимость
            if (zone.Cost > 0)
            {
                // Здесь должна быть проверка баланса игрока
                player.ChatMessage($"<color=yellow>Стоимость телепортации: {zone.Cost}</color>");
            }

            // Начинаем телепортацию
            StartWarp(player, zone);
        }

        private void StartWarp(BasePlayer player, WarpZone zone)
        {
            player.ChatMessage($"<color=yellow>Телепортация в {zone.Name} через {config.WarpDelay} секунд...</color>");
            warpingPlayers[player.userID] = Time.time + config.WarpDelay;

            timer.Once(config.WarpDelay, () =>
            {
                if (warpingPlayers.ContainsKey(player.userID) && player.IsConnected)
                {
                    player.Teleport(zone.Position);
                    player.ChatMessage($"<color=green>Телепортация в {zone.Name} завершена</color>");
                    lastWarpTime[player.userID] = Time.time;
                    warpingPlayers.Remove(player.userID);
                }
            });
        }

        private void CancelWarp(BasePlayer player)
        {
            if (warpingPlayers.ContainsKey(player.userID))
            {
                warpingPlayers.Remove(player.userID);
                player.ChatMessage("<color=red>Телепортация отменена</color>");
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
                ShowWarpList(player);
                return;
            }

            var zoneId = args[0].ToLower();
            WarpToZone(player, zoneId);
        }

        [ChatCommand("zone")]
        private void ZoneCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableWarpZones)
            {
                player.ChatMessage("Система варп-зон отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowZoneInfo(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "info":
                    ShowZoneInfo(player);
                    break;
                case "list":
                    ShowZoneList(player);
                    break;
                case "create":
                    if (!player.IsAdmin)
                    {
                        player.ChatMessage("Только админы могут создавать зоны");
                        return;
                    }
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /zone create <название>");
                        return;
                    }
                    CreateZoneCommand(player, args[1]);
                    break;
                case "remove":
                    if (!player.IsAdmin)
                    {
                        player.ChatMessage("Только админы могут удалять зоны");
                        return;
                    }
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /zone remove <id>");
                        return;
                    }
                    RemoveZoneCommand(player, args[1]);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowWarpList(BasePlayer player)
        {
            var availableZones = warpZones.Values.Where(z => z.IsActive && 
                (!z.RequirePermission || string.IsNullOrEmpty(z.Permission) || 
                 permission.UserHasPermission(player.UserIDString, z.Permission))).ToList();

            if (availableZones.Count == 0)
            {
                player.ChatMessage("Нет доступных зон для телепортации");
                return;
            }

            player.ChatMessage("<color=yellow>=== ДОСТУПНЫЕ ВАРП-ЗОНЫ ===</color>");
            foreach (var zone in availableZones)
            {
                var cost = zone.Cost > 0 ? $" (Стоимость: {zone.Cost})" : "";
                player.ChatMessage($"<color=cyan>/warp {zone.Id}</color> - {zone.Name}{cost}");
                player.ChatMessage($"  {zone.Description}");
            }
        }

        private void ShowZoneInfo(BasePlayer player)
        {
            var zone = GetPlayerZone(player);
            if (zone == null)
            {
                player.ChatMessage("Вы не находитесь в зоне");
                return;
            }

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О ЗОНЕ ===</color>");
            player.ChatMessage($"Название: {zone.Name}");
            player.ChatMessage($"Описание: {zone.Description}");
            player.ChatMessage($"Тип: {zone.Type}");
            player.ChatMessage($"Радиус: {zone.Radius} м");
            
            if (zone.Effects.Count > 0)
            {
                player.ChatMessage("Эффекты:");
                foreach (var effect in zone.Effects)
                {
                    player.ChatMessage($"  - {effect.Type}: {effect.Value}");
                }
            }
        }

        private void ShowZoneList(BasePlayer player)
        {
            if (warpZones.Count == 0)
            {
                player.ChatMessage("Нет созданных зон");
                return;
            }

            player.ChatMessage("<color=yellow>=== СПИСОК ВСЕХ ЗОН ===</color>");
            foreach (var zone in warpZones.Values)
            {
                var status = zone.IsActive ? "Активна" : "Неактивна";
                player.ChatMessage($"{zone.Id}: {zone.Name} - {status}");
            }
        }

        private void CreateZoneCommand(BasePlayer player, string zoneName)
        {
            var zoneId = zoneName.ToLower().Replace(" ", "_");
            
            if (warpZones.ContainsKey(zoneId))
            {
                player.ChatMessage($"Зона с ID {zoneId} уже существует");
                return;
            }

            var zone = new WarpZone
            {
                Id = zoneId,
                Name = zoneName,
                Description = "Новая зона",
                Position = player.transform.position,
                Radius = config.DefaultZoneRadius,
                Type = ZoneType.Safe,
                IsActive = true
            };

            warpZones[zoneId] = zone;
            player.ChatMessage($"<color=green>Зона {zoneName} создана</color>");
            SaveData();
        }

        private void RemoveZoneCommand(BasePlayer player, string zoneId)
        {
            if (!warpZones.ContainsKey(zoneId))
            {
                player.ChatMessage($"Зона {zoneId} не найдена");
                return;
            }

            warpZones.Remove(zoneId);
            player.ChatMessage($"<color=green>Зона {zoneId} удалена</color>");
            SaveData();
        }
        #endregion
    }
}
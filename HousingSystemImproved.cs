using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Housing System Improved", "BULBARUST", "2.0.0")]
    [Description("Улучшенная система домов с защитой для BULBARUST")]
    public class HousingSystemImproved : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableHousing { get; set; } = true;
            public bool EnableHouseProtection { get; set; } = true;
            public bool EnableHouseSharing { get; set; } = true;
            public bool EnableHouseRenting { get; set; } = true;
            public int MaxHousesPerPlayer { get; set; } = 3;
            public float HouseProtectionRadius { get; set; } = 25f;
            public float HouseMaintenanceCost { get; set; } = 100f;
            public float MaintenanceInterval { get; set; } = 86400f; // 24 часа
            public float HouseRentPrice { get; set; } = 500f;
            public List<HouseType> HouseTypes { get; set; } = new List<HouseType>();
        }

        private class HouseType
        {
            public string Name { get; set; }
            public int MaxRooms { get; set; }
            public float Cost { get; set; }
            public List<string> RequiredItems { get; set; } = new List<string>();
            public Vector3 Size { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            config.HouseTypes.AddRange(new List<HouseType>
            {
                new HouseType
                {
                    Name = "Малый дом",
                    MaxRooms = 3,
                    Cost = 5000f,
                    Size = new Vector3(10, 5, 10),
                    RequiredItems = new List<string> { "wood:5000", "stone:3000", "metal.fragments:1000" }
                },
                new HouseType
                {
                    Name = "Средний дом",
                    MaxRooms = 6,
                    Cost = 15000f,
                    Size = new Vector3(15, 8, 15),
                    RequiredItems = new List<string> { "wood:10000", "stone:8000", "metal.fragments:3000" }
                },
                new HouseType
                {
                    Name = "Большой дом",
                    MaxRooms = 10,
                    Cost = 30000f,
                    Size = new Vector3(20, 10, 20),
                    RequiredItems = new List<string> { "wood:20000", "stone:15000", "metal.fragments:5000" }
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
        private Dictionary<string, House> houses = new Dictionary<string, House>();
        private Dictionary<ulong, List<string>> playerHouses = new Dictionary<ulong, List<string>>();
        private Dictionary<ulong, float> lastMaintenancePayment = new Dictionary<ulong, float>();

        private class House
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public ulong Owner { get; set; }
            public Vector3 Position { get; set; }
            public float ProtectionRadius { get; set; }
            public string Type { get; set; }
            public bool IsProtected { get; set; } = true;
            public float LastMaintenance { get; set; }
            public List<HouseRoom> Rooms { get; set; } = new List<HouseRoom>();
            public List<ulong> SharedWith { get; set; } = new List<ulong>();
            public List<ulong> Renters { get; set; } = new List<ulong>();
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }

        private class HouseRoom
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Size { get; set; }
            public string Type { get; set; }
            public List<ulong> AccessList { get; set; } = new List<ulong>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Улучшенная система домов BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableHousing)
            {
                timer.Every(config.MaintenanceInterval, ProcessHouseMaintenance);
                timer.Every(300f, CheckHouseProtection);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableHousing)
            {
                InitializePlayerHousing(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SavePlayerHousingData(player.userID);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableHouseProtection) return null;

            var position = entity.transform.position;
            var house = GetHouseAtPosition(position);
            
            if (house != null && house.IsProtected)
            {
                var attacker = info.InitiatorPlayer;
                if (attacker != null && !CanAccessHouse(attacker.userID, house))
                {
                    attacker.ChatMessage($"<color=red>Дом {house.Name} защищен от повреждений</color>");
                    return true; // Блокируем урон
                }
            }

            return null;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableHousing) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var position = go.transform.position;
            var house = GetHouseAtPosition(position);

            if (house != null && !CanAccessHouse(player.userID, house))
            {
                player.ChatMessage("<color=red>Вы не можете строить в чужом доме</color>");
                go.Kill();
            }
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            houses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, House>>("houses") ?? new Dictionary<string, House>();
            playerHouses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("player_houses") ?? new Dictionary<ulong, List<string>>();
            lastMaintenancePayment = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("house_maintenance") ?? new Dictionary<ulong, float>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("houses", houses);
            Interface.Oxide.DataFileSystem.WriteObject("player_houses", playerHouses);
            Interface.Oxide.DataFileSystem.WriteObject("house_maintenance", lastMaintenancePayment);
        }

        private void SavePlayerHousingData(ulong playerId)
        {
            if (playerHouses.ContainsKey(playerId))
            {
                Interface.Oxide.DataFileSystem.WriteObject($"housing_data_{playerId}", playerHouses[playerId]);
            }
        }

        private void InitializePlayerHousing(BasePlayer player)
        {
            if (!playerHouses.ContainsKey(player.userID))
            {
                playerHouses[player.userID] = new List<string>();
            }
        }

        private House GetHouseAtPosition(Vector3 position)
        {
            return houses.Values.FirstOrDefault(h => Vector3.Distance(h.Position, position) <= h.ProtectionRadius);
        }

        private bool CanAccessHouse(ulong playerId, House house)
        {
            return house.Owner == playerId || 
                   house.SharedWith.Contains(playerId) || 
                   house.Renters.Contains(playerId);
        }

        private void ProcessHouseMaintenance()
        {
            var currentTime = Time.time;
            var housesToRemove = new List<string>();

            foreach (var house in houses.Values)
            {
                if (currentTime - house.LastMaintenance >= config.MaintenanceInterval)
                {
                    var owner = BasePlayer.FindByID(house.Owner);
                    if (owner != null)
                    {
                        // Здесь должна быть проверка баланса игрока и списание средств
                        house.LastMaintenance = currentTime;
                        owner.ChatMessage($"<color=yellow>Обслуживание дома {house.Name} оплачено</color>");
                    }
                    else
                    {
                        housesToRemove.Add(house.Id);
                    }
                }
            }

            foreach (var houseId in housesToRemove)
            {
                RemoveHouse(houseId);
            }
        }

        private void CheckHouseProtection()
        {
            foreach (var house in houses.Values)
            {
                if (house.IsProtected)
                {
                    // Проверяем защиту дома
                    var owner = BasePlayer.FindByID(house.Owner);
                    if (owner == null || !owner.IsConnected)
                    {
                        // Владелец не в сети, проверяем время последней активности
                    }
                }
            }
        }

        private void RemoveHouse(string houseId)
        {
            if (houses.ContainsKey(houseId))
            {
                var house = houses[houseId];
                if (playerHouses.ContainsKey(house.Owner))
                {
                    playerHouses[house.Owner].Remove(houseId);
                }
                houses.Remove(houseId);
                SaveData();
            }
        }

        private string CreateHouse(BasePlayer player, string houseType, Vector3 position)
        {
            var type = config.HouseTypes.FirstOrDefault(t => t.Name == houseType);
            if (type == null) return null;

            var houseId = Guid.NewGuid().ToString();
            var house = new House
            {
                Id = houseId,
                Name = $"Дом {player.displayName}",
                Owner = player.userID,
                Position = position,
                ProtectionRadius = config.HouseProtectionRadius,
                Type = houseType,
                IsProtected = true,
                LastMaintenance = Time.time
            };

            houses[houseId] = house;

            if (!playerHouses.ContainsKey(player.userID))
            {
                playerHouses[player.userID] = new List<string>();
            }
            playerHouses[player.userID].Add(houseId);

            SaveData();
            return houseId;
        }
        #endregion

        #region Commands
        [ChatCommand("house")]
        private void HouseCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableHousing)
            {
                player.ChatMessage("Система домов отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowHouseHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "create":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house create <тип>");
                        return;
                    }
                    CreateHouseCommand(player, args[1]);
                    break;
                case "info":
                    ShowHouseInfo(player);
                    break;
                case "share":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house share <игрок>");
                        return;
                    }
                    ShareHouseCommand(player, args[1]);
                    break;
                case "unshare":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house unshare <игрок>");
                        return;
                    }
                    UnshareHouseCommand(player, args[1]);
                    break;
                case "rent":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house rent <игрок>");
                        return;
                    }
                    RentHouseCommand(player, args[1]);
                    break;
                case "list":
                    ListPlayerHouses(player);
                    break;
                case "remove":
                    RemoveHouseCommand(player);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowHouseHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ ДОМОВ ===</color>");
            player.ChatMessage("/house create <тип> - создать дом");
            player.ChatMessage("/house info - информация о доме");
            player.ChatMessage("/house share <игрок> - поделиться домом");
            player.ChatMessage("/house unshare <игрок> - убрать доступ");
            player.ChatMessage("/house rent <игрок> - сдать в аренду");
            player.ChatMessage("/house list - мои дома");
            player.ChatMessage("/house remove - удалить дом");
        }

        private void CreateHouseCommand(BasePlayer player, string houseType)
        {
            var playerHouseCount = playerHouses.ContainsKey(player.userID) ? playerHouses[player.userID].Count : 0;
            if (playerHouseCount >= config.MaxHousesPerPlayer)
            {
                player.ChatMessage($"<color=red>Максимум домов: {config.MaxHousesPerPlayer}</color>");
                return;
            }

            var type = config.HouseTypes.FirstOrDefault(t => t.Name.ToLower() == houseType.ToLower());
            if (type == null)
            {
                player.ChatMessage("Неверный тип дома. Доступные типы:");
                foreach (var t in config.HouseTypes)
                {
                    player.ChatMessage($"- {t.Name} (Стоимость: {t.Cost})");
                }
                return;
            }

            var houseId = CreateHouse(player, type.Name, player.transform.position);
            if (houseId != null)
            {
                player.ChatMessage($"<color=green>Дом {type.Name} создан!</color>");
            }
            else
            {
                player.ChatMessage("<color=red>Ошибка создания дома</color>");
            }
        }

        private void ShowHouseInfo(BasePlayer player)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null)
            {
                player.ChatMessage("Вы не находитесь в доме");
                return;
            }

            var owner = BasePlayer.FindByID(house.Owner);
            var ownerName = owner?.displayName ?? "Неизвестно";

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О ДОМЕ ===</color>");
            player.ChatMessage($"Название: {house.Name}");
            player.ChatMessage($"Владелец: {ownerName}");
            player.ChatMessage($"Тип: {house.Type}");
            player.ChatMessage($"Защищен: {(house.IsProtected ? "Да" : "Нет")}");
            player.ChatMessage($"Комнат: {house.Rooms.Count}");
            player.ChatMessage($"Поделен с: {house.SharedWith.Count} игроками");
        }

        private void ShareHouseCommand(BasePlayer player, string targetName)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null || house.Owner != player.userID)
            {
                player.ChatMessage("Вы не владелец этого дома");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок {targetName} не найден");
                return;
            }

            if (house.SharedWith.Contains(target.userID))
            {
                player.ChatMessage($"Игрок {target.displayName} уже имеет доступ");
                return;
            }

            house.SharedWith.Add(target.userID);
            player.ChatMessage($"<color=green>Дом поделен с {target.displayName}</color>");
            target.ChatMessage($"<color=green>{player.displayName} поделился домом с вами</color>");
            SaveData();
        }

        private void UnshareHouseCommand(BasePlayer player, string targetName)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null || house.Owner != player.userID)
            {
                player.ChatMessage("Вы не владелец этого дома");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок {targetName} не найден");
                return;
            }

            if (!house.SharedWith.Contains(target.userID))
            {
                player.ChatMessage($"Игрок {target.displayName} не имеет доступа");
                return;
            }

            house.SharedWith.Remove(target.userID);
            player.ChatMessage($"<color=green>Доступ для {target.displayName} убран</color>");
            target.ChatMessage($"<color=red>{player.displayName} убрал ваш доступ к дому</color>");
            SaveData();
        }

        private void RentHouseCommand(BasePlayer player, string targetName)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null || house.Owner != player.userID)
            {
                player.ChatMessage("Вы не владелец этого дома");
                return;
            }

            var target = BasePlayer.Find(targetName);
            if (target == null)
            {
                player.ChatMessage($"Игрок {targetName} не найден");
                return;
            }

            if (house.Renters.Contains(target.userID))
            {
                player.ChatMessage($"Игрок {target.displayName} уже арендует дом");
                return;
            }

            house.Renters.Add(target.userID);
            player.ChatMessage($"<color=green>Дом сдан в аренду {target.displayName}</color>");
            target.ChatMessage($"<color=green>Вы арендуете дом у {player.displayName}</color>");
            SaveData();
        }

        private void ListPlayerHouses(BasePlayer player)
        {
            if (!playerHouses.ContainsKey(player.userID) || playerHouses[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет домов");
                return;
            }

            player.ChatMessage("<color=yellow>=== ВАШИ ДОМА ===</color>");
            foreach (var houseId in playerHouses[player.userID])
            {
                if (houses.ContainsKey(houseId))
                {
                    var house = houses[houseId];
                    player.ChatMessage($"{house.Name} - {house.Type} - {(house.IsProtected ? "Защищен" : "Не защищен")}");
                }
            }
        }

        private void RemoveHouseCommand(BasePlayer player)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null || house.Owner != player.userID)
            {
                player.ChatMessage("Вы не владелец этого дома");
                return;
            }

            RemoveHouse(house.Id);
            player.ChatMessage($"<color=green>Дом {house.Name} удален</color>");
        }
        #endregion
    }
}
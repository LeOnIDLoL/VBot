using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Housing System", "BULBARUST", "1.0.0")]
    [Description("Система домов и недвижимости для сервера BULBARUST")]
    public class HousingSystem : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableHousing { get; set; } = true;
            public bool EnableRentals { get; set; } = true;
            public bool EnablePropertySales { get; set; } = true;
            public bool EnableHouseProtection { get; set; } = true;
            public float MaxHouseSize { get; set; } = 100f;
            public float MinHouseDistance { get; set; } = 50f;
            public float HouseProtectionRadius { get; set; } = 30f;
            public float RentPricePerDay { get; set; } = 100f;
            public float PropertyTaxRate { get; set; } = 0.05f; // 5%
            public int MaxHousesPerPlayer { get; set; } = 3;
            public List<HouseType> HouseTypes { get; set; } = new List<HouseType>();
            public List<ProtectedZone> ProtectedZones { get; set; } = new List<ProtectedZone>();
        }

        private class HouseType
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public float BasePrice { get; set; }
            public float MaxSize { get; set; }
            public List<string> AllowedBlocks { get; set; } = new List<string>();
            public List<string> RequiredBlocks { get; set; } = new List<string>();
        }

        private class ProtectedZone
        {
            public string Name { get; set; }
            public Vector3 Center { get; set; }
            public float Radius { get; set; }
            public bool AllowBuilding { get; set; } = false;
            public bool AllowPvP { get; set; } = true;
            public string Owner { get; set; } = "";
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            
            // Добавляем типы домов
            config.HouseTypes.AddRange(new List<HouseType>
            {
                new HouseType
                {
                    Name = "Маленький дом",
                    Description = "Простой дом для начинающих",
                    BasePrice = 1000f,
                    MaxSize = 25f,
                    AllowedBlocks = new List<string> { "stone", "wood", "metal" },
                    RequiredBlocks = new List<string> { "foundation", "wall", "door" }
                },
                new HouseType
                {
                    Name = "Средний дом",
                    Description = "Комфортный дом для семьи",
                    BasePrice = 5000f,
                    MaxSize = 50f,
                    AllowedBlocks = new List<string> { "stone", "wood", "metal", "armored" },
                    RequiredBlocks = new List<string> { "foundation", "wall", "door", "window" }
                },
                new HouseType
                {
                    Name = "Большой дом",
                    Description = "Просторный дом для больших семей",
                    BasePrice = 15000f,
                    MaxSize = 100f,
                    AllowedBlocks = new List<string> { "stone", "wood", "metal", "armored", "hqm" },
                    RequiredBlocks = new List<string> { "foundation", "wall", "door", "window", "roof" }
                },
                new HouseType
                {
                    Name = "Особняк",
                    Description = "Роскошный особняк для VIP",
                    BasePrice = 50000f,
                    MaxSize = 200f,
                    AllowedBlocks = new List<string> { "armored", "hqm", "metal" },
                    RequiredBlocks = new List<string> { "foundation", "wall", "door", "window", "roof", "floor" }
                }
            });

            // Добавляем защищенные зоны
            config.ProtectedZones.AddRange(new List<ProtectedZone>
            {
                new ProtectedZone
                {
                    Name = "Спавн",
                    Center = new Vector3(0, 100, 0),
                    Radius = 100f,
                    AllowBuilding = false,
                    AllowPvP = false
                },
                new ProtectedZone
                {
                    Name = "Торговая зона",
                    Center = new Vector3(100, 100, 100),
                    Radius = 50f,
                    AllowBuilding = false,
                    AllowPvP = false
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
        private Dictionary<ulong, List<House>> playerHouses = new Dictionary<ulong, List<House>>();
        private Dictionary<string, House> houses = new Dictionary<string, House>();
        private Dictionary<ulong, float> lastRentPayment = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastTaxPayment = new Dictionary<ulong, float>();

        private class House
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public ulong Owner { get; set; }
            public string OwnerName { get; set; }
            public Vector3 Position { get; set; }
            public float Size { get; set; }
            public string HouseType { get; set; }
            public float Price { get; set; }
            public bool IsRented { get; set; } = false;
            public ulong? Renter { get; set; }
            public string RenterName { get; set; }
            public float RentExpiry { get; set; }
            public float Created { get; set; }
            public float LastTaxPayment { get; set; }
            public bool IsProtected { get; set; } = true;
            public List<ulong> AllowedPlayers { get; set; } = new List<ulong>();
            public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            LoadData();
            Puts("Система домов BULBARUST загружена");
        }

        void OnServerInitialized()
        {
            if (config.EnableHousing)
            {
                timer.Every(3600f, ProcessRentAndTaxes); // Каждый час
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableHousing) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var entity = go.GetComponent<BaseEntity>();
            if (entity == null) return;

            // Проверяем, можно ли строить в этой зоне
            if (!CanBuildHere(player, entity.transform.position))
            {
                player.ChatMessage("<color=red>Строительство запрещено в этой зоне!</color>");
                entity.Kill();
                return;
            }

            // Проверяем, не слишком ли близко к другому дому
            if (IsTooCloseToOtherHouse(entity.transform.position, player.userID))
            {
                player.ChatMessage($"<color=red>Слишком близко к другому дому! Минимальное расстояние: {config.MinHouseDistance}м</color>");
                entity.Kill();
                return;
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!config.EnableHouseProtection) return;

            var player = info.InitiatorPlayer;
            if (player == null) return;

            // Проверяем, не является ли это защищенным домом
            var house = GetHouseAtPosition(entity.transform.position);
            if (house != null && house.IsProtected)
            {
                if (house.Owner != player.userID && !house.AllowedPlayers.Contains(player.userID))
                {
                    player.ChatMessage("<color=red>Этот дом защищен от разрушения!</color>");
                    // Восстанавливаем здоровье блока
                    entity.health = entity.MaxHealth();
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Обрабатываем отключение игрока
        }
        #endregion

        #region Methods
        private void LoadData()
        {
            playerHouses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<House>>>("player_houses") ?? new Dictionary<ulong, List<House>>();
            houses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, House>>("houses") ?? new Dictionary<string, House>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("player_houses", playerHouses);
            Interface.Oxide.DataFileSystem.WriteObject("houses", houses);
        }

        private bool CanBuildHere(BasePlayer player, Vector3 position)
        {
            // Проверяем защищенные зоны
            foreach (var zone in config.ProtectedZones)
            {
                if (Vector3.Distance(position, zone.Center) <= zone.Radius)
                {
                    if (!zone.AllowBuilding)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsTooCloseToOtherHouse(Vector3 position, ulong playerId)
        {
            foreach (var house in houses.Values)
            {
                if (house.Owner == playerId) continue; // Игнорируем собственные дома

                if (Vector3.Distance(position, house.Position) < config.MinHouseDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private House GetHouseAtPosition(Vector3 position)
        {
            foreach (var house in houses.Values)
            {
                if (Vector3.Distance(position, house.Position) <= house.Size)
                {
                    return house;
                }
            }

            return null;
        }

        private void CreateHouse(ulong playerId, string playerName, Vector3 position, string houseTypeName)
        {
            var houseType = config.HouseTypes.FirstOrDefault(ht => ht.Name == houseTypeName);
            if (houseType == null)
            {
                return;
            }

            // Проверяем лимит домов
            if (!playerHouses.ContainsKey(playerId))
            {
                playerHouses[playerId] = new List<House>();
            }

            if (playerHouses[playerId].Count >= config.MaxHousesPerPlayer)
            {
                return;
            }

            var house = new House
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Дом {playerName}",
                Owner = playerId,
                OwnerName = playerName,
                Position = position,
                Size = houseType.MaxSize,
                HouseType = houseTypeName,
                Price = houseType.BasePrice,
                Created = Time.time,
                LastTaxPayment = Time.time
            };

            houses[house.Id] = house;
            playerHouses[playerId].Add(house);

            SaveData();
        }

        private void ProcessRentAndTaxes()
        {
            var currentTime = Time.time;

            foreach (var house in houses.Values)
            {
                // Обрабатываем аренду
                if (house.IsRented && currentTime >= house.RentExpiry)
                {
                    EndRental(house);
                }

                // Обрабатываем налоги
                if (currentTime - house.LastTaxPayment >= 86400f) // 24 часа
                {
                    ProcessTaxes(house);
                }
            }
        }

        private void EndRental(House house)
        {
            house.IsRented = false;
            house.Renter = null;
            house.RenterName = "";

            var owner = BasePlayer.FindByID(house.Owner);
            if (owner != null && owner.IsConnected)
            {
                owner.ChatMessage($"<color=yellow>Аренда дома '{house.Name}' истекла</color>");
            }

            SaveData();
        }

        private void ProcessTaxes(House house)
        {
            var taxAmount = house.Price * config.PropertyTaxRate;
            
            // Здесь должна быть логика списания налогов с игрока
            // Для примера просто обновляем время последней оплаты
            house.LastTaxPayment = Time.time;
            SaveData();

            var owner = BasePlayer.FindByID(house.Owner);
            if (owner != null && owner.IsConnected)
            {
                owner.ChatMessage($"<color=yellow>Налог на дом '{house.Name}': {taxAmount:F2}</color>");
            }
        }

        private bool IsPlayerInHouse(BasePlayer player, House house)
        {
            return Vector3.Distance(player.transform.position, house.Position) <= house.Size;
        }

        private void SetHouseProtection(House house, bool isProtected)
        {
            house.IsProtected = isProtected;
            SaveData();
        }

        private void AddAllowedPlayer(House house, ulong playerId)
        {
            if (!house.AllowedPlayers.Contains(playerId))
            {
                house.AllowedPlayers.Add(playerId);
                SaveData();
            }
        }

        private void RemoveAllowedPlayer(House house, ulong playerId)
        {
            house.AllowedPlayers.Remove(playerId);
            SaveData();
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
                        player.ChatMessage("Использование: /house create <тип_дома>");
                        return;
                    }
                    CreateHouseCommand(player, args[1]);
                    break;
                case "list":
                    ShowPlayerHouses(player);
                    break;
                case "info":
                    ShowHouseInfo(player, args.Length > 1 ? args[1] : null);
                    break;
                case "sell":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house sell <id_дома>");
                        return;
                    }
                    SellHouseCommand(player, args[1]);
                    break;
                case "rent":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house rent <id_дома>");
                        return;
                    }
                    RentHouseCommand(player, args[1]);
                    break;
                case "protect":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house protect <on|off>");
                        return;
                    }
                    SetProtectionCommand(player, args[1]);
                    break;
                case "allow":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house allow <игрок>");
                        return;
                    }
                    AllowPlayerCommand(player, args[1]);
                    break;
                case "disallow":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /house disallow <игрок>");
                        return;
                    }
                    DisallowPlayerCommand(player, args[1]);
                    break;
                case "types":
                    ShowHouseTypes(player);
                    break;
                case "near":
                    ShowNearbyHouses(player);
                    break;
            }
        }

        [ChatCommand("rent")]
        private void RentCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableRentals)
            {
                player.ChatMessage("Система аренды отключена");
                return;
            }

            if (args.Length == 0)
            {
                ShowRentHelp(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    ShowAvailableRentals(player);
                    break;
                case "end":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Использование: /rent end <id_дома>");
                        return;
                    }
                    EndRentalCommand(player, args[1]);
                    break;
            }
        }
        #endregion

        #region Command Methods
        private void ShowHouseHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ ДОМОВ ===</color>");
            player.ChatMessage("/house create <тип> - создать дом");
            player.ChatMessage("/house list - мои дома");
            player.ChatMessage("/house info [id] - информация о доме");
            player.ChatMessage("/house sell <id> - продать дом");
            player.ChatMessage("/house rent <id> - арендовать дом");
            player.ChatMessage("/house protect <on|off> - защита дома");
            player.ChatMessage("/house allow <игрок> - разрешить доступ");
            player.ChatMessage("/house disallow <игрок> - запретить доступ");
            player.ChatMessage("/house types - типы домов");
            player.ChatMessage("/house near - дома рядом");
        }

        private void ShowRentHelp(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== КОМАНДЫ АРЕНДЫ ===</color>");
            player.ChatMessage("/rent list - доступная аренда");
            player.ChatMessage("/rent end <id> - прекратить аренду");
        }

        private void CreateHouseCommand(BasePlayer player, string houseTypeName)
        {
            var houseType = config.HouseTypes.FirstOrDefault(ht => ht.Name.ToLower() == houseTypeName.ToLower());
            if (houseType == null)
            {
                player.ChatMessage($"Тип дома '{houseTypeName}' не найден");
                return;
            }

            if (!playerHouses.ContainsKey(player.userID))
            {
                playerHouses[player.userID] = new List<House>();
            }

            if (playerHouses[player.userID].Count >= config.MaxHousesPerPlayer)
            {
                player.ChatMessage($"Максимальное количество домов: {config.MaxHousesPerPlayer}");
                return;
            }

            // Проверяем, можно ли строить здесь
            if (!CanBuildHere(player, player.transform.position))
            {
                player.ChatMessage("Строительство запрещено в этой зоне");
                return;
            }

            if (IsTooCloseToOtherHouse(player.transform.position, player.userID))
            {
                player.ChatMessage($"Слишком близко к другому дому! Минимальное расстояние: {config.MinHouseDistance}м");
                return;
            }

            CreateHouse(player.userID, player.displayName, player.transform.position, houseType.Name);
            player.ChatMessage($"<color=green>Дом '{houseType.Name}' создан!</color>");
            player.ChatMessage($"Цена: {houseType.BasePrice:F2}, Размер: {houseType.MaxSize}м");
        }

        private void ShowPlayerHouses(BasePlayer player)
        {
            if (!playerHouses.ContainsKey(player.userID) || playerHouses[player.userID].Count == 0)
            {
                player.ChatMessage("У вас нет домов");
                return;
            }

            player.ChatMessage("<color=yellow>=== ВАШИ ДОМА ===</color>");
            var houses = playerHouses[player.userID];
            for (int i = 0; i < houses.Count; i++)
            {
                var house = houses[i];
                var status = house.IsRented ? $"Арендован ({house.RenterName})" : "Свободен";
                var protection = house.IsProtected ? "Защищен" : "Не защищен";
                player.ChatMessage($"{i + 1}. {house.Name} ({house.HouseType})");
                player.ChatMessage($"   ID: {house.Id}");
                player.ChatMessage($"   Статус: {status}");
                player.ChatMessage($"   Защита: {protection}");
                player.ChatMessage($"   Цена: {house.Price:F2}");
            }
        }

        private void ShowHouseInfo(BasePlayer player, string houseId = null)
        {
            House house = null;

            if (string.IsNullOrEmpty(houseId))
            {
                // Ищем ближайший дом
                house = GetHouseAtPosition(player.transform.position);
                if (house == null)
                {
                    player.ChatMessage("Рядом нет домов");
                    return;
                }
            }
            else
            {
                house = houses.Values.FirstOrDefault(h => h.Id == houseId);
                if (house == null)
                {
                    player.ChatMessage($"Дом с ID '{houseId}' не найден");
                    return;
                }
            }

            player.ChatMessage($"<color=yellow>=== ИНФОРМАЦИЯ О ДОМЕ ===</color>");
            player.ChatMessage($"Название: {house.Name}");
            player.ChatMessage($"Владелец: {house.OwnerName}");
            player.ChatMessage($"Тип: {house.HouseType}");
            player.ChatMessage($"Размер: {house.Size}м");
            player.ChatMessage($"Цена: {house.Price:F2}");
            player.ChatMessage($"Защита: {(house.IsProtected ? "Включена" : "Отключена")}");
            player.ChatMessage($"Статус: {(house.IsRented ? $"Арендован ({house.RenterName})" : "Свободен")}");
            player.ChatMessage($"Создан: {TimeSpan.FromSeconds(Time.time - house.Created).TotalDays:F1} дней назад");
        }

        private void SellHouseCommand(BasePlayer player, string houseId)
        {
            var house = houses.Values.FirstOrDefault(h => h.Id == houseId && h.Owner == player.userID);
            if (house == null)
            {
                player.ChatMessage("Дом не найден или не принадлежит вам");
                return;
            }

            if (house.IsRented)
            {
                player.ChatMessage("Нельзя продать арендованный дом");
                return;
            }

            // Удаляем дом
            houses.Remove(house.Id);
            playerHouses[player.userID].Remove(house);
            SaveData();

            player.ChatMessage($"<color=green>Дом '{house.Name}' продан за {house.Price:F2}!</color>");
        }

        private void RentHouseCommand(BasePlayer player, string houseId)
        {
            if (!config.EnableRentals)
            {
                player.ChatMessage("Аренда домов отключена");
                return;
            }

            var house = houses.Values.FirstOrDefault(h => h.Id == houseId);
            if (house == null)
            {
                player.ChatMessage("Дом не найден");
                return;
            }

            if (house.Owner == player.userID)
            {
                player.ChatMessage("Нельзя арендовать собственный дом");
                return;
            }

            if (house.IsRented)
            {
                player.ChatMessage("Дом уже арендован");
                return;
            }

            // Здесь должна быть логика проверки средств и списания
            var rentPrice = config.RentPricePerDay;
            
            house.IsRented = true;
            house.Renter = player.userID;
            house.RenterName = player.displayName;
            house.RentExpiry = Time.time + 86400f; // 24 часа
            SaveData();

            player.ChatMessage($"<color=green>Дом '{house.Name}' арендован на 24 часа за {rentPrice:F2}!</color>");
        }

        private void SetProtectionCommand(BasePlayer player, string status)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null)
            {
                player.ChatMessage("Рядом нет домов");
                return;
            }

            if (house.Owner != player.userID)
            {
                player.ChatMessage("Этот дом не принадлежит вам");
                return;
            }

            var isProtected = status.ToLower() == "on";
            SetHouseProtection(house, isProtected);

            player.ChatMessage($"<color=green>Защита дома {(isProtected ? "включена" : "отключена")}</color>");
        }

        private void AllowPlayerCommand(BasePlayer player, string targetName)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null)
            {
                player.ChatMessage("Рядом нет домов");
                return;
            }

            if (house.Owner != player.userID)
            {
                player.ChatMessage("Этот дом не принадлежит вам");
                return;
            }

            var targetPlayer = BasePlayer.Find(targetName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            AddAllowedPlayer(house, targetPlayer.userID);
            player.ChatMessage($"<color=green>{targetPlayer.displayName} получил доступ к дому</color>");
            targetPlayer.ChatMessage($"<color=green>Вы получили доступ к дому '{house.Name}'</color>");
        }

        private void DisallowPlayerCommand(BasePlayer player, string targetName)
        {
            var house = GetHouseAtPosition(player.transform.position);
            if (house == null)
            {
                player.ChatMessage("Рядом нет домов");
                return;
            }

            if (house.Owner != player.userID)
            {
                player.ChatMessage("Этот дом не принадлежит вам");
                return;
            }

            var targetPlayer = BasePlayer.Find(targetName);
            if (targetPlayer == null)
            {
                player.ChatMessage($"Игрок '{targetName}' не найден");
                return;
            }

            RemoveAllowedPlayer(house, targetPlayer.userID);
            player.ChatMessage($"<color=red>{targetPlayer.displayName} лишен доступа к дому</color>");
            targetPlayer.ChatMessage($"<color=red>Вы лишены доступа к дому '{house.Name}'</color>");
        }

        private void ShowHouseTypes(BasePlayer player)
        {
            player.ChatMessage("<color=yellow>=== ТИПЫ ДОМОВ ===</color>");
            foreach (var houseType in config.HouseTypes)
            {
                player.ChatMessage($"{houseType.Name} - {houseType.Description}");
                player.ChatMessage($"  Цена: {houseType.BasePrice:F2}");
                player.ChatMessage($"  Размер: {houseType.MaxSize}м");
            }
        }

        private void ShowNearbyHouses(BasePlayer player)
        {
            var nearbyHouses = houses.Values
                .Where(h => Vector3.Distance(player.transform.position, h.Position) <= 200f)
                .OrderBy(h => Vector3.Distance(player.transform.position, h.Position))
                .Take(10)
                .ToList();

            if (nearbyHouses.Count == 0)
            {
                player.ChatMessage("Рядом нет домов");
                return;
            }

            player.ChatMessage("<color=yellow>=== ДОМА РЯДОМ ===</color>");
            foreach (var house in nearbyHouses)
            {
                var distance = Vector3.Distance(player.transform.position, house.Position);
                var status = house.IsRented ? "Арендован" : "Свободен";
                player.ChatMessage($"{house.Name} ({house.HouseType}) - {distance:F1}м - {status}");
            }
        }

        private void ShowAvailableRentals(BasePlayer player)
        {
            var availableHouses = houses.Values
                .Where(h => !h.IsRented && h.Owner != player.userID)
                .ToList();

            if (availableHouses.Count == 0)
            {
                player.ChatMessage("Нет доступных домов для аренды");
                return;
            }

            player.ChatMessage("<color=yellow>=== ДОСТУПНАЯ АРЕНДА ===</color>");
            foreach (var house in availableHouses)
            {
                var distance = Vector3.Distance(player.transform.position, house.Position);
                player.ChatMessage($"{house.Name} ({house.HouseType}) - {distance:F1}м");
                player.ChatMessage($"  ID: {house.Id}");
                player.ChatMessage($"  Цена аренды: {config.RentPricePerDay:F2}/день");
            }
        }

        private void EndRentalCommand(BasePlayer player, string houseId)
        {
            var house = houses.Values.FirstOrDefault(h => h.Id == houseId && h.Renter == player.userID);
            if (house == null)
            {
                player.ChatMessage("Аренда не найдена или не принадлежит вам");
                return;
            }

            house.IsRented = false;
            house.Renter = null;
            house.RenterName = "";
            SaveData();

            player.ChatMessage($"<color=green>Аренда дома '{house.Name}' прекращена</color>");
        }
        #endregion
    }
}
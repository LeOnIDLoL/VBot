using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Raid Protection", "BULBARUST", "1.0.0")]
    [Description("Автоматическая защита от рейдов для сервера BULBARUST")]
    public class AutoRaidProtection : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public bool EnableAutoProtection { get; set; } = true;
            public float ProtectionRadius { get; set; } = 50f;
            public int MaxRaidAttempts { get; set; } = 3;
            public float ProtectionDuration { get; set; } = 300f; // 5 минут
            public bool NotifyAdmins { get; set; } = true;
            public bool AutoKickRaiders { get; set; } = false;
            public List<string> ProtectedZones { get; set; } = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
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
        private Dictionary<ulong, RaidData> raidData = new Dictionary<ulong, RaidData>();
        private Dictionary<ulong, float> protectionEndTime = new Dictionary<ulong, float>();

        private class RaidData
        {
            public int Attempts { get; set; } = 0;
            public float LastAttempt { get; set; } = 0f;
            public Vector3 LastPosition { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfig();
            if (config.EnableAutoProtection)
            {
                Puts("Автозащита от рейдов активирована для сервера BULBARUST");
            }
        }

        void OnServerInitialized()
        {
            if (config.EnableAutoProtection)
            {
                timer.Every(60f, CheckProtectionStatus);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!config.EnableAutoProtection) return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            var entity = go.GetComponent<BaseEntity>();
            if (entity == null) return;

            // Проверяем, является ли постройка рейдовой
            if (IsRaidStructure(entity))
            {
                CheckRaidAttempt(player, entity.transform.position);
            }
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!config.EnableAutoProtection) return;

            CheckRaidAttempt(player, entity.transform.position);
        }
        #endregion

        #region Methods
        private bool IsRaidStructure(BaseEntity entity)
        {
            // Проверяем различные типы рейдовых структур
            return entity is C4 || entity is TimedExplosive || 
                   entity is RocketLauncher || entity is GrenadeLauncher ||
                   entity.name.Contains("explosive") || entity.name.Contains("c4");
        }

        private void CheckRaidAttempt(BasePlayer player, Vector3 position)
        {
            var playerId = player.userID;
            var currentTime = Time.time;

            if (!raidData.ContainsKey(playerId))
            {
                raidData[playerId] = new RaidData();
            }

            var data = raidData[playerId];
            data.Attempts++;
            data.LastAttempt = currentTime;
            data.LastPosition = position;

            Puts($"Игрок {player.displayName} ({playerId}) совершил попытку рейда #{data.Attempts}");

            if (data.Attempts >= config.MaxRaidAttempts)
            {
                ActivateProtection(player, position);
            }
        }

        private void ActivateProtection(BasePlayer player, Vector3 position)
        {
            var playerId = player.userID;
            var protectionEnd = Time.time + config.ProtectionDuration;
            
            protectionEndTime[playerId] = protectionEnd;

            // Уведомляем игрока
            player.ChatMessage($"<color=red>ВНИМАНИЕ!</color> Активирована автозащита от рейдов на {config.ProtectionDuration} секунд!");
            
            // Уведомляем админов
            if (config.NotifyAdmins)
            {
                NotifyAdmins($"Автозащита активирована для игрока {player.displayName} в позиции {position}");
            }

            // Автокик при необходимости
            if (config.AutoKickRaiders)
            {
                timer.Once(5f, () => {
                    if (player.IsConnected)
                    {
                        player.Kick("Автозащита: превышено количество попыток рейда");
                    }
                });
            }

            // Сброс счетчика попыток
            raidData[playerId].Attempts = 0;
        }

        private void CheckProtectionStatus()
        {
            var currentTime = Time.time;
            var expiredProtections = new List<ulong>();

            foreach (var protection in protectionEndTime)
            {
                if (currentTime >= protection.Value)
                {
                    expiredProtections.Add(protection.Key);
                }
            }

            foreach (var playerId in expiredProtections)
            {
                protectionEndTime.Remove(playerId);
                var player = BasePlayer.FindByID(playerId);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage("<color=green>Автозащита от рейдов деактивирована</color>");
                }
            }
        }

        private void NotifyAdmins(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin)
                {
                    player.ChatMessage($"<color=yellow>[АВТОЗАЩИТА]</color> {message}");
                }
            }
        }

        private bool IsPlayerProtected(ulong playerId)
        {
            return protectionEndTime.ContainsKey(playerId) && Time.time < protectionEndTime[playerId];
        }
        #endregion

        #region Commands
        [ChatCommand("raidprotect")]
        private void RaidProtectCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length == 0)
            {
                player.ChatMessage("Использование: /raidprotect <on|off|status|clear>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    config.EnableAutoProtection = true;
                    SaveConfig();
                    player.ChatMessage("Автозащита от рейдов включена");
                    break;
                case "off":
                    config.EnableAutoProtection = false;
                    SaveConfig();
                    player.ChatMessage("Автозащита от рейдов выключена");
                    break;
                case "status":
                    var status = config.EnableAutoProtection ? "включена" : "выключена";
                    player.ChatMessage($"Автозащита: {status}");
                    player.ChatMessage($"Активных защит: {protectionEndTime.Count}");
                    break;
                case "clear":
                    protectionEndTime.Clear();
                    raidData.Clear();
                    player.ChatMessage("Все данные автозащиты очищены");
                    break;
            }
        }
        #endregion
    }
}
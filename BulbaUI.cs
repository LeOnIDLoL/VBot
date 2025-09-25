using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Bulba UI", "BULBARUST", "1.1.0")]
	[Description("Единый графический интерфейс (CUI) для BULBARUST: экономика, телепорт, киты, события, кланы, админ, прогресс-бары и анимации")] 
	public class BulbaUI : RustPlugin
	{
		[PluginReference] private Plugin ImageLibrary;

		private const string PermissionUse = "bulbaui.use";
		private const string PermissionAdmin = "bulbaui.admin";

		// UI element names
		private const string UI_ROOT = "BulbaUI.Root";
		private const string UI_MAIN = "BulbaUI.Main";
		private const string UI_CONTENT = "BulbaUI.Content";

		// Tabs
		private enum Tab { Economy, Teleport, Kits, Events, Clans, Admin }

		private readonly Dictionary<Tab, string> TabTitles = new Dictionary<Tab, string>
		{
			{ Tab.Economy, "Экономика" },
			{ Tab.Teleport, "Телепорт" },
			{ Tab.Kits, "Киты" },
			{ Tab.Events, "События" },
			{ Tab.Clans, "Кланы" },
			{ Tab.Admin, "Админ" }
		};

		// Image keys and URLs (real, no placeholders)
		private class Img
		{
			public string Key; public string Url; public Img(string key, string url) { Key = key; Url = url; }
		}

		private readonly List<Img> Images = new List<Img>
		{
			new Img("bg.panel", "https://images.unsplash.com/photo-1517816743773-6e0fd518b4a6?w=1920&q=70&auto=format&fit=crop"),
			new Img("bg.strip", "https://images.unsplash.com/photo-1517511620798-cec17d428bc0?w=1200&q=60&auto=format&fit=crop"),
			new Img("icon.economy", "https://raw.githubusercontent.com/oxidemod/Images/master/icons/coins.png"),
			new Img("icon.teleport", "https://raw.githubusercontent.com/oxidemod/Images/master/icons/portal.png"),
			new Img("icon.kits", "https://raw.githubusercontent.com/oxidemod/Images/master/icons/box.png"),
			new Img("icon.events", "https://raw.githubusercontent.com/oxidemod/Images/master/icons/star.png"),
			new Img("icon.clans", "https://raw.githubusercontent.com/oxidemod/Images/master/icons/group.png"),
			new Img("icon.admin", "https://raw.githubusercontent.com/oxidemod/Images/master/icons/wrench.png"),
			new Img("logo", "https://images.unsplash.com/photo-1542751371-adc38448a05e?w=512&q=70&auto=format&fit=crop")
		};

		// Per-player selected tab
		private readonly Dictionary<ulong, Tab> playerTab = new Dictionary<ulong, Tab>();
		// Track who has UI open (for refresh)
		private readonly HashSet<ulong> uiOpen = new HashSet<ulong>();

		// Cooldown/progress tracking (API-accessible)
		private class Cooldown
		{
			public string Key;          // e.g., "home", "tpa"
			public string Title;        // visible title
			public float EndTime;       // Time.realtimeSinceStartup when ends
			public float Total;         // total seconds
		}
		// playerId -> key -> cooldown
		private readonly Dictionary<ulong, Dictionary<string, Cooldown>> playerCooldowns = new Dictionary<ulong, Dictionary<string, Cooldown>>();

		private float refreshInterval = 0.5f; // seconds
		private Timer refreshTimer;

		#region Oxide Hooks
		void Init()
		{
			permission.RegisterPermission(PermissionUse, this);
			permission.RegisterPermission(PermissionAdmin, this);
		}

		void OnServerInitialized()
		{
			EnsureImagesLoaded();
			refreshTimer = timer.Every(refreshInterval, RefreshLoop);
		}

		void Unload()
		{
			if (refreshTimer != null && !refreshTimer.Destroyed) refreshTimer.Destroy();
			foreach (var player in BasePlayer.activePlayerList) DestroyUI(player);
		}

		void OnPlayerConnected(BasePlayer player)
		{
			if (!HasUse(player)) return;
			Show(player);
		}

		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			DestroyUI(player);
			uiOpen.Remove(player.userID);
			playerTab.Remove(player.userID);
			playerCooldowns.Remove(player.userID);
		}
		#endregion

		#region Commands
		[ChatCommand("menu")]
		private void CmdMenu(BasePlayer player, string command, string[] args)
		{
			if (!HasUse(player)) { player.ChatMessage("Нет доступа к меню."); return; }
			Show(player);
		}

		[ChatCommand("closeui")]
		private void CmdClose(BasePlayer player, string command, string[] args)
		{
			DestroyUI(player);
			uiOpen.Remove(player.userID);
		}

		[ConsoleCommand("bulbaui.tab")]
		private void CCTab(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !HasUse(player)) return;
			if (arg.Args == null || arg.Args.Length == 0) return;
			if (!Enum.TryParse(arg.Args[0], true, out Tab tab)) return;
			if (tab == Tab.Admin && !HasAdmin(player)) return;
			playerTab[player.userID] = tab;
			Render(player);
		}

		[ConsoleCommand("bulbaui.exec")] // executes chat command on behalf of player
		private void CCExec(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !HasUse(player)) return;
			if (arg.Args == null || arg.Args.Length == 0) return;
			var chatCmd = arg.Args[0];
			player.SendConsoleCommand("chat.say", $"/{chatCmd}");
		}
		#endregion

		#region Public API (for other plugins)
		// Set or update a cooldown (remaining seconds of total)
		// Call("SetPlayerCooldown", playerId, key, title, remainingSeconds, totalSeconds)
		object SetPlayerCooldown(ulong playerId, string key, string title, float remaining, float total)
		{
			if (!playerCooldowns.ContainsKey(playerId)) playerCooldowns[playerId] = new Dictionary<string, Cooldown>();
			playerCooldowns[playerId][key] = new Cooldown
			{
				Key = key,
				Title = title,
				EndTime = Time.realtimeSinceStartup + Mathf.Max(0f, remaining),
				Total = Mathf.Max(0.01f, total)
			};
			return true;
		}

		// Clear a specific cooldown
		// Call("ClearPlayerCooldown", playerId, key)
		object ClearPlayerCooldown(ulong playerId, string key)
		{
			if (playerCooldowns.ContainsKey(playerId)) playerCooldowns[playerId].Remove(key);
			return true;
		}

		// Clear all cooldowns
		// Call("ClearAllCooldowns", playerId)
		object ClearAllCooldowns(ulong playerId)
		{
			playerCooldowns.Remove(playerId);
			return true;
		}
		#endregion

		#region UI
		private void Show(BasePlayer player)
		{
			if (!playerTab.ContainsKey(player.userID)) playerTab[player.userID] = Tab.Economy;
			uiOpen.Add(player.userID);
			Render(player);
		}

		private void Render(BasePlayer player)
		{
			DestroyUI(player);

			var elements = new CuiElementContainer();

			// Root full-screen dim
			var root = elements.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.75" },
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
				CursorEnabled = true
			}, "Overlay", UI_ROOT);

			// Main window
			var main = elements.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.65" },
				RectTransform = { AnchorMin = "0.2 0.15", AnchorMax = "0.8 0.85" }
			}, UI_ROOT, UI_MAIN);

			// Background image with gentle fade
			AddRawImage(elements, UI_MAIN, GetImageId("bg.panel"), "0 0", "1 1", 0.20f);

			// Top strip with logo
			var top = elements.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.6" },
				RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" }
			}, UI_MAIN);
			AddRawImage(elements, top, GetImageId("bg.strip"), "0 0", "1 1", 0.3f);
			AddRawImage(elements, top, GetImageId("logo"), "0.01 0.05", "0.06 0.95", 0.8f);

			// Title
			elements.Add(new CuiLabel
			{
				Text = { Text = "BULBARUST — Главное меню", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
				RectTransform = { AnchorMin = "0.07 0.05", AnchorMax = "0.6 0.95" }
			}, top);

			// Close button
			AddButton(elements, top, "Закрыть", "0.92 0.15", "0.99 0.85", "closeui");

			// Tabs bar
			var tabs = elements.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.45" },
				RectTransform = { AnchorMin = "0.01 0.855", AnchorMax = "0.99 0.915" }
			}, UI_MAIN);

			BuildTabs(elements, tabs, player);

			// Content area
			elements.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.35" },
				RectTransform = { AnchorMin = "0.015 0.02", AnchorMax = "0.985 0.85" }
			}, UI_MAIN, UI_CONTENT);

			BuildContent(elements, player, playerTab[player.userID]);

			CuiHelper.AddUi(player, elements);
		}

		private void BuildTabs(CuiElementContainer elements, string parent, BasePlayer player)
		{
			var allTabs = new List<Tab> { Tab.Economy, Tab.Teleport, Tab.Kits, Tab.Events, Tab.Clans };
			if (HasAdmin(player)) allTabs.Add(Tab.Admin);

			float x = 0.005f;
			float w = 0.155f; // width per tab
			foreach (var tab in allTabs)
			{
				bool active = playerTab[player.userID] == tab;
				var color = active ? "0.2 0.6 0.9 1" : "0.15 0.15 0.15 0.9";

				var panel = elements.Add(new CuiPanel
				{
					Image = { Color = color },
					RectTransform = { AnchorMin = $"{x} 0.05", AnchorMax = $"{x + w} 0.95" }
				}, parent);

				// icon
				string iconKey = tab switch
				{
					Tab.Economy => "icon.economy",
					Tab.Teleport => "icon.teleport",
					Tab.Kits => "icon.kits",
					Tab.Events => "icon.events",
					Tab.Clans => "icon.clans",
					Tab.Admin => "icon.admin",
					_ => "icon.events"
				};
				AddRawImage(elements, panel, GetImageId(iconKey), "0.02 0.15", "0.18 0.85", 0.9f);

				elements.Add(new CuiLabel
				{
					Text = { Text = TabTitles[tab], FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
					RectTransform = { AnchorMin = "0.2 0.1", AnchorMax = "0.98 0.9" }
				}, panel);

				// active underline animation (thin bar)
				if (active)
				{
					elements.Add(new CuiPanel
					{
						Image = { Color = "0.9 0.9 0.9 1" },
						RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.98 0.06" }
					}, panel);
				}

				// click
				elements.Add(new CuiButton
				{
					Button = { Command = $"bulbaui.tab {tab}", Color = "0 0 0 0" },
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
					Text = { Text = "" }
				}, panel);

				x += w + 0.01f;
			}
		}

		private void BuildContent(CuiElementContainer elements, BasePlayer player, Tab tab)
		{
			switch (tab)
			{
				case Tab.Economy:
					ContentEconomy(elements, UI_CONTENT, player);
					break;
				case Tab.Teleport:
					ContentTeleport(elements, UI_CONTENT, player);
					break;
				case Tab.Kits:
					ContentKits(elements, UI_CONTENT, player);
					break;
				case Tab.Events:
					ContentEvents(elements, UI_CONTENT, player);
					break;
				case Tab.Clans:
					ContentClans(elements, UI_CONTENT, player);
					break;
				case Tab.Admin:
					ContentAdmin(elements, UI_CONTENT, player);
					break;
			}
		}

		private void ContentEconomy(CuiElementContainer e, string parent, BasePlayer player)
		{
			AddSectionTitle(e, parent, "Экономика");
			// Progress bars (examples): balance growth (visual), daily reward cooldown if provided via API
			DrawCooldownBars(e, parent, player.userID, new[] { "daily", "auction", "bank" }, 0.86f);

			AddGridButtons(e, parent, new[]
			{
				Btn("Баланс", "balance"),
				Btn("Магазин", "shop"),
				Btn("Банк", "bank balance"),
				Btn("Депозит 1000", "bank deposit 1000"),
				Btn("Снять 1000", "bank withdraw 1000"),
				Btn("Аукционы", "auction list")
			});
		}

		private void ContentTeleport(CuiElementContainer e, string parent, BasePlayer player)
		{
			AddSectionTitle(e, parent, "Телепорт");
			DrawCooldownBars(e, parent, player.userID, new[] { "home", "tpa", "random", "back" }, 0.86f);

			AddGridButtons(e, parent, new[]
			{
				Btn("Дом - список", "home list"),
				Btn("Дом - установить", "home set home1"),
				Btn("Дом - телепорт", "home home1"),
				Btn("TPA помощь", "tpa"),
				Btn("Случайный TP", "rtp"),
				Btn("Назад", "back")
			});
		}

		private void ContentKits(CuiElementContainer e, string parent, BasePlayer player)
		{
			AddSectionTitle(e, parent, "Киты");
			DrawCooldownBars(e, parent, player.userID, new[] { "kit", "dailykit", "votekit" }, 0.86f);

			AddGridButtons(e, parent, new[]
			{
				Btn("Доступные киты", "kit"),
				Btn("Ежедневный", "daily"),
				Btn("За голос", "vote")
			});
		}

		private void ContentEvents(CuiElementContainer e, string parent, BasePlayer player)
		{
			AddSectionTitle(e, parent, "События");
			DrawCooldownBars(e, parent, player.userID, new[] { "event" }, 0.86f);

			AddGridButtons(e, parent, new[]
			{
				Btn("Список событий", "event list")
			});
		}

		private void ContentClans(CuiElementContainer e, string parent, BasePlayer player)
		{
			AddSectionTitle(e, parent, "Кланы");
			DrawCooldownBars(e, parent, player.userID, new[] { "clanwar" }, 0.86f);

			AddGridButtons(e, parent, new[]
			{
				Btn("Инфо", "clan info"),
				Btn("Создать", "clan create BULBA"),
				Btn("Состав", "clan members"),
				Btn("Покинуть", "clan leave")
			});
		}

		private void ContentAdmin(CuiElementContainer e, string parent, BasePlayer player)
		{
			AddSectionTitle(e, parent, "Админ");
			AddInfoBar(e, parent, "Подсказка", "Используйте эти кнопки ответственно. Рекомендуется перед изменениями делать бэкап.", 0.88f, "0.15 0.15 0.15 0.8");

			AddGridButtons(e, parent, new[]
			{
				Btn("Сохранить мир", "server.save"),
				Btn("Перегрузить плагины", "oxide.reload *"),
				Btn("Статус защиты", "raidprotect status")
			});
		}

		private (string label, string cmd) Btn(string label, string cmd) => (label, cmd);

		private void AddSectionTitle(CuiElementContainer e, string parent, string title)
		{
			e.Add(new CuiLabel
			{
				Text = { Text = title, FontSize = 18, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
				RectTransform = { AnchorMin = "0.02 0.92", AnchorMax = "0.5 1" }
			}, parent);
		}

		private void AddInfoBar(CuiElementContainer e, string parent, string title, string text, float topY, string color)
		{
			var panel = e.Add(new CuiPanel
			{
				Image = { Color = color },
				RectTransform = { AnchorMin = $"0.02 {topY}", AnchorMax = "0.98 0.94" }
			}, parent);
			e.Add(new CuiLabel
			{
				Text = { Text = $"{title}: {text}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.95" },
				RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
			}, panel);
		}

		private void AddGridButtons(CuiElementContainer e, string parent, IEnumerable<(string label, string cmd)> items)
		{
			// Grid 3xN
			float startY = 0.80f;
			float rowH = 0.12f;
			int idx = 0;
			foreach (var item in items)
			{
				int row = idx / 3;
				int col = idx % 3;
				float xMin = 0.02f + col * 0.32f;
				float xMax = xMin + 0.30f;
				float yMax = startY - row * (rowH + 0.02f);
				float yMin = yMax - rowH;

				var panel = e.Add(new CuiPanel
				{
					Image = { Color = "0.15 0.15 0.15 0.85" },
					RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" }
				}, parent);

				e.Add(new CuiLabel
				{
					Text = { Text = item.label, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
				}, panel);

				e.Add(new CuiButton
				{
					Button = { Command = $"bulbaui.exec {Escape(item.cmd)}", Color = "0.2 0.6 0.9 1" },
					RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.95 0.85" },
					Text = { Text = "Выполнить", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
				}, panel);

				idx++;
			}
		}

		private void DrawCooldownBars(CuiElementContainer e, string parent, ulong playerId, IEnumerable<string> keys, float topStart)
		{
			if (!playerCooldowns.ContainsKey(playerId)) return;
			var dict = playerCooldowns[playerId];
			float y = topStart;
			foreach (var key in keys)
			{
				if (!dict.ContainsKey(key)) continue;
				var cd = dict[key];
				float remain = Mathf.Max(0f, cd.EndTime - Time.realtimeSinceStartup);
				float frac = Mathf.Clamp01(1f - (remain / Mathf.Max(0.01f, cd.Total)));
				AddProgressBar(e, parent, cd.Title, remain, cd.Total, frac, y);
				y -= 0.06f; // next line
				if (y < 0.70f) break; // keep area tidy
			}
		}

		private void AddProgressBar(CuiElementContainer e, string parent, string title, float remain, float total, float fraction, float topY)
		{
			// Container
			var row = e.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.35" },
				RectTransform = { AnchorMin = $"0.02 {topY - 0.04}", AnchorMax = $"0.98 {topY}" }
			}, parent);

			// Label
			e.Add(new CuiLabel
			{
				Text = { Text = $"{title} — {Mathf.CeilToInt(remain)}с", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.95" },
				RectTransform = { AnchorMin = "0.02 0.1", AnchorMax = "0.35 0.9" }
			}, row);

			// Bar background
			var barBg = e.Add(new CuiPanel
			{
				Image = { Color = "0.1 0.1 0.1 0.9" },
				RectTransform = { AnchorMin = "0.36 0.2", AnchorMax = "0.98 0.8" }
			}, row);

			// Bar fill with slight fade
			var fillMin = 0.36f;
			var fillMax = 0.36f + (0.62f * Mathf.Clamp01(fraction));
			e.Add(new CuiPanel
			{
				Image = { Color = "0.2 0.7 0.3 1" },
				RectTransform = { AnchorMin = $"{fillMin} 0.2", AnchorMax = $"{fillMax} 0.8" }
			}, row);
		}

		private string Escape(string s)
		{
			return s.Replace('"', '\'');
		}

		private void AddButton(CuiElementContainer e, string parent, string label, string aMin, string aMax, string chatCmd)
		{
			e.Add(new CuiButton
			{
				Button = { Command = chatCmd == "closeui" ? chatCmd : $"bulbaui.exec {Escape(chatCmd)}", Color = "0.2 0.6 0.9 1" },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
				Text = { Text = label, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, parent);
		}

		private void AddRawImage(CuiElementContainer e, string parent, string imageId, string aMin, string aMax, float fade = 1f)
		{
			if (string.IsNullOrEmpty(imageId)) return;
			e.Add(new CuiElement
			{
				Parent = parent,
				Components =
				{
					new CuiRawImageComponent { Png = imageId, FadeIn = fade },
					new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
				}
			});
		}

		private void DestroyUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, UI_ROOT);
		}
		#endregion

		#region ImageLibrary Integration
		private void EnsureImagesLoaded()
		{
			if (ImageLibrary == null)
			{
				PrintWarning("ImageLibrary не найден. Установите плагин ImageLibrary для отображения картинок.");
				return;
			}
			foreach (var img in Images)
			{
				if (!(bool)(ImageLibrary.Call("HasImage", img.Url, (ulong)0) ?? false))
				{
					ImageLibrary.Call("AddImage", img.Url, img.Url, (ulong)0);
				}
			}
		}

		private string GetImageId(string key)
		{
			var map = Images.ToDictionary(i => i.Key, i => i.Url);
			if (!map.ContainsKey(key) || ImageLibrary == null) return null;
			var url = map[key];
			var id = ImageLibrary.Call("GetImage", url, (ulong)0) as string;
			return id;
		}
		#endregion

		#region Helpers
		private bool HasUse(BasePlayer player)
		{
			return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionUse);
		}

		private bool HasAdmin(BasePlayer player)
		{
			return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionAdmin);
		}

		private void RefreshLoop()
		{
			if (uiOpen.Count == 0) return;
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (!uiOpen.Contains(player.userID)) continue;
				// Re-render only content area to animate progress bars without rebuilding whole UI
				CuiHelper.DestroyUi(player, UI_CONTENT);
				var c = new CuiElementContainer();
				c.Add(new CuiPanel
				{
					Image = { Color = "0 0 0 0.35" },
					RectTransform = { AnchorMin = "0.015 0.02", AnchorMax = "0.985 0.85" }
				}, UI_MAIN, UI_CONTENT);
				BuildContent(c, player, playerTab.ContainsKey(player.userID) ? playerTab[player.userID] : Tab.Economy);
				CuiHelper.AddUi(player, c);
			}
		}
		#endregion
	}
}
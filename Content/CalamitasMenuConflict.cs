using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace DieWithASmile.Content
{
	public class CalamitasMenuConflict : ModSystem
	{
		internal const string EntropyMod = "CalamityEntropy";
		internal const string EntropyTitle = "Calamity Entropy";
		internal const string OurMod = "DieWithASmile";

		private static readonly Color PanelFill = new Color(22, 20, 21) * 0.97f;
		private static readonly Color CardFill = new Color(29, 27, 28) * 0.88f;
		private static readonly Color CardFillOurs = new Color(32, 24, 28) * 0.9f;
		private static readonly Color TextMain = new Color(255, 236, 236);
		private static readonly Color TextBody = new Color(220, 205, 205);
		private static readonly Color EnabledGreen = new Color(48, 220, 96);
		private static readonly Color ButtonFill = new Color(48, 22, 28) * 0.95f;
		private static readonly Color ButtonHover = new Color(86, 32, 40) * 0.98f;
		private static readonly Color ExitFill = new Color(28, 28, 32) * 0.95f;
		private static readonly Color ExitHover = new Color(48, 48, 56) * 0.98f;

		private static MethodInfo _disableMod;
		private static MethodInfo _reload;
		private static MethodInfo _findMods;
		private static MethodInfo _deleteMod;
		private static int _loadModsId = -1;
		private const float BlackSeconds = 0.72f;
		private const float RsodSeconds = 3.15f;
		private const float UiFadeSeconds = 1.25f;

		private static bool _armed;
		private static bool _busy;
		private static bool _openedSound;
		private static bool _hovering;
		private static bool _deathAudio;
		private static bool _mouseHeld;
		private static bool _wasBlocking;
		private static bool _silencedOthers;
		private static int _lastAdvanceMs;
		private static float _intro;
		private static string _busyLabel = "";
		private static string _queuedMod;
		private static bool _queuedExit;
		private static Wisp[] _wisps;
		private static Ember[] _embers;

		private struct Wisp
		{
			public float Angle, AngleRate, Dist0, DistRate, SizeX, SizeY, Rot, Spin, Alpha, Shade, Z;
		}

		private struct Ember
		{
			public float Angle, AngleRate, Dist0, DistRate, Size, Glow, Phase, Depth, Pulse;
		}

		internal static bool Blocking => EntropyPresent();

		internal static bool OverlayActive
		{
			get
			{
				if (!Blocking)
					return false;
				if (!Main.gameMenu)
					return true;
				if (_loadModsId >= 0 && Main.menuMode == _loadModsId)
					return false;
				return true;
			}
		}

		public override void Load()
		{
			On_Main.DrawMenu += DrawMenuHook;
			On_Main.DoUpdate += DoUpdateHook;
		}

		public override void PostSetupContent()
		{
			CacheApi();
			FinishPendingDelete();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			TickState();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (!Blocking || Main.gameMenu)
				return;

			layers.Add(new LegacyGameInterfaceLayer(
				"DieWithASmile: RightsHolderConflict",
				() =>
				{
					try {
						Pump(null);
						DrawOverlay(Main.spriteBatch, inWorld: true);
					}
					catch {
					}

					return true;
				},
				InterfaceScaleType.UI));
		}

		private static string L(string key) => CalamitasMenuText.UI(key);

		private static bool EntropyPresent()
		{
			try {
				return ModLoader.HasMod(EntropyMod);
			}
			catch {
				return false;
			}
		}

		private static void CacheApi()
		{
			const BindingFlags stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			Type loader = typeof(ModLoader);
			_disableMod = loader.GetMethod("DisableMod", stat);
			_reload = loader.GetMethod("Reload", stat);
			Type organizer = loader.Assembly.GetType("Terraria.ModLoader.Core.ModOrganizer");
			if (organizer != null) {
				foreach (MethodInfo method in organizer.GetMethods(stat)) {
					if (method.Name == "FindMods" && _findMods == null)
						_findMods = method;
					if (method.Name == "DeleteMod")
						_deleteMod = method;
				}
			}
			Type iface = loader.Assembly.GetType("Terraria.ModLoader.UI.Interface");
			if (iface?.GetField("loadModsID", stat)?.GetValue(null) is int id)
				_loadModsId = id;
		}

		private static void DrawMenuHook(On_Main.orig_DrawMenu orig, Main self, GameTime time)
		{
			bool steal = OverlayActive && Main.gameMenu;
			bool savedRelease = Main.mouseLeftRelease;
			if (steal) {
				Main.blockMouse = true;
				Main.mouseLeftRelease = false;
			}

			orig(self, time);

			if (steal)
				Main.mouseLeftRelease = savedRelease;

			if (!OverlayActive || !Main.gameMenu)
				return;

			Pump(time);
			try {
				DrawOverlay(Main.spriteBatch, inWorld: false);
			}
			catch {
				TryEnd(Main.spriteBatch);
			}
		}

		private static void DoUpdateHook(On_Main.orig_DoUpdate orig, Main self, ref GameTime time)
		{
			bool steal = OverlayActive;
			bool savedRelease = Main.mouseLeftRelease;
			if (steal) {
				Main.blockMouse = true;
				Main.mouseLeftRelease = false;
				CalamitasMenuPlaylist.MuteVanillaMusic();
				CalamitasMenuPlaylist.PauseBuiltInTrack();
			}

			orig(self, ref time);
			if (steal)
				Main.mouseLeftRelease = savedRelease;

			TickState();
			if (!OverlayActive || _busy || !Main.gameMenu)
				return;

			if (_loadModsId >= 0 && Main.menuMode == _loadModsId)
				return;

			if (!CoolerMenuCompat.OnTitleLike)
				Main.menuMode = 0;
		}

		private static void TickState()
		{
			Pump(null);
		}

		private static void Pump(GameTime time)
		{
			if (!Blocking) {
				StopDeathAudio();
				_queuedMod = null;
				_queuedExit = false;
				_busy = false;
				_intro = 0f;
				_wisps = null;
				_embers = null;
				_openedSound = false;
				_silencedOthers = false;
				_mouseHeld = false;
				_wasBlocking = false;
				_armed = false;
				_lastAdvanceMs = 0;
				return;
			}

			if (!OverlayActive)
				return;

			if (!_wasBlocking) {
				_intro = 0f;
				_wisps = null;
				_embers = null;
				_openedSound = false;
				_silencedOthers = false;
				_mouseHeld = false;
				_armed = false;
				_deathAudio = false;
				_lastAdvanceMs = 0;
				CalamitasMenuCustomAudio.ForceFullMix = false;
				CalamitasMenuCustomAudio.Stop();
			}

			_wasBlocking = true;
			Main.blockMouse = true;
			AdvanceIntro(time);
			try {
				TickAudio();
			}
			catch {
			}

			try {
				TickClicks();
			}
			catch {
			}

			FlushQueue();
		}

		private static bool AdvanceIntro(GameTime time)
		{
			float dt = 1f / 60f;
			if (time != null) {
				double seconds = time.ElapsedGameTime.TotalSeconds;
				if (seconds > 0.0 && seconds <= 0.25)
					dt = (float)seconds;
			}

			int now = Environment.TickCount;
			if (_lastAdvanceMs != 0) {
				int elapsed = now - _lastAdvanceMs;
				if (elapsed < 8)
					return false;
				if (elapsed < 0 || elapsed > 250)
					elapsed = (int)(dt * 1000f);
				dt = elapsed / 1000f;
			}

			_lastAdvanceMs = now;
			_intro += Math.Clamp(dt, 1f / 120f, 0.1f);
			return true;
		}

		private static void FlushQueue()
		{
			if (_queuedExit) {
				_queuedExit = false;
				Quit();
				return;
			}

			if (string.IsNullOrEmpty(_queuedMod))
				return;

			string name = _queuedMod;
			_queuedMod = null;
			Resolve(name);
		}

		private static void DrawOverlay(SpriteBatch spriteBatch, bool inWorld)
		{
			bool ownBatch = !inWorld;
			if (ownBatch) {
				TryEnd(spriteBatch);
				spriteBatch.Begin(
					SpriteSortMode.Deferred,
					BlendState.AlphaBlend,
					SamplerState.LinearClamp,
					DepthStencilState.None,
					RasterizerState.CullCounterClockwise,
					null,
					Matrix.Identity);
			}

			try {
				DrawOverlayContents(spriteBatch, ownBatch);
			}
			finally {
				if (ownBatch)
					TryEnd(spriteBatch);
			}
		}

		private static void DrawOverlayContents(SpriteBatch spriteBatch, bool coverSpace)
		{
			_hovering = false;
			Point space = OverlaySpace(coverSpace);
			Point mouse = OverlayMouse(space, coverSpace);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(
				pixel,
				new Rectangle(-800, -800, space.X + 1600, space.Y + 1600),
				Color.Black);

			float rsod = RsodAlpha();
			if (rsod > 0.001f) {
				try {
					DrawRsod(spriteBatch, pixel, space, rsod, coverSpace);
				}
				catch {
					if (coverSpace) {
						TryEnd(spriteBatch);
						spriteBatch.Begin(
							SpriteSortMode.Deferred,
							BlendState.AlphaBlend,
							SamplerState.LinearClamp,
							DepthStencilState.None,
							RasterizerState.CullCounterClockwise,
							null,
							Matrix.Identity);
					}
				}
			}

			float ui = UiAlpha();
			if (ui <= 0.01f) {
				DrawConflictCursor(space, mouse);
				return;
			}

			Layout layout = MakeLayout(space);
			Color fade = Color.White * ui;
			DrawPanel(spriteBatch, pixel, layout.Panel, PanelFill * ui, CalamitasMenuAccent.Mid * ui);

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			int pad = 28;
			int x = layout.Panel.X + pad;
			int width = layout.Panel.Width - pad * 2;
			int y = layout.Panel.Y + 22;

			y += DrawWrapped(spriteBatch, font, L("ConflictTitle"), x, y, width, 1.08f, TextMain * ui) + 14;
			y += DrawWrapped(spriteBatch, font, L("ConflictBody"), x, y, width, 0.92f, TextBody * ui) + 18;

			DrawModCard(spriteBatch, font, pixel, new Rectangle(x, y, width, 90), ours: true, ui);
			y += 100;
			DrawModCard(spriteBatch, font, pixel, new Rectangle(x, y, width, 90), ours: false, ui);

			string entropyLabel = _busy && _busyLabel == EntropyMod ? L("ConflictWorking") : L("ConflictRemoveEntropy");
			string oursLabel = _busy && _busyLabel == OurMod ? L("ConflictWorking") : L("ConflictRemoveUs");

			DrawButton(spriteBatch, font, pixel, layout.Entropy, entropyLabel, ButtonFill, ButtonHover, mouse, ui);
			DrawButton(spriteBatch, font, pixel, layout.Ours, oursLabel, ButtonFill, ButtonHover, mouse, ui);
			DrawButton(spriteBatch, font, pixel, layout.Exit, L("ConflictExit"), ExitFill, ExitHover, mouse, ui);

			if (_hovering)
				Main.blockMouse = true;

			DrawConflictCursor(space, mouse);
		}

		private static Point OverlaySpace(bool coverSpace)
		{
			if (coverSpace)
				return CalamitasMenuDraw.CoverSize;
			return new Point(Math.Max(1, Main.screenWidth), Math.Max(1, Main.screenHeight));
		}

		private static Point OverlayMouse(Point space, bool coverSpace)
		{
			if (!coverSpace)
				return new Point(Main.mouseX, Main.mouseY);

			MouseState ms = Mouse.GetState();
			int winW = Math.Max(1, space.X);
			int winH = Math.Max(1, space.Y);
			try {
				GameWindow window = Main.instance?.Window;
				if (window != null) {
					Rectangle client = window.ClientBounds;
					if (client.Width > 1)
						winW = client.Width;
					if (client.Height > 1)
						winH = client.Height;
				}
			}
			catch {
			}

			return new Point(
				(int)Math.Round(ms.X * (space.X / (float)winW)),
				(int)Math.Round(ms.Y * (space.Y / (float)winH)));
		}

		private static void DrawConflictCursor(Point space, Point mouse)
		{
			int ox = Main.mouseX;
			int oy = Main.mouseY;
			Main.mouseX = Math.Clamp(mouse.X, 0, Math.Max(1, space.X - 1));
			Main.mouseY = Math.Clamp(mouse.Y, 0, Math.Max(1, space.Y - 1));
			try {
				Main.DrawCursor(Main.DrawThickCursor());
			}
			catch {
			}

			Main.mouseX = ox;
			Main.mouseY = oy;
		}

		private static void TryEnd(SpriteBatch spriteBatch)
		{
			try {
				spriteBatch.End();
			}
			catch {
			}
		}

		private static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color fill, Color border)
		{
			spriteBatch.Draw(pixel, rect, fill);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, 1), border * 0.35f);
		}

		private static void DrawModCard(SpriteBatch spriteBatch, DynamicSpriteFont font, Texture2D pixel, Rectangle rect, bool ours, float ui)
		{
			Color fill = (ours ? CardFillOurs : CardFill) * ui;
			Color border = (ours ? CalamitasMenuAccent.Mid : new Color(70, 70, 78)) * ui;
			DrawPanel(spriteBatch, pixel, rect, fill, border);

			ModLoader.TryGetMod(ours ? OurMod : EntropyMod, out Mod mod);
			Texture2D icon = ModIcon(mod);
			int iconPad = 8;
			int iconSize = rect.Height - iconPad * 2;
			var iconRect = new Rectangle(rect.X + iconPad, rect.Y + iconPad, iconSize, iconSize);
			spriteBatch.Draw(pixel, iconRect, Color.Black * (0.35f * ui));
			if (icon != null)
				spriteBatch.Draw(icon, iconRect, Color.White * ui);

			float textX = iconRect.Right + 16;
			string name = ours ? LanguageOr(L("ConflictOurName"), "The End Of A Calamity Menu Theme") : EntropyDisplayName();
			string version = mod?.Version?.ToString();
			if (!ours && !string.IsNullOrEmpty(version))
				name += " v" + version;

			Vector2 namePos = new(textX, rect.Y + 16);
			if (ours)
				Utils.DrawBorderString(spriteBatch, name, namePos, TextMain * ui, 0.92f);
			else
				Utils.DrawBorderString(spriteBatch, name, namePos, Color.White * ui, 0.86f);

			Utils.DrawBorderString(spriteBatch, L("ConflictEnabled"), new Vector2(textX, rect.Y + 48), EnabledGreen * ui, 0.9f);
		}

		private static string EntropyDisplayName()
		{
			try {
				if (ModLoader.TryGetMod(EntropyMod, out Mod mod) && !string.IsNullOrWhiteSpace(mod.DisplayName))
					return mod.DisplayName;
			}
			catch {
			}

			return EntropyTitle;
		}

		private static string LanguageOr(string value, string fallback) =>
			string.IsNullOrWhiteSpace(value) || value.StartsWith("Mods.", StringComparison.Ordinal) ? fallback : value;

		private static Texture2D ModIcon(Mod mod)
		{
			if (mod == null)
				return TextureAssets.MagicPixel.Value;

			try {
				var asset = mod.Assets?.Request<Texture2D>("icon", ReLogic.Content.AssetRequestMode.ImmediateLoad);
				if (asset?.Value != null && !asset.Value.IsDisposed)
					return asset.Value;
			}
			catch {
			}

			return TextureAssets.MagicPixel.Value;
		}

		private static void DrawButton(
			SpriteBatch spriteBatch,
			DynamicSpriteFont font,
			Texture2D pixel,
			Rectangle rect,
			string text,
			Color fill,
			Color hoverFill,
			Point mouse,
			float ui)
		{
			bool hover = ui > 0.85f && rect.Contains(mouse);
			if (hover)
				_hovering = true;

			Color use = (hover ? hoverFill : fill) * ui;
			Color border = (hover ? Color.Lerp(CalamitasMenuAccent.Mid, Color.White, 0.25f) : CalamitasMenuAccent.Mid * 0.7f) * ui;
			DrawPanel(spriteBatch, pixel, rect, use, border);
			DrawWrapped(spriteBatch, font, text, rect.X + 16, rect.Y + 8, rect.Width - 32, 0.86f, TextMain * ui, center: true, maxHeight: rect.Height - 12);
		}

		private static int MeasureWrapped(DynamicSpriteFont font, string text, int width, float scale)
		{
			List<string> lines = Wrap(text, font, width, scale);
			float lineH = font.LineSpacing * scale + 2f;
			return (int)Math.Ceiling(lines.Count * lineH);
		}

		private static int DrawWrapped(
			SpriteBatch spriteBatch,
			DynamicSpriteFont font,
			string text,
			int x,
			int y,
			int width,
			float scale,
			Color color,
			bool center = false,
			int maxHeight = 0)
		{
			List<string> lines = Wrap(text, font, width, scale);
			float lineH = font.LineSpacing * scale + 2f;
			float total = lines.Count * lineH;
			float startY = y;
			if (center && maxHeight > 0)
				startY = y + Math.Max(0, (maxHeight - total) * 0.5f);

			for (int i = 0; i < lines.Count; i++) {
				float drawX = x;
				if (center) {
					float w = font.MeasureString(lines[i]).X * scale;
					drawX = x + (width - w) * 0.5f;
				}

				Utils.DrawBorderString(spriteBatch, lines[i], new Vector2(drawX, startY + i * lineH), color, scale);
			}

			return (int)Math.Ceiling(total);
		}

		private static List<string> Wrap(string text, DynamicSpriteFont font, float maxWidth, float scale)
		{
			var lines = new List<string>();
			if (string.IsNullOrWhiteSpace(text))
				return lines;

			foreach (string paragraph in text.Replace("\r", "").Split('\n')) {
				if (paragraph.IndexOf(' ') >= 0)
					WrapWords(paragraph, font, maxWidth, scale, lines);
				else
					WrapChars(paragraph, font, maxWidth, scale, lines);
			}

			return lines;
		}

		private static void WrapWords(string paragraph, DynamicSpriteFont font, float maxWidth, float scale, List<string> lines)
		{
			string line = "";
			foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
				string test = line.Length == 0 ? word : line + " " + word;
				if (font.MeasureString(test).X * scale > maxWidth && line.Length > 0) {
					lines.Add(line);
					line = word;
				}
				else {
					line = test;
				}
			}

			if (line.Length > 0)
				lines.Add(line);
		}

		private static void WrapChars(string paragraph, DynamicSpriteFont font, float maxWidth, float scale, List<string> lines)
		{
			string line = "";
			foreach (char c in paragraph) {
				string test = line + c;
				if (font.MeasureString(test).X * scale > maxWidth && line.Length > 0) {
					lines.Add(line);
					line = c.ToString();
				}
				else {
					line = test;
				}
			}

			if (line.Length > 0)
				lines.Add(line);
		}

		private static float Smooth01(float t) =>
			MathHelper.Clamp(t, 0f, 1f) * MathHelper.Clamp(t, 0f, 1f) * (3f - 2f * MathHelper.Clamp(t, 0f, 1f));

		private static float RsodAlpha()
		{
			if (_intro < BlackSeconds)
				return 0f;
			return Smooth01((_intro - BlackSeconds) / 0.42f);
		}

		private static float UiAlpha()
		{
			float start = BlackSeconds + RsodSeconds;
			if (_intro < start)
				return 0f;
			return Smooth01((_intro - start) / UiFadeSeconds);
		}

		private readonly struct Layout
		{
			public readonly Rectangle Panel;
			public readonly Rectangle Entropy;
			public readonly Rectangle Ours;
			public readonly Rectangle Exit;

			public Layout(Rectangle panel, Rectangle entropy, Rectangle ours, Rectangle exit)
			{
				Panel = panel;
				Entropy = entropy;
				Ours = ours;
				Exit = exit;
			}
		}

		private static Layout MakeLayout(Point space)
		{
			int panelW = Math.Clamp((int)(Math.Min(space.X, space.Y * 1.55f) * 0.64f), 620, 1080);
			int panelH = Math.Clamp((int)(space.Y * 0.80f), 520, 840);
			var panel = new Rectangle((space.X - panelW) / 2, (space.Y - panelH) / 2, panelW, panelH);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			int pad = 28;
			int x = panel.X + pad;
			int width = panel.Width - pad * 2;
			int y = panel.Y + 22;
			y += MeasureWrapped(font, L("ConflictTitle"), width, 1.08f) + 14;
			y += MeasureWrapped(font, L("ConflictBody"), width, 0.92f) + 18;
			y += 90 + 10;
			y += 90 + 18;

			int buttonH = 58;
			int remain = panel.Bottom - pad - y;
			const int buttons = 3;
			int need = buttonH * buttons + 10 * (buttons - 1);
			if (remain < need && remain > 120)
				buttonH = Math.Max(44, (remain - 20) / buttons);

			var entropy = new Rectangle(x, y, width, buttonH);
			y += buttonH + 10;
			var ours = new Rectangle(x, y, width, buttonH);
			y += buttonH + 10;
			var exit = new Rectangle(x, y, width, buttonH);
			return new Layout(panel, entropy, ours, exit);
		}

		private static void TickClicks()
		{
			MouseState ms = Mouse.GetState();
			bool down = ms.LeftButton == ButtonState.Pressed;
			bool pressed = down && !_mouseHeld;
			_mouseHeld = down;
			if (!down)
				_armed = true;
			if (!pressed || !_armed || _busy || UiAlpha() < 0.88f)
				return;
			if (!string.IsNullOrEmpty(_queuedMod) || _queuedExit)
				return;

			try {
				Point space = OverlaySpace(Main.gameMenu);
				Point mouse = OverlayMouse(space, Main.gameMenu);
				Layout layout = MakeLayout(space);
				if (layout.Entropy.Contains(mouse))
					_queuedMod = EntropyMod;
				else if (layout.Ours.Contains(mouse))
					_queuedMod = OurMod;
				else if (layout.Exit.Contains(mouse))
					_queuedExit = true;
			}
			catch {
			}
		}

		private static void EnsureFx()
		{
			if (_wisps != null)
				return;

			var rng = new Random(3316697);
			_wisps = new Wisp[64];
			for (int i = 0; i < _wisps.Length; i++) {
				_wisps[i] = new Wisp {
					Angle = (float)(rng.NextDouble() * MathHelper.TwoPi),
					AngleRate = (float)(rng.NextDouble() - 0.5) * 0.07f,
					Dist0 = 8f + (float)rng.NextDouble() * 70f,
					DistRate = 8f + (float)rng.NextDouble() * 28f,
					SizeX = 1.1f + (float)rng.NextDouble() * 2.8f,
					SizeY = 0.35f + (float)rng.NextDouble() * 1.15f,
					Rot = (float)rng.NextDouble() * MathHelper.TwoPi,
					Spin = (float)(rng.NextDouble() - 0.5) * 0.18f,
					Alpha = 0.1f + (float)rng.NextDouble() * 0.28f,
					Shade = (float)rng.NextDouble(),
					Z = (float)rng.NextDouble()
				};
			}

			_embers = new Ember[78];
			for (int i = 0; i < _embers.Length; i++) {
				_embers[i] = new Ember {
					Angle = (float)(rng.NextDouble() * MathHelper.TwoPi),
					AngleRate = (float)(rng.NextDouble() - 0.5) * 0.11f,
					Dist0 = 6f + (float)rng.NextDouble() * 40f,
					DistRate = 0.045f + (float)rng.NextDouble() * 0.09f,
					Size = 0.22f + (float)rng.NextDouble() * 0.95f,
					Glow = 0.35f + (float)rng.NextDouble() * 0.65f,
					Phase = (float)rng.NextDouble(),
					Depth = (float)rng.NextDouble(),
					Pulse = 0.6f + (float)rng.NextDouble() * 1.8f
				};
			}
		}

		private static void DrawRsod(SpriteBatch spriteBatch, Texture2D pixel, Point space, float appear, bool ownBatch)
		{
			EnsureFx();
			float t = Math.Max(0f, _intro - BlackSeconds);
			float pulse = 0.82f + 0.18f * MathF.Sin(t * 0.95f);
			float behind = 1f - UiAlpha() * 0.38f;
			float fade = appear * behind;
			Vector2 center = new(space.X * 0.5f, space.Y * 0.5f);
			float minSide = Math.Min(space.X, space.Y);
			Texture2D shine = CalamitasMenuShine.Texture;
			if (shine == null)
				return;

			Vector2 origin = new(shine.Width * 0.5f, shine.Height * 0.5f);
			float unit = minSide / shine.Width;

			if (ownBatch) {
				TryEnd(spriteBatch);
				spriteBatch.Begin(
					SpriteSortMode.Deferred,
					BlendState.Additive,
					SamplerState.LinearClamp,
					DepthStencilState.None,
					RasterizerState.CullCounterClockwise,
					null,
					Matrix.Identity);
			}

			spriteBatch.Draw(shine, center, null, new Color(48, 0, 0) * (0.7f * fade), 0f, origin, unit * 5.2f * pulse, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, center, null, new Color(90, 4, 2) * (0.55f * fade), 0f, origin, new Vector2(unit * 3.6f, unit * 2.1f) * pulse, SpriteEffects.None, 0f);

			const int rings = 8;
			for (int i = 0; i < rings; i++) {
				float cycle = (t * 0.07f + i / (float)rings) % 1f;
				float ring = (0.18f + cycle * 2.6f) * unit;
				float ringA = MathF.Sin(cycle * MathF.PI) * (1f - cycle * 0.35f) * 0.2f * fade;
				spriteBatch.Draw(shine, center, null, new Color(78, 6, 4) * ringA, 0f, origin, new Vector2(ring * 1.35f, ring * 0.78f), SpriteEffects.None, 0f);
			}

			for (int i = 0; i < _wisps.Length; i++) {
				Wisp w = _wisps[i];
				float z = (w.Z + t * 0.032f) % 1f;
				float dist = w.Dist0 + z * z * (minSide * 0.42f);
				float ang = w.Angle + t * w.AngleRate;
				Vector2 pos = center + ang.ToRotationVector2() * dist;
				float grow = 0.45f + z * 1.85f;
				var smoke = Color.Lerp(new Color(36, 4, 4), new Color(110, 16, 10), w.Shade);
				float alpha = w.Alpha * fade * (0.35f + 0.65f * (1f - z));
				spriteBatch.Draw(
					shine,
					pos,
					null,
					smoke * alpha,
					w.Rot + t * w.Spin,
					origin,
					new Vector2(w.SizeX, w.SizeY) * grow * (minSide / 980f),
					SpriteEffects.None,
					0f);
			}

			spriteBatch.Draw(shine, center, null, new Color(150, 8, 4) * (0.72f * fade), 0f, origin, unit * 2.15f * pulse, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, center, null, new Color(210, 28, 12) * (0.5f * fade), 0f, origin, unit * 0.95f * pulse, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, center, null, new Color(255, 70, 32) * (0.38f * fade), 0f, origin, unit * 0.32f * pulse, SpriteEffects.None, 0f);

			for (int i = 0; i < _embers.Length; i++) {
				Ember e = _embers[i];
				float life = (e.Depth + t * e.DistRate) % 1.15f;
				if (life > 1f)
					continue;

				float z = life;
				float dist = e.Dist0 + z * z * (minSide * 0.58f);
				float ang = e.Angle + t * e.AngleRate + z * 0.35f;
				Vector2 pos = center + ang.ToRotationVector2() * dist;
				float beat = 0.82f + 0.18f * MathF.Sin(t * e.Pulse + e.Phase * MathHelper.TwoPi);
				float size = e.Size * (0.06f + z * z * 1.15f) * (minSide / 760f) * beat;
				float alpha = e.Glow * fade * MathF.Sin(z * MathF.PI);
				if (alpha <= 0.01f)
					continue;

				spriteBatch.Draw(shine, pos, null, new Color(90, 8, 6) * (alpha * 0.55f), 0f, origin, size * 2.8f, SpriteEffects.None, 0f);
				spriteBatch.Draw(shine, pos, null, new Color(200, 36, 18) * (alpha * 0.9f), 0f, origin, size, SpriteEffects.None, 0f);
				spriteBatch.Draw(shine, pos, null, new Color(255, 150, 90) * (alpha * 0.7f), 0f, origin, size * 0.28f, SpriteEffects.None, 0f);
			}

			if (ownBatch) {
				TryEnd(spriteBatch);
				spriteBatch.Begin(
					SpriteSortMode.Deferred,
					BlendState.AlphaBlend,
					SamplerState.LinearClamp,
					DepthStencilState.None,
					RasterizerState.CullCounterClockwise,
					null,
					Matrix.Identity);
			}

			DrawVignette(spriteBatch, shine, origin, space, fade);
		}

		private static void DrawVignette(SpriteBatch spriteBatch, Texture2D shine, Vector2 origin, Point space, float fade)
		{
			float scale = Math.Max(space.X, space.Y) / (float)shine.Width * 1.15f;
			var black = Color.Black * (0.82f * fade);
			spriteBatch.Draw(shine, new Vector2(0f, 0f), null, black, 0f, origin, scale * 0.85f, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, new Vector2(space.X, 0f), null, black, 0f, origin, scale * 0.85f, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, new Vector2(0f, space.Y), null, black, 0f, origin, scale * 0.85f, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, new Vector2(space.X, space.Y), null, black, 0f, origin, scale * 0.85f, SpriteEffects.None, 0f);
			spriteBatch.Draw(
				shine,
				new Vector2(space.X * 0.5f, -space.Y * 0.18f),
				null,
				Color.Black * (0.5f * fade),
				0f,
				origin,
				new Vector2(scale * 1.7f, scale * 0.75f),
				SpriteEffects.None,
				0f);
			spriteBatch.Draw(
				shine,
				new Vector2(space.X * 0.5f, space.Y * 1.18f),
				null,
				Color.Black * (0.5f * fade),
				0f,
				origin,
				new Vector2(scale * 1.7f, scale * 0.75f),
				SpriteEffects.None,
				0f);
		}

		private static bool IsDeathPath(string path) =>
			!string.IsNullOrEmpty(path) && path.IndexOf("RedScreenOfDeath", StringComparison.OrdinalIgnoreCase) >= 0;

		private static void TickAudio()
		{
			CalamitasMenuPlaylist.MuteVanillaMusic();
			CalamitasMenuPlaylist.PauseBuiltInTrack();
			try {
				Main.curMusic = 0;
			}
			catch {
			}

			if (!_silencedOthers) {
				_silencedOthers = true;
				try {
					SoundEngine.StopTrackedSounds();
				}
				catch {
				}
			}

			if (_intro < BlackSeconds) {
				CalamitasMenuCustomAudio.ForceFullMix = false;
				CalamitasMenuCustomAudio.Stop();
				_deathAudio = false;
				return;
			}

			CalamitasMenuCustomAudio.ForceFullMix = true;
			bool deathPlaying = IsDeathPath(CalamitasMenuCustomAudio.PlayingPath) &&
				CalamitasMenuCustomAudio.IsPlaying &&
				!CalamitasMenuCustomAudio.Finished;
			if (!deathPlaying) {
				if (!IsDeathPath(CalamitasMenuCustomAudio.PlayingPath))
					CalamitasMenuCustomAudio.Stop();
				string path = DeathTrackFile();
				if (!string.IsNullOrEmpty(path))
					CalamitasMenuCustomAudio.Play(path);
				_deathAudio = true;
			}

			if (!_openedSound && UiAlpha() > 0.05f) {
				SoundEngine.PlaySound(SoundID.MenuOpen);
				_openedSound = true;
			}

			CalamitasMenuCustomAudio.Update();
		}

		private static void StopDeathAudio()
		{
			CalamitasMenuCustomAudio.ForceFullMix = false;
			if (_deathAudio || IsDeathPath(CalamitasMenuCustomAudio.PlayingPath))
				CalamitasMenuCustomAudio.Stop();
			_deathAudio = false;
		}

		private static string DeathTrackFile()
		{
			try {
				DieWithASmileSave.EnsureFolders();
				string dest = Path.Combine(DieWithASmileSave.RootFolder, "RedScreenOfDeath.mp3");
				if (File.Exists(dest) && new FileInfo(dest).Length > 1000)
					return dest;

				Mod mod = ModLoader.GetMod(OurMod);
				byte[] data = null;
				string[] packed = {
					"EntropyDeathScreen/RedScreenOfDeath.mp3",
					"EntropyDeathScreen/05. Red Screen of Death (Longer).mp3"
				};
				if (mod != null) {
					foreach (string path in packed) {
						try {
							data = mod.GetFileBytes(path);
							if (data != null && data.Length > 1000)
								break;
						}
						catch {
							data = null;
						}
					}

					if ((data == null || data.Length < 1000) && !string.IsNullOrEmpty(mod.SourceFolder)) {
						string[] sources = {
							Path.Combine(mod.SourceFolder, "EntropyDeathScreen", "RedScreenOfDeath.mp3"),
							Path.Combine(mod.SourceFolder, "EntropyDeathScreen", "05. Red Screen of Death (Longer).mp3")
						};
						foreach (string src in sources) {
							if (!File.Exists(src))
								continue;
							File.Copy(src, dest, true);
							return dest;
						}
					}
				}

				if (data != null && data.Length > 1000) {
					File.WriteAllBytes(dest, data);
					return dest;
				}
			}
			catch {
			}

			return null;
		}

		private static void Resolve(string modName)
		{
			_busy = true;
			_busyLabel = modName;
			_armed = false;
			SoundEngine.PlaySound(SoundID.MenuTick);
			try {
				WritePending(modName);
				Disable(modName);
				try {
					TryDelete(modName);
				}
				catch {
				}

				Reload();
			}
			catch (Exception ex) {
				ModContent.GetInstance<DieWithASmile>()?.Logger.Warn("Conflict resolve failed: " + ex);
				_busy = false;
			}
		}

		private static void Quit()
		{
			_busy = true;
			SoundEngine.PlaySound(SoundID.MenuClose);
			try {
				Main.instance.Exit();
			}
			catch {
				Environment.Exit(0);
			}
		}

		private static void Disable(string name)
		{
			CacheApi();
			_disableMod?.Invoke(null, new object[] { name });
		}

		private static void Reload()
		{
			CacheApi();
			if (_reload != null) {
				_reload.Invoke(null, null);
				return;
			}

			if (_loadModsId >= 0)
				Main.menuMode = _loadModsId;
		}

		private static void TryDelete(string name)
		{
			CacheApi();
			if (_findMods == null || _deleteMod == null)
				return;

			object found = null;
			object raw = _findMods.GetParameters().Length == 0
				? _findMods.Invoke(null, null)
				: _findMods.Invoke(null, new object[] { false });
			if (raw is Array mods) {
				foreach (object local in mods) {
					if (local == null)
						continue;
					string localName = local.GetType().GetProperty("Name")?.GetValue(local) as string;
					if (localName == name) {
						found = local;
						break;
					}
				}
			}

			if (found == null)
				return;

			_deleteMod.Invoke(null, new[] { found });
		}

		private static string PendingPath =>
			Path.Combine(DieWithASmileSave.RootFolder, "pending-remove.txt");

		private static void WritePending(string name)
		{
			try {
				DieWithASmileSave.EnsureFolders();
				File.WriteAllText(PendingPath, name);
			}
			catch {
			}
		}

		private static void FinishPendingDelete()
		{
			string pending = null;
			try {
				if (File.Exists(PendingPath))
					pending = File.ReadAllText(PendingPath).Trim();
			}
			catch {
			}

			if (string.IsNullOrEmpty(pending) || pending == OurMod)
				return;

			if (ModLoader.HasMod(pending))
				return;

			try {
				TryDelete(pending);
			}
			catch {
			}

			try {
				if (File.Exists(PendingPath))
					File.Delete(PendingPath);
			}
			catch {
			}
		}
	}
}

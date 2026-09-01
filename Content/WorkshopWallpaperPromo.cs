using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace DieWithASmile.Content
{
	public class WorkshopWallpaperPromo : ModSystem
	{
		internal const string WallpaperMod = "WallpaperEngine";
		internal const string WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3792811842";

		private static readonly Color Ice = new(88, 140, 196);
		private static readonly Color IceLight = new(152, 196, 236);
		private static readonly Color IceDark = new(28, 48, 96);
		private static readonly Color PanelFill = new Color(18, 22, 38) * 0.96f;
		private static readonly Color TextMain = new(236, 244, 255);
		private static readonly Color TextBody = new(186, 210, 232);

		private static Type _hubType;
		private static FieldInfo _descriptionField;
		private static MethodInfo _findMods;
		private static Asset<Texture2D> _icon;
		private static EventInfo _openedEvent;
		private static Delegate _openedHandler;
		private static int _nextCheck;
		private static bool _wallpaperPresent;
		private static bool _hidDescription;
		private static bool _hoverSound;
		private static Rectangle _buttonHit;

		public override void Load()
		{
			if (Main.dedServ)
				return;

			try {
				_icon = ModContent.Request<Texture2D>(
					"DieWithASmile/Assets/Textures/UI/WallpaperEngineIcon",
					AssetRequestMode.ImmediateLoad);
			}
			catch {
				_icon = null;
			}

			_hubType = typeof(ModLoader).Assembly.GetType("Terraria.GameContent.UI.States.UIWorkshopHub");
			if (_hubType == null) {
				Mod.Logger.Warn("Could not find UIWorkshopHub; skipping Wallpaper Engine promo.");
				return;
			}

			_descriptionField = _hubType.GetField("_descriptionText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			CacheFindMods();

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			TryAddHook(
				_hubType.GetMethod("Draw", flags, binder: null, types: new[] { typeof(SpriteBatch) }, modifiers: null),
				DrawHook);
			TryAddHook(_hubType.GetMethod("ShowOptionDescription", flags), ShowOptionDescriptionHook);
			TryAddHook(_hubType.GetMethod("ClearOptionDescription", flags), ClearOptionDescriptionHook);

			_openedEvent = _hubType.GetEvent("OnWorkshopHubMenuOpened", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			if (_openedEvent != null) {
				_openedHandler = new Action(OnHubOpened);
				try {
					_openedEvent.AddEventHandler(null, _openedHandler);
				}
				catch {
					_openedHandler = null;
				}
			}
		}

		public override void Unload()
		{
			if (_openedEvent != null && _openedHandler != null) {
				try {
					_openedEvent.RemoveEventHandler(null, _openedHandler);
				}
				catch {
				}
			}

			_openedEvent = null;
			_openedHandler = null;
			_icon = null;
			_hubType = null;
			_descriptionField = null;
			_findMods = null;
		}

		private static void OnHubOpened()
		{
			_nextCheck = 0;
			_hoverSound = false;
		}

		internal static bool ShouldShow()
		{
			if (!Main.gameMenu)
				return false;

			int now = Environment.TickCount;
			if (_nextCheck == 0 || now - _nextCheck > 1500) {
				_nextCheck = now;
				_wallpaperPresent = WallpaperEngineInstalled();
			}

			return !_wallpaperPresent;
		}

		private static void OpenWorkshop()
		{
			SoundEngine.PlaySound(SoundID.MenuOpen);
			try {
				Utils.OpenToURL(WorkshopUrl);
			}
			catch {
			}
		}

		private static string Loc(string key, string fallback)
		{
			string value = CalamitasMenuText.UI(key);
			if (string.IsNullOrWhiteSpace(value) || value.StartsWith("Mods.", StringComparison.Ordinal))
				return fallback;
			return value;
		}

		private void TryAddHook(MethodInfo method, Delegate hook)
		{
			if (method == null) {
				Mod.Logger.Warn($"Could not hook {hook.Method.Name}: target method not found.");
				return;
			}

			MonoModHooks.Add(method, hook);
		}

		private static void CacheFindMods()
		{
			Type organizer = typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.Core.ModOrganizer");
			if (organizer == null)
				return;

			foreach (MethodInfo method in organizer.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
				if (method.Name == "FindMods") {
					_findMods = method;
					break;
				}
			}
		}

		private static void DrawHook(Action<object, SpriteBatch> orig, object self, SpriteBatch spriteBatch)
		{
			orig(self, spriteBatch);
			try {
				DrawPromo(self, spriteBatch);
			}
			catch {
			}
		}

		private static void ShowOptionDescriptionHook(Action<object, UIMouseEvent, UIElement> orig, object self, UIMouseEvent evt, UIElement listeningElement)
		{
			if (ShouldShow())
				return;

			orig(self, evt, listeningElement);
		}

		private static void ClearOptionDescriptionHook(Action<object, UIMouseEvent, UIElement> orig, object self, UIMouseEvent evt, UIElement listeningElement)
		{
			if (ShouldShow())
				return;

			orig(self, evt, listeningElement);
		}

		private static void DrawPromo(object hub, SpriteBatch spriteBatch)
		{
			bool show = ShouldShow();
			SyncDescription(hub, show);
			if (!show) {
				_hoverSound = false;
				return;
			}

			if (!TryGetFooter(hub, out Rectangle rect))
				return;

			float time = Main.GlobalTimeWrappedHourly;
			float pulse = 0.55f + 0.45f * MathF.Sin(time * 3.4f);
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			spriteBatch.Draw(pixel, rect, PanelFill);
			DrawIceFill(spriteBatch, pixel, rect, pulse);
			DrawLayout(spriteBatch, pixel, rect, time);
			DrawAnimatedBorder(spriteBatch, pixel, rect, time, pulse);
			TickInput(rect);
		}

		private static bool TryGetFooter(object hub, out Rectangle rect)
		{
			rect = default;
			if (TryRectFromElement(DescriptionBox(hub), out rect))
				return true;

			if (hub is not UIElement root)
				return false;

			UIElement found = null;
			Walk(root, element => {
				if (element is UIText)
					found = element.Parent ?? element;
			});
			return TryRectFromElement(found, out rect);
		}

		private static UIElement DescriptionBox(object hub)
		{
			if (_descriptionField?.GetValue(hub) is not UIElement text)
				return null;
			if (text.Parent != null && text.Parent != hub)
				return text.Parent;
			return text;
		}

		private static bool TryRectFromElement(UIElement element, out Rectangle rect)
		{
			rect = default;
			if (element == null)
				return false;

			CalculatedStyle dims = element.GetDimensions();
			if (dims.Width < 24f || dims.Height < 16f)
				return false;

			rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
			return true;
		}

		private static void Walk(UIElement node, Action<UIElement> visit)
		{
			if (node == null)
				return;

			visit(node);
			foreach (UIElement child in node.Children)
				Walk(child, visit);
		}

		private static void SyncDescription(object hub, bool hide)
		{
			if (_descriptionField?.GetValue(hub) is not UIText text)
				return;

			if (hide) {
				text.TextColor = Color.Transparent;
				text.ShadowColor = Color.Transparent;
				_hidDescription = true;
				return;
			}

			if (!_hidDescription)
				return;

			text.TextColor = Color.White;
			text.ShadowColor = Color.Black;
			_hidDescription = false;
		}

		private static void TickInput(Rectangle rect)
		{
			bool hover = rect.Contains(Main.mouseX, Main.mouseY);
			if (!hover) {
				_hoverSound = false;
				return;
			}

			Main.LocalPlayer.mouseInterface = true;
			if (!_hoverSound) {
				_hoverSound = true;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}

			if (Main.mouseLeft && Main.mouseLeftRelease) {
				Main.mouseLeftRelease = false;
				OpenWorkshop();
			}
		}

		private static bool WallpaperEngineInstalled()
		{
			try {
				if (ModLoader.HasMod(WallpaperMod))
					return true;
			}
			catch {
			}

			if (LocalModNamed(WallpaperMod))
				return true;

			return TmodFileExists();
		}

		private static bool TmodFileExists()
		{
			try {
				string[] roots =
				{
					ModLoader.ModPath,
					Path.Combine(Main.SavePath, "Mods")
				};
				foreach (string root in roots) {
					if (string.IsNullOrEmpty(root))
						continue;
					if (File.Exists(Path.Combine(root, WallpaperMod + ".tmod")))
						return true;
				}
			}
			catch {
			}

			return false;
		}

		private static bool LocalModNamed(string name)
		{
			try {
				if (_findMods == null)
					CacheFindMods();
				if (_findMods == null)
					return false;

				object raw = _findMods.GetParameters().Length == 0
					? _findMods.Invoke(null, null)
					: _findMods.Invoke(null, new object[] { false });
				if (raw is not Array mods)
					return false;

				foreach (object local in mods) {
					if (local == null)
						continue;
					string localName = local.GetType().GetProperty("Name")?.GetValue(local) as string;
					if (localName == name)
						return true;
				}
			}
			catch {
			}

			return false;
		}

		private static Texture2D IconOrNull()
		{
			try {
				if (_icon != null && _icon.IsLoaded && _icon.Value != null && !_icon.Value.IsDisposed)
					return _icon.Value;
			}
			catch {
			}

			return null;
		}

		private static void DrawLayout(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float time)
		{
			float pad = MathHelper.Clamp(rect.Height * 0.12f, 5f, 10f);
			int iconSize = (int)MathHelper.Clamp(rect.Height - pad * 2f, 22f, 72f);
			var iconRect = new Rectangle(rect.X + (int)pad, rect.Y + (int)((rect.Height - iconSize) * 0.5f), iconSize, iconSize);
			spriteBatch.Draw(pixel, iconRect, IceDark * 0.85f);
			Texture2D icon = IconOrNull();
			if (icon != null)
				spriteBatch.Draw(icon, iconRect, Color.White);
			else
				spriteBatch.Draw(pixel, Inflated(iconRect, -6), Ice);

			DrawNewBadge(spriteBatch, pixel, iconRect, time);

			string button = Loc("WePromoButton", "Workshop");
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float btnScale = rect.Height >= 56 ? 0.86f : 0.78f;
			Vector2 btnSize = font.MeasureString(button) * btnScale;
			int btnW = (int)MathHelper.Clamp(btnSize.X + 28f, 92f, Math.Max(92f, rect.Width * 0.26f));
			int btnH = (int)MathHelper.Clamp(rect.Height - pad * 2f, 22f, 38f);
			_buttonHit = new Rectangle(
				rect.Right - (int)pad - btnW,
				rect.Y + (rect.Height - btnH) / 2,
				btnW,
				btnH);
			bool btnHover = _buttonHit.Contains(Main.mouseX, Main.mouseY);
			Color btnFill = btnHover ? Color.Lerp(IceDark, Ice, 0.55f) : IceDark * 0.95f;
			Color btnBorder = btnHover ? Color.White : Color.Lerp(Ice, IceLight, 0.45f + 0.25f * MathF.Sin(time * 5f));
			spriteBatch.Draw(pixel, _buttonHit, btnFill);
			DrawBorder(spriteBatch, pixel, _buttonHit, btnBorder, 2);
			Vector2 btnText = new(
				_buttonHit.X + (_buttonHit.Width - btnSize.X) * 0.5f,
				_buttonHit.Y + (_buttonHit.Height - btnSize.Y) * 0.5f - 2f);
			Utils.DrawBorderString(spriteBatch, button, btnText, TextMain, btnScale);

			float textX = iconRect.Right + 12f;
			float textRight = _buttonHit.X - 12f;
			float textW = Math.Max(40f, textRight - textX);
			string title = Loc("WePromoTitle", "Wallpaper Engine");
			string body = Loc("WePromoBody", "A new menu theme with no limits and extra features — wallpapers, music, logos, and widgets.");
			float titleScale = rect.Height >= 64 ? 0.92f : 0.82f;
			float bodyScale = rect.Height >= 64 ? 0.74f : 0.66f;
			float titleY = rect.Y + pad - 1f;
			Utils.DrawBorderString(spriteBatch, title, new Vector2(textX, titleY), TextMain, titleScale);

			float bodyY = titleY + FontAssets.MouseText.Value.LineSpacing * titleScale - 2f;
			int bodyHeight = (int)(rect.Bottom - pad - bodyY);
			if (bodyHeight > 10 && textW > 20f)
				DrawWrapped(spriteBatch, body, textX, bodyY, textW, bodyScale, bodyHeight);
		}

		private static void DrawNewBadge(SpriteBatch spriteBatch, Texture2D pixel, Rectangle iconRect, float time)
		{
			string label = Loc("WePromoNew", "NEW");
			float scale = 0.62f;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * scale;
			int w = (int)size.X + 12;
			int h = (int)size.Y + 2;
			var badge = new Rectangle(iconRect.X - 2, Math.Max(iconRect.Y - 2, iconRect.Y - 4), w, h);
			float glow = 0.7f + 0.3f * MathF.Sin(time * 5.2f);
			spriteBatch.Draw(pixel, Inflated(badge, 2), Ice * (0.35f * glow));
			spriteBatch.Draw(pixel, badge, Color.Lerp(IceDark, Ice, 0.4f));
			DrawBorder(spriteBatch, pixel, badge, IceLight * glow, 1);
			Utils.DrawBorderString(
				spriteBatch,
				label,
				new Vector2(badge.X + (badge.Width - size.X) * 0.5f, badge.Y - 1f),
				Color.Lerp(Color.White, IceLight, 0.25f),
				scale);
		}

		private static void DrawIceFill(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float pulse)
		{
			var top = new Rectangle(rect.X, rect.Y, rect.Width, Math.Max(2, rect.Height / 3));
			spriteBatch.Draw(pixel, top, IceDark * (0.35f + 0.12f * pulse));
		}

		private static void DrawAnimatedBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float time, float pulse)
		{
			for (int i = 4; i >= 1; i--) {
				var glow = new Rectangle(rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2);
				float alpha = (0.08f + 0.05f * pulse) * (5 - i);
				DrawBorder(spriteBatch, pixel, glow, Ice * alpha, 1);
			}

			DrawBorder(spriteBatch, pixel, rect, Color.Lerp(Ice, IceLight, 0.35f + 0.25f * pulse), 2);
			DrawBorder(spriteBatch, pixel, Inflated(rect, -2), IceLight * (0.25f + 0.2f * pulse), 1);

			Texture2D shine = CalamitasMenuShine.Texture;
			const int comets = 2;
			for (int c = 0; c < comets; c++) {
				float head = (time * 0.28f + c * 0.5f) % 1f;
				for (int s = 0; s < 10; s++) {
					float t = (head - s * 0.012f + 1f) % 1f;
					Vector2 pos = PerimeterPoint(rect, t);
					float fade = (1f - s / 10f) * (0.85f + 0.15f * pulse);
					Color color = Color.Lerp(Ice, IceLight, 1f - s / 10f) * fade;
					if (shine != null && !shine.IsDisposed && s < 4) {
						spriteBatch.Draw(
							shine,
							pos,
							null,
							color * 0.65f,
							0f,
							new Vector2(shine.Width, shine.Height) * 0.5f,
							(0.18f - s * 0.018f) * (0.9f + 0.2f * pulse),
							SpriteEffects.None,
							0f);
					}

					int size = Math.Max(2, 5 - s / 2);
					spriteBatch.Draw(pixel, new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size), color);
				}
			}
		}

		private static Vector2 PerimeterPoint(Rectangle rect, float t)
		{
			float peri = 2f * (rect.Width + rect.Height);
			if (peri < 1f)
				return rect.Center.ToVector2();

			float d = ((t % 1f) + 1f) % 1f * peri;
			if (d < rect.Width)
				return new Vector2(rect.X + d, rect.Y);
			d -= rect.Width;
			if (d < rect.Height)
				return new Vector2(rect.Right, rect.Y + d);
			d -= rect.Height;
			if (d < rect.Width)
				return new Vector2(rect.Right - d, rect.Bottom);
			d -= rect.Width;
			return new Vector2(rect.X, rect.Bottom - d);
		}

		private static Rectangle Inflated(Rectangle rect, int amount)
		{
			return new Rectangle(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);
		}

		private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
		{
			if (rect.Width <= 0 || rect.Height <= 0)
				return;

			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private static void DrawWrapped(SpriteBatch spriteBatch, string text, float x, float y, float width, float scale, int maxHeight)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float lineH = font.LineSpacing * scale - 2f;
			int maxLines = Math.Max(1, (int)(maxHeight / Math.Max(8f, lineH)));
			List<string> lines = Wrap(text, font, width, scale);
			if (lines.Count > maxLines) {
				lines.RemoveRange(maxLines, lines.Count - maxLines);
				if (lines.Count > 0)
					lines[^1] = TrimEllipsis(lines[^1], font, width, scale);
			}

			for (int i = 0; i < lines.Count; i++)
				Utils.DrawBorderString(spriteBatch, lines[i], new Vector2(x, y + i * lineH), TextBody, scale);
		}

		private static string TrimEllipsis(string line, DynamicSpriteFont font, float width, float scale)
		{
			const string dots = "…";
			while (line.Length > 3 && font.MeasureString(line + dots).X * scale > width)
				line = line[..^1];
			return line + dots;
		}

		private static List<string> Wrap(string text, DynamicSpriteFont font, float maxWidth, float scale)
		{
			var lines = new List<string>();
			if (string.IsNullOrWhiteSpace(text))
				return lines;

			string line = "";
			foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
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
			return lines;
		}
	}
}

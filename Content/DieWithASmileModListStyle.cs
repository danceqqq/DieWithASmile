using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace DieWithASmile.Content
{
	public class DieWithASmileModListStyle : ModSystem
	{
		private static readonly Color PanelColor = new Color(29, 27, 28) * 0.72f;
		private static readonly Color PanelHoverColor = new Color(38, 34, 35) * 0.82f;
		private static Color Neon => CalamitasMenuAccent.Mid;
		private static readonly Color TextFill = new Color(255, 236, 236);

		private static Type _uiModItemType;
		private static PropertyInfo _modNameProperty;
		private static FieldInfo _modNameField;
		private static FieldInfo _displayNameCleanField;

		public override void Load()
		{
			_uiModItemType = typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.UI.UIModItem");
			if (_uiModItemType == null) {
				Mod.Logger.Warn("Could not find UIModItem; skipping mods-menu styling.");
				return;
			}

			_modNameProperty = _uiModItemType.GetProperty("ModName", BindingFlags.Public | BindingFlags.Instance);
			_modNameField = _uiModItemType.GetField("_modName", BindingFlags.NonPublic | BindingFlags.Instance);
			_displayNameCleanField = _uiModItemType.GetField("DisplayNameClean", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			TryAddHook(_uiModItemType.GetMethod("OnInitialize", flags), OnInitializeHook);
			TryAddHook(_uiModItemType.GetMethod("SetHoverColors", flags), SetHoverColorsHook);
			TryAddHook(
				_uiModItemType.GetMethod("Draw", flags, binder: null, types: new[] { typeof(SpriteBatch) }, modifiers: null),
				DrawHook);
		}

		private void TryAddHook(MethodInfo method, Delegate hook)
		{
			if (method == null) {
				Mod.Logger.Warn($"Could not hook {hook.Method.Name}: target method not found.");
				return;
			}

			MonoModHooks.Add(method, hook);
		}

		private static void OnInitializeHook(Action<object> orig, object self)
		{
			orig(self);
			if (!IsOurs(self) || self is not UIPanel panel)
				return;

			ApplyPanelColors(panel, hovered: false);
			if (_modNameField?.GetValue(self) is UIText name) {
				string text = ResolveName(self, name);
				if (!string.IsNullOrWhiteSpace(text))
					name.SetText(text);

				name.TextColor = TextFill;
				name.ShadowColor = Neon;
			}
		}

		private static void SetHoverColorsHook(Action<object, bool> orig, object self, bool hovered)
		{
			if (!IsOurs(self)) {
				orig(self, hovered);
				return;
			}

			if (self is UIPanel panel)
				ApplyPanelColors(panel, hovered);
		}

		private static void DrawHook(Action<object, SpriteBatch> orig, object self, SpriteBatch spriteBatch)
		{
			if (!IsOurs(self) || self is not UIElement element) {
				orig(self, spriteBatch);
				return;
			}

			string text = null;
			UIText name = _modNameField?.GetValue(self) as UIText;
			if (name != null) {
				text = ResolveName(self, name);
				if (!string.IsNullOrWhiteSpace(text)) {
					name.SetText(text);
					name.TextColor = Color.Transparent;
					name.ShadowColor = Color.Transparent;
				}
			}

			CalculatedStyle dims = element.GetDimensions();
			DrawNeonFrame(spriteBatch, dims, outer: true);
			orig(self, spriteBatch);
			DrawNeonFrame(spriteBatch, dims, outer: false);

			if (name != null && !string.IsNullOrWhiteSpace(text))
				DrawNeonModName(name, spriteBatch, text);
		}

		private static string ResolveName(object item, UIText name)
		{
			string text = StripVersion(name?.Text);
			if (!string.IsNullOrWhiteSpace(text) && !LooksLikeVersionOnly(text))
				return text.Trim();

			if (_displayNameCleanField?.GetValue(item) is string clean && !string.IsNullOrWhiteSpace(clean))
				return StripVersion(clean).Trim();

			string localized = Language.GetTextValue("Mods.DieWithASmile.DisplayName");
			if (!string.IsNullOrWhiteSpace(localized) && localized != "Mods.DieWithASmile.DisplayName")
				return localized;

			return "The End Of A Calamity Menu Theme";
		}

		private static string StripVersion(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return "";

			int index = text.LastIndexOf(" v", StringComparison.Ordinal);
			if (index <= 0)
				return text.Trim();

			string stripped = text[..index].Trim();
			return string.IsNullOrWhiteSpace(stripped) ? text.Trim() : stripped;
		}

		private static bool LooksLikeVersionOnly(string text)
		{
			text = text.Trim();
			return text.StartsWith("v", StringComparison.OrdinalIgnoreCase) && text.Length < 12;
		}

		private static bool IsOurs(object item)
		{
			return _modNameProperty?.GetValue(item) as string == nameof(DieWithASmile);
		}

		private static void ApplyPanelColors(UIPanel panel, bool hovered)
		{
			panel.BackgroundColor = hovered ? PanelHoverColor : PanelColor;
			panel.BorderColor = hovered ? Color.Lerp(Neon, Color.White, 0.28f) : Neon;
		}

		private static void DrawNeonFrame(SpriteBatch spriteBatch, CalculatedStyle dims, bool outer)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int start = outer ? 4 : 1;
			int end = outer ? 1 : 0;

			for (int i = start; i >= end; i--) {
				float alpha = outer ? 0.12f * (5 - i) : 0.85f;
				var rect = new Rectangle(
					(int)dims.X - i,
					(int)dims.Y - i,
					(int)dims.Width + i * 2,
					(int)dims.Height + i * 2);

				DrawBorder(spriteBatch, pixel, rect, Neon * alpha, outer ? 1 : 2);
			}
		}

		private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
		{
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private static void DrawNeonModName(UIText name, SpriteBatch spriteBatch, string text)
		{
			Vector2 pos = name.GetInnerDimensions().Position();
			Utils.DrawBorderString(spriteBatch, text, pos, TextFill);
		}
	}
}

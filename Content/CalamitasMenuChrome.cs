using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace DieWithASmile.Content
{
	public class CalamitasMenuChrome : ModSystem
	{
		private const float RightMargin = 18f;
		private const float IconGap = 20f;
		private const string CalamitasWord = "Calamitas";

		private static readonly List<Spark> _sparks = new();
		private static MethodInfo _handleNews;
		private static MethodInfo _offsetModMenu;
		private static FieldInfo _newsText;
		private static FieldInfo _newsURL;
		private static Rectangle _themeHitbox;
		private static Vector2 _calamitasPos;
		private static Vector2 _calamitasSize;
		private static float _themeScale = 1f;

		private struct Spark
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Life;
			public float MaxLife;
			public float Size;
		}

		internal static bool Active =>
			!CalamitasMenuConflict.Blocking &&
			Main.gameMenu &&
			CoolerMenuCompat.OnTitleLike &&
			(MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu || CalamitasMenuForeign.HoldingCurrent);

		public override void Load()
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			_handleNews = typeof(Main).GetMethod("HandleNews", flags);
			_offsetModMenu = typeof(MenuLoader).GetMethod("OffsetModMenu", flags);
			_newsText = typeof(Main).GetField("newsText", flags);
			_newsURL = typeof(Main).GetField("newsURL", flags);

			TryAddHook(typeof(Main).GetMethod("DrawSocialMediaButtons", flags), DrawTerrariaSocialHook);
			TryAddHook(typeof(Main).GetMethod("DrawtModLoaderSocialMediaButtons", flags), DrawTmlSocialHook);
			TryAddHook(typeof(Main).GetMethod("DrawVersionNumber", flags), DrawVersionNumberHook);

			MethodInfo themeInner = typeof(MenuLoader).GetMethod("UpdateAndDrawModMenuInner", flags);
			if (themeInner != null)
				MonoModHooks.Modify(themeInner, PatchThemeSwap);

			if (_offsetModMenu != null)
				MonoModHooks.Add(_offsetModMenu, OffsetModMenuHook);

			if (_handleNews != null)
				MonoModHooks.Modify(_handleNews, PatchNewsPosition);
		}

		private void TryAddHook(MethodInfo method, Delegate hook)
		{
			if (method == null) {
				Mod.Logger.Warn($"Could not hook {hook.Method.Name}.");
				return;
			}

			MonoModHooks.Add(method, hook);
		}

		private static void DrawTerrariaSocialHook(Action<Color, float> orig, Color color, float upBump)
		{
			if (!Active) {
				orig(color, upBump);
				return;
			}

			if (CalamitasMenuLayout.Editing)
				return;

			DrawIconRow(Main.TitleLinks, GetTerrariaIconAnchor());
		}

		private static void DrawTmlSocialHook(Action<Color, float> orig, Color color, float upBump)
		{
			if (!Active) {
				orig(color, upBump);
				return;
			}

			if (CalamitasMenuLayout.Editing)
				return;

			DrawIconRow(Main.tModLoaderTitleLinks, GetTmlIconAnchor());
		}

		private static void DrawVersionNumberHook(Action<Color, float> orig, Color color, float upBump)
		{
			if (Active && CalamitasMenuLayout.Editing)
				return;

			if (!Active) {
				if (CoolerMenuCompat.CoreActive && MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu)
					return;

				orig(color, upBump);
				return;
			}

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Color textColor = new(
				(byte)((255 + color.R) / 2),
				(byte)((255 + color.R) / 2),
				(byte)((255 + color.R) / 2),
				(byte)(color.A * 0.85f));

			DrawRightText(font, Terraria.ModLoader.ModLoader.versionedName, GetTmlVersionPos(font), textColor, 1f);
			DrawRightText(font, "Terraria " + Main.versionNumber, GetTerrariaVersionPos(font), textColor, 1f);
			DrawSwitchVersion(font, textColor);

			if (CoolerMenuCompat.CoreActive) {
				DrawIconRow(Main.tModLoaderTitleLinks, GetTmlIconAnchor());
				DrawIconRow(Main.TitleLinks, GetTerrariaIconAnchor());
				DrawNews(textColor);
				DrawThemeSwapManual();
			}
			else {
				_handleNews?.Invoke(null, new object[] { color });
			}
		}

		private static void DrawIconRow(List<TitleLinkButton> links, Vector2 anchor)
		{
			if (links == null)
				return;

			Vector2 pos = new((int)MathF.Round(anchor.X), (int)MathF.Round(anchor.Y));
			for (int i = 0; i < links.Count; i++) {
				links[i].Draw(Main.spriteBatch, pos);
				pos.X += 30f;
			}
		}

		private static void DrawRightText(DynamicSpriteFont font, string text, Vector2 bottomRight, Color color, float scale)
		{
			Vector2 size = font.MeasureString(text) * scale;
			Vector2 pos = new(bottomRight.X - size.X, bottomRight.Y - size.Y);
			ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, text, pos, color, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static void DrawNews(Color color)
		{
			string news = _newsText?.GetValue(null) as string ?? "";
			string text = Language.GetTextValue("tModLoader.LatestNews", news);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			const float scale = 1.2f;
			Vector2 size = ChatManager.GetStringSize(font, text, new Vector2(scale));
			Vector2 bottomRight = new(Main.screenWidth - RightMargin, GetNewsBottomY());
			var hit = new Rectangle(
				(int)(bottomRight.X - size.X),
				(int)(bottomRight.Y - size.Y),
				(int)size.X,
				(int)size.Y);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			string url = _newsURL?.GetValue(null) as string;
			Color drawColor = hover && !string.IsNullOrEmpty(url) ? Main.highVersionColor : color;
			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				font,
				text,
				new Vector2(hit.X, hit.Y),
				drawColor,
				0f,
				Vector2.Zero,
				new Vector2(scale));

			if (hover && Main.mouseLeftRelease && Main.mouseLeft && !string.IsNullOrEmpty(url)) {
				SoundEngine.PlaySound(SoundID.MenuOpen);
				Utils.OpenToURL(url);
			}
		}

		private static void DrawThemeSwapManual()
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string text = GetThemeText();
			Vector2 size = ChatManager.GetStringSize(font, text, Vector2.One);
			_themeScale = MathHelper.Min(1f, Main.screenWidth * 0.42f / Math.Max(size.X, 1f));
			size *= _themeScale;
			_themeHitbox = new Rectangle(
				(int)(Main.screenWidth - RightMargin - size.X),
				(int)(Main.screenHeight - 8f - size.Y),
				(int)size.X,
				(int)size.Y);

			bool hover = _themeHitbox.Contains(Main.mouseX, Main.mouseY) && !Main.alreadyGrabbingSunOrMoon;
			Color color = hover ? CalamitasMenuButtonSystem.GetAnimatedHoverColor() : new Color(120, 120, 120, 76);
			DrawThemedLabel(Main.spriteBatch, font, text, new Vector2(_themeHitbox.X, _themeHitbox.Y), color, _themeScale);

			if (!hover)
				return;

			if (CalamitasMenuLayout.ShouldBlockThemeSwap)
				return;

			if (Main.mouseLeftRelease && Main.mouseLeft) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				_offsetModMenu?.Invoke(null, new object[] { 1 });
			}
			else if (Main.mouseRightRelease && Main.mouseRight) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				_offsetModMenu?.Invoke(null, new object[] { -1 });
			}
		}

		private static void DrawSwitchVersion(DynamicSpriteFont font, Color color)
		{
			string text = Language.GetTextValue("tModLoader.SwitchVersionInfoButton");
			Vector2 size = font.MeasureString(text);
			Vector2 pos = GetSwitchVersionBottomRight();
			const float scale = 1.2f;
			var hit = new Rectangle(
				(int)(pos.X - size.X * scale),
				(int)(pos.Y - size.Y * scale),
				(int)(size.X * scale),
				(int)(size.Y * scale));
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			Color drawColor = hover ? Main.highVersionColor : color;
			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				font,
				text,
				new Vector2(hit.X, hit.Y),
				drawColor,
				0f,
				Vector2.Zero,
				new Vector2(scale));

			if (hover && Main.mouseLeftRelease && Main.mouseLeft) {
				SoundEngine.PlaySound(SoundID.MenuOpen);
				Type steamed = typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.Engine.SteamedWraps");
				bool steam = steamed?.GetProperty("SteamClient", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is true;
				if (steam)
					Main.menuMode = 10029;
				else
					Utils.OpenToURL("https://github.com/tModLoader/tModLoader/wiki/tModLoader-guide-for-players#beta-branches");
			}
		}

		private static Vector2 GetTmlVersionPos(DynamicSpriteFont font)
		{
			float y = GetStackTop(font) + font.MeasureString(Terraria.ModLoader.ModLoader.versionedName).Y;
			return new Vector2(GetLineRight(Main.tModLoaderTitleLinks), y);
		}

		private static Vector2 GetTerrariaVersionPos(DynamicSpriteFont font)
		{
			float y = GetTmlVersionPos(font).Y + 4f + Math.Max(font.MeasureString("Terraria " + Main.versionNumber).Y, 26f);
			return new Vector2(GetLineRight(Main.TitleLinks), y);
		}

		private static Vector2 GetTmlIconAnchor()
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Vector2 versionBottomRight = GetTmlVersionPos(font);
			return new Vector2(GetIconRowLeft(Main.tModLoaderTitleLinks), versionBottomRight.Y - 22f);
		}

		private static Vector2 GetTerrariaIconAnchor()
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Vector2 versionBottomRight = GetTerrariaVersionPos(font);
			return new Vector2(GetIconRowLeft(Main.TitleLinks), versionBottomRight.Y - 22f);
		}

		private static float GetIconRowLeft(List<TitleLinkButton> links)
		{
			float icons = (links?.Count ?? 0) * 30f;
			return Main.screenWidth - RightMargin - icons;
		}

		private static float GetLineRight(List<TitleLinkButton> links)
		{
			return GetIconRowLeft(links) - IconGap;
		}

		private static float GetStackTop(DynamicSpriteFont font)
		{
			string theme = GetThemeText();
			Vector2 themeSize = ChatManager.GetStringSize(font, theme, Vector2.One);
			_themeScale = MathHelper.Min(1f, Main.screenWidth * 0.42f / Math.Max(themeSize.X, 1f));
			themeSize *= _themeScale;

			string news = Language.GetTextValue("tModLoader.LatestNews", _newsText?.GetValue(null) as string ?? "");
			Vector2 newsSize = ChatManager.GetStringSize(font, news, new Vector2(1.2f));
			Vector2 switchSize = font.MeasureString(Language.GetTextValue("tModLoader.SwitchVersionInfoButton")) * 1.2f;
			Vector2 terrariaSize = font.MeasureString("Terraria " + Main.versionNumber);
			Vector2 tmlSize = font.MeasureString(Terraria.ModLoader.ModLoader.versionedName);

			float y = Main.screenHeight - 8f;
			y -= themeSize.Y + 6f;
			if (CoolerMenuCompat.ModEnabled)
				y -= GetCoreThemeSize(font).Y + 6f;
			y -= switchSize.Y + 4f;
			y -= newsSize.Y + 8f;
			y -= Math.Max(terrariaSize.Y, 26f) + 4f;
			y -= Math.Max(tmlSize.Y, 26f);
			return y;
		}

		private static Vector2 GetSwitchVersionBottomRight()
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string theme = GetThemeText();
			Vector2 themeSize = ChatManager.GetStringSize(font, theme, Vector2.One);
			_themeScale = MathHelper.Min(1f, Main.screenWidth * 0.42f / Math.Max(themeSize.X, 1f));
			themeSize *= _themeScale;
			float y = Main.screenHeight - 8f - themeSize.Y - 6f;
			if (CoolerMenuCompat.ModEnabled)
				y -= GetCoreThemeSize(font).Y + 6f;
			return new Vector2(Main.screenWidth - RightMargin, y);
		}

		internal static Rectangle GetCoreThemeHitbox(Vector2 size)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string theme = GetThemeText();
			Vector2 themeSize = ChatManager.GetStringSize(font, theme, Vector2.One);
			_themeScale = MathHelper.Min(1f, Main.screenWidth * 0.42f / Math.Max(themeSize.X, 1f));
			themeSize *= _themeScale;
			return new Rectangle(
				(int)(Main.screenWidth - RightMargin - size.X),
				(int)(Main.screenHeight - 8f - themeSize.Y - 6f - size.Y),
				(int)size.X,
				(int)size.Y);
		}

		private static Vector2 GetCoreThemeSize(DynamicSpriteFont font)
		{
			return ChatManager.GetStringSize(font, CoolerMenuCompat.GetCoreThemeText(), Vector2.One);
		}

		private static float GetNewsBottomY()
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Vector2 switchSize = font.MeasureString(Language.GetTextValue("tModLoader.SwitchVersionInfoButton")) * 1.2f;
			return GetSwitchVersionBottomRight().Y - switchSize.Y - 4f;
		}

		internal static int ThemeSwapReserve
		{
			get
			{
				DynamicSpriteFont font = FontAssets.MouseText.Value;
				string text = GetThemeText();
				Vector2 size = ChatManager.GetStringSize(font, text, Vector2.One);
				float scale = MathHelper.Min(1f, Main.screenWidth * 0.42f / Math.Max(size.X, 1f));
				return (int)(size.Y * scale) + 10;
			}
		}

		private static void OffsetModMenuHook(Action<int> orig, int offset)
		{
			if (CalamitasMenuLayout.ShouldBlockThemeSwap)
				return;

			orig(offset);
		}

		private static string GetThemeText()
		{
			return Language.GetTextValue("tModLoader.ModMenuSwap") + ": " + (MenuLoader.CurrentMenu?.DisplayName ?? "");
		}

		private static void PatchNewsPosition(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchLdcR4(38f)))
				return;

			cursor.Remove();
			cursor.EmitDelegate<Func<float>>(() => Active ? Main.screenHeight - GetNewsBottomY() : 38f);
		}

		private static void PatchThemeSwap(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			if (!cursor.TryGotoNext(MoveType.After, i => i.MatchNewobj<Rectangle>(), i => i.MatchStloc(out _)))
				return;

			if (cursor.Prev.MatchStloc(out int rectIndex)) {
				cursor.Emit(OpCodes.Ldloca, il.Body.Variables[rectIndex]);
				cursor.EmitDelegate<AdjustRect>(AdjustThemeRect);
			}

			MethodInfo draw = typeof(ChatManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(m =>
					m.Name == "DrawColorCodedStringWithShadow" &&
					m.GetParameters().Length == 10 &&
					m.GetParameters()[2].ParameterType == typeof(string));
			if (draw == null || !cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(draw)))
				return;

			cursor.Remove();
			cursor.EmitDelegate(DrawThemeSwap);
		}

		private delegate void AdjustRect(ref Rectangle rectangle);

		private static void AdjustThemeRect(ref Rectangle rectangle)
		{
			if (!Active || rectangle.IsEmpty)
				return;

			if (CalamitasMenuLayout.ShouldBlockThemeSwap) {
				rectangle = Rectangle.Empty;
				_themeHitbox = rectangle;
				return;
			}

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string text = GetThemeText();
			Vector2 size = ChatManager.GetStringSize(font, text, Vector2.One);
			_themeScale = MathHelper.Min(1f, Main.screenWidth * 0.42f / Math.Max(size.X, 1f));
			size *= _themeScale;
			rectangle = new Rectangle(
				(int)(Main.screenWidth - RightMargin - size.X),
				(int)(Main.screenHeight - 8f - size.Y),
				(int)size.X,
				(int)size.Y);
			_themeHitbox = rectangle;
		}

		private static Vector2 DrawThemeSwap(
			SpriteBatch spriteBatch,
			DynamicSpriteFont font,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			Vector2 baseScale,
			float maxWidth,
			float spread)
		{
			if (!Active) {
				return ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, text, position, color, rotation, origin, baseScale, maxWidth, spread);
			}

			if (CalamitasMenuLayout.ShouldBlockThemeSwap)
				return position;

			DrawThemedLabel(spriteBatch, font, text, new Vector2(_themeHitbox.X, _themeHitbox.Y), color, _themeScale);
			return position;
		}

		private static void DrawThemedLabel(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 position, Color color, float scale)
		{
			int calamitasAt = text.LastIndexOf(CalamitasWord, StringComparison.Ordinal);
			if (calamitasAt < 0) {
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, position, color, 0f, Vector2.Zero, new Vector2(scale));
				return;
			}

			string prefix = text[..calamitasAt];
			string suffix = text[(calamitasAt + CalamitasWord.Length)..];
			Vector2 prefixSize = font.MeasureString(prefix) * scale;
			Vector2 wordSize = font.MeasureString(CalamitasWord) * scale;

			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, prefix, position, color, 0f, Vector2.Zero, new Vector2(scale));

			Vector2 wordPos = position + new Vector2(prefixSize.X, 0f);
			_calamitasPos = wordPos;
			_calamitasSize = wordSize;
			Color glow = CalamitasMenuButtonSystem.GetAnimatedHoverColor();
			DrawCalamitasGlow(spriteBatch, wordPos, wordSize, glow);
			DrawOutlinedString(spriteBatch, font, CalamitasWord, wordPos, Color.Black, glow, scale);

			if (suffix.Length > 0) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					suffix,
					wordPos + new Vector2(wordSize.X, 0f),
					color,
					0f,
					Vector2.Zero,
					new Vector2(scale));
			}

			UpdateAndDrawSparks(spriteBatch, glow);
		}

		private static void DrawOutlinedString(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 position, Color fill, Color outline, float scale)
		{
			for (int y = -3; y <= 3; y++) {
				for (int x = -3; x <= 3; x++) {
					if (x == 0 && y == 0)
						continue;

					float dist = MathF.Sqrt(x * x + y * y);
					if (dist > 3.2f)
						continue;

					float alpha = dist >= 2.4f ? 0.45f : 1f;
					spriteBatch.DrawString(font, text, position + new Vector2(x, y), outline * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				}
			}

			spriteBatch.DrawString(font, text, position, fill, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private static void DrawCalamitasGlow(SpriteBatch spriteBatch, Vector2 wordPos, Vector2 wordSize, Color color)
		{
			Texture2D shine = CalamitasMenuShine.Texture;
			if (shine == null)
				return;

			Vector2 center = wordPos + wordSize * 0.5f;
			float pulse = 0.55f + 0.45f * CalamitasMenuSpectrum.SmoothBeat;
			spriteBatch.Draw(
				shine,
				center,
				null,
				color * (0.28f * pulse),
				0f,
				shine.Size() * 0.5f,
				new Vector2(wordSize.X / shine.Width * 1.35f, wordSize.Y / shine.Height * 2.4f),
				SpriteEffects.None,
				0f);
		}

		private static void UpdateAndDrawSparks(SpriteBatch spriteBatch, Color color)
		{
			if (Main.rand.NextBool(2)) {
				float edge = Main.rand.NextFloat();
				Vector2 spawn = edge switch {
					< 0.35f => _calamitasPos + new Vector2(Main.rand.NextFloat(_calamitasSize.X), 0f),
					< 0.70f => _calamitasPos + new Vector2(Main.rand.NextFloat(_calamitasSize.X), _calamitasSize.Y),
					< 0.85f => _calamitasPos + new Vector2(0f, Main.rand.NextFloat(_calamitasSize.Y)),
					_ => _calamitasPos + new Vector2(_calamitasSize.X, Main.rand.NextFloat(_calamitasSize.Y))
				};

				_sparks.Add(new Spark {
					Position = spawn,
					Velocity = new Vector2(Main.rand.NextFloat(-0.28f, 0.28f), Main.rand.NextFloat(-0.9f, -0.2f)),
					Life = 0f,
					MaxLife = Main.rand.NextFloat(28f, 48f),
					Size = Main.rand.NextFloat(0.035f, 0.07f)
				});
			}

			Texture2D shine = CalamitasMenuShine.Texture;
			if (shine == null)
				return;

			for (int i = _sparks.Count - 1; i >= 0; i--) {
				Spark spark = _sparks[i];
				spark.Life++;
				spark.Position += spark.Velocity;
				if (spark.Life >= spark.MaxLife) {
					_sparks.RemoveAt(i);
					continue;
				}

				float t = spark.Life / spark.MaxLife;
				float alpha = (1f - t) * 0.85f;
				spriteBatch.Draw(
					shine,
					spark.Position,
					null,
					color * alpha,
					0f,
					shine.Size() * 0.5f,
					spark.Size,
					SpriteEffects.None,
					0f);
				_sparks[i] = spark;
			}

			if (_sparks.Count > 40)
				_sparks.RemoveRange(0, _sparks.Count - 40);
		}
	}
}

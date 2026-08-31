using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public class CalamitasMenuButtonSystem : ModSystem
	{
		internal const float LeftMenuButtonX = 340f;

		internal static Color HoverColorDark => CalamitasMenuAccent.Dark;

		internal static Color HoverColorLight => CalamitasMenuAccent.Light;

		internal static Rectangle MenuDrawBounds;
		internal static float VanillaMenuY = 220f;
		private static bool _menuDrawStarted;
		private static bool _drawWaveHover;

		public override void Load()
		{
			IL_Main.DrawMenu += PatchMenuButtonPositions;
			IL_Main.DrawMenu += PatchMenuHoverColor;
			IL_Main.DrawMenu += PatchMenuHoverDrawString;
			On_Main.DrawMenu += DrawMenuHook;
		}

		private static void DrawMenuHook(On_Main.orig_DrawMenu orig, Main self, GameTime time)
		{
			bool steal = false;
			bool savedRelease = Main.mouseLeftRelease;
			int savedMouseY = Main.mouseY;
			bool remapY = false;
			if (ShouldShiftButtons()) {
				ResetMenuDrawTracking();
				CalamitasMenuPanels.HandleTitleInput();
				CalamitasMenuPlayerUI.HandleTitleInput();
				CalamitasMenuLogo.HandleTitleInput();
				CalamitasMenuLayout.HandleTitleInput();
				steal = CalamitasMenuPanels.StealVanillaClicks;
				if (steal) {
					Main.blockMouse = true;
					Main.mouseLeftRelease = false;
				}

				int dy = (int)Math.Round(CalamitasMenuLayout.Menu.Y - VanillaMenuY);
				if (dy != 0 && !CalamitasMenuPanels.OverlayOpen) {
					Main.mouseY -= dy;
					remapY = true;
				}
			}

			orig(self, time);

			if (steal)
				Main.mouseLeftRelease = savedRelease;
			if (remapY)
				Main.mouseY = savedMouseY;
		}

		private static void ResetMenuDrawTracking()
		{
			_menuDrawStarted = false;
			MenuDrawBounds = Rectangle.Empty;
		}

		private void PatchMenuButtonPositions(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			int offY = -1;

			// DrawMenu (2026): int offY = 250; int num2 = screenWidth / 2;
			if (!cursor.TryGotoNext(MoveType.After,
				i => i.MatchLdcI4(250),
				i => i.MatchStloc(out offY),
				i => i.MatchLdsfld(typeof(Main), nameof(Main.screenWidth)),
				i => i.MatchLdcI4(2),
				i => i.MatchDiv())) {
				Mod.Logger.Warn("Could not patch main menu button X (num2 = screenWidth / 2).");
				return;
			}

			cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonX);

			if (offY < 0)
				return;

			cursor.Index = 0;
			if (!cursor.TryGotoNext(MoveType.After,
				i => i.MatchLdcI4(250),
				i => i.MatchStloc(offY),
				i => i.MatchLdsfld(typeof(Main), nameof(Main.screenWidth)))) {
				Mod.Logger.Warn("Could not patch main menu button Y (offY = 250).");
				return;
			}

			cursor.Index--;
			cursor.EmitLdloc(offY);
			cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonY);
			cursor.EmitStloc(offY);

			// Title screen later overwrites offY with 220 (then AddMenuButtons may touch it again).
			cursor.Index = 0;
			if (cursor.TryGotoNext(MoveType.After,
				i => i.MatchLdcI4(220),
				i => i.MatchStloc(offY),
				i => i.MatchLdcI4(7))) {
				cursor.Index--;
				cursor.EmitLdloc(offY);
				cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonY);
				cursor.EmitStloc(offY);
			}
			else {
				Mod.Logger.Warn("Could not patch title menu button Y (offY = 220).");
			}

			cursor.Index = 0;
			bool patchedAdd = false;
			while (cursor.TryGotoNext(MoveType.After, i =>
				(i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
				i.Operand is MethodReference method &&
				method.Name == "AddMenuButtons")) {
				cursor.EmitLdloc(offY);
				cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonY);
				cursor.EmitStloc(offY);
				patchedAdd = true;
				break;
			}

			if (!patchedAdd)
				Mod.Logger.Warn("Could not re-apply main menu button Y after AddMenuButtons.");
		}

		private void PatchMenuHoverColor(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);

			// Unique yellow-green in the focused-item lerp:
			//   r = r * (1 - t) + 255 * t
			//   g = g * (1 - t) + 215 * t
			//   b = b * (1 - t) +   0 * t
			if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchLdcR4(215f))) {
				Mod.Logger.Warn("Could not find main menu hover green constant (215).");
				return;
			}

			Instruction green = cursor.Next;
			Instruction red = FindLdcR4Before(il, cursor.Index, 255f, 20);
			Instruction blue = FindLdcR4After(il, cursor.Index, 0f, 20);

			if (red == null || blue == null) {
				Mod.Logger.Warn("Could not find main menu hover RGB constants (255 / 0).");
				return;
			}

			ReplaceLdcR4WithDelegate(cursor, blue, GetFocusHoverB);
			ReplaceLdcR4WithDelegate(cursor, green, GetFocusHoverG);
			ReplaceLdcR4WithDelegate(cursor, red, GetFocusHoverR);
		}

		private void PatchMenuHoverDrawString(ILContext il)
		{
			MethodInfo drawString = typeof(DynamicSpriteFontExtensionMethods).GetMethod(
				nameof(DynamicSpriteFontExtensionMethods.DrawString),
				new[] {
					typeof(SpriteBatch),
					typeof(DynamicSpriteFont),
					typeof(string),
					typeof(Vector2),
					typeof(Color),
					typeof(float),
					typeof(Vector2),
					typeof(float),
					typeof(SpriteEffects),
					typeof(float)
				});

			if (drawString == null) {
				Mod.Logger.Warn("Could not find DynamicSpriteFont DrawString overload.");
				return;
			}

			ILCursor cursor = new ILCursor(il);
			int patched = 0;

			while (cursor.TryGotoNext(MoveType.Before,
				i => i.MatchLdsfld(typeof(FontAssets), nameof(FontAssets.DeathText)))) {
				int deathTextAt = cursor.Index;
				if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(drawString)))
					break;

				if (cursor.Index - deathTextAt > 60) {
					cursor.Index = deathTextAt + 1;
					continue;
				}

				cursor.Remove();
				cursor.EmitDelegate(DrawMenuItemString);
				patched++;
			}

			if (patched < 2)
				Mod.Logger.Warn($"Could not wrap main menu DrawString calls (patched {patched}).");
		}

		private static Instruction FindLdcR4Before(ILContext il, int fromIndex, float value, int lookback)
		{
			int start = Math.Max(0, fromIndex - lookback);
			for (int i = fromIndex - 1; i >= start; i--) {
				if (il.Instrs[i].MatchLdcR4(value))
					return il.Instrs[i];
			}

			return null;
		}

		private static Instruction FindLdcR4After(ILContext il, int fromIndex, float value, int lookahead)
		{
			int end = Math.Min(il.Instrs.Count, fromIndex + lookahead);
			for (int i = fromIndex + 1; i < end; i++) {
				if (il.Instrs[i].MatchLdcR4(value))
					return il.Instrs[i];
			}

			return null;
		}

		private static void ReplaceLdcR4WithDelegate(ILCursor cursor, Instruction target, Func<float> getter)
		{
			cursor.Goto(target, MoveType.Before);
			cursor.Remove();
			cursor.EmitDelegate(getter);
		}

		private static float GetFocusHoverR()
		{
			if (!ShouldShiftButtons())
				return 255f;

			_drawWaveHover = true;
			return GetAnimatedHoverColor().R;
		}

		private static float GetFocusHoverG() => ShouldShiftButtons() ? GetAnimatedHoverColor().G : 215f;

		private static float GetFocusHoverB() => ShouldShiftButtons() ? GetAnimatedHoverColor().B : 0f;

		internal static Color GetAnimatedHoverColor()
		{
			return CalamitasMenuAccent.Hover;
		}

		private static void DrawMenuItemString(
			SpriteBatch spriteBatch,
			DynamicSpriteFont spriteFont,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			float scale,
			SpriteEffects effects,
			float layerDepth)
		{
			bool wave = _drawWaveHover;
			_drawWaveHover = false;

			if (ShouldShiftButtons() && CalamitasMenuPanels.OverlayOpen)
				return;

			if (ShouldShiftButtons())
				ShiftMenuItem(spriteFont, text, ref position, origin, scale);

			if (!wave || string.IsNullOrEmpty(text) || color.A < 16) {
				spriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effects, layerDepth);
				return;
			}

			DrawWavedHoverText(spriteBatch, spriteFont, text, position, color, rotation, origin, scale, effects, layerDepth);
		}

		internal static void DrawWavedHoverText(
			SpriteBatch spriteBatch,
			DynamicSpriteFont spriteFont,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			float scale,
			SpriteEffects effects = SpriteEffects.None,
			float layerDepth = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return;

			Vector2 size = spriteFont.MeasureString(text);
			float timed = Main.GlobalTimeWrappedHourly * 0.65f;
			float cycle = timed % 2f;
			float front = cycle % 1f;
			bool lightSweeping = cycle < 1f;
			const float band = 0.22f;

			for (int i = 0; i < text.Length; i++) {
				float x0 = i == 0 ? 0f : spriteFont.MeasureString(text.Substring(0, i)).X;
				string glyph = text.Substring(i, 1);
				float x1 = spriteFont.MeasureString(text.Substring(0, i + 1)).X;
				float center = (x0 + x1) * 0.5f;
				float t = size.X <= 1f ? 0f : center / size.X;

				float blend = 1f - MathHelper.SmoothStep(front - band, front + band, t);
				float waveT = lightSweeping ? blend : 1f - blend;
				Color local = Color.Lerp(CalamitasMenuAccent.Dark, CalamitasMenuAccent.Light, waveT);
				local.A = color.A;

				spriteBatch.DrawString(
					spriteFont,
					glyph,
					position,
					local,
					rotation,
					origin - new Vector2(x0, 0f),
					scale,
					effects,
					layerDepth);
			}
		}

		private static void ShiftMenuItem(DynamicSpriteFont font, string text, ref Vector2 position, Vector2 origin, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return;

			if (Math.Abs(position.X - CalamitasMenuLayout.Menu.X) > 360f)
				return;

			float offY = position.Y - origin.Y * scale;
			if (!_menuDrawStarted) {
				_menuDrawStarted = true;
				VanillaMenuY = offY;
			}

			position.Y += CalamitasMenuLayout.Menu.Y - VanillaMenuY;
			Vector2 size = font.MeasureString(text) * scale;
			var rect = new Rectangle(
				(int)MathF.Round(position.X - origin.X * scale),
				(int)MathF.Round(position.Y - origin.Y * scale),
				Math.Max(1, (int)MathF.Ceiling(size.X)),
				Math.Max(1, (int)MathF.Ceiling(size.Y)));
			MenuDrawBounds = MenuDrawBounds.IsEmpty ? rect : Rectangle.Union(MenuDrawBounds, rect);
		}

		private static int ApplyMainMenuButtonY(int offY)
		{
			if (!ShouldShiftButtons())
				return offY;

			if (CalamitasMenuPanels.OverlayOpen)
				return 5000;

			return (int)Math.Round(CalamitasMenuLayout.Menu.Y);
		}

		private static int ApplyMainMenuButtonX(int menuCenterX)
		{
			if (!ShouldShiftButtons())
				return menuCenterX;

			return (int)Math.Round(CalamitasMenuLayout.Menu.X);
		}

		private static bool ShouldShiftButtons() =>
			Main.gameMenu &&
			CoolerMenuCompat.OnTitleLike &&
			MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu;
	}
}

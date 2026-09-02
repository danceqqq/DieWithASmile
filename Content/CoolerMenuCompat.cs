using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.UI;
using Terraria.UI.Chat;

namespace DieWithASmile.Content
{
	public class CoolerMenuCompat : ModSystem
	{
		private const string ModName = "CoolerMenu";
		private const int CoolerMenuMode = 1007;

		private static FieldInfo _configInstance;
		private static FieldInfo _isMenuActive;
		private static FieldInfo _setCoolerMenu;
		private static MethodInfo _saveChanges;
		private static bool _hooksApplied;

		internal static bool ModEnabled => ModLoader.HasMod(ModName);

		internal static bool CoreActive
		{
			get
			{
				if (!TryGetConfig(out object config) || _isMenuActive == null)
					return false;

				return _isMenuActive.GetValue(config) is true;
			}
		}

		internal static bool WorldGenUiActive =>
			WorldGen.generatingWorld || WorldLoadUiOpen;

		internal static bool MenuBackdropActive =>
			Main.gameMenu && !WorldGenUiActive;

		internal static bool OnTitleLike =>
			MenuBackdropActive &&
			(Main.menuMode == 0 || (ModEnabled && Main.menuMode == CoolerMenuMode));

		private static bool WorldLoadUiOpen
		{
			get
			{
				try {
					UIState state = Main.MenuUI?.CurrentState;
					if (state == null)
						return false;
					return state.GetType().Name.Contains("WorldLoad", StringComparison.Ordinal);
				}
				catch {
					return false;
				}
			}
		}

		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod(ModName, out Mod cooler))
				return;

			try {
				BindConfig(cooler);
				if (_hooksApplied)
					return;

				Type menuSystem = cooler.Code.GetType("CoolerMenu.Common.Menu.MenuSystem");
				if (menuSystem == null)
					return;

				MethodInfo coreToggle = menuSystem.GetMethod("RenderCoreMenuToggle", BindingFlags.NonPublic | BindingFlags.Static);
				if (coreToggle != null)
					MonoModHooks.Add(coreToggle, (Action orig) => DrawCoreToggle(orig));

				MethodInfo vanillaToggle = menuSystem.GetMethod("RenderVanillaMenuToggle", BindingFlags.NonPublic | BindingFlags.Static);
				if (vanillaToggle != null)
					MonoModHooks.Add(vanillaToggle, (Action orig) => {
						if (!CalamitasMenuChrome.Active)
							orig();
					});

				MethodInfo drawButton = menuSystem.GetMethod("DrawButton", BindingFlags.NonPublic | BindingFlags.Static);
				if (drawButton != null)
					MonoModHooks.Modify(drawButton, PatchCoolerHoverColor);

				MethodInfo updateMenu = menuSystem.GetMethod("UpdateMenu", BindingFlags.Public | BindingFlags.Static);
				if (updateMenu != null)
					MonoModHooks.Add(updateMenu, (Action orig) => {
						orig();
						if (MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu menu)
							menu.Tick();
					});

				_setCoolerMenu = menuSystem.GetField("SetCoolerMenu", BindingFlags.NonPublic | BindingFlags.Static);
				_hooksApplied = true;
			}
			catch (Exception e) {
				Mod.Logger.Warn("Could not hook CoolerMenu: " + e.Message);
			}
		}

		internal static string GetCoreThemeText()
		{
			string prefix = Language.GetTextValue("Mods.CoolerMenu.CoreMenu.CoreMenuSwap");
			string value = CoreActive
				? Language.GetTextValue("Mods.CoolerMenu.CoreMenu.CoolerMenu")
				: Language.GetTextValue("Mods.CoolerMenu.CoreMenu.Vanilla");
			return prefix + value;
		}

		private static void BindConfig(Mod cooler)
		{
			Type configType = cooler.Code.GetType("CoolerMenu.Common.Config.CoolerMenuConfig");
			if (configType == null)
				return;

			_configInstance = configType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
			_isMenuActive = configType.GetField("IsMenuActive", BindingFlags.Public | BindingFlags.Instance);
			_saveChanges = typeof(ModConfig).GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.FirstOrDefault(m => m.Name == "SaveChanges");
		}

		private static bool TryGetConfig(out object config)
		{
			config = null;
			if (!ModEnabled)
				return false;

			if (_configInstance == null && ModLoader.TryGetMod(ModName, out Mod cooler))
				BindConfig(cooler);

			config = _configInstance?.GetValue(null);
			return config != null;
		}

		private static void DrawCoreToggle(Action orig)
		{
			if (WorldGenUiActive)
				return;

			if (!CalamitasMenuChrome.Active || !ModEnabled) {
				orig();
				return;
			}

			if (CalamitasMenuLayout.Editing)
				return;

			if (Main.menuMode != 0 && Main.menuMode != CoolerMenuMode)
				return;

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string text = GetCoreThemeText();
			Vector2 size = ChatManager.GetStringSize(font, text, Vector2.One);
			Rectangle hit = CalamitasMenuChrome.GetCoreThemeHitbox(size);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY) && !Main.alreadyGrabbingSunOrMoon;
			Color color = hover ? CalamitasMenuButtonSystem.GetAnimatedHoverColor() : new Color(120, 120, 120, 76);
			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				font,
				text,
				new Vector2(hit.X, hit.Y),
				color,
				0f,
				Vector2.Zero,
				Vector2.One);

			if (hover && ((Main.mouseLeftRelease && Main.mouseLeft) || (Main.mouseRightRelease && Main.mouseRight))) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				ToggleCore();
			}
		}

		private static void ToggleCore()
		{
			if (!TryGetConfig(out object config) || _isMenuActive == null)
				return;

			bool next = _isMenuActive.GetValue(config) is not true;
			_isMenuActive.SetValue(config, next);
			_setCoolerMenu?.SetValue(null, false);
			TrySaveConfig(config);

			if (!next && Main.menuMode == CoolerMenuMode)
				Main.menuMode = 0;
		}

		private static void TrySaveConfig(object config)
		{
			if (_saveChanges == null)
				return;

			var args = _saveChanges.GetParameters();
			var values = new object[args.Length];
			for (int i = 0; i < args.Length; i++) {
				if (args[i].ParameterType == typeof(bool))
					values[i] = true;
				else
					values[i] = args[i].HasDefaultValue ? args[i].DefaultValue : null;
			}

			_saveChanges.Invoke(config, values);
		}

		private static void PatchCoolerHoverColor(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			while (cursor.TryGotoNext(MoveType.Before, i => i.MatchLdsfld(typeof(Main), nameof(Main.OurFavoriteColor)))) {
				cursor.Remove();
				cursor.EmitDelegate(GetButtonHoverColor);
			}

			MethodInfo drawShadow = typeof(ChatManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(m =>
					m.Name == nameof(ChatManager.DrawColorCodedStringWithShadow) &&
					m.GetParameters().Length == 10 &&
					m.GetParameters()[7].ParameterType == typeof(Vector2));

			int draws = 0;
			if (drawShadow != null) {
				cursor.Index = 0;
				while (cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(drawShadow))) {
					draws++;
					if (draws == 2) {
						cursor.Remove();
						cursor.EmitDelegate(DrawCoolerButtonString);
						break;
					}

					cursor.Index++;
				}
			}
		}

		private static Vector2 DrawCoolerButtonString(
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
			bool ourMenu = MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu;
			bool hovered = color.R < 248 || color.G < 248 || color.B < 248;
			if (!ourMenu || !hovered || string.IsNullOrEmpty(text)) {
				return ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					text,
					position,
					color,
					rotation,
					origin,
					baseScale,
					maxWidth,
					spread);
			}

			CalamitasMenuButtonSystem.DrawWavedHoverText(
				spriteBatch,
				font,
				text,
				position,
				color,
				rotation,
				origin,
				baseScale.X);
			return position + font.MeasureString(text) * baseScale;
		}

		private static Color GetButtonHoverColor()
		{
			if (MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu)
				return CalamitasMenuButtonSystem.GetAnimatedHoverColor();

			return Main.OurFavoriteColor;
		}
	}
}

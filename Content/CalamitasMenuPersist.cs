using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public class CalamitasMenuPersist : ModSystem
	{
		private static FieldInfo _switchToMenu;
		private static FieldInfo _lastSelected;
		private static FieldInfo _loading;
		private static int _restoreCooldown;

		internal static bool MenuStillLoading => _loading?.GetValue(null) is true;

		public override void Load()
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			_switchToMenu = typeof(MenuLoader).GetField("switchToMenu", flags);
			_lastSelected = typeof(MenuLoader).GetField("LastSelectedModMenu", flags);
			_loading = typeof(MenuLoader).GetField("loading", flags);

			MethodInfo gotoSaved = typeof(MenuLoader).GetMethod("GotoSavedModMenu", flags);
			if (gotoSaved != null)
				MonoModHooks.Add(gotoSaved, GotoSavedHook);

			MethodInfo activateOld = typeof(MenuLoader).GetMethod(nameof(MenuLoader.ActivateOldVanillaMenu), flags);
			if (activateOld != null)
				MonoModHooks.Add(activateOld, ActivateOldHook);
		}

		public override void PostSetupContent()
		{
			BootstrapFromTml();
			QueueRestore();
		}

		public override void PreUpdatePlayers()
		{
			CalamitasMenuPlaylist.HandleMenuLifecycle();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			CalamitasMenuPlaylist.HandleMenuLifecycle();
			if (Main.gameMenu)
				DieWithASmileSettings.TickScenes();
			if (!Main.gameMenu)
				return;

			if (Main.instance.playOldTile && DieWithASmileSave.Data.KeepMenuSelected) {
				Main.instance.playOldTile = false;
				Main.alreadyGrabbingSunOrMoon = false;
				QueueRestore();
			}

			if (_restoreCooldown > 0) {
				_restoreCooldown--;
				Restore();
			}
			else if (DieWithASmileSave.Data.KeepMenuSelected && MenuLoader.CurrentMenu is not DieWithASmileCalamitasMenu)
				Restore();
		}

		internal static void OnOurMenuSelected()
		{
			DieWithASmileSave.Data.KeepMenuSelected = true;
			DieWithASmileSave.Save();
			RememberInTml();
		}

		internal static void OnOurMenuDeselected()
		{
			if (_loading?.GetValue(null) is true)
				return;

			DieWithASmileSave.Data.KeepMenuSelected = false;
			DieWithASmileSave.Save();
		}

		private static void GotoSavedHook(Action orig)
		{
			orig();
			BootstrapFromTml();
			Restore();
		}

		private static void ActivateOldHook(Action orig)
		{
			if (DieWithASmileSave.Data.KeepMenuSelected) {
				if (Main.instance != null)
					Main.instance.playOldTile = false;
				Main.alreadyGrabbingSunOrMoon = false;
				return;
			}

			orig();
		}

		private static void BootstrapFromTml()
		{
			if (DieWithASmileSave.Data.KeepMenuSelected)
				return;

			string last = _lastSelected?.GetValue(null) as string;
			if (!string.IsNullOrEmpty(last) && last.Contains("DieWithASmile", StringComparison.OrdinalIgnoreCase)) {
				DieWithASmileSave.Data.KeepMenuSelected = true;
				DieWithASmileSave.Save();
			}
		}

		private static void QueueRestore()
		{
			if (!DieWithASmileSave.Data.KeepMenuSelected)
				return;

			_restoreCooldown = 30;
			Restore();
		}

		private static void Restore()
		{
			if (!DieWithASmileSave.Data.KeepMenuSelected)
				return;

			DieWithASmileCalamitasMenu menu = ModContent.GetInstance<DieWithASmileCalamitasMenu>();
			if (menu == null)
				return;

			if (Main.instance != null)
				Main.instance.playOldTile = false;
			RememberInTml();
			if (MenuLoader.CurrentMenu == menu)
				return;

			_switchToMenu?.SetValue(null, menu);
		}

		private static void RememberInTml()
		{
			DieWithASmileCalamitasMenu menu = ModContent.GetInstance<DieWithASmileCalamitasMenu>();
			if (menu == null || _lastSelected == null)
				return;

			try {
				_lastSelected.SetValue(null, menu.FullName);
				Main.SaveSettings();
			}
			catch {
			}
		}
	}
}

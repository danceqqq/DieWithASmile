using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace DieWithASmile.Content
{
	public enum MenuScene
	{
		Calamitas = 0,
		DontForget = 1,
		ComeAlong = 2,
		Meadow = 3,
		Yharim = 4,
		Witch = 5,
		Soul = 6
	}

	internal static class DieWithASmileSettings
	{
		internal static DieWithASmileSaveData Current => DieWithASmileSave.Data;

		internal static bool PlayerEnabled => Current.PlayerEnabled;

		internal static bool FollowMusic => Current.FollowMusic;

		internal static bool ShuffleScenes => Current.ShuffleScenes && !Current.FollowMusic;

		internal static bool ShuffleLogos => Current.ShuffleLogos;

		internal static float ShuffleSceneSeconds => Math.Clamp(Current.ShuffleSceneSeconds, 0f, 15f);

		internal static bool TimerShuffleScenes => ShuffleScenes && ShuffleSceneSeconds >= 1f;

		internal static MenuLogo Logo => Current.MenuLogo;

		internal static MenuScene LockedScene => Current.LockedScene;

		internal static float MenuMusicVolume => Math.Clamp(Current.MenuMusicVolume, 0f, 1f);

		internal static float EffectiveMusicVolume => Math.Clamp(Main.musicVolume * MenuMusicVolume, 0f, 1f);

		internal static bool HasCustomPlayerPosition => Current.PlayerPositionSet;

		internal static Vector2 PlayerAnchorPixels
		{
			get
			{
				float w = Math.Max(1, Main.screenWidth);
				float h = Math.Max(1, Main.screenHeight);
				return new Vector2(Current.PlayerAnchorX * w, Current.PlayerAnchorY * h);
			}
		}

		internal static void SetPlayerAnchorPixels(Vector2 pixels)
		{
			float w = Math.Max(1, Main.screenWidth);
			float h = Math.Max(1, Main.screenHeight);
			Current.PlayerPositionSet = true;
			Current.PlayerAnchorX = Math.Clamp(pixels.X / w, 0f, 1f);
			Current.PlayerAnchorY = Math.Clamp(pixels.Y / h, 0f, 1f);
			DieWithASmileSave.Save();
		}

		internal static bool HasCustomLogoPosition => Current.LogoPositionSet;

		internal static Vector2 LogoAnchorPixels
		{
			get
			{
				float w = Math.Max(1, Main.screenWidth);
				float h = Math.Max(1, Main.screenHeight);
				return new Vector2(Current.LogoAnchorX * w, Current.LogoAnchorY * h);
			}
		}

		internal static void SetLogoAnchorPixels(Vector2 pixels)
		{
			float w = Math.Max(1, Main.screenWidth);
			float h = Math.Max(1, Main.screenHeight);
			Current.LogoPositionSet = true;
			Current.LogoAnchorX = Math.Clamp(pixels.X / w, 0f, 1f);
			Current.LogoAnchorY = Math.Clamp(pixels.Y / h, 0f, 1f);
			DieWithASmileSave.Save();
		}

		internal static bool HasCustomMenuPosition => Current.MenuPositionSet;

		internal static Vector2 MenuAnchorPixels
		{
			get
			{
				float w = Math.Max(1, Main.screenWidth);
				float h = Math.Max(1, Main.screenHeight);
				return new Vector2(Current.MenuAnchorX * w, Current.MenuAnchorY * h);
			}
		}

		internal static void SaveLayout(Vector2 logo, Vector2 player, Vector2 menu, float logoScale, Vector2 wallpaperPan)
		{
			float w = Math.Max(1, Main.screenWidth);
			float h = Math.Max(1, Main.screenHeight);
			Current.LogoPositionSet = true;
			Current.LogoAnchorX = Math.Clamp(logo.X / w, 0f, 1f);
			Current.LogoAnchorY = Math.Clamp(logo.Y / h, 0f, 1f);
			Current.PlayerPositionSet = true;
			Current.PlayerAnchorX = Math.Clamp(player.X / w, 0f, 1f);
			Current.PlayerAnchorY = Math.Clamp(player.Y / h, 0f, 1f);
			Current.MenuPositionSet = true;
			Current.MenuAnchorX = Math.Clamp(menu.X / w, 0f, 1f);
			Current.MenuAnchorY = Math.Clamp(menu.Y / h, 0f, 1f);
			Current.LogoScale = Math.Clamp(logoScale, 0.45f, 2f);
			SaveWallpaperPan(wallpaperPan, save: false);
			DieWithASmileSave.Save();
		}

		internal static Vector2 SavedWallpaperPan
		{
			get
			{
				if (!TryGetCurrentWallpaper(out CustomArtRecord record))
					return new Vector2(0.5f, 0.5f);

				return new Vector2(Math.Clamp(record.PanX, 0f, 1f), Math.Clamp(record.PanY, 0f, 1f));
			}
		}

		internal static Vector2 LiveWallpaperPan =>
			CalamitasMenuLayout.Editing ? CalamitasMenuLayout.WorkPan : SavedWallpaperPan;

		internal static Vector2 WallpaperPanOf(string id)
		{
			if (string.IsNullOrEmpty(id))
				return new Vector2(0.5f, 0.5f);

			if (CalamitasMenuLayout.Editing && Current.CustomWallpaperId == id)
				return CalamitasMenuLayout.WorkPan;

			CustomArtRecord record = Current.CustomWallpapers.Find(item => item.Id == id);
			if (record == null)
				return new Vector2(0.5f, 0.5f);

			return new Vector2(Math.Clamp(record.PanX, 0f, 1f), Math.Clamp(record.PanY, 0f, 1f));
		}

		internal static void SaveWallpaperPan(Vector2 pan, bool save = true)
		{
			if (!TryGetCurrentWallpaper(out CustomArtRecord record))
				return;

			record.PanX = Math.Clamp(pan.X, 0f, 1f);
			record.PanY = Math.Clamp(pan.Y, 0f, 1f);
			if (save)
				DieWithASmileSave.Save();
		}

		private static bool TryGetCurrentWallpaper(out CustomArtRecord record)
		{
			record = null;
			if (string.IsNullOrEmpty(Current.CustomWallpaperId))
				return false;

			record = Current.CustomWallpapers.Find(item => item.Id == Current.CustomWallpaperId);
			return record != null;
		}

		internal static float LogoScale => Math.Clamp(Current.LogoScale <= 0f ? 1f : Current.LogoScale, 0.45f, 2f);

		internal static bool UsingCustomLogo =>
			!string.IsNullOrEmpty(Current.CustomLogoId) || !string.IsNullOrEmpty(Current.ForeignLogoId);

		internal static bool UsingFileLogo => !string.IsNullOrEmpty(Current.CustomLogoId);

		internal static bool UsingForeignLogo =>
			string.IsNullOrEmpty(Current.CustomLogoId) && !string.IsNullOrEmpty(Current.ForeignLogoId);

		internal static bool UsingCustomWallpaper =>
			!string.IsNullOrEmpty(Current.CustomWallpaperId) ||
			!string.IsNullOrEmpty(Current.ForeignWallpaperId) ||
			UsingVanillaWallpaper ||
			UsingTmlWallpaper ||
			UsingOrphanWallpaper;

		internal static bool UsingFileWallpaper => !string.IsNullOrEmpty(Current.CustomWallpaperId);

		internal static bool UsingForeignWallpaper =>
			string.IsNullOrEmpty(Current.CustomWallpaperId) && !string.IsNullOrEmpty(Current.ForeignWallpaperId);

		internal static bool UsingVanillaWallpaper => Current.VanillaBgStyle >= 0;

		internal static bool UsingTmlWallpaper => Current.TmlWallpaper;

		internal static bool UsingOrphanWallpaper => !string.IsNullOrEmpty(Current.OrphanStyleKey);

		internal static bool UsingPassthroughSky => UsingVanillaWallpaper || UsingTmlWallpaper;

		internal static bool UseDontForgetScene =>
			!UsingCustomWallpaper && (FollowMusic ? CalamitasMenuPlaylist.IsDontForget : LockedScene == MenuScene.DontForget);

		internal static bool UseComeAlongScene =>
			!UsingCustomWallpaper && (FollowMusic ? CalamitasMenuPlaylist.IsComeAlong : LockedScene == MenuScene.ComeAlong);

		internal static bool UseMeadowScene => !UsingCustomWallpaper && !FollowMusic && LockedScene == MenuScene.Meadow;

		internal static bool UseYharimScene => !UsingCustomWallpaper && !FollowMusic && LockedScene == MenuScene.Yharim;

		internal static bool UseWitchScene => !UsingCustomWallpaper && !FollowMusic && LockedScene == MenuScene.Witch;

		internal static bool UseSoulScene => !UsingCustomWallpaper && !FollowMusic && LockedScene == MenuScene.Soul;

		internal static void SetPlayerEnabled(bool enabled)
		{
			if (Current.PlayerEnabled == enabled)
				return;

			Current.PlayerEnabled = enabled;
			DieWithASmileSave.Save();
			CalamitasMenuPlaylist.ApplyPlayerEnabled(enabled);
		}

		private static void ClearWallpaperChoice()
		{
			Current.CustomWallpaperId = "";
			Current.ForeignWallpaperId = "";
			Current.VanillaBgStyle = -1;
			Current.TmlWallpaper = false;
			Current.OrphanStyleKey = "";
		}

		internal static void AbandonBrokenWallpaper()
		{
			if (!UsingCustomWallpaper)
				return;

			ClearWallpaperChoice();
			Current.FollowMusic = true;
			Current.ShuffleScenes = false;
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetFollowMusic()
		{
			Current.FollowMusic = true;
			Current.ShuffleScenes = false;
			ClearWallpaperChoice();
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetShuffleScenes()
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = true;
			_sceneTimer = 0f;
			CalamitasMenuWallpaper.Reroll(save: true);
		}

		internal static void SetShuffleLogos()
		{
			Current.ShuffleLogos = true;
			CalamitasMenuLogo.Reroll(save: true);
		}

		internal static void ApplyLogo(string key, bool keepShuffle)
		{
			Current.ShuffleLogos = keepShuffle;
			Current.CustomLogoId = "";
			Current.ForeignLogoId = "";
			Current.MenuLogo = MenuLogo.Classic;
			if (string.IsNullOrEmpty(key) || key.StartsWith("logo:", StringComparison.Ordinal)) {
				int index = 0;
				if (key != null && key.Length > 5)
					int.TryParse(key.AsSpan(5), out index);
				Current.MenuLogo = (MenuLogo)Math.Clamp(index, 0, 4);
			}
			else if (key.StartsWith("foreign:", StringComparison.Ordinal))
				Current.ForeignLogoId = key[8..];
			else if (key.StartsWith("custom:", StringComparison.Ordinal))
				Current.CustomLogoId = key[7..];

			DieWithASmileSave.Save();
		}

		internal static void SetLockedScene(MenuScene scene)
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = false;
			Current.LockedScene = scene;
			ClearWallpaperChoice();
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetLogo(MenuLogo logo) =>
			ApplyLogo("logo:" + (int)logo, keepShuffle: false);

		internal static void SetCustomLogo(string id) =>
			ApplyLogo(string.IsNullOrEmpty(id) ? "logo:0" : "custom:" + id, keepShuffle: false);

		internal static void SetForeignLogo(string fullName) =>
			ApplyLogo(string.IsNullOrEmpty(fullName) ? "logo:0" : "foreign:" + fullName, keepShuffle: false);

		internal static void SetCustomWallpaper(string id)
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = false;
			ClearWallpaperChoice();
			Current.CustomWallpaperId = id ?? "";
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetForeignWallpaper(string fullName)
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = false;
			ClearWallpaperChoice();
			Current.ForeignWallpaperId = fullName ?? "";
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetVanillaWallpaper(int style)
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = false;
			ClearWallpaperChoice();
			Current.VanillaBgStyle = CalamitasMenuVanilla.IsKnown(style) ? style : SurfaceBackgroundID.Forest1;
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetTmlWallpaper()
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = false;
			ClearWallpaperChoice();
			Current.TmlWallpaper = true;
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetOrphanWallpaper(string key)
		{
			Current.FollowMusic = false;
			Current.ShuffleScenes = false;
			ClearWallpaperChoice();
			if (string.IsNullOrEmpty(key) ||
			    CalamitasMenuForeign.IsSkippedStyleKey(key) ||
			    CalamitasMenuForeign.NeedsManualOrphan(CalamitasMenuForeign.FindStyle(key))) {
				Current.FollowMusic = true;
				DieWithASmileSave.Save();
				return;
			}

			Current.OrphanStyleKey = key;
			_sceneTimer = 0f;
			DieWithASmileSave.Save();
		}

		internal static void SetAccent(int index)
		{
			int clamped = Math.Clamp(index, 0, CalamitasMenuAccent.Palettes.Length - 1);
			if (Current.AccentIndex == clamped)
				return;

			Current.AccentIndex = clamped;
			DieWithASmileSave.Save();
		}

		internal static void SetShuffleSceneSeconds(float seconds, bool save = true)
		{
			seconds = MathF.Round(Math.Clamp(seconds, 0f, 15f));
			if (Math.Abs(Current.ShuffleSceneSeconds - seconds) < 0.01f)
				return;

			Current.ShuffleSceneSeconds = seconds;
			_sceneTimer = 0f;
			if (save)
				DieWithASmileSave.Save();
		}

		internal static void TickScenes()
		{
			if (!Main.gameMenu || !TimerShuffleScenes || !CoolerMenuCompat.OnTitleLike) {
				_sceneTimer = 0f;
				_lastSceneTick = -1f;
				return;
			}

			float now = Main.GlobalTimeWrappedHourly;
			float dt = _lastSceneTick < 0f ? 1f / 60f : now - _lastSceneTick;
			_lastSceneTick = now;
			if (dt < 0f)
				dt += 3600f;
			if (dt <= 0.0001f)
				return;
			if (dt > 0.25f)
				dt = 1f / 60f;

			_sceneTimer += dt;
			if (_sceneTimer < ShuffleSceneSeconds)
				return;

			_sceneTimer = 0f;
			CalamitasMenuWallpaper.Reroll(save: false);
		}

		internal static void RerollScene(bool save = true) =>
			CalamitasMenuWallpaper.Reroll(save);

		private static float _sceneTimer;
		private static float _lastSceneTick = -1f;
	}
}

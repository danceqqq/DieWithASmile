using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal sealed class CustomTrackRecord
	{
		public string Id { get; set; } = "";
		public string FileName { get; set; } = "";
		public string Title { get; set; } = "";
		public string Artist { get; set; } = "Custom";
	}

	internal sealed class CustomArtRecord
	{
		public string Id { get; set; } = "";
		public string FileName { get; set; } = "";
		public float PanX { get; set; } = 0.5f;
		public float PanY { get; set; } = 0.5f;
	}

	internal sealed class DieWithASmileSaveData
	{
		public bool PlayerEnabled { get; set; } = true;
		public bool FollowMusic { get; set; } = true;
		public bool ShuffleScenes { get; set; }
		public bool ShuffleLogos { get; set; }
		public MenuScene LockedScene { get; set; } = MenuScene.Calamitas;
		public bool LoopEnabled { get; set; }
		public bool ShuffleEnabled { get; set; }
		public string LoopedTrackId { get; set; } = "";
		public List<string> DisabledBuiltInIds { get; set; } = new();
		public List<string> DisabledCustomIds { get; set; } = new();
		public List<CustomTrackRecord> CustomTracks { get; set; } = new();
		public bool KeepMenuSelected { get; set; }
		public float MenuMusicVolume { get; set; } = 1f;
		public float ShuffleSceneSeconds { get; set; } = 10f;
		public List<int> ShuffleScenePool { get; set; } = new();
		public List<string> ShuffleWallpaperPool { get; set; } = new();
		public List<string> HiddenWallpapers { get; set; } = new();
		public MenuLogo MenuLogo { get; set; } = MenuLogo.Classic;
		public bool PlayerPositionSet { get; set; }
		public float PlayerAnchorX { get; set; }
		public float PlayerAnchorY { get; set; }
		public bool LogoPositionSet { get; set; }
		public float LogoAnchorX { get; set; }
		public float LogoAnchorY { get; set; }
		public bool MenuPositionSet { get; set; }
		public float MenuAnchorX { get; set; }
		public float MenuAnchorY { get; set; }
		public float LogoScale { get; set; } = 1f;
		public string CustomLogoId { get; set; } = "";
		public string CustomWallpaperId { get; set; } = "";
		public string ForeignLogoId { get; set; } = "";
		public string ForeignWallpaperId { get; set; } = "";
		public int VanillaBgStyle { get; set; } = -1;
		public bool TmlWallpaper { get; set; }
		public string OrphanStyleKey { get; set; } = "";
		public int AccentIndex { get; set; }
		public List<CustomArtRecord> CustomLogos { get; set; } = new();
		public List<CustomArtRecord> CustomWallpapers { get; set; } = new();
	}

	internal static class DieWithASmileSave
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		private static DieWithASmileSaveData _data = new();
		private static bool _loaded;

		internal static DieWithASmileSaveData Data
		{
			get
			{
				EnsureLoaded();
				return _data;
			}
		}

		internal static string RootFolder => Path.Combine(Main.SavePath, "DieWithASmile");

		internal static string MusicFolder => Path.Combine(RootFolder, "Music");

		internal static string BrokenFolder => Path.Combine(RootFolder, "Broken");

		internal static string LogoFolder => Path.Combine(RootFolder, "logo");

		internal static string WallpaperFolder => Path.Combine(RootFolder, "wallpaper");

		internal static string SettingsPath => Path.Combine(RootFolder, "settings.json");

		internal static string PlayGuardPath => Path.Combine(RootFolder, "playing.lock");

		internal static void EnsureFolders()
		{
			Directory.CreateDirectory(MusicFolder);
			Directory.CreateDirectory(BrokenFolder);
			Directory.CreateDirectory(LogoFolder);
			Directory.CreateDirectory(WallpaperFolder);
		}

		internal static void EnsureLoaded()
		{
			if (_loaded)
				return;

			_loaded = true;
			EnsureFolders();
			MigrateLegacyConfig();
			Load();
		}

		internal static void Load()
		{
			try {
				if (!File.Exists(SettingsPath))
					return;

				DieWithASmileSaveData parsed = JsonSerializer.Deserialize<DieWithASmileSaveData>(File.ReadAllText(SettingsPath), JsonOptions);
				if (parsed != null)
					_data = parsed;

				_data.DisabledBuiltInIds ??= new List<string>();
				_data.DisabledCustomIds ??= new List<string>();
				_data.CustomTracks ??= new List<CustomTrackRecord>();
				_data.CustomLogos ??= new List<CustomArtRecord>();
				_data.CustomWallpapers ??= new List<CustomArtRecord>();
				_data.CustomLogoId ??= "";
				_data.CustomWallpaperId ??= "";
				_data.ForeignLogoId ??= "";
				_data.ForeignWallpaperId ??= "";
				_data.OrphanStyleKey ??= "";
				_data.ShuffleScenePool ??= new List<int>();
				_data.ShuffleWallpaperPool ??= new List<string>();
				_data.HiddenWallpapers ??= new List<string>();
				if (_data.ShuffleWallpaperPool.Count == 0 && _data.ShuffleScenePool.Count > 0) {
					foreach (int id in _data.ShuffleScenePool) {
						if (id >= 0 && id <= 5)
							_data.ShuffleWallpaperPool.Add(CalamitasMenuWallpaper.Scene(id));
					}
				}

				_data.ShuffleScenePool.RemoveAll(i => i < 0 || i > 5);
				if (_data.OrphanStyleKey != null &&
				    CalamitasMenuForeign.IsSkippedStyleKey(_data.OrphanStyleKey))
					_data.OrphanStyleKey = "";
				_data.ShuffleWallpaperPool?.RemoveAll(CalamitasMenuForeign.IsSkippedWallpaperKey);
				if (_data.AccentIndex < 0 || _data.AccentIndex >= CalamitasMenuAccent.Palettes.Length)
					_data.AccentIndex = 0;
				if (_data.VanillaBgStyle >= 0 && !CalamitasMenuVanilla.IsKnown(_data.VanillaBgStyle))
					_data.VanillaBgStyle = -1;
				_data.MenuMusicVolume = Math.Clamp(_data.MenuMusicVolume, 0f, 1f);
				bool menuVolumeWasZero = _data.MenuMusicVolume <= 0.0005f;
				if (menuVolumeWasZero)
					_data.MenuMusicVolume = 1f;
				if (_data.ShuffleSceneSeconds < 0f || _data.ShuffleSceneSeconds > 15f)
					_data.ShuffleSceneSeconds = 10f;
				if ((int)_data.MenuLogo < 0 || (int)_data.MenuLogo > 4)
					_data.MenuLogo = MenuLogo.Classic;
				if (_data.LogoScale < 0.45f || _data.LogoScale > 2f)
					_data.LogoScale = 1f;
				_data.PlayerAnchorX = Math.Clamp(_data.PlayerAnchorX, 0f, 1f);
				_data.PlayerAnchorY = Math.Clamp(_data.PlayerAnchorY, 0f, 1f);
				_data.LogoAnchorX = Math.Clamp(_data.LogoAnchorX, 0f, 1f);
				_data.LogoAnchorY = Math.Clamp(_data.LogoAnchorY, 0f, 1f);
				_data.MenuAnchorX = Math.Clamp(_data.MenuAnchorX, 0f, 1f);
				_data.MenuAnchorY = Math.Clamp(_data.MenuAnchorY, 0f, 1f);
				foreach (CustomArtRecord wall in _data.CustomWallpapers) {
					if (wall == null)
						continue;
					wall.PanX = Math.Clamp(wall.PanX, 0f, 1f);
					wall.PanY = Math.Clamp(wall.PanY, 0f, 1f);
				}

				if (menuVolumeWasZero)
					Save();
			}
			catch {
			}
		}

		internal static void WritePlayGuard(string fileName)
		{
			try {
				EnsureFolders();
				File.WriteAllText(PlayGuardPath, fileName ?? "");
			}
			catch {
			}
		}

		internal static string ReadPlayGuard()
		{
			try {
				return File.Exists(PlayGuardPath) ? File.ReadAllText(PlayGuardPath).Trim() : "";
			}
			catch {
				return "";
			}
		}

		internal static void ClearPlayGuard()
		{
			try {
				if (File.Exists(PlayGuardPath))
					File.Delete(PlayGuardPath);
			}
			catch {
			}
		}

		internal static void Save()
		{
			try {
				EnsureFolders();
				File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_data, JsonOptions));
			}
			catch {
			}
		}

		private static void MigrateLegacyConfig()
		{
			if (File.Exists(SettingsPath))
				return;

			string legacy = Path.Combine(Main.SavePath, "ModConfigs", "DieWithASmile_DieWithASmileConfig.json");
			if (!File.Exists(legacy))
				return;

			try {
				using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(legacy));
				JsonElement root = doc.RootElement;
				if (root.TryGetProperty("DisableMusicPlayer", out JsonElement disable))
					_data.PlayerEnabled = !disable.GetBoolean();
				if (root.TryGetProperty("LockedScene", out JsonElement scene) && Enum.TryParse(scene.GetString(), out MenuScene parsed)) {
					_data.LockedScene = parsed;
					_data.FollowMusic = false;
				}

				if (root.TryGetProperty("LoopEnabled", out JsonElement loop) && loop.GetBoolean() &&
				    root.TryGetProperty("LoopedTrackIndex", out JsonElement index)) {
					int i = index.GetInt32();
					if (i >= 0 && i < CalamitasMenuPlaylist.BuiltIn.Length) {
						_data.LoopEnabled = true;
						_data.LoopedTrackId = CalamitasMenuPlaylist.BuiltIn[i].Id;
					}
				}
			}
			catch {
			}
		}
	}
}

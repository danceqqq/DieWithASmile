using System;
using System.Collections.Generic;
using Terraria;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuWallpaper
	{
		internal const string Tml = "tml";

		internal static string Scene(int index) => "scene:" + index;

		internal static string Vanilla(int style) => "vanilla:" + style;

		internal static string Orphan(string key) => "orphan:" + (key ?? "");

		internal static string Foreign(string id) => "foreign:" + (id ?? "");

		internal static string Custom(string id) => "custom:" + (id ?? "");

		internal static string CurrentKey()
		{
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			if (DieWithASmileSettings.UsingFileWallpaper)
				return Custom(data.CustomWallpaperId);
			if (DieWithASmileSettings.UsingForeignWallpaper)
				return Foreign(data.ForeignWallpaperId);
			if (DieWithASmileSettings.UsingVanillaWallpaper)
				return Vanilla(data.VanillaBgStyle);
			if (DieWithASmileSettings.UsingTmlWallpaper)
				return Tml;
			if (DieWithASmileSettings.UsingOrphanWallpaper)
				return Orphan(data.OrphanStyleKey);
			if (DieWithASmileSettings.FollowMusic)
				return "";
			return Scene((int)DieWithASmileSettings.LockedScene);
		}

		internal static bool IsHidden(string key)
		{
			if (string.IsNullOrEmpty(key))
				return false;
			List<string> hidden = DieWithASmileSave.Data.HiddenWallpapers;
			return hidden != null && hidden.Contains(key);
		}

		internal static int HiddenCount()
		{
			List<string> hidden = DieWithASmileSave.Data.HiddenWallpapers;
			return hidden?.Count ?? 0;
		}

		internal static void ToggleHidden(string key)
		{
			if (string.IsNullOrEmpty(key))
				return;

			List<string> hidden = DieWithASmileSave.Data.HiddenWallpapers ??= new List<string>();
			if (hidden.Contains(key))
				hidden.Remove(key);
			else {
				hidden.Add(key);
				RemoveFromStoredPool(key);
			}

			DieWithASmileSave.Save();
			if (DieWithASmileSettings.ShuffleScenes && !InShuffle(CurrentKey()))
				Reroll(save: true);
		}

		internal static bool InShuffle(string key)
		{
			if (string.IsNullOrEmpty(key) || IsHidden(key))
				return false;
			return EffectivePool().Contains(key);
		}

		internal static void ToggleShuffle(string key)
		{
			if (string.IsNullOrEmpty(key) || IsHidden(key) || CalamitasMenuForeign.IsSkippedWallpaperKey(key))
				return;

			List<string> pool = EffectivePool();
			if (pool.Contains(key)) {
				if (pool.Count <= 1)
					return;
				pool.Remove(key);
			}
			else {
				pool.Add(key);
			}

			DieWithASmileSave.Data.ShuffleWallpaperPool = IsDefaultScenePool(pool) ? new List<string>() : pool;
			if (!pool.Contains(CurrentKey()))
				Reroll(save: false);
			DieWithASmileSave.Save();
		}

		internal static void Apply(string key, bool keepShuffle)
		{
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			data.FollowMusic = false;
			data.ShuffleScenes = keepShuffle;
			ClearChoice(data);

			if (string.IsNullOrEmpty(key) || key.StartsWith("scene:", StringComparison.Ordinal)) {
				int index = 0;
				if (key != null && key.Length > 6)
					int.TryParse(key.AsSpan(6), out index);
				index = Math.Clamp(index, 0, (int)MenuScene.Soul);
				data.LockedScene = (MenuScene)index;
			}
			else if (key == Tml) {
				data.TmlWallpaper = true;
			}
			else if (key.StartsWith("vanilla:", StringComparison.Ordinal) &&
			         int.TryParse(key.AsSpan(8), out int style) &&
			         CalamitasMenuVanilla.IsKnown(style)) {
				data.VanillaBgStyle = style;
			}
			else if (key.StartsWith("orphan:", StringComparison.Ordinal)) {
				data.OrphanStyleKey = key[7..];
			}
			else if (key.StartsWith("foreign:", StringComparison.Ordinal)) {
				data.ForeignWallpaperId = key[8..];
			}
			else if (key.StartsWith("custom:", StringComparison.Ordinal)) {
				data.CustomWallpaperId = key[7..];
			}

			DieWithASmileSave.Save();
		}

		internal static void Reroll(bool save)
		{
			List<string> pool = EffectivePool();
			string current = CurrentKey();
			string next = pool[0];
			if (pool.Count > 1) {
				int guard = 0;
				do
					next = pool[NextRand(pool.Count)];
				while (next == current && ++guard < 16);
			}

			Apply(next, keepShuffle: true);
			if (!save)
				return;
			DieWithASmileSave.Save();
		}

		internal static List<string> EffectivePool()
		{
			List<string> stored = DieWithASmileSave.Data.ShuffleWallpaperPool;
			List<string> pool;
			if (stored == null || stored.Count == 0)
				pool = DefaultScenes();
			else {
				pool = new List<string>();
				foreach (string key in stored) {
					if (string.IsNullOrEmpty(key) || IsHidden(key) || pool.Contains(key))
						continue;
					if (CalamitasMenuForeign.IsSkippedWallpaperKey(key))
						continue;
					pool.Add(key);
				}

				if (pool.Count == 0)
					pool = DefaultScenes();
			}

			pool.RemoveAll(IsHidden);
			if (pool.Count == 0)
				pool = DefaultScenes();
			return pool;
		}

		private static List<string> DefaultScenes()
		{
			var list = new List<string>(7);
			for (int i = 0; i <= (int)MenuScene.Soul; i++) {
				string key = Scene(i);
				if (!IsHidden(key))
					list.Add(key);
			}

			if (list.Count == 0)
				list.Add(Scene(0));
			return list;
		}

		private static bool IsDefaultScenePool(List<string> pool)
		{
			List<string> def = DefaultScenes();
			if (pool.Count != def.Count)
				return false;
			foreach (string key in def) {
				if (!pool.Contains(key))
					return false;
			}

			return true;
		}

		private static void RemoveFromStoredPool(string key)
		{
			List<string> stored = DieWithASmileSave.Data.ShuffleWallpaperPool;
			stored?.Remove(key);
		}

		private static void ClearChoice(DieWithASmileSaveData data)
		{
			data.CustomWallpaperId = "";
			data.ForeignWallpaperId = "";
			data.VanillaBgStyle = -1;
			data.TmlWallpaper = false;
			data.OrphanStyleKey = "";
		}

		private static int NextRand(int count)
		{
			if (Main.rand != null)
				return Main.rand.Next(count);
			return Random.Shared.Next(count);
		}
	}
}

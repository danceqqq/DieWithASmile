using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuUserArt
	{
		private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
		private static readonly Dictionary<string, Texture2D> _cache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, DateTime> _times = new(StringComparer.OrdinalIgnoreCase);

		internal static IReadOnlyList<CustomArtRecord> Logos => DieWithASmileSave.Data.CustomLogos;

		internal static IReadOnlyList<CustomArtRecord> Wallpapers => DieWithASmileSave.Data.CustomWallpapers;

		internal static void Scan()
		{
			DieWithASmileSave.EnsureLoaded();
			DieWithASmileSave.EnsureFolders();
			SyncFolder(DieWithASmileSave.LogoFolder, DieWithASmileSave.Data.CustomLogos, "logo:");
			SyncFolder(DieWithASmileSave.WallpaperFolder, DieWithASmileSave.Data.CustomWallpapers, "wall:");
			if (!string.IsNullOrEmpty(DieWithASmileSave.Data.CustomLogoId) &&
			    DieWithASmileSave.Data.CustomLogos.All(item => item.Id != DieWithASmileSave.Data.CustomLogoId))
				DieWithASmileSave.Data.CustomLogoId = "";
			if (!string.IsNullOrEmpty(DieWithASmileSave.Data.CustomWallpaperId) &&
			    DieWithASmileSave.Data.CustomWallpapers.All(item => item.Id != DieWithASmileSave.Data.CustomWallpaperId))
				DieWithASmileSave.Data.CustomWallpaperId = "";
			CalamitasMenuForeign.DropMissing();
		}

		internal static bool TryGetSelectedLogo(out Texture2D texture)
		{
			texture = TextureOf(DieWithASmileSave.LogoFolder, DieWithASmileSave.Data.CustomLogoId, DieWithASmileSave.Data.CustomLogos);
			return texture != null;
		}

		internal static bool TryGetSelectedWallpaper(out Texture2D texture)
		{
			texture = TextureOf(DieWithASmileSave.WallpaperFolder, DieWithASmileSave.Data.CustomWallpaperId, DieWithASmileSave.Data.CustomWallpapers);
			return texture != null;
		}

		internal static Texture2D TextureOf(CustomArtRecord record, bool logo)
		{
			if (record == null)
				return null;

			return TextureOf(logo ? DieWithASmileSave.LogoFolder : DieWithASmileSave.WallpaperFolder, record.Id, logo ? DieWithASmileSave.Data.CustomLogos : DieWithASmileSave.Data.CustomWallpapers);
		}

		internal static bool TryImportLogo()
		{
			if (!CalamitasMenuLibrary.TryPickImageFile(out string path))
				return false;

			CustomArtRecord record = ImportFile(path, DieWithASmileSave.LogoFolder, DieWithASmileSave.Data.CustomLogos, "logo:");
			if (record == null)
				return false;

			DieWithASmileSettings.SetCustomLogo(record.Id);
			return true;
		}

		internal static bool TryImportWallpaper()
		{
			if (!CalamitasMenuLibrary.TryPickImageFile(out string path))
				return false;

			CustomArtRecord record = ImportFile(path, DieWithASmileSave.WallpaperFolder, DieWithASmileSave.Data.CustomWallpapers, "wall:");
			if (record == null)
				return false;

			DieWithASmileSettings.SetCustomWallpaper(record.Id);
			return true;
		}

		internal static void DeleteLogo(CustomArtRecord record)
		{
			DeleteRecord(record, DieWithASmileSave.LogoFolder, DieWithASmileSave.Data.CustomLogos);
			if (DieWithASmileSave.Data.CustomLogoId == record.Id)
				DieWithASmileSave.Data.CustomLogoId = "";
			DieWithASmileSave.Save();
		}

		internal static void DeleteWallpaper(CustomArtRecord record)
		{
			DeleteRecord(record, DieWithASmileSave.WallpaperFolder, DieWithASmileSave.Data.CustomWallpapers);
			if (DieWithASmileSave.Data.CustomWallpaperId == record.Id)
				DieWithASmileSave.Data.CustomWallpaperId = "";
			DieWithASmileSave.Save();
		}

		internal static void Unload()
		{
			foreach (Texture2D tex in _cache.Values) {
				Texture2D local = tex;
				Main.QueueMainThreadAction(() => {
					try {
						if (local != null && !local.IsDisposed)
							local.Dispose();
					}
					catch {
					}
				});
			}

			_cache.Clear();
			_times.Clear();
		}

		private static void SyncFolder(string folder, List<CustomArtRecord> records, string prefix)
		{
			var files = Directory.Exists(folder)
				? Directory.GetFiles(folder)
					.Where(path => ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
					.Select(Path.GetFileName)
					.Where(name => !string.IsNullOrEmpty(name))
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			records.RemoveAll(item => !files.Contains(item.FileName));
			foreach (string fileName in files) {
				if (records.Any(item => string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
					continue;

				records.Add(new CustomArtRecord {
					Id = prefix + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(),
					FileName = fileName
				});
			}

			EnsureUniqueIds(records, prefix);
		}

		private static void EnsureUniqueIds(List<CustomArtRecord> records, string prefix)
		{
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (CustomArtRecord record in records) {
				if (string.IsNullOrEmpty(record.Id) || !used.Add(record.Id)) {
					string baseId = prefix + Path.GetFileNameWithoutExtension(record.FileName).ToLowerInvariant();
					string id = baseId;
					int n = 2;
					while (!used.Add(id))
						id = baseId + "_" + n++;
					record.Id = id;
				}
			}
		}

		private static CustomArtRecord ImportFile(string source, string folder, List<CustomArtRecord> records, string prefix)
		{
			try {
				DieWithASmileSave.EnsureFolders();
				string ext = Path.GetExtension(source).ToLowerInvariant();
				if (!ImageExtensions.Contains(ext))
					return null;

				string dest = UniquePath(folder, Path.GetFileName(source));
				File.Copy(source, dest, overwrite: false);
				var record = new CustomArtRecord {
					Id = prefix + Path.GetFileNameWithoutExtension(dest).ToLowerInvariant(),
					FileName = Path.GetFileName(dest)
				};
				records.RemoveAll(item => string.Equals(item.FileName, record.FileName, StringComparison.OrdinalIgnoreCase));
				records.Add(record);
				EnsureUniqueIds(records, prefix);
				DieWithASmileSave.Save();
				return record;
			}
			catch {
				return null;
			}
		}

		private static void DeleteRecord(CustomArtRecord record, string folder, List<CustomArtRecord> records)
		{
			if (record == null)
				return;

			string path = Path.Combine(folder, record.FileName);
			try {
				if (File.Exists(path)) {
					File.SetAttributes(path, FileAttributes.Normal);
					File.Delete(path);
				}
			}
			catch {
			}

			if (_cache.Remove(path, out Texture2D tex)) {
				Main.QueueMainThreadAction(() => {
					try {
						if (tex != null && !tex.IsDisposed)
							tex.Dispose();
					}
					catch {
					}
				});
			}

			_times.Remove(path);
			records.RemoveAll(item => item.Id == record.Id || string.Equals(item.FileName, record.FileName, StringComparison.OrdinalIgnoreCase));
		}

		private static Texture2D TextureOf(string folder, string id, List<CustomArtRecord> records)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			CustomArtRecord record = records.FirstOrDefault(item => item.Id == id);
			if (record == null)
				return null;

			string path = Path.Combine(folder, record.FileName);
			if (!File.Exists(path))
				return null;

			DateTime write = File.GetLastWriteTimeUtc(path);
			if (_cache.TryGetValue(path, out Texture2D cached) && cached != null && !cached.IsDisposed &&
			    _times.TryGetValue(path, out DateTime known) && known == write)
				return cached;

			try {
				using FileStream stream = File.OpenRead(path);
				Texture2D tex = Texture2D.FromStream(Main.instance.GraphicsDevice, stream);
				if (cached != null && !cached.IsDisposed)
					cached.Dispose();
				_cache[path] = tex;
				_times[path] = write;
				return tex;
			}
			catch {
				return null;
			}
		}

		private static string UniquePath(string folder, string fileName)
		{
			string dest = Path.Combine(folder, fileName);
			if (!File.Exists(dest))
				return dest;

			string name = Path.GetFileNameWithoutExtension(fileName);
			string ext = Path.GetExtension(fileName);
			for (int i = 2; i < 100; i++) {
				dest = Path.Combine(folder, $"{name}_{i}{ext}");
				if (!File.Exists(dest))
					return dest;
			}

			return Path.Combine(folder, $"{name}_{Guid.NewGuid():N}{ext}");
		}
	}
}

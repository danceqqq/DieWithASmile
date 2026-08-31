using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Audio;
using NLayer;
using NVorbis;
using OggVorbisEncoder;
using Terraria.Audio;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuLibrary
	{
		private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };
		private static readonly object ImportLock = new();

		internal static volatile bool Importing;
		internal static volatile float ImportProgress;
		internal static volatile string ImportStatus = "";
		internal static volatile string ImportTitle = "";
		internal static volatile string ImportError = "";
		internal static string LastNotice = "";
		internal static CancellationTokenSource ImportCancel;

		internal static string MusicFolder
		{
			get
			{
				DieWithASmileSave.EnsureFolders();
				return DieWithASmileSave.MusicFolder;
			}
		}

		internal static void ScanIntoSave()
		{
			DieWithASmileSave.EnsureLoaded();
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			string folder = MusicFolder;
			var files = Directory.Exists(folder)
				? Directory.GetFiles(folder)
					.Where(path => AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
					.Select(Path.GetFileName)
					.Where(name => !string.IsNullOrEmpty(name))
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			data.CustomTracks.RemoveAll(track => !files.Contains(track.FileName));

			foreach (string fileName in files) {
				if (data.CustomTracks.Any(track => string.Equals(track.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
					continue;

				string path = FullPath(fileName);
				if (!LooksLikeAudio(path))
					continue;

				data.CustomTracks.Add(new CustomTrackRecord {
					Id = "custom:" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(),
					FileName = fileName,
					Title = Prettify(Path.GetFileNameWithoutExtension(fileName)),
					Artist = "Custom"
				});
			}

			EnsureUniqueCustomIds(data);
		}

		internal static string FullPath(string fileName) => Path.Combine(MusicFolder, fileName);

		internal static void OpenMusicFolder() => OpenFolder(MusicFolder);

		internal static void OpenBrokenFolder() => OpenFolder(DieWithASmileSave.BrokenFolder);

		internal static void Quarantine(MenuTrack track)
		{
			if (track == null || !track.IsCustom)
				return;

			CalamitasMenuCustomAudio.Stop();
			string source = track.AudioPath;
			string fileName = track.FileName;
			DieWithASmileSave.EnsureFolders();
			string dest = UniqueBrokenPath(fileName);
			bool moved = false;
			for (int i = 0; i < 6; i++) {
				try {
					if (File.Exists(source)) {
						File.SetAttributes(source, FileAttributes.Normal);
						File.Move(source, dest);
						moved = true;
					}

					break;
				}
				catch {
					System.Threading.Thread.Sleep(30);
				}
			}

			DieWithASmileSaveData data = DieWithASmileSave.Data;
			data.CustomTracks.RemoveAll(item => item.Id == track.Id || string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase));
			data.DisabledCustomIds.Remove(track.Id);
			if (data.LoopedTrackId == track.Id) {
				data.LoopEnabled = false;
				data.LoopedTrackId = "";
			}

			DieWithASmileSave.Save();
			string name = string.IsNullOrWhiteSpace(track.Title) ? fileName : track.Title;
			LastNotice = moved
				? $"Couldn't play \"{name}\". Moved it to the Broken folder so the mod can load."
				: $"Couldn't play \"{name}\". Disable or delete it from the playlist, or remove it from the Music folder.";
		}

		internal static bool TryPickAudioFile(out string path) => TryPickFile(out path, ShowOpenDialog);

		internal static bool TryPickImageFile(out string path) => TryPickFile(out path, ShowOpenImageDialog);

		private static bool TryPickFile(out string path, Func<string> picker)
		{
			path = null;
			string picked = null;
			var thread = new Thread(() => picked = picker());
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if (string.IsNullOrEmpty(picked) || !File.Exists(picked))
				return false;

			path = picked;
			return true;
		}

		internal static void StartImport(string sourcePath)
		{
			lock (ImportLock) {
				if (Importing)
					return;

				Importing = true;
				ImportProgress = 0f;
				ImportError = "";
				ImportTitle = Path.GetFileNameWithoutExtension(sourcePath);
				ImportStatus = "Preparing...";
				ImportCancel = new CancellationTokenSource();
				CancellationToken token = ImportCancel.Token;
				Task.Run(() => {
					try {
						ImportFile(sourcePath, token);
					}
					catch (OperationCanceledException) {
						ImportStatus = "Cancelled";
					}
					catch (Exception e) {
						ImportError = e.Message;
						ImportStatus = "Failed";
					}
					finally {
						Importing = false;
					}
				}, token);
			}
		}

		internal static void CancelImport()
		{
			try {
				ImportCancel?.Cancel();
			}
			catch {
			}
		}

		internal static IAudioTrack OpenTrack(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;

			var stream = new MemoryStream(File.ReadAllBytes(path), writable: false);
			string ext = Path.GetExtension(path).ToLowerInvariant();
			try {
				return ext switch {
					".mp3" => new MP3AudioTrack(stream),
					".wav" => CreateWavTrack(stream),
					_ => new OGGAudioTrack(stream)
				};
			}
			catch {
				stream.Dispose();
				throw;
			}
		}

		private static IAudioTrack CreateWavTrack(Stream stream)
		{
			try {
				return new WAVAudioTrack(stream);
			}
			catch {
				stream.Position = 0;
				return new OGGAudioTrack(stream);
			}
		}

		private static void ImportFile(string sourcePath, CancellationToken token)
		{
			DieWithASmileSave.EnsureFolders();
			string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
			if (string.IsNullOrEmpty(ext) || !AudioExtensions.Contains(ext))
				ext = ".ogg";

			string baseName = UniqueFileName(Path.GetFileNameWithoutExtension(sourcePath));
			string destName = baseName + ext;
			string destPath = FullPath(destName);
			string tempPath = destPath + ".part";

			try {
				ImportStatus = ext == ".mp3" ? "Importing MP3..." : ext == ".wav" ? "Importing WAV..." : "Importing OGG...";
				if (ext == ".ogg") {
					CopyWithProgress(sourcePath, tempPath, token);
				}
				else {
					try {
						ImportStatus = ext == ".mp3" ? "Converting MP3 to OGG..." : "Converting WAV to OGG...";
						EncodeToOgg(sourcePath, tempPath, ext, token);
						destName = baseName + ".ogg";
						destPath = FullPath(destName);
					}
					catch {
						ImportStatus = "Saving original file...";
						CopyWithProgress(sourcePath, tempPath, token);
						destName = baseName + ext;
						destPath = FullPath(destName);
					}
				}

				token.ThrowIfCancellationRequested();
				if (File.Exists(destPath))
					File.Delete(destPath);
				File.Move(tempPath, destPath);

				if (!CanPlay(destPath)) {
					if (ext == ".ogg")
						throw new InvalidDataException("Could not read the selected audio file.");

					ImportStatus = "Using original file...";
					try {
						if (File.Exists(destPath))
							File.Delete(destPath);
					}
					catch {
					}

					destName = baseName + ext;
					destPath = FullPath(destName);
					CopyWithProgress(sourcePath, destPath, token);
				}

				ImportProgress = 1f;
				ImportStatus = "Added to playlist";
				var tags = ReadTags(destPath);
				string title = string.IsNullOrWhiteSpace(tags.Title)
					? Prettify(Path.GetFileNameWithoutExtension(sourcePath))
					: tags.Title;
				MainThreadAddTrack(destName, title, string.IsNullOrWhiteSpace(tags.Artist) ? "Custom" : tags.Artist);
			}
			finally {
				try {
					if (File.Exists(tempPath))
						File.Delete(tempPath);
				}
				catch {
				}
			}
		}

		private static bool CanPlay(string path)
		{
			try {
				using IAudioTrack track = OpenTrack(path);
				if (track == null)
					return false;

				track.Reuse();
				track.Play();
				bool ok = track.IsPlaying || !track.IsStopped;
				track.Stop(AudioStopOptions.Immediate);
				return ok;
			}
			catch {
				return false;
			}
		}

		private static void MainThreadAddTrack(string fileName, string title, string artist)
		{
			Terraria.Main.QueueMainThreadAction(() => {
				DieWithASmileSaveData data = DieWithASmileSave.Data;
				if (!data.CustomTracks.Any(track => string.Equals(track.FileName, fileName, StringComparison.OrdinalIgnoreCase))) {
					data.CustomTracks.Add(new CustomTrackRecord {
						Id = "custom:" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(),
						FileName = fileName,
						Title = title,
						Artist = string.IsNullOrWhiteSpace(artist) ? "Custom" : artist
					});
					EnsureUniqueCustomIds(data);
				}

				data.DisabledCustomIds.RemoveAll(id => data.CustomTracks.Any(track =>
					track.FileName == fileName && track.Id == id));
				DieWithASmileSave.Save();
				CalamitasMenuPlaylist.Rebuild(playId: data.CustomTracks.FirstOrDefault(t => t.FileName == fileName)?.Id);
			});
		}

		private static void CopyWithProgress(string source, string dest, CancellationToken token)
		{
			using FileStream input = File.OpenRead(source);
			using FileStream output = File.Create(dest);
			var buffer = new byte[64 * 1024];
			long total = Math.Max(1, input.Length);
			long copied = 0;
			int read;
			while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
				token.ThrowIfCancellationRequested();
				output.Write(buffer, 0, read);
				copied += read;
				ImportProgress = copied / (float)total * 0.98f;
			}
		}

		private static void EncodeToOgg(string source, string dest, string ext, CancellationToken token)
		{
			if (ext == ".mp3")
				EncodeMp3(source, dest, token);
			else
				EncodeWav(source, dest, token);
		}

		private static void EncodeMp3(string source, string dest, CancellationToken token)
		{
			using var mpeg = new MpegFile(source);
			int channels = Math.Clamp(mpeg.Channels, 1, 2);
			int sampleRate = mpeg.SampleRate > 0 ? mpeg.SampleRate : 44100;
			long totalSamples = mpeg.Length > 0 ? mpeg.Length : 0;
			EncodePcmStream(dest, channels, sampleRate, token, (buffer, frames) => {
				int want = frames * mpeg.Channels;
				var scratch = new float[want];
				int read = mpeg.ReadSamples(scratch, 0, want);
				if (read <= 0)
					return 0;

				int got = read / Math.Max(1, mpeg.Channels);
				Downmix(scratch, mpeg.Channels, buffer, channels, got);
				if (totalSamples > 0)
					ImportProgress = MathHelperClamp(mpeg.Position / (float)totalSamples, 0f, 0.98f);
				else
					ImportProgress = Math.Min(0.95f, ImportProgress + 0.01f);
				return got;
			});
		}

		private static void EncodeWav(string source, string dest, CancellationToken token)
		{
			using FileStream fs = File.OpenRead(source);
			using var reader = new BinaryReader(fs, System.Text.Encoding.ASCII, leaveOpen: true);
			if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
				throw new InvalidDataException("Not a WAV file.");
			reader.ReadInt32();
			if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
				throw new InvalidDataException("Not a WAV file.");

			short format = 1;
			short channels = 2;
			int sampleRate = 44100;
			short bits = 16;
			int dataSize = 0;
			long dataOffset = 0;
			while (fs.Position + 8 <= fs.Length) {
				string id = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
				int size = reader.ReadInt32();
				long next = fs.Position + size;
				if (id == "fmt ") {
					format = reader.ReadInt16();
					channels = reader.ReadInt16();
					sampleRate = reader.ReadInt32();
					reader.ReadInt32();
					reader.ReadInt16();
					bits = reader.ReadInt16();
				}
				else if (id == "data") {
					dataSize = size;
					dataOffset = fs.Position;
					break;
				}

				fs.Position = next + (size & 1);
			}

			if (format != 1 && format != 3)
				throw new InvalidDataException("Unsupported WAV format.");
			if (dataOffset <= 0)
				throw new InvalidDataException("WAV has no data chunk.");

			fs.Position = dataOffset;
			int srcChannels = Math.Max(1, (int)channels);
			int outChannels = Math.Clamp(srcChannels, 1, 2);
			int bytesPerSample = Math.Max(1, bits / 8);
			int frameBytes = bytesPerSample * srcChannels;
			long framesTotal = dataSize > 0 ? dataSize / frameBytes : Math.Max(1, (fs.Length - dataOffset) / frameBytes);
			long framesDone = 0;
			var raw = new byte[1024 * frameBytes];

			EncodePcmStream(dest, outChannels, sampleRate, token, (buffer, frames) => {
				int toRead = Math.Min(frames, raw.Length / frameBytes) * frameBytes;
				int read = fs.Read(raw, 0, toRead);
				if (read < frameBytes)
					return 0;

				int got = read / frameBytes;
				for (int i = 0; i < got; i++) {
					for (int c = 0; c < outChannels; c++) {
						int src = Math.Min(c, srcChannels - 1);
						int offset = i * frameBytes + src * bytesPerSample;
						buffer[c][i] = bits == 8
							? (raw[offset] - 128) / 128f
							: format == 3
								? BitConverter.ToSingle(raw, offset)
								: BitConverter.ToInt16(raw, offset) / 32768f;
					}
				}

				framesDone += got;
				ImportProgress = MathHelperClamp(framesDone / (float)framesTotal, 0f, 0.98f);
				return got;
			});
		}

		private static void EncodePcmStream(
			string dest,
			int channels,
			int sampleRate,
			CancellationToken token,
			Func<float[][], int, int> readFrames)
		{
			ImportStatus = "Encoding OGG...";
			const int chunk = 1024;
			var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, 0.4f);
			var oggStream = new OggStream(Random.Shared.Next());
			var comments = new Comments();
			comments.AddTag("ENCODER", "Die With A Smile");
			oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
			oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
			oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));

			using FileStream output = File.Create(dest);
			FlushPages(oggStream, output, true);
			var processing = ProcessingState.Create(info);
			var planar = new float[channels][];
			for (int i = 0; i < channels; i++)
				planar[i] = new float[chunk];

			int read;
			while ((read = readFrames(planar, chunk)) > 0) {
				token.ThrowIfCancellationRequested();
				processing.WriteData(planar, read);
				while (!oggStream.Finished && processing.PacketOut(out OggPacket packet)) {
					oggStream.PacketIn(packet);
					FlushPages(oggStream, output, false);
				}
			}

			processing.WriteEndOfStream();
			while (!oggStream.Finished && processing.PacketOut(out OggPacket packet)) {
				oggStream.PacketIn(packet);
				FlushPages(oggStream, output, false);
			}

			FlushPages(oggStream, output, true);
			ImportProgress = 0.99f;
			ImportStatus = "Saving...";
		}

		private static void FlushPages(OggStream oggStream, Stream output, bool force)
		{
			while (oggStream.PageOut(out OggPage page, force)) {
				output.Write(page.Header, 0, page.Header.Length);
				output.Write(page.Body, 0, page.Body.Length);
			}
		}

		private static void Downmix(float[] interleaved, int srcChannels, float[][] dest, int destChannels, int frames)
		{
			for (int i = 0; i < frames; i++) {
				if (srcChannels == 1) {
					float sample = interleaved[i];
					dest[0][i] = sample;
					if (destChannels > 1)
						dest[1][i] = sample;
				}
				else {
					dest[0][i] = interleaved[i * srcChannels];
					if (destChannels > 1)
						dest[1][i] = interleaved[i * srcChannels + 1];
				}
			}
		}

		private static string UniqueFileName(string raw)
		{
			string safe = new string(raw.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
			if (string.IsNullOrWhiteSpace(safe))
				safe = "track";

			string candidate = safe;
			int n = 2;
			while (AudioExtensions.Any(ext => File.Exists(FullPath(candidate + ext))) || File.Exists(FullPath(candidate + ".part"))) {
				candidate = safe + "_" + n;
				n++;
			}

			return candidate;
		}

		private static void EnsureUniqueCustomIds(DieWithASmileSaveData data)
		{
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (CustomTrackRecord track in data.CustomTracks) {
				if (string.IsNullOrWhiteSpace(track.Id) || !used.Add(track.Id)) {
					string next = "custom:" + Path.GetFileNameWithoutExtension(track.FileName).ToLowerInvariant();
					int i = 2;
					while (!used.Add(next)) {
						next = "custom:" + Path.GetFileNameWithoutExtension(track.FileName).ToLowerInvariant() + "_" + i;
						i++;
					}

					track.Id = next;
				}
			}
		}

		private static string Prettify(string name)
		{
			name = (name ?? "Custom track").Replace('_', ' ').Replace('-', ' ').Trim();
			return string.IsNullOrEmpty(name) ? "Custom track" : name;
		}

		private static bool LooksLikeAudio(string path)
		{
			try {
				var info = new FileInfo(path);
				return info.Exists && info.Length > 256;
			}
			catch {
				return false;
			}
		}

		private static (string Title, string Artist) ReadTags(string path)
		{
			try {
				string ext = Path.GetExtension(path).ToLowerInvariant();
				if (ext == ".ogg") {
					using var vorbis = new VorbisReader(path);
					return (CleanTag(vorbis.Tags?.Title), CleanTag(vorbis.Tags?.Artist));
				}

				if (ext == ".mp3")
					return ReadId3(path);
			}
			catch {
			}

			return (null, null);
		}

		private static (string Title, string Artist) ReadId3(string path)
		{
			using FileStream fs = File.OpenRead(path);
			if (fs.Length < 128)
				return (null, null);

			fs.Seek(-128, SeekOrigin.End);
			var buf = new byte[128];
			if (fs.Read(buf, 0, 128) < 128)
				return (null, null);
			if (System.Text.Encoding.ASCII.GetString(buf, 0, 3) != "TAG")
				return (null, null);

			return (CleanTag(System.Text.Encoding.Latin1.GetString(buf, 3, 30)), CleanTag(System.Text.Encoding.Latin1.GetString(buf, 33, 30)));
		}

		private static string CleanTag(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			value = value.Replace('\0', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
			return string.IsNullOrEmpty(value) ? null : value;
		}

		private static void OpenFolder(string folder)
		{
			try {
				Directory.CreateDirectory(folder);
				Process.Start(new ProcessStartInfo {
					FileName = folder,
					UseShellExecute = true
				});
			}
			catch {
			}
		}

		private static string UniqueBrokenPath(string fileName)
		{
			string dest = Path.Combine(DieWithASmileSave.BrokenFolder, fileName);
			if (!File.Exists(dest))
				return dest;

			string name = Path.GetFileNameWithoutExtension(fileName);
			string ext = Path.GetExtension(fileName);
			for (int i = 2; i < 100; i++) {
				dest = Path.Combine(DieWithASmileSave.BrokenFolder, $"{name}_{i}{ext}");
				if (!File.Exists(dest))
					return dest;
			}

			return Path.Combine(DieWithASmileSave.BrokenFolder, $"{name}_{Guid.NewGuid():N}{ext}");
		}

		private static float MathHelperClamp(float value, float min, float max) =>
			value < min ? min : value > max ? max : value;

		private static string ShowOpenDialog()
		{
			var ofn = new OpenFileName();
			ofn.lStructSize = Marshal.SizeOf<OpenFileName>();
			ofn.lpstrFilter = "Audio (*.ogg;*.mp3;*.wav)\0*.ogg;*.mp3;*.wav\0Ogg\0*.ogg\0MP3\0*.mp3\0WAV\0*.wav\0";
			ofn.lpstrFile = new string('\0', 1024);
			ofn.nMaxFile = ofn.lpstrFile.Length;
			ofn.lpstrTitle = "Upload a song";
			ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800;
			return GetOpenFileName(ref ofn) ? ofn.lpstrFile.Split('\0')[0] : null;
		}

		private static string ShowOpenImageDialog()
		{
			var ofn = new OpenFileName();
			ofn.lStructSize = Marshal.SizeOf<OpenFileName>();
			ofn.lpstrFilter = "Images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0PNG\0*.png\0JPEG\0*.jpg;*.jpeg\0";
			ofn.lpstrFile = new string('\0', 1024);
			ofn.nMaxFile = ofn.lpstrFile.Length;
			ofn.lpstrTitle = "Upload an image";
			ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800;
			return GetOpenFileName(ref ofn) ? ofn.lpstrFile.Split('\0')[0] : null;
		}

		[DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool GetOpenFileName(ref OpenFileName ofn);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct OpenFileName
		{
			public int lStructSize;
			public IntPtr hwndOwner;
			public IntPtr hInstance;
			public string lpstrFilter;
			public string lpstrCustomFilter;
			public int nMaxCustFilter;
			public int nFilterIndex;
			public string lpstrFile;
			public int nMaxFile;
			public string lpstrFileTitle;
			public int nMaxFileTitle;
			public string lpstrInitialDir;
			public string lpstrTitle;
			public int Flags;
			public short nFileOffset;
			public short nFileExtension;
			public string lpstrDefExt;
			public IntPtr lCustData;
			public IntPtr lpfnHook;
			public string lpTemplateName;
			public IntPtr pvReserved;
			public int dwReserved;
			public int FlagsEx;
		}
	}
}

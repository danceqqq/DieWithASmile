using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal sealed class MenuTrack
	{
		public string Id;
		public string Path;
		public string FileName;
		public string Title;
		public string Artist;
		public string CoverArtist;
		public float StartSeconds;
		public bool IsCustom;
		public bool Enabled = true;

		internal string AudioPath => IsCustom ? CalamitasMenuLibrary.FullPath(FileName) : Path;
	}

	internal static class CalamitasMenuPlaylist
	{
		internal static readonly MenuTrack[] BuiltIn =
		{
			new() {
				Id = "dwas",
				Path = "Assets/Music/DieWithASmileMenu",
				Title = "Die With A Smile",
				Artist = "Lady Gaga & Bruno Mars",
				CoverArtist = "Celine Wanyi",
				StartSeconds = 127f
			},
			new() {
				Id = "survive",
				Path = "Assets/Music/IWillSurviveMenu",
				Title = "I Will Survive",
				Artist = "Gloria Gaynor",
				CoverArtist = "Jennel Garcia"
			},
			new() {
				Id = "goodbye",
				Path = "Assets/Music/GoodbyeToAWorldMenu",
				Title = "Goodbye To A World",
				Artist = "Porter Robinson"
			},
			new() {
				Id = "dontforget",
				Path = "Assets/Music/DontForgetMenu",
				Title = "Don't Forget",
				Artist = "Toby Fox · Laura Shigihara"
			},
			new() {
				Id = "comealong",
				Path = "Assets/Music/ComeAlongWithMeMenu",
				Title = "Come Along with Me",
				Artist = "Ashley Eriksson",
				CoverArtist = "Ariela"
			}
		};

		private static readonly List<MenuTrack> _active = new();
		private static FieldInfo _vorbisField;
		private static FieldInfo _mp3StreamField;
		private static bool _paused;
		private static bool _loop;
		private static bool _shuffle;
		private static int _index;
		private static string _loopedId = "";
		private static float _fallbackTime;
		private static float _fallbackStamp;
		private static int _forceStartFrames;
		private static float _cachedDuration;
		private static string _cachedDurationId = "";
		private static Mod _mod;
		private static IAudioTrack _customTrack;
		private static string _customPath;
		private static bool _playerWasEnabled = true;
		private static bool _pendingStart;
		private static bool _returnFromWorld;
		private static bool _visitedWorld;
		private static bool _menuAudioStarted;
		private static int _customRetryDelay;
		private static float _mix;
		private static uint _lifeFrame = uint.MaxValue;
		private static readonly List<int> _shuffleHistory = new();
		private static float _scanTimer;
		private static int _ignoreEndFrames;
		private static int _customPlayFails;
		private static string _customFailId = "";

		internal static IReadOnlyList<MenuTrack> Active => _active;
		internal static int CurrentIndex => _index;
		internal static MenuTrack Current => _active.Count == 0 ? BuiltIn[0] : _active[Math.Clamp(_index, 0, _active.Count - 1)];
		internal static bool IsDontForget => Current.Id == "dontforget";
		internal static bool IsComeAlong => Current.Id == "comealong";
		internal static bool IsPaused => _paused;
		internal static bool LoopEnabled => _loop;
		internal static bool ShuffleEnabled => _shuffle;
		internal static float OutputMix => MathHelper.Clamp(_mix, 0f, 1f);
		internal static int MenuMusicId
		{
			get
			{
				if (!Main.gameMenu || CalamitasMenuPersist.MenuStillLoading || !_menuAudioStarted)
					return 0;

				if (!DieWithASmileSettings.PlayerEnabled || _paused || Current.IsCustom)
					return 0;

				return CurrentSlot;
			}
		}
		internal static int CurrentSlot =>
			_mod == null || Current.IsCustom ? 0 : MusicLoader.GetMusicSlot(_mod, Current.Path);

		private static int _skipDepth;

		internal static void Load(Mod mod)
		{
			_mod = mod;
			_vorbisField = typeof(OGGAudioTrack).GetField("_vorbisReader", BindingFlags.Instance | BindingFlags.NonPublic);
			_mp3StreamField = typeof(MP3AudioTrack).GetField("_mp3Stream", BindingFlags.Instance | BindingFlags.NonPublic);
			DieWithASmileSave.EnsureLoaded();
			RecoverCrashedCustom();
			try {
				Rebuild(play: false);
			}
			catch {
				_active.Clear();
				_active.Add(BuiltIn[0]);
				_index = 0;
			}
		}

		private static void RecoverCrashedCustom()
		{
			string fileName = DieWithASmileSave.ReadPlayGuard();
			DieWithASmileSave.ClearPlayGuard();
			if (string.IsNullOrEmpty(fileName))
				return;

			DieWithASmileSaveData data = DieWithASmileSave.Data;
			CustomTrackRecord record = data.CustomTracks.FirstOrDefault(item =>
				string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase));
			var track = new MenuTrack {
				Id = record?.Id ?? ("custom:" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant()),
				FileName = fileName,
				Title = record?.Title ?? fileName,
				IsCustom = true
			};
			CalamitasMenuLibrary.Quarantine(track);
		}

		internal static void Unload()
		{
			DisposeCustom();
			CalamitasMenuCustomAudio.Stop();
			_mod = null;
		}

		internal static void Rebuild(string playId = null, bool play = true)
		{
			string keepId = playId ?? Current?.Id;
			CalamitasMenuLibrary.ScanIntoSave();
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			_active.Clear();

			foreach (MenuTrack built in BuiltIn) {
				built.Enabled = !data.DisabledBuiltInIds.Contains(built.Id);
				if (built.Enabled)
					_active.Add(built);
			}

			foreach (CustomTrackRecord record in data.CustomTracks) {
				var track = new MenuTrack {
					Id = record.Id,
					FileName = record.FileName,
					Title = string.IsNullOrWhiteSpace(record.Title) ? record.FileName : record.Title,
					Artist = string.IsNullOrWhiteSpace(record.Artist) ? "Custom" : record.Artist,
					IsCustom = true,
					Enabled = !data.DisabledCustomIds.Contains(record.Id)
				};
				if (track.Enabled && File.Exists(track.AudioPath))
					_active.Add(track);
			}

			if (_active.Count == 0)
				_active.Add(BuiltIn[0]);

			_loop = data.LoopEnabled && _active.Any(track => track.Id == data.LoopedTrackId);
			_loopedId = _loop ? data.LoopedTrackId : "";
			_shuffle = data.ShuffleEnabled;
			int next = Math.Max(0, _active.FindIndex(track => track.Id == keepId));
			if (_loop && !string.IsNullOrEmpty(_loopedId)) {
				int looped = _active.FindIndex(track => track.Id == _loopedId);
				if (looped >= 0)
					next = looped;
			}

			if (!play) {
				_index = next;
				return;
			}

			if (playId != null || _index != next || Current.Id != keepId)
				PlayIndex(next, fromThemeStart: playId != null, blend: false);
			else
				_index = next;
		}

		internal static IReadOnlyList<MenuTrack> Catalog()
		{
			CalamitasMenuLibrary.ScanIntoSave();
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			var list = new List<MenuTrack>();
			foreach (MenuTrack built in BuiltIn) {
				built.Enabled = !data.DisabledBuiltInIds.Contains(built.Id);
				list.Add(built);
			}

			foreach (CustomTrackRecord record in data.CustomTracks) {
				list.Add(new MenuTrack {
					Id = record.Id,
					FileName = record.FileName,
					Title = string.IsNullOrWhiteSpace(record.Title) ? record.FileName : record.Title,
					Artist = string.IsNullOrWhiteSpace(record.Artist) ? "Custom" : record.Artist,
					IsCustom = true,
					Enabled = !data.DisabledCustomIds.Contains(record.Id)
				});
			}

			return list;
		}

		internal static void SetEnabled(MenuTrack track, bool enabled)
		{
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			List<string> bucket = track.IsCustom ? data.DisabledCustomIds : data.DisabledBuiltInIds;
			if (enabled)
				bucket.Remove(track.Id);
			else if (!bucket.Contains(track.Id))
				bucket.Add(track.Id);

			DieWithASmileSave.Save();
			Rebuild();
		}

		internal static void SetArtist(MenuTrack track, string artist)
		{
			if (track == null || !track.IsCustom)
				return;

			artist = (artist ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
			if (artist.Length > 48)
				artist = artist[..48];
			if (string.IsNullOrEmpty(artist))
				artist = "Custom";

			DieWithASmileSaveData data = DieWithASmileSave.Data;
			CustomTrackRecord record = data.CustomTracks.FirstOrDefault(item => item.Id == track.Id)
				?? data.CustomTracks.FirstOrDefault(item => string.Equals(item.FileName, track.FileName, StringComparison.OrdinalIgnoreCase));
			if (record != null)
				record.Artist = artist;

			track.Artist = artist;
			MenuTrack live = _active.FirstOrDefault(item => item.Id == track.Id);
			if (live != null)
				live.Artist = artist;

			DieWithASmileSave.Save();
		}

		internal static void DeleteCustom(MenuTrack track)
		{
			if (!track.IsCustom)
				return;

			string path = track.AudioPath;
			if (Current.IsCustom && Current.Id == track.Id) {
				try {
					GetTrack()?.Stop(AudioStopOptions.Immediate);
				}
				catch {
				}

				DisposeCustom();
			}

			for (int i = 0; i < 8; i++) {
				try {
					if (File.Exists(path)) {
						File.SetAttributes(path, FileAttributes.Normal);
						File.Delete(path);
					}

					break;
				}
				catch {
					DisposeCustom();
					System.Threading.Thread.Sleep(30);
				}
			}

			DieWithASmileSaveData data = DieWithASmileSave.Data;
			data.CustomTracks.RemoveAll(item => item.Id == track.Id || string.Equals(item.FileName, track.FileName, StringComparison.OrdinalIgnoreCase));
			data.DisabledCustomIds.Remove(track.Id);
			if (data.LoopedTrackId == track.Id) {
				data.LoopEnabled = false;
				data.LoopedTrackId = "";
			}

			DieWithASmileSave.Save();
			Rebuild();
		}

		internal static void OnThemeSelected()
		{
			Rebuild(play: false);
			if (!DieWithASmileSettings.PlayerEnabled) {
				Silence();
				return;
			}

			if (_menuAudioStarted)
				return;

			_pendingStart = true;
		}

		internal static void ApplyPlayerEnabled(bool enabled)
		{
			if (enabled) {
				if (!_playerWasEnabled && Main.gameMenu)
					OnThemeSelected();
			}
			else {
				Silence();
			}

			_playerWasEnabled = enabled;
		}

		internal static void PauseBuiltInTrack()
		{
			try {
				IAudioTrack track = GetTrack();
				if (track != null && track.IsPlaying)
					track.Pause();
			}
			catch {
			}
		}

		internal static void Silence()
		{
			_menuAudioStarted = false;
			_mix = 0f;
			try {
				IAudioTrack track = GetTrack();
				if (track != null && track.IsPlaying)
					track.Pause();
			}
			catch {
			}

			CalamitasMenuCustomAudio.Stop();
			MuteVanillaMusic();
		}

		internal static void MuteVanillaMusic(int keepSlot = -1)
		{
			Main.newMusic = keepSlot > 0 ? keepSlot : 0;
			int max = Main.musicFade == null ? 0 : Math.Min(Main.maxMusic, Main.musicFade.Length);
			for (int i = 0; i < max; i++) {
				if (i == keepSlot)
					continue;
				Main.musicFade[i] = 0f;
			}
		}

		internal static void MarkLeftTitle()
		{
			_returnFromWorld = true;
			_visitedWorld = true;
		}

		internal static void HandleMenuLifecycle()
		{
			if (CalamitasMenuConflict.OverlayActive) {
				MuteVanillaMusic();
				PauseBuiltInTrack();
				_menuAudioStarted = false;
				_mix = 0f;
				string path = CalamitasMenuCustomAudio.PlayingPath;
				if (string.IsNullOrEmpty(path) || path.IndexOf("RedScreenOfDeath", StringComparison.OrdinalIgnoreCase) < 0)
					CalamitasMenuCustomAudio.Stop();
				return;
			}

			if (!Main.gameMenu) {
				MarkLeftTitle();
				if (_lifeFrame != Main.GameUpdateCount) {
					_lifeFrame = Main.GameUpdateCount;
					_pendingStart = false;
					TickMix(0f, 0.018f);
					if (Current.IsCustom) {
						CalamitasMenuCustomAudio.Update();
						if (_mix <= 0.001f)
							CalamitasMenuCustomAudio.Stop();
					}
					else {
						int slot = CurrentSlot;
						if (slot > 0 && slot < Main.musicFade.Length)
							Main.musicFade[slot] = Math.Min(Main.musicFade[slot], OutputMix * DieWithASmileSettings.MenuMusicVolume);
					}
				}

				return;
			}

			if (CalamitasMenuPersist.MenuStillLoading) {
				_pendingStart = true;
				return;
			}

			bool fromWorld = _visitedWorld || _returnFromWorld;
			if (fromWorld) {
				_visitedWorld = false;
				_returnFromWorld = false;
				_pendingStart = false;
				ReturnToTitle(fromWorld: true);
				_menuAudioStarted = DieWithASmileSettings.PlayerEnabled;
			}
			else if (!_menuAudioStarted && DieWithASmileSettings.PlayerEnabled && _pendingStart) {
				_pendingStart = false;
				ReturnToTitle(fromWorld: false);
				_menuAudioStarted = true;
			}

			if (_lifeFrame == Main.GameUpdateCount)
				return;

			_lifeFrame = Main.GameUpdateCount;
			if (!DieWithASmileSettings.PlayerEnabled || _paused)
				return;

			TickMix(1f, 0.012f);
			AssertTitleMusic();
			if (Current.IsCustom)
				CalamitasMenuCustomAudio.Update();
		}

		private static void ReturnToTitle(bool fromWorld)
		{
			if (!DieWithASmileSettings.PlayerEnabled) {
				Silence();
				return;
			}

			if (DieWithASmileSettings.ShuffleLogos)
				CalamitasMenuLogo.Reroll(save: true);
			if (fromWorld && DieWithASmileSettings.ShuffleScenes)
				DieWithASmileSettings.RerollScene();

			int index = _index;
			if (_loop) {
				int looped = _active.FindIndex(track => track.Id == _loopedId);
				if (looped >= 0)
					index = looped;
			}
			else if (_shuffle && !_loop)
				index = RandomIndex(_index);

			bool sameTrack = _active.Count > 0
				&& index == _index
				&& Current.Id == _active[Math.Clamp(index, 0, _active.Count - 1)].Id;
			if (fromWorld && sameTrack)
				EnsurePlaying(blend: true);
			else
				PlayIndex(index, fromThemeStart: true, blend: true);
		}

		internal static void AssertTitleMusic()
		{
			if (!Main.gameMenu || CalamitasMenuPersist.MenuStillLoading || !_menuAudioStarted)
				return;
			if (!DieWithASmileSettings.PlayerEnabled || _paused)
				return;

			if (Current.IsCustom) {
				Main.newMusic = 0;
				return;
			}

			int slot = CurrentSlot;
			if (slot <= 0)
				return;

			Main.newMusic = slot;
			if (slot >= Main.musicFade.Length)
				return;

			Main.musicNoCrossFade[slot] = false;
			float want = OutputMix * DieWithASmileSettings.MenuMusicVolume;
			if (Main.musicFade[slot] < want)
				Main.musicFade[slot] = Math.Min(want, Main.musicFade[slot] + 0.02f);
		}

		private static void EnsurePlaying(bool blend)
		{
			_paused = false;
			if (blend)
				_mix = Math.Min(_mix, 0.02f);
			try {
				if (Current.IsCustom) {
					Main.newMusic = 0;
					if (!CalamitasMenuCustomAudio.IsPlaying)
						TryPlayCustom(Current);
					return;
				}

				int slot = CurrentSlot;
				if (slot <= 0)
					return;

				Main.newMusic = slot;
				Main.musicNoCrossFade[slot] = false;
				Main.musicFade[slot] = OutputMix * DieWithASmileSettings.MenuMusicVolume;
				IAudioTrack track = GetTrack();
				if (track == null)
					return;
				if (track.IsPaused)
					track.Resume();
				else if (track.IsStopped) {
					track.Reuse();
					track.Play();
				}
			}
			catch {
			}
		}

		internal static void Update()
		{
			if (CalamitasMenuConflict.OverlayActive)
				return;

			_scanTimer += 1f / 60f;
			if (_scanTimer > 0.8f) {
				_scanTimer = 0f;
				if (CalamitasMenuPanels.PlaylistOpen && !CalamitasMenuPanels.EditingArtist)
					Rebuild();
			}

			if (!DieWithASmileSettings.PlayerEnabled) {
				Silence();
				return;
			}

			if (Current.IsCustom) {
				Main.newMusic = 0;
				SoftMuteOthers(-1);
				TickMix(1f, 0.01f);
				if (_paused)
					return;

				if (!CalamitasMenuCustomAudio.IsPlaying && !CalamitasMenuCustomAudio.Finished) {
					if (_customRetryDelay > 0)
						_customRetryDelay--;
					else if (!TryPlayCustom(Current))
						_customRetryDelay = 20;
				}

				CalamitasMenuCustomAudio.Update();
				if (CalamitasMenuCustomAudio.Finished) {
					if (_loop)
						PlayIndex(_index, fromThemeStart: true, blend: false);
					else
						Next();
				}

				return;
			}

			IAudioTrack track = GetTrack();
			if (track == null)
				return;

			if (_paused) {
				MuteVanillaMusic();
				try {
					if (track.IsPlaying)
						track.Pause();
				}
				catch {
				}

				return;
			}

			int slot = CurrentSlot;
			if (slot > 0) {
				Main.newMusic = slot;
				TickMix(1f, 0.01f);
				Main.musicFade[slot] = OutputMix * DieWithASmileSettings.MenuMusicVolume;
				Main.musicNoCrossFade[slot] = false;
				SoftMuteOthers(slot);
			}

			if (_ignoreEndFrames > 0)
				_ignoreEndFrames--;

			if (track.IsStopped) {
				if (ReachedEndSafe(track)) {
					if (_loop)
						PlayIndex(_index, fromThemeStart: true, blend: false);
					else
						Next();
				}
				else {
					try {
						track.Reuse();
						track.Play();
						if (Current.StartSeconds > 0.5f && _forceStartFrames <= 0)
							SeekSeconds(_fallbackTime);
					}
					catch {
					}
				}

				return;
			}

			if (track.IsPaused)
				track.Resume();

			if (_forceStartFrames > 0) {
				_forceStartFrames--;
				if (Current.StartSeconds > 0.5f && TryGetTime(track, out float now) && now < Current.StartSeconds - 0.35f)
					SeekSeconds(Current.StartSeconds);
			}

			if (DieWithASmileSettings.EffectiveMusicVolume <= 0.01f) {
				_fallbackStamp = Main.GlobalTimeWrappedHourly;
				return;
			}

			if (ReachedEndSafe(track)) {
				if (_loop)
					PlayIndex(_index, fromThemeStart: true, blend: false);
				else
					Next();
			}
		}

		private static bool ReachedEndSafe(IAudioTrack track)
		{
			if (_ignoreEndFrames > 0)
				return false;

			float duration = GetDuration();
			if (duration <= 1f)
				return false;

			if (TryGetTime(track, out float time))
				return time >= duration - 0.25f;

			return GetDisplayTime() >= duration - 0.25f;
		}

		internal static void TogglePause()
		{
			if (Current.IsCustom) {
				_paused = !_paused;
				CalamitasMenuCustomAudio.TogglePause();
				if (_paused)
					MuteVanillaMusic();
				return;
			}

			IAudioTrack track = GetTrack();
			if (track == null)
				return;

			if (_paused) {
				_paused = false;
				if (track.IsStopped)
					PlayIndex(_index, fromThemeStart: false, blend: false);
				else
					track.Resume();
				_fallbackStamp = Main.GlobalTimeWrappedHourly;
			}
			else {
				_paused = true;
				_fallbackTime = GetDisplayTime();
				try {
					track.Pause();
				}
				catch {
				}

				MuteVanillaMusic();
			}
		}

		internal static void Next()
		{
			int next = _shuffle && !_loop ? RandomIndex(_index) : (_index + 1) % Math.Max(1, _active.Count);
			if (_shuffle && !_loop)
				_shuffleHistory.Add(_index);

			PlayIndex(next, fromThemeStart: true, blend: false);
		}

		internal static void Previous()
		{
			if (_shuffle && !_loop && _shuffleHistory.Count > 0) {
				int last = _shuffleHistory[^1];
				_shuffleHistory.RemoveAt(_shuffleHistory.Count - 1);
				PlayIndex(Math.Clamp(last, 0, Math.Max(0, _active.Count - 1)), fromThemeStart: true, blend: false);
				return;
			}

			PlayIndex((_index - 1 + Math.Max(1, _active.Count)) % Math.Max(1, _active.Count), fromThemeStart: true, blend: false);
		}

		internal static void ToggleLoop()
		{
			_loop = !_loop;
			_loopedId = _loop ? Current.Id : "";
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			data.LoopEnabled = _loop;
			data.LoopedTrackId = _loopedId;
			DieWithASmileSave.Save();
		}

		internal static void ToggleShuffle()
		{
			_shuffle = !_shuffle;
			DieWithASmileSaveData data = DieWithASmileSave.Data;
			data.ShuffleEnabled = _shuffle;
			DieWithASmileSave.Save();
		}

		internal static void Seek01(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			if (Current.IsCustom) {
				CalamitasMenuCustomAudio.Seek01(t);
				_fallbackTime = CalamitasMenuCustomAudio.Time;
				_fallbackStamp = Main.GlobalTimeWrappedHourly;
				return;
			}

			float duration = GetDuration();
			if (duration <= 0.5f)
				return;

			SeekSeconds(t * duration);
		}

		internal static float GetDisplayTime()
		{
			if (Current.IsCustom)
				return CalamitasMenuCustomAudio.Time;

			IAudioTrack track = GetTrack();
			if (track != null && TryGetTime(track, out float time) && time > 0.12f) {
				_fallbackTime = time;
				_fallbackStamp = Main.GlobalTimeWrappedHourly;
				return time;
			}

			if (_paused)
				return _fallbackTime;

			return _fallbackTime + Math.Max(0f, Main.GlobalTimeWrappedHourly - _fallbackStamp);
		}

		internal static float GetDuration()
		{
			if (Current.IsCustom)
				return CalamitasMenuCustomAudio.Duration;

			if (_cachedDurationId == Current.Id && _cachedDuration > 1f)
				return _cachedDuration;

			IAudioTrack track = GetTrack();
			if (track != null && TryGetDuration(track, out float duration) && duration > 1f) {
				_cachedDuration = duration;
				_cachedDurationId = Current.Id;
				return duration;
			}

			return Math.Max(Current.StartSeconds + 30f, 180f);
		}

		internal static string FormatTime(float seconds)
		{
			seconds = Math.Max(0f, seconds);
			int whole = (int)seconds;
			return $"{whole / 60}:{whole % 60:00}";
		}

		internal static IAudioTrack GetTrack()
		{
			if (Current.IsCustom)
				return null;

			return _mod == null ? null : MusicLoader.GetMusic(_mod, Current.Path);
		}

		private static void PlayIndex(int index, bool fromThemeStart, bool blend)
		{
			string previousId = Current?.Id;
			if (!blend) {
				try {
					if (!Current.IsCustom)
						GetTrack()?.Stop(AudioStopOptions.Immediate);
				}
				catch {
				}

				CalamitasMenuCustomAudio.Stop();
			}
			else if (Current.IsCustom) {
				CalamitasMenuCustomAudio.Stop();
			}

			if (_active.Count == 0)
				Rebuild(play: false);

			_index = Math.Clamp(index, 0, Math.Max(0, _active.Count - 1));
			if (_cachedDurationId != Current.Id)
				_cachedDuration = 0f;
			_paused = false;
			_customRetryDelay = 0;
			_mix = blend ? 0f : 1f;

			try {
				if (Current.IsCustom) {
					Main.musicBox2 = -1;
					Main.newMusic = 0;
					if (!blend)
						MuteVanillaMusic();
					DisposeCustom();
					DieWithASmileSave.WritePlayGuard(Current.FileName);
					bool played = TryPlayCustom(Current);
					DieWithASmileSave.ClearPlayGuard();
					if (!played)
						return;
				}
				else {
					int slot = CurrentSlot;
					if (slot <= 0)
						return;

					DisposeCustom();
					Main.musicBox2 = -1;
					Main.newMusic = slot;
					if (!blend)
						MuteVanillaMusic(slot);
					Main.musicFade[slot] = OutputMix * DieWithASmileSettings.MenuMusicVolume;
					Main.musicNoCrossFade[slot] = false;
					IAudioTrack track = GetTrack();
					track?.Reuse();
					track?.Play();
				}
			}
			catch {
				DieWithASmileSave.ClearPlayGuard();
				if (Current.IsCustom) {
					SkipUnplayable(Current);
					return;
				}
			}

			float start = fromThemeStart ? Current.StartSeconds : GetDisplayTime();
			_fallbackTime = start;
			_fallbackStamp = Main.GlobalTimeWrappedHourly;
			_ignoreEndFrames = 75;
			_forceStartFrames = fromThemeStart && !Current.IsCustom && Current.StartSeconds > 0.5f ? 12 : 0;

			if (DieWithASmileSettings.TimerShuffleScenes && previousId != Current.Id)
				DieWithASmileSettings.RerollScene();
		}

		private static void SoftMuteOthers(int keepSlot)
		{
			int max = Main.musicFade?.Length ?? 0;
			for (int i = 0; i < max; i++) {
				if (i == keepSlot)
					continue;

				if (Main.musicFade[i] > 0f)
					Main.musicFade[i] = Math.Max(0f, Main.musicFade[i] - 0.01f);
			}
		}

		private static void TickMix(float target, float speed)
		{
			if (_mix < target)
				_mix = Math.Min(target, _mix + speed);
			else if (_mix > target)
				_mix = Math.Max(target, _mix - speed);
		}

		private static int RandomIndex(int except)
		{
			int count = Math.Max(1, _active.Count);
			if (count <= 1)
				return 0;

			int next = except;
			int guard = 0;
			while (next == except && guard++ < 24)
				next = Main.rand != null ? Main.rand.Next(count) : Random.Shared.Next(count);

			if (next == except)
				next = (except + 1) % count;

			return next;
		}

		private static bool TryPlayCustom(MenuTrack track)
		{
			if (track == null || !track.IsCustom)
				return false;

			bool played = CalamitasMenuCustomAudio.Play(track.AudioPath);
			if (played) {
				_customPlayFails = 0;
				_customFailId = "";
				return true;
			}

			if (_customFailId != track.Id) {
				_customFailId = track.Id;
				_customPlayFails = 0;
			}

			_customPlayFails++;
			if (_customPlayFails >= 12)
				SkipUnplayable(track);

			return false;
		}

		private static void SkipUnplayable(MenuTrack track)
		{
			if (track == null || _skipDepth > 12)
				return;

			_skipDepth++;
			try {
				string nextId = _active.FirstOrDefault(item => item.Id != track.Id)?.Id;
				CalamitasMenuLibrary.Quarantine(track);
				Rebuild(playId: nextId);
			}
			catch {
				_active.RemoveAll(item => item.Id == track.Id);
				if (_active.Count == 0)
					_active.Add(BuiltIn[0]);
				_index = 0;
			}
			finally {
				_skipDepth--;
			}
		}

		private static void DisposeCustom()
		{
			CalamitasMenuCustomAudio.Stop();
			if (_customTrack == null) {
				_customPath = null;
				return;
			}

			IAudioTrack track = _customTrack;
			_customTrack = null;
			_customPath = null;
			try {
				track.Stop(AudioStopOptions.Immediate);
			}
			catch {
			}

			try {
				track.Dispose();
			}
			catch {
			}
		}

		private static void SeekSeconds(float seconds)
		{
			IAudioTrack track = GetTrack();
			if (track == null)
				return;

			seconds = MathHelper.Clamp(seconds, 0f, Math.Max(GetDuration() - 0.05f, 0f));
			if (seconds <= 0.05f)
				return;
			if (!TrySetTime(track, seconds)) {
				track.Reuse();
				track.Play();
				TrySetTime(track, seconds);
			}

			_fallbackTime = seconds;
			_fallbackStamp = Main.GlobalTimeWrappedHourly;
			if (_paused)
				track.Pause();
		}

		private static bool TryGetTime(IAudioTrack track, out float seconds)
		{
			seconds = 0f;
			try {
				object reader = GetTimeReader(track);
				if (reader == null)
					return false;

				if (FindTimeProperty(reader, "DecodedTime", "TimePosition", "CurrentTime")?.GetValue(reader) is TimeSpan span) {
					seconds = (float)span.TotalSeconds;
					return true;
				}
			}
			catch {
			}

			return false;
		}

		private static bool TryGetDuration(IAudioTrack track, out float seconds)
		{
			seconds = 0f;
			try {
				object reader = GetTimeReader(track);
				if (reader == null)
					return false;

				if (FindTimeProperty(reader, "TotalTime", "Duration")?.GetValue(reader) is TimeSpan span && span.TotalSeconds > 1d) {
					seconds = (float)span.TotalSeconds;
					return true;
				}
			}
			catch {
			}

			return false;
		}

		private static bool TrySetTime(IAudioTrack track, float seconds)
		{
			try {
				object reader = GetTimeReader(track);
				PropertyInfo prop = FindTimeProperty(reader, "DecodedTime", "TimePosition", "CurrentTime");
				if (prop == null || !prop.CanWrite)
					return false;

				prop.SetValue(reader, TimeSpan.FromSeconds(seconds));
				return true;
			}
			catch {
				return false;
			}
		}

		private static PropertyInfo FindTimeProperty(object reader, params string[] names)
		{
			if (reader == null)
				return null;

			Type type = reader.GetType();
			foreach (string name in names) {
				PropertyInfo prop = type.GetProperty(name);
				if (prop != null && prop.PropertyType == typeof(TimeSpan))
					return prop;
			}

			return null;
		}

		private static object GetTimeReader(IAudioTrack track)
		{
			try {
				if (track is OGGAudioTrack)
					return _vorbisField?.GetValue(track);

				if (track is MP3AudioTrack)
					return _mp3StreamField?.GetValue(track);
			}
			catch {
			}

			return null;
		}
	}
}

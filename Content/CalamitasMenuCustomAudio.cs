using System;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using NLayer;
using NVorbis;
using Terraria;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuCustomAudio
	{
		private const int ChunkFrames = 2048;
		private const int BufferAhead = 4;
		private const int SpectrumDelay = 3;

		private static DynamicSoundEffectInstance _instance;
		private static IDisposable _reader;
		private static Func<short[], int> _readFrames;
		private static Action<float> _seek;
		private static int _sampleRate = 44100;
		private static int _channels = 2;
		private static float _duration;
		private static float _time;
		private static bool _paused;
		private static bool _finished;
		private static string _path;
		private static short[] _frames;
		private static byte[] _bytes;
		private static float[][] _spectrumRing;
		private static int[] _spectrumRingFrames;
		private static int _spectrumWrite;
		private static int _spectrumFilled;
		private static int _emptyReads;

		internal static bool IsPlaying => _instance != null && !_paused && !_finished;
		internal static bool IsPaused => _paused;
		internal static bool Finished => _finished;
		internal static float Time => _time;
		internal static float Duration => _duration > 1f ? _duration : 180f;
		internal static float SampleRate => _sampleRate;
		internal static float Amplitude { get; private set; }
		internal static float[] SpectrumSamples { get; private set; }
		internal static int SpectrumSampleCount { get; private set; }

		internal static bool Play(string path)
		{
			Stop();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return false;

			try {
				if (!OpenReader(path))
					return false;

				if (_sampleRate < 8000 || _sampleRate > 48000)
					return false;

				_path = path;
				_paused = false;
				_finished = false;
				_emptyReads = 0;
				_time = 0f;
				_frames = new short[ChunkFrames * _channels];
				_bytes = new byte[_frames.Length * 2];
				_instance = new DynamicSoundEffectInstance(_sampleRate, _channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
				_instance.Volume = Volume();
				FillBuffers();
				if (_instance == null)
					return false;

				_instance.Play();
				return true;
			}
			catch {
				Stop();
				return false;
			}
		}

		internal static void Stop()
		{
			Amplitude = 0f;
			SpectrumSampleCount = 0;
			_spectrumWrite = 0;
			_spectrumFilled = 0;
			_finished = false;
			_paused = false;
			_time = 0f;
			_path = null;
			_readFrames = null;
			_seek = null;

			if (_instance != null) {
				DynamicSoundEffectInstance inst = _instance;
				_instance = null;
				try {
					inst.Stop();
					inst.Dispose();
				}
				catch {
				}
			}

			if (_reader != null) {
				try {
					_reader.Dispose();
				}
				catch {
				}

				_reader = null;
			}
		}

		internal static void TogglePause()
		{
			if (_instance == null)
				return;

			_paused = !_paused;
			try {
				if (_paused) {
					Amplitude = 0f;
					_instance.Volume = 0f;
					if (_instance.State == SoundState.Playing)
						_instance.Pause();
					if (_instance.State == SoundState.Playing)
						_instance.Stop();
				}
				else {
					_instance.Volume = Volume();
					if (_instance.State == SoundState.Paused)
						_instance.Resume();
					else {
						FillBuffers();
						_instance.Play();
					}
				}
			}
			catch {
			}
		}

		internal static void Seek01(float t)
		{
			if (_instance == null || _seek == null)
				return;

			t = Math.Clamp(t, 0f, 1f);
			_time = t * Duration;
			_finished = false;
			_spectrumWrite = 0;
			_spectrumFilled = 0;
			SpectrumSampleCount = 0;
			try {
				_seek(_time);
				_instance.Stop();
				FillBuffers();
				if (!_paused)
					_instance.Play();
			}
			catch {
			}
		}

		internal static void Update()
		{
			if (_instance == null)
				return;

			if (_paused || _finished) {
				Amplitude = 0f;
				try {
					_instance.Volume = 0f;
					if (_paused && _instance.State == SoundState.Playing)
						_instance.Pause();
				}
				catch {
				}

				return;
			}

			_instance.Volume = Volume();

			try {
				FillBuffers();
				if (_instance != null && _instance.State != SoundState.Playing)
					_instance.Play();

				if (_finished && _instance != null && _instance.PendingBufferCount <= 0)
					_instance.Stop();
			}
			catch {
				_finished = true;
				Amplitude = 0f;
			}
		}

		private static void FillBuffers()
		{
			if (_instance == null || _readFrames == null)
				return;

			while (_instance.PendingBufferCount < BufferAhead && !_finished) {
				int frames = _readFrames(_frames);
				if (frames <= 0) {
					_emptyReads++;
					if (_emptyReads < 6 && _time < 0.05f)
						return;

					_finished = true;
					Amplitude = 0f;
					return;
				}

				_emptyReads = 0;

				int samples = frames * _channels;
				int bytes = samples * 2;
				for (int i = 0; i < samples; i++) {
					short sample = _frames[i];
					_bytes[i * 2] = (byte)sample;
					_bytes[i * 2 + 1] = (byte)(sample >> 8);
				}

				PushSpectrumChunk(frames);
				_time += frames / (float)_sampleRate;
				_instance.SubmitBuffer(_bytes, 0, bytes);
			}
		}

		private static void PushSpectrumChunk(int frames)
		{
			if (_spectrumRing == null) {
				_spectrumRing = new float[BufferAhead][];
				_spectrumRingFrames = new int[BufferAhead];
				for (int i = 0; i < BufferAhead; i++)
					_spectrumRing[i] = new float[ChunkFrames];
			}

			float[] slot = _spectrumRing[_spectrumWrite];
			if (slot.Length < frames)
				slot = _spectrumRing[_spectrumWrite] = new float[frames];

			if (_channels <= 1) {
				for (int i = 0; i < frames; i++)
					slot[i] = _frames[i] / 32768f;
			}
			else {
				for (int i = 0; i < frames; i++) {
					int left = i * _channels;
					slot[i] = (_frames[left] + _frames[left + 1]) * (0.5f / 32768f);
				}
			}

			_spectrumRingFrames[_spectrumWrite] = frames;
			_spectrumWrite = (_spectrumWrite + 1) % BufferAhead;
			if (_spectrumFilled < BufferAhead)
				_spectrumFilled++;

			int delay = Math.Min(SpectrumDelay, _spectrumFilled - 1);
			int read = (_spectrumWrite - 1 - delay + BufferAhead * 8) % BufferAhead;
			SpectrumSamples = _spectrumRing[read];
			SpectrumSampleCount = _spectrumRingFrames[read];

			float playPeak = 0f;
			int playFrames = SpectrumSampleCount;
			float[] play = SpectrumSamples;
			for (int i = 0; i < playFrames; i++)
				playPeak = Math.Max(playPeak, Math.Abs(play[i]));
			Amplitude = playPeak;
		}

		internal static bool ForceFullMix;
		internal static string PlayingPath => _path;

		private static float Volume()
		{
			if (ForceFullMix)
				return Math.Clamp(Math.Max(Main.musicVolume, 0.78f), 0.6f, 1f);

			return DieWithASmileSettings.EffectiveMusicVolume * CalamitasMenuPlaylist.OutputMix;
		}

		private static bool OpenReader(string path)
		{
			try {
				string kind = SniffAudio(path);
				if (kind == "wav")
					return OpenWav(path);
				if (kind == "mp3")
					return OpenMp3(path);
				return OpenOgg(path);
			}
			catch {
				return false;
			}
		}

		private static string SniffAudio(string path)
		{
			using var fs = File.OpenRead(path);
			var buf = new byte[12];
			int n = fs.Read(buf, 0, buf.Length);
			if (n >= 12 &&
				buf[0] == (byte)'R' && buf[1] == (byte)'I' && buf[2] == (byte)'F' && buf[3] == (byte)'F' &&
				buf[8] == (byte)'W' && buf[9] == (byte)'A' && buf[10] == (byte)'V' && buf[11] == (byte)'E')
				return "wav";
			if (n >= 4 && buf[0] == (byte)'O' && buf[1] == (byte)'g' && buf[2] == (byte)'g' && buf[3] == (byte)'S')
				return "ogg";
			if (n >= 3 && buf[0] == (byte)'I' && buf[1] == (byte)'D' && buf[2] == (byte)'3')
				return "mp3";
			if (n >= 2 && buf[0] == 0xFF && (buf[1] & 0xE0) == 0xE0)
				return "mp3";

			string ext = Path.GetExtension(path).ToLowerInvariant();
			if (ext == ".wav")
				return "wav";
			if (ext == ".mp3")
				return "mp3";
			return "ogg";
		}

		private static bool OpenMp3(string path)
		{
			var mpeg = new MpegFile(path);
			_reader = mpeg;
			_sampleRate = mpeg.SampleRate > 0 ? mpeg.SampleRate : 44100;
			_channels = Math.Clamp(mpeg.Channels, 1, 2);
			_duration = mpeg.Duration.TotalSeconds > 1d ? (float)mpeg.Duration.TotalSeconds : 0f;
			if (_duration < 1f && mpeg.Length > 0)
				_duration = mpeg.Length / (float)_sampleRate;

			var scratch = new float[ChunkFrames * Math.Max(1, mpeg.Channels)];
			_readFrames = dest => {
				int read = mpeg.ReadSamples(scratch, 0, scratch.Length);
				if (read <= 0)
					return 0;

				int srcCh = Math.Max(1, mpeg.Channels);
				int frames = read / srcCh;
				for (int i = 0; i < frames; i++) {
					for (int c = 0; c < _channels; c++) {
						int src = Math.Min(c, srcCh - 1);
						dest[i * _channels + c] = FloatToShort(scratch[i * srcCh + src]);
					}
				}

				return frames;
			};
			_seek = seconds => {
				try {
					mpeg.Time = TimeSpan.FromSeconds(seconds);
				}
				catch {
					mpeg.Position = (long)(seconds * _sampleRate);
				}
			};
			return true;
		}

		private static bool OpenOgg(string path)
		{
			var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536);
			VorbisReader vorbis;
			try {
				vorbis = new VorbisReader(fs, true);
			}
			catch {
				fs.Dispose();
				return false;
			}

			_reader = vorbis;
			int srcRate = vorbis.SampleRate > 0 ? vorbis.SampleRate : 44100;
			int srcCh = Math.Max(1, vorbis.Channels);
			_channels = Math.Clamp(srcCh, 1, 2);
			try {
				double seconds = vorbis.TotalTime.TotalSeconds;
				_duration = seconds > 0.5d && !double.IsNaN(seconds) && !double.IsInfinity(seconds)
					? (float)seconds
					: 0f;
			}
			catch {
				_duration = 0f;
			}

			try {
				vorbis.TimePosition = TimeSpan.Zero;
			}
			catch {
			}

			_sampleRate = ChooseOutputRate(srcRate);
			var scratch = new float[ChunkFrames * srcCh];
			Func<short[], int> readSrc = dest => {
				int read = vorbis.ReadSamples(scratch, 0, scratch.Length);
				if (read <= 0)
					return 0;

				int frames = read / srcCh;
				if (frames <= 0)
					return 0;

				for (int i = 0; i < frames; i++) {
					for (int c = 0; c < _channels; c++) {
						int src = Math.Min(c, srcCh - 1);
						dest[i * _channels + c] = FloatToShort(scratch[i * srcCh + src]);
					}
				}

				return frames;
			};
			_readFrames = WrapResample(readSrc, srcRate, _sampleRate, _channels);
			_seek = seconds => {
				try {
					vorbis.TimePosition = TimeSpan.FromSeconds(Math.Max(0d, seconds));
				}
				catch {
				}
			};
			return true;
		}

		private static bool OpenWav(string path)
		{
			var fs = File.OpenRead(path);
			var reader = new BinaryReader(fs);
			if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") {
				fs.Dispose();
				return false;
			}

			reader.ReadInt32();
			if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") {
				fs.Dispose();
				return false;
			}

			short format = 1;
			short channels = 2;
			int sampleRate = 44100;
			short bits = 16;
			long dataOffset = 0;
			int dataSize = 0;
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
					dataOffset = fs.Position;
					dataSize = size;
					break;
				}

				fs.Position = next + (size & 1);
			}

			if (dataOffset <= 0 || (format != 1 && format != 3)) {
				fs.Dispose();
				return false;
			}

			_reader = fs;
			_sampleRate = sampleRate > 0 ? sampleRate : 44100;
			_channels = Math.Clamp(channels, (short)1, (short)2);
			int srcCh = Math.Max(1, (int)channels);
			int bytesPer = Math.Max(1, bits / 8);
			int frameBytes = bytesPer * srcCh;
			_duration = dataSize > 0 ? dataSize / (float)(frameBytes * _sampleRate) : 0f;
			var raw = new byte[ChunkFrames * frameBytes];
			_readFrames = dest => {
				int read = fs.Read(raw, 0, raw.Length);
				if (read < frameBytes)
					return 0;

				int frames = read / frameBytes;
				for (int i = 0; i < frames; i++) {
					for (int c = 0; c < _channels; c++) {
						int src = Math.Min(c, srcCh - 1);
						int offset = i * frameBytes + src * bytesPer;
						float sample = bits == 8
							? (raw[offset] - 128) / 128f
							: format == 3
								? BitConverter.ToSingle(raw, offset)
								: BitConverter.ToInt16(raw, offset) / 32768f;
						dest[i * _channels + c] = FloatToShort(sample);
					}
				}

				return frames;
			};
			_seek = seconds => {
				long frame = (long)(seconds * _sampleRate);
				fs.Position = dataOffset + frame * frameBytes;
			};
			return true;
		}

		private static int ChooseOutputRate(int srcRate)
		{
			if (srcRate >= 8000 && srcRate <= 48000)
				return srcRate;
			if (srcRate > 48000) {
				if (srcRate % 48000 == 0)
					return 48000;
				if (srcRate % 44100 == 0)
					return 44100;
				return 48000;
			}

			return 44100;
		}

		private static Func<short[], int> WrapResample(Func<short[], int> readSrc, int srcRate, int outRate, int channels)
		{
			if (readSrc == null || srcRate <= 0 || outRate <= 0 || srcRate == outRate)
				return readSrc;

			int factor = srcRate % outRate == 0 ? srcRate / outRate : 0;
			if (factor > 1) {
				var hold = new short[ChunkFrames * factor * channels];
				return dest => {
					int got = readSrc(hold);
					if (got <= 0)
						return 0;

					int frames = got / factor;
					for (int i = 0; i < frames; i++) {
						int src = i * factor * channels;
						int dst = i * channels;
						for (int c = 0; c < channels; c++)
							dest[dst + c] = hold[src + c];
					}

					return frames;
				};
			}

			double step = srcRate / (double)outRate;
			var srcBuf = new short[Math.Max(ChunkFrames * channels * 4, channels * 8)];
			var chunk = new short[ChunkFrames * channels];
			int filled = 0;
			double cursor = 0d;
			return dest => {
				int written = 0;
				while (written < ChunkFrames) {
					int i1 = (int)cursor + 1;
					while (filled <= i1) {
						int space = srcBuf.Length / channels - filled;
						if (space < 1) {
							int drop = (int)cursor;
							int keep = Math.Max(0, filled - drop);
							if (keep > 0 && drop > 0)
								Array.Copy(srcBuf, drop * channels, srcBuf, 0, keep * channels);
							filled = keep;
							cursor -= drop;
							i1 = (int)cursor + 1;
							space = srcBuf.Length / channels - filled;
							if (space < 1)
								break;
						}

						int got = readSrc(chunk);
						if (got <= 0)
							return written;

						int copy = Math.Min(got, space);
						Array.Copy(chunk, 0, srcBuf, filled * channels, copy * channels);
						filled += copy;
					}

					if (filled <= i1)
						break;

					int i0 = (int)cursor;
					float t = (float)(cursor - i0);
					for (int c = 0; c < channels; c++) {
						short a = srcBuf[i0 * channels + c];
						short b = srcBuf[i1 * channels + c];
						dest[written * channels + c] = (short)(a + (b - a) * t);
					}

					written++;
					cursor += step;
				}

				int consumed = (int)cursor;
				if (consumed > 0 && consumed < filled) {
					int keep = filled - consumed;
					Array.Copy(srcBuf, consumed * channels, srcBuf, 0, keep * channels);
					filled = keep;
					cursor -= consumed;
				}

				return written;
			};
		}

		private static short FloatToShort(float sample)
		{
			sample = Math.Clamp(sample, -1f, 1f);
			return (short)(sample * 32767f);
		}
	}
}

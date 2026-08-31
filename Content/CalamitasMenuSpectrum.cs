using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuSpectrum
	{
		private const int BarCount = 88;
		private const int WingBarCount = 15;
		private const float WingGap = 2f;
		private const float WingBarWidth = 3f;
		private const float WingSlot = 6f;
		private static Color BarColor => CalamitasMenuAccent.Mid;
		private const float BaseAlpha = 0.82f;
		private const int CircleSegments = 7;

		private const int FftSize = 512;
		private const float MinHz = 40f;
		private const float MaxHz = 12000f;

		private static float[] _barHeights;
		private static float[] _barTargets;
		private static float[] _rawBands;
		private static float[] _fftReal;
		private static float[] _fftImag;
		private static float[] _hann;
		private static float[] _magnitudes;
		private static float[] _monoScratch;
		private static float _ceiling = 0.08f;
		private static float _spin;
		private static float _activeStart;
		private static FieldInfo _audioBufferField;
		internal static float Beat { get; private set; }
		internal static float SmoothBeat { get; private set; }

		internal static void Load()
		{
			_barHeights = new float[BarCount];
			_barTargets = new float[BarCount];
			_rawBands = new float[BarCount];
			_fftReal = new float[FftSize];
			_fftImag = new float[FftSize];
			_hann = new float[FftSize];
			_magnitudes = new float[FftSize / 2];
			for (int i = 0; i < FftSize; i++)
				_hann[i] = 0.5f * (1f - MathF.Cos(MathHelper.TwoPi * i / (FftSize - 1)));

			_audioBufferField = typeof(ASoundEffectBasedAudioTrack).GetField(
				"_temporaryBuffer",
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		internal static void Reset()
		{
			if (_barHeights == null)
				return;

			Array.Clear(_barHeights, 0, _barHeights.Length);
			Array.Clear(_barTargets, 0, _barTargets.Length);
			if (_rawBands != null)
				Array.Clear(_rawBands, 0, _rawBands.Length);
			_ceiling = 0.08f;
			_spin = 0f;
			_activeStart = 0f;
			Beat = 0f;
			SmoothBeat = 0f;
		}

		internal static void Update(Mod _)
		{
			if (_barTargets == null)
				return;

			if (IsSilent()) {
				DecayToIdle();
				return;
			}

			if (!TryFillBandsFromAudio()) {
				DecayToIdle();
				return;
			}

			float amplitude = 0f;
			int bassCount = Math.Max(1, BarCount / 4);
			for (int i = 0; i < BarCount; i++) {
				amplitude += _barTargets[i];
				float current = _barHeights[i];
				float target = _barTargets[i];
				float step = target > current ? 0.46f : 0.18f;
				_barHeights[i] = MathHelper.Lerp(current, target, step);
			}

			amplitude /= BarCount;
			float bass = 0f;
			for (int i = 0; i < bassCount; i++)
				bass += _barTargets[i];
			bass /= bassCount;

			float beat = MathHelper.Clamp(bass * 0.72f + amplitude * 0.28f, 0f, 1f);
			Beat = MathHelper.Lerp(Beat, beat, 0.38f);
			SmoothBeat = MathHelper.Lerp(SmoothBeat, beat, 0.045f);
			_spin += 0.0032f + SmoothBeat * 0.0014f;
			if (_spin > MathHelper.TwoPi)
				_spin -= MathHelper.TwoPi;
		}

		internal static void Draw(
			SpriteBatch spriteBatch,
			float fade,
			float expand,
			Vector2 circleCenter,
			float buttonRadius,
			Rectangle card,
			float pulse)
		{
			if (_barHeights == null || fade <= 0f)
				return;

			float circular = 1f - expand;
			if (circular > 0.02f)
				DrawCircular(spriteBatch, fade * circular, circleCenter, buttonRadius, pulse);

			if (expand > 0.02f)
				DrawWings(spriteBatch, fade * expand, card);
		}

		private static void DrawWings(SpriteBatch spriteBatch, float fade, Rectangle card)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			float maxHeight = MathHelper.Max(24f, card.Height * 0.92f);
			float baseY = card.Bottom;

			DrawWing(spriteBatch, pixel, fade, maxHeight, baseY, card.X - WingGap, -1);
			DrawWing(spriteBatch, pixel, fade, maxHeight, baseY, card.Right + WingGap, 1);
		}

		private static void DrawWing(
			SpriteBatch spriteBatch,
			Texture2D pixel,
			float fade,
			float maxHeight,
			float baseY,
			float edge,
			int direction)
		{
			for (int i = 0; i < WingBarCount; i++) {
				float t = WingBarCount == 1 ? 1f : i / (float)(WingBarCount - 1);
				float fadeOut = (1f - t) * (1f - t);
				int bar = (int)MathF.Round(t * (BarCount - 1));
				float height = MathHelper.Max(2f, _barHeights[bar] * maxHeight);
				float x = direction < 0
					? edge - (i + 1) * WingSlot + (WingSlot - WingBarWidth) * 0.5f
					: edge + i * WingSlot + (WingSlot - WingBarWidth) * 0.5f;

				var rect = new Rectangle((int)x, (int)(baseY - height), (int)WingBarWidth, (int)height);
				spriteBatch.Draw(pixel, rect, BarColor * (BaseAlpha * fade * fadeOut));
			}
		}

		private static void DrawCircular(SpriteBatch spriteBatch, float fade, Vector2 center, float buttonRadius, float pulse)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			float inner = MathHelper.Max(8f, buttonRadius);
			float maxLen = 32f * pulse;
			int count = 56;
			int half = count / 2;
			int activeCount = Math.Max(16, BarCount * 2 / 5);
			int activeStart = FindMostActiveSlice(activeCount);

			for (int i = 0; i < count; i++) {
				float ang = i / (float)count * MathHelper.TwoPi - MathHelper.PiOver2 + _spin;
				int mirrored = i <= half ? i : count - i;
				int bar = activeStart + (int)MathF.Round(mirrored / (float)half * (activeCount - 1));
				float len = (6f + _barHeights[bar] * maxLen) * pulse;
				Vector2 dir = ang.ToRotationVector2();
				DrawFadedRay(spriteBatch, pixel, center, dir, inner, len, fade);
			}
		}

		private static int FindMostActiveSlice(int length)
		{
			int last = BarCount - length;
			int bestStart = 0;
			float best = -1f;
			for (int start = 0; start <= last; start++) {
				float sum = 0f;
				for (int i = 0; i < length; i++)
					sum += _barHeights[start + i];
				if (sum > best) {
					best = sum;
					bestStart = start;
				}
			}

			_activeStart = MathHelper.Lerp(_activeStart, bestStart, 0.08f);
			return (int)MathF.Round(_activeStart);
		}

		private static void DrawFadedRay(
			SpriteBatch spriteBatch,
			Texture2D pixel,
			Vector2 center,
			Vector2 dir,
			float inner,
			float length,
			float fade)
		{
			if (length < 1f)
				return;

			for (int s = 0; s < CircleSegments; s++) {
				float t0 = s / (float)CircleSegments;
				float t1 = (s + 1f) / CircleSegments;
				float fadeIn = MathHelper.Clamp(t0 / 0.28f, 0f, 1f);
				float fadeOut = 1f - MathHelper.Clamp((t0 - 0.55f) / 0.45f, 0f, 1f);
				float alpha = fadeIn * fadeOut * fade * BaseAlpha;
				if (alpha < 0.01f)
					continue;

				Vector2 from = center + dir * (inner + length * t0);
				Vector2 to = center + dir * (inner + length * t1);
				DrawThickLine(spriteBatch, pixel, from, to, 2.2f, BarColor * alpha);
			}
		}

		private static void DrawThickLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, float thickness, Color color)
		{
			Vector2 delta = to - from;
			float length = delta.Length();
			if (length < 0.5f)
				return;

			spriteBatch.Draw(
				pixel,
				from,
				null,
				color,
				delta.ToRotation(),
				new Vector2(0f, 0.5f),
				new Vector2(length / pixel.Width, thickness / pixel.Height),
				SpriteEffects.None,
				0f);
		}

		private static bool IsSilent()
		{
			if (CalamitasMenuPlaylist.IsPaused || DieWithASmileSettings.EffectiveMusicVolume <= 0.01f)
				return true;

			if (CalamitasMenuPlaylist.Current.IsCustom)
				return !CalamitasMenuCustomAudio.IsPlaying;

			IAudioTrack track = CalamitasMenuPlaylist.GetTrack();
			return track == null || !track.IsPlaying;
		}

		private static void DecayToIdle()
		{
			Beat = MathHelper.Lerp(Beat, 0f, 0.28f);
			SmoothBeat = MathHelper.Lerp(SmoothBeat, 0f, 0.14f);
			for (int i = 0; i < BarCount; i++) {
				_barTargets[i] = 0f;
				_barHeights[i] = MathHelper.Lerp(_barHeights[i], 0f, 0.38f);
				if (_barHeights[i] < 0.01f)
					_barHeights[i] = 0f;
			}
		}

		private static bool TryFillBandsFromAudio()
		{
			if (CalamitasMenuPlaylist.Current.IsCustom)
				return AnalyzeCustom();

			return AnalyzeBuiltIn(CalamitasMenuPlaylist.GetTrack());
		}

		private static bool AnalyzeCustom()
		{
			float[] samples = CalamitasMenuCustomAudio.SpectrumSamples;
			int count = CalamitasMenuCustomAudio.SpectrumSampleCount;
			if (samples == null || count < 32)
				return false;

			AnalyzeMono(samples, count, CalamitasMenuCustomAudio.SampleRate);
			return true;
		}

		private static bool AnalyzeBuiltIn(IAudioTrack track)
		{
			if (track is not ASoundEffectBasedAudioTrack effectTrack || _audioBufferField == null)
				return false;

			if (_audioBufferField.GetValue(effectTrack) is not float[] buffer || buffer.Length < 64)
				return false;

			int frames = MixToMono(buffer, buffer.Length);
			AnalyzeMono(_monoScratch, frames, 44100f);
			return true;
		}

		private static int MixToMono(float[] buffer, int length)
		{
			int frames = length / 2;
			if (frames < 32) {
				if (_monoScratch == null || _monoScratch.Length < length)
					_monoScratch = new float[length];
				Array.Copy(buffer, _monoScratch, length);
				return length;
			}

			if (_monoScratch == null || _monoScratch.Length < frames)
				_monoScratch = new float[frames];

			for (int i = 0; i < frames; i++)
				_monoScratch[i] = (buffer[i * 2] + buffer[i * 2 + 1]) * 0.5f;

			return frames;
		}

		private static void AnalyzeMono(float[] samples, int count, float sampleRate)
		{
			Array.Clear(_fftReal, 0, FftSize);
			Array.Clear(_fftImag, 0, FftSize);

			int start = Math.Max(0, count - FftSize);
			int n = Math.Min(FftSize, count);
			float peakSample = 0f;
			for (int i = 0; i < n; i++)
				peakSample = MathF.Max(peakSample, MathF.Abs(samples[start + i]));

			float sampleScale = peakSample > 2f ? 1f / 32768f : 1f;
			for (int i = 0; i < n; i++)
				_fftReal[i] = samples[start + i] * sampleScale * _hann[i];

			Fft(_fftReal, _fftImag);

			int bins = FftSize / 2;
			float invN = 2f / FftSize;
			for (int i = 0; i < bins; i++) {
				float re = _fftReal[i];
				float im = _fftImag[i];
				_magnitudes[i] = MathF.Sqrt(re * re + im * im) * invN;
			}

			float nyquist = sampleRate * 0.5f;
			float maxHz = MathF.Min(MaxHz, nyquist * 0.96f);
			float ratio = maxHz / MinHz;
			float binHz = sampleRate / FftSize;
			float peakBand = 0f;

			for (int i = 0; i < BarCount; i++) {
				float t0 = i / (float)BarCount;
				float t1 = (i + 1f) / BarCount;
				float f0 = MinHz * MathF.Pow(ratio, t0);
				float f1 = MinHz * MathF.Pow(ratio, t1);
				int b0 = Math.Clamp((int)(f0 / binHz), 1, bins - 1);
				int b1 = Math.Clamp((int)MathF.Ceiling(f1 / binHz), b0 + 1, bins);

				float energy = 0f;
				for (int b = b0; b < b1; b++)
					energy += _magnitudes[b];
				energy /= b1 - b0;
				energy *= 1f + t0 * 1.35f;

				_rawBands[i] = energy;
				if (energy > peakBand)
					peakBand = energy;
			}

			if (peakBand > _ceiling)
				_ceiling = MathHelper.Lerp(_ceiling, peakBand, 0.42f);
			else
				_ceiling = MathHelper.Lerp(_ceiling, MathF.Max(peakBand, 0.012f), 0.035f);

			float scale = 1f / MathF.Max(_ceiling, 0.012f);
			for (int i = 0; i < BarCount; i++) {
				float nrm = MathHelper.Clamp(_rawBands[i] * scale, 0f, 1f);
				_barTargets[i] = MathF.Pow(nrm, 0.62f) * 0.92f;
			}
		}

		private static void Fft(float[] re, float[] im)
		{
			int n = re.Length;
			for (int i = 1, j = 0; i < n; i++) {
				int bit = n >> 1;
				for (; (j & bit) != 0; bit >>= 1)
					j ^= bit;
				j ^= bit;
				if (i < j) {
					(re[i], re[j]) = (re[j], re[i]);
					(im[i], im[j]) = (im[j], im[i]);
				}
			}

			for (int len = 2; len <= n; len <<= 1) {
				float ang = -MathHelper.TwoPi / len;
				float wlenRe = MathF.Cos(ang);
				float wlenIm = MathF.Sin(ang);
				int half = len >> 1;
				for (int i = 0; i < n; i += len) {
					float wRe = 1f;
					float wIm = 0f;
					for (int j = 0; j < half; j++) {
						int u = i + j;
						int v = u + half;
						float tRe = re[v] * wRe - im[v] * wIm;
						float tIm = re[v] * wIm + im[v] * wRe;
						re[v] = re[u] - tRe;
						im[v] = im[u] - tIm;
						re[u] += tRe;
						im[u] += tIm;
						float nextRe = wRe * wlenRe - wIm * wlenIm;
						wIm = wRe * wlenIm + wIm * wlenRe;
						wRe = nextRe;
					}
				}
			}
		}
	}
}

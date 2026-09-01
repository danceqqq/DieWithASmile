using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuMeadow
	{
		private const string Art1Path = "DieWithASmile/Assets/Textures/Menu/MeadowArt1";
		private const string Art2Path = "DieWithASmile/Assets/Textures/Menu/MeadowArt2";
		private const float FadeSpeed = 0.018f;
		private const float SlideSeconds = 3f;
		private const float CrossfadeSeconds = 1.45f;
		private const int SpeckCount = 72;
		private const int BloomCount = 14;
		private const int WispCount = 10;

		private struct Speck
		{
			public Vector2 Pos;
			public Vector2 Drift;
			public float Size;
			public float Phase;
			public float Depth;
			public Color Tint;
		}

		private static Asset<Texture2D> _art1;
		private static Asset<Texture2D> _art2;
		private static float _scene;
		private static bool _sampled;
		private static bool _spawned;
		private static Color _top1 = new(28, 46, 32);
		private static Color _bottom1 = new(18, 32, 22);
		private static Color _top2 = new(28, 46, 32);
		private static Color _bottom2 = new(18, 32, 22);
		private static readonly Color FallbackDarkRed = new(96, 18, 28);
		private static Color[] _palette = { FallbackDarkRed };
		private static Color _darkRed = FallbackDarkRed;
		private static readonly Speck[] _specks = new Speck[SpeckCount];
		private static readonly Speck[] _blooms = new Speck[BloomCount];
		private static readonly Speck[] _wisps = new Speck[WispCount];

		internal static float SceneEase
		{
			get
			{
				float t = MathHelper.Clamp(_scene, 0f, 1f);
				return t * t * (3f - 2f * t);
			}
		}

		internal static void Load()
		{
			_art1 = ModContent.Request<Texture2D>(Art1Path);
			_art2 = ModContent.Request<Texture2D>(Art2Path);
		}

		internal static void Unload()
		{
			_art1 = null;
			_art2 = null;
			_sampled = false;
			_spawned = false;
		}

		internal static void Reset() => _scene = 0f;

		internal static void Update()
		{
			float target = DieWithASmileSettings.UseMeadowScene ? 1f : 0f;
			if (_scene < target)
				_scene = MathHelper.Min(target, _scene + FadeSpeed);
			else if (_scene > target)
				_scene = MathHelper.Max(target, _scene - FadeSpeed);
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			float alpha = SceneEase * fade;
			if (alpha <= 0.02f || _art1?.Value == null || _art2?.Value == null)
				return;

			EnsureEdgeColors();
			EnsureSpecks();
			GetSlide(out Texture2D from, out Texture2D to, out float cross, out Color top, out Color bottom);
			GetArtDestination(to, out Rectangle art);

			Point screen = CalamitasMenuDraw.CoverSize;
			Color cover = Darken(Color.Lerp(top, bottom, 0.55f), 0.16f);
			DrawCover(spriteBatch, cover, alpha);
			Rectangle topField = new(-220, -220, screen.X + 440, Math.Max(8, art.Y + 220));
			Rectangle bottomField = new(-220, art.Bottom, screen.X + 440, screen.Y - art.Bottom + 220);
			DrawPaintField(spriteBatch, topField, top, true, alpha);
			DrawPaintField(spriteBatch, bottomField, bottom, false, alpha);
			DrawWisps(spriteBatch, topField, top, true, alpha);
			DrawWisps(spriteBatch, bottomField, bottom, false, alpha);
			DrawArt(spriteBatch, from, art, alpha);
			DrawArt(spriteBatch, to, art, alpha * cross);
			DrawSeam(spriteBatch, art, cover, alpha);
			DrawSpecks(spriteBatch, art, top, bottom, alpha);
			DrawVignette(spriteBatch, alpha);
		}

		private static void GetSlide(out Texture2D from, out Texture2D to, out float cross, out Color top, out Color bottom)
		{
			float time = Main.GlobalTimeWrappedHourly;
			int index = (int)(time / SlideSeconds) % 2;
			int previous = 1 - index;
			float local = time % SlideSeconds;
			cross = Smoother(local / CrossfadeSeconds);
			from = previous == 0 ? _art1.Value : _art2.Value;
			to = index == 0 ? _art1.Value : _art2.Value;
			Color topFrom = previous == 0 ? _top1 : _top2;
			Color topTo = index == 0 ? _top1 : _top2;
			Color bottomFrom = previous == 0 ? _bottom1 : _bottom2;
			Color bottomTo = index == 0 ? _bottom1 : _bottom2;
			top = Color.Lerp(topFrom, topTo, cross);
			bottom = Color.Lerp(bottomFrom, bottomTo, cross);
		}

		private static void GetArtDestination(Texture2D texture, out Rectangle art) =>
			art = CalamitasMenuDraw.FitScreenDestination(texture, 0.14f);

		private static void DrawCover(SpriteBatch spriteBatch, Color color, float alpha)
		{
			Point size = CalamitasMenuDraw.CoverSize;
			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				new Rectangle(-240, -240, size.X + 480, size.Y + 480),
				color * alpha);
		}

		private static void DrawArt(SpriteBatch spriteBatch, Texture2D texture, Rectangle destination, float alpha)
		{
			if (texture == null || alpha <= 0.01f)
				return;

			spriteBatch.Draw(texture, destination, Color.White * alpha);
		}

		private static void DrawPaintField(SpriteBatch spriteBatch, Rectangle region, Color edge, bool top, float alpha)
		{
			if (region.Width <= 0 || region.Height <= 0)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Color far = Darken(edge, 0.14f);
			Color mid = Darken(edge, 0.28f);
			Color near = Darken(edge, 0.46f);
			float time = Main.GlobalTimeWrappedHourly;
			const int bands = 56;
			for (int i = 0; i < bands; i++) {
				float t = i / (float)(bands - 1);
				float wave = MathF.Sin(time * 0.27f + t * 7.4f) * 0.16f
					+ MathF.Sin(time * 0.13f + t * 3.1f + 1.7f) * 0.1f
					+ MathF.Sin(time * 0.07f + t * 18f + 0.4f) * 0.045f;
				float along = top ? t : 1f - t;
				Color a = along < 0.55f ? Color.Lerp(far, mid, along / 0.55f) : Color.Lerp(mid, near, (along - 0.55f) / 0.45f);
				Color color = Color.Lerp(a, ShiftHue(a, wave * 40f), 0.35f);
				int y = region.Y + (int)(region.Height * t);
				int h = Math.Max(3, region.Height / bands + 5);
				int xShift = (int)(MathF.Sin(time * 0.19f + t * 4.4f) * 28f + MathF.Sin(time * 0.41f + t * 1.3f) * 12f);
				spriteBatch.Draw(pixel, new Rectangle(region.X + xShift - 40, y, region.Width + 80, h), color * alpha);
			}

			for (int i = 0; i < BloomCount; i++) {
				Speck bloom = _blooms[i];
				float px = region.X + (bloom.Pos.X + MathF.Sin(time * bloom.Drift.X + bloom.Phase) * 0.12f) * region.Width;
				float py = region.Y + (bloom.Pos.Y + MathF.Cos(time * bloom.Drift.Y + bloom.Phase * 0.7f) * 0.1f) * region.Height;
				Texture2D glow = CalamitasMenuShine.Texture;
				Color oil = ShiftHue(Darken(Color.Lerp(bloom.Tint, _darkRed, 0.35f), 0.7f), MathF.Sin(time * 0.33f + bloom.Phase) * 16f);
				if (glow != null) {
					spriteBatch.Draw(
						glow,
						new Vector2(px, py),
						null,
						oil * (0.2f * alpha),
						0f,
						glow.Size() * 0.5f,
						new Vector2(bloom.Size * 2.4f, bloom.Size * 1.15f),
						SpriteEffects.None,
						0f);
				}
			}
		}

		private static void DrawWisps(SpriteBatch spriteBatch, Rectangle region, Color edge, bool top, float alpha)
		{
			if (region.Height <= 8)
				return;

			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			for (int i = 0; i < WispCount; i++) {
				Speck wisp = _wisps[i];
				float x = region.X + (wisp.Pos.X + MathF.Sin(time * wisp.Drift.X + wisp.Phase) * 0.18f) * region.Width;
				float y01 = wisp.Pos.Y + MathF.Sin(time * 0.21f + wisp.Phase) * 0.12f;
				float y = region.Y + MathHelper.Clamp(y01, 0f, 1f) * region.Height;
				float rot = MathF.Sin(time * 0.16f + wisp.Phase) * 0.45f + (top ? 0.15f : -0.15f);
				Color ink = Darken(Color.Lerp(edge, _darkRed, 0.4f + 0.2f * MathF.Sin(wisp.Phase)), 0.35f);
				spriteBatch.Draw(
					glow,
					new Vector2(x, y),
					null,
					ink * (0.16f * alpha),
					rot,
					glow.Size() * 0.5f,
					new Vector2(0.18f + wisp.Size * 0.22f, 0.55f + wisp.Size * 0.7f),
					SpriteEffects.None,
					0f);
			}
		}

		private static void DrawSeam(SpriteBatch spriteBatch, Rectangle art, Color cover, float alpha)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			const int depth = 54;
			for (int i = 0; i < depth; i++) {
				float t = i / (float)(depth - 1);
				float a = (1f - t) * (1f - t) * 0.55f * alpha;
				spriteBatch.Draw(pixel, new Rectangle(art.X - 20, art.Y - i, art.Width + 40, 2), cover * a);
				spriteBatch.Draw(pixel, new Rectangle(art.X - 20, art.Bottom + i, art.Width + 40, 2), cover * a);
			}

			for (int i = 0; i < 18; i++) {
				float t = i / 17f;
				float a = (1f - t) * 0.22f * alpha;
				spriteBatch.Draw(pixel, new Rectangle(art.X - 20, art.Y + i, art.Width + 40, 2), Color.Black * a);
				spriteBatch.Draw(pixel, new Rectangle(art.X - 20, art.Bottom - 1 - i, art.Width + 40, 2), Color.Black * a);
			}
		}

		private static void DrawVignette(SpriteBatch spriteBatch, float alpha)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Point screen = CalamitasMenuDraw.CoverSize;
			int band = Math.Max(70, screen.Y / 10);
			for (int i = 0; i < band; i++) {
				float t = i / (float)band;
				float a = (1f - t) * (1f - t) * 0.42f * alpha;
				spriteBatch.Draw(pixel, new Rectangle(-20, i - 20, screen.X + 40, 2), Color.Black * a);
				spriteBatch.Draw(pixel, new Rectangle(-20, screen.Y - i, screen.X + 40, 2), Color.Black * a);
			}

			int side = Math.Max(50, screen.X / 14);
			for (int i = 0; i < side; i++) {
				float t = i / (float)side;
				float a = (1f - t) * 0.2f * alpha;
				spriteBatch.Draw(pixel, new Rectangle(i - 20, -20, 2, screen.Y + 40), Color.Black * a);
				spriteBatch.Draw(pixel, new Rectangle(screen.X - i, -20, 2, screen.Y + 40), Color.Black * a);
			}
		}

		private static void DrawSpecks(SpriteBatch spriteBatch, Rectangle art, Color top, Color bottom, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			Point screen = CalamitasMenuDraw.CoverSize;
			for (int i = 0; i < SpeckCount; i++) {
				Speck speck = _specks[i];
				bool upper = speck.Pos.Y < 0.5f;
				float fieldH = upper ? Math.Max(1, art.Y) : Math.Max(1, screen.Y - art.Bottom);
				float originY = upper ? 0f : art.Bottom;
				Vector2 parallax = CalamitasMenuParallax.ForDepth(speck.Depth);
				float x = (speck.Pos.X + time * speck.Drift.X + MathF.Sin(time * 0.6f + speck.Phase) * 0.03f) % 1f;
				if (x < 0f)
					x += 1f;

				float y = speck.Pos.Y + MathF.Sin(time * speck.Drift.Y + speck.Phase) * 0.04f;
				float drawX = x * screen.X + parallax.X;
				float drawY = originY + (upper ? y * 2f : (y - 0.5f) * 2f) * fieldH;
				if (upper && drawY > art.Y - 4)
					continue;
				if (!upper && drawY < art.Bottom + 4)
					continue;

				Color edge = upper ? top : bottom;
				Color color = Color.Lerp(Color.Lerp(speck.Tint, _darkRed, 0.28f), edge, 0.12f);
				color = Color.Lerp(color, ShiftHue(color, MathF.Sin(time + speck.Phase) * 18f), 0.2f) * (0.58f * alpha);
				float scale = MathHelper.Clamp(speck.Size * 0.12f, 0.07f, 0.22f);
				spriteBatch.Draw(glow, new Vector2(drawX, drawY), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			}
		}

		private static void EnsureSpecks()
		{
			if (_spawned)
				return;

			_spawned = true;
			var rng = new Random(180818);
			for (int i = 0; i < SpeckCount; i++) {
				_specks[i] = new Speck {
					Pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
					Drift = new Vector2((float)(rng.NextDouble() * 0.018 + 0.004) * (rng.Next(2) == 0 ? 1 : -1), (float)(rng.NextDouble() * 0.35 + 0.12)),
					Size = rng.Next(3, 9),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.35 + rng.NextDouble() * 0.8),
					Tint = PickPalette(rng)
				};
			}

			for (int i = 0; i < BloomCount; i++) {
				_blooms[i] = new Speck {
					Pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
					Drift = new Vector2((float)(0.12 + rng.NextDouble() * 0.18), (float)(0.08 + rng.NextDouble() * 0.14)),
					Size = (float)(0.16 + rng.NextDouble() * 0.28),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Tint = PickPalette(rng)
				};
			}

			for (int i = 0; i < WispCount; i++) {
				_wisps[i] = new Speck {
					Pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
					Drift = new Vector2((float)(0.08 + rng.NextDouble() * 0.14), (float)(0.1 + rng.NextDouble() * 0.12)),
					Size = (float)(0.35 + rng.NextDouble() * 0.5),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi
				};
			}
		}

		private static void EnsureEdgeColors()
		{
			if (_sampled || _art1?.Value == null || _art2?.Value == null)
				return;

			try {
				SampleEdges(_art1.Value, out _top1, out _bottom1);
				SampleEdges(_art2.Value, out _top2, out _bottom2);
				BuildPalette(_art1.Value, _art2.Value);
				_sampled = true;
				_spawned = false;
			}
			catch {
			}
		}

		private static void SampleEdges(Texture2D texture, out Color top, out Color bottom)
		{
			top = AverageBand(texture, 0, Math.Min(6, texture.Height));
			bottom = AverageBand(texture, Math.Max(0, texture.Height - 6), texture.Height);
		}

		private static Color AverageBand(Texture2D texture, int y0, int y1)
		{
			long r = 0;
			long g = 0;
			long b = 0;
			int n = 0;
			int width = texture.Width;
			int step = Math.Max(1, width / 220);
			var row = new Color[width];
			for (int y = y0; y < y1; y++) {
				texture.GetData(0, new Rectangle(0, y, width, 1), row, 0, width);
				for (int x = 0; x < width; x += step) {
					Color c = row[x];
					if (c.A < 16)
						continue;

					r += c.R;
					g += c.G;
					b += c.B;
					n++;
				}
			}

			if (n == 0)
				return new Color(28, 46, 32);

			return new Color((int)(r / n), (int)(g / n), (int)(b / n));
		}

		private static void BuildPalette(Texture2D art1, Texture2D art2)
		{
			var colors = new List<Color>(40);
			CollectPalette(art1, colors);
			CollectPalette(art2, colors);
			_darkRed = FallbackDarkRed;
			int redWeight = 0;
			long redR = 0;
			long redG = 0;
			long redB = 0;
			for (int i = 0; i < colors.Count; i++) {
				Color c = colors[i];
				if (!IsDarkRed(c))
					continue;

				redR += c.R;
				redG += c.G;
				redB += c.B;
				redWeight++;
			}

			if (redWeight > 0)
				_darkRed = new Color((int)(redR / redWeight), (int)(redG / redWeight), (int)(redB / redWeight));

			for (int i = 0; i < 16; i++)
				colors.Add(_darkRed);
			colors.Add(new Color(118, 16, 28));
			colors.Add(new Color(72, 8, 16));
			colors.Add(Darken(_darkRed, 0.55f));

			_palette = colors.Count > 0 ? colors.ToArray() : new[] { FallbackDarkRed };
		}

		private static void CollectPalette(Texture2D texture, List<Color> colors)
		{
			int width = texture.Width;
			int height = texture.Height;
			int xStep = Math.Max(4, width / 72);
			int yStep = Math.Max(4, height / 40);
			var row = new Color[width];
			for (int y = yStep; y < height; y += yStep) {
				texture.GetData(0, new Rectangle(0, y, width, 1), row, 0, width);
				for (int x = xStep; x < width; x += xStep) {
					Color c = row[x];
					if (c.A < 40 || IsNearBlack(c) || IsNearWhite(c))
						continue;

					colors.Add(c);
					if (IsDarkRed(c)) {
						colors.Add(c);
						colors.Add(c);
						colors.Add(Darken(c, 0.65f));
					}
				}
			}
		}

		private static bool IsDarkRed(Color c) =>
			c.R > 42 && c.R > c.G + 10 && c.R > c.B + 6 && c.G < 110 && c.B < 110;

		private static bool IsNearBlack(Color c) => c.R + c.G + c.B < 48;

		private static bool IsNearWhite(Color c) => c.R > 220 && c.G > 220 && c.B > 210;

		private static Color PickPalette(Random rng)
		{
			if (_palette == null || _palette.Length == 0)
				return FallbackDarkRed;

			if (rng.NextDouble() < 0.38)
				return _darkRed;

			return _palette[rng.Next(_palette.Length)];
		}

		private static Color Darken(Color color, float mul) =>
			new(
				(int)(color.R * mul),
				(int)(color.G * mul),
				(int)(color.B * mul));

		private static Color ShiftHue(Color color, float delta)
		{
			return new Color(
				(int)Math.Clamp(color.R + delta, 0f, 255f),
				(int)Math.Clamp(color.G + delta * 0.45f, 0f, 255f),
				(int)Math.Clamp(color.B - delta * 0.25f, 0f, 255f));
		}

		private static float Smoother(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * t * (t * (t * 6f - 15f) + 10f);
		}
	}
}

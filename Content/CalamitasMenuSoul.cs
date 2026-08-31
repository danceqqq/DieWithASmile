using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuSoul
	{
		private const string ArtPath = "DieWithASmile/Assets/Textures/Menu/SoulOfTheUniverse";
		private const float FadeSpeed = 0.018f;
		private const float BarTop = 146f / 2852f;
		private const float BarBottom = 175f / 2852f;
		private const int DustCount = 70;
		private const int EmberCount = 36;
		private const int MoteCount = 32;
		private const int MagentaCount = 18;
		private const int StarCount = 52;

		private static readonly Bloom[] Blooms =
		{
			new(653f, 1261f, 0x92, 0xFA, 0xFD),
			new(382f, 2471f, 0xFB, 0x27, 0x23),
			new(2039f, 776f, 0x24, 0x1C, 0xF9),
			new(2098f, 1052f, 0x7D, 0xF1, 0xB0),
			new(2151f, 1411f, 0xFA, 0xF9, 0x6B),
			new(2793f, 1111f, 0x3B, 0x76, 0xEA),
			new(1360f, 1272f, 0xE1, 0x64, 0xEC),
			new(1299f, 1101f, 0xCE, 0xF5, 0xA2),
			new(2954f, 2261f, 0xEF, 0xEB, 0xEC),
			new(1179f, 2294f, 0xE2, 0x38, 0x27),
			new(428f, 1149f, 0xDC, 0xF6, 0xF7),
			new(3326f, 935f, 0x99, 0xF5, 0xF0),
			new(3342f, 1745f, 0xF2, 0x40, 0x94)
		};

		private readonly struct Bloom
		{
			public readonly float X;
			public readonly float Y;
			public readonly Color Tint;

			public Bloom(float x, float y, byte r, byte g, byte b)
			{
				X = x;
				Y = y;
				Tint = new Color(r, g, b);
			}
		}

		private struct Spark
		{
			public Vector2 Pos;
			public Vector2 Drift;
			public float Size;
			public float Phase;
			public float Depth;
			public Color Tint;
		}

		private static Asset<Texture2D> _art;
		private static float _scene;
		private static bool _spawned;
		private static readonly Spark[] _dust = new Spark[DustCount];
		private static readonly Spark[] _embers = new Spark[EmberCount];
		private static readonly Spark[] _motes = new Spark[MoteCount];
		private static readonly Spark[] _magenta = new Spark[MagentaCount];
		private static readonly Spark[] _stars = new Spark[StarCount];
		private static readonly Color Void = new(4, 3, 10);
		private static readonly Color Cyan = new(70, 210, 230);
		private static readonly Color Ember = new(255, 92, 42);
		private static readonly Color Gold = new(250, 200, 90);
		private static readonly Color Magenta = new(230, 70, 150);

		internal static float SceneEase
		{
			get
			{
				float t = MathHelper.Clamp(_scene, 0f, 1f);
				return t * t * (3f - 2f * t);
			}
		}

		internal static void Load() => _art = ModContent.Request<Texture2D>(ArtPath);

		internal static void Unload()
		{
			_art = null;
			_spawned = false;
		}

		internal static void Reset() => _scene = 0f;

		internal static void Update()
		{
			float target = DieWithASmileSettings.UseSoulScene ? 1f : 0f;
			if (_scene < target)
				_scene = MathHelper.Min(target, _scene + FadeSpeed);
			else if (_scene > target)
				_scene = MathHelper.Max(target, _scene - FadeSpeed);
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			float alpha = SceneEase * fade;
			if (alpha <= 0.02f || _art?.Value == null)
				return;

			EnsureSparks();
			Texture2D texture = _art.Value;
			Rectangle dest = FillScreen();
			Rectangle content = ContentBand(dest);
			Point cover = CalamitasMenuDraw.CoverSize;
			DrawVoid(spriteBatch, cover, alpha);
			spriteBatch.Draw(texture, dest, Color.White * alpha);
			DrawBlooms(spriteBatch, dest, texture, alpha);
			DrawFieldSparks(spriteBatch, content, alpha);
			DrawBarHaze(spriteBatch, BarRegion(dest, content, true), true, alpha);
			DrawBarHaze(spriteBatch, BarRegion(dest, content, false), false, alpha);
			DrawSeam(spriteBatch, content, alpha);
			DrawStars(spriteBatch, dest, content, alpha);
		}

		private static Rectangle FillScreen()
		{
			Point cover = CalamitasMenuDraw.CoverSize;
			Vector2 shift = CalamitasMenuParallax.ForDepth(0.12f);
			const float over = 1.04f;
			int w = Math.Max(1, (int)(cover.X * over));
			int h = Math.Max(1, (int)(cover.Y * over));
			return new Rectangle(
				(int)((cover.X - w) * 0.5f + shift.X),
				(int)((cover.Y - h) * 0.5f + shift.Y),
				w,
				h);
		}

		private static Rectangle ContentBand(Rectangle dest)
		{
			int top = dest.Y + (int)(dest.Height * BarTop);
			int bottom = dest.Bottom - (int)(dest.Height * BarBottom);
			return new Rectangle(dest.X, top, dest.Width, Math.Max(1, bottom - top));
		}

		private static Rectangle BarRegion(Rectangle dest, Rectangle content, bool top)
		{
			if (top)
				return new Rectangle(dest.X, dest.Y, dest.Width, Math.Max(1, content.Y - dest.Y));
			return new Rectangle(dest.X, content.Bottom, dest.Width, Math.Max(1, dest.Bottom - content.Bottom));
		}

		private static Vector2 MapPoint(Rectangle dest, Texture2D texture, float texX, float texY) =>
			new(
				dest.X + texX / Math.Max(1, texture.Width) * dest.Width,
				dest.Y + texY / Math.Max(1, texture.Height) * dest.Height);

		private static void DrawVoid(SpriteBatch spriteBatch, Point cover, float alpha)
		{
			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				new Rectangle(-240, -240, cover.X + 480, cover.Y + 480),
				Void * alpha);
		}

		private static void DrawBarHaze(SpriteBatch spriteBatch, Rectangle region, bool top, float alpha)
		{
			if (region.Width <= 0 || region.Height <= 0)
				return;

			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			Color washL = top ? Magenta : Ember;
			Color washR = top ? Cyan : Gold;
			for (int i = 0; i < 7; i++) {
				float u = (i + 0.5f) / 7f;
				float x = region.X + region.Width * (0.08f + u * 0.84f);
				float y = region.Y + region.Height * (top ? 0.72f : 0.28f);
				x += MathF.Sin(time * 0.18f + i * 1.1f) * 36f;
				Color tint = Color.Lerp(washL, washR, u);
				float size = (0.5f + 0.32f * MathF.Sin(time * 0.4f + i)) * (region.Height / 95f);
				spriteBatch.Draw(glow, new Vector2(x, y), null, tint * (0.055f * alpha), 0f, origin, new Vector2(size * 3.2f, size * 0.48f), SpriteEffects.None, 0f);
			}
		}

		private static void DrawSeam(SpriteBatch spriteBatch, Rectangle art, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			Vector2 origin = glow.Size() * 0.5f;
			float time = Main.GlobalTimeWrappedHourly;
			float pulse = 0.7f + 0.3f * MathF.Sin(time * 0.9f);
			const int beads = 9;
			for (int i = 0; i < beads; i++) {
				float u = i / (float)(beads - 1);
				float x = art.X + art.Width * (0.06f + u * 0.88f);
				Color tint = Color.Lerp(Cyan, Gold, u);
				spriteBatch.Draw(glow, new Vector2(x, art.Y), null, tint * (0.1f * pulse * alpha), 0f, origin, new Vector2(art.Width / 140f, 0.14f), SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, new Vector2(x, art.Bottom), null, tint * (0.08f * pulse * alpha), 0f, origin, new Vector2(art.Width / 150f, 0.12f), SpriteEffects.None, 0f);
			}
		}

		private static void DrawBlooms(SpriteBatch spriteBatch, Rectangle dest, Texture2D texture, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			float unit = Math.Max(dest.Width, dest.Height) / 1400f;

			for (int i = 0; i < Blooms.Length; i++) {
				Bloom bloom = Blooms[i];
				Vector2 pos = MapPoint(dest, texture, bloom.X, bloom.Y);
				float pulse = 0.78f + 0.22f * MathF.Sin(time * (1.05f + i * 0.07f) + i * 0.9f);
				float breathe = 0.88f + 0.12f * MathF.Sin(time * 0.45f + i * 0.4f);
				bool gem = i == 4;
				float size = gem ? 1.35f : 1f;
				Color hot = Color.Lerp(bloom.Tint, Color.White, gem ? 0.62f : 0.45f);
				spriteBatch.Draw(glow, pos, null, bloom.Tint * (0.2f * pulse * alpha), 0f, origin, unit * 2.05f * breathe * size, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, bloom.Tint * (0.36f * pulse * alpha), 0f, origin, unit * 0.98f * breathe * size, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, hot * (0.4f * pulse * alpha), 0f, origin, unit * 0.38f * size, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, Color.White * (0.18f * pulse * alpha), 0f, origin, unit * 0.14f * size, SpriteEffects.None, 0f);
				if (gem) {
					spriteBatch.Draw(glow, pos, null, Color.White * (0.12f * pulse * alpha), 0f, origin, new Vector2(unit * 6.4f, unit * 0.12f), SpriteEffects.None, 0f);
					spriteBatch.Draw(glow, pos, null, Gold * (0.1f * pulse * alpha), 0f, origin, new Vector2(unit * 0.16f, unit * 3.6f), SpriteEffects.None, 0f);
				}
			}

			DrawFlares(spriteBatch, dest, texture, glow, origin, alpha);
		}

		private static void DrawFlares(SpriteBatch spriteBatch, Rectangle dest, Texture2D texture, Texture2D glow, Vector2 origin, float alpha)
		{
			float time = Main.GlobalTimeWrappedHourly;
			int[] ids = { 0, 4, 5, 11 };
			for (int n = 0; n < ids.Length; n++) {
				Bloom bloom = Blooms[ids[n]];
				Vector2 pos = MapPoint(dest, texture, bloom.X, bloom.Y);
				float pulse = 0.45f + 0.2f * MathF.Sin(time * 0.7f + n);
				spriteBatch.Draw(
					glow,
					pos,
					null,
					bloom.Tint * (0.08f * pulse * alpha),
					0f,
					origin,
					new Vector2(dest.Width / 55f, 0.07f),
					SpriteEffects.None,
					0f);
			}
		}

		private static void DrawFieldSparks(SpriteBatch spriteBatch, Rectangle art, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			for (int i = 0; i < DustCount; i++) {
				Spark spark = _dust[i];
				Along(spark, time, 0.22f, out float x, out float y);
				Vector2 pos = new Vector2(art.X + x * art.Width, art.Y + y * art.Height) + CalamitasMenuParallax.ForDepth(spark.Depth);
				float flicker = 0.55f + 0.45f * MathF.Sin(time * 3.4f + spark.Phase);
				spriteBatch.Draw(glow, pos, null, spark.Tint * (0.22f * flicker * alpha), 0f, origin, spark.Size, SpriteEffects.None, 0f);
			}

			for (int i = 0; i < EmberCount; i++) {
				Spark spark = _embers[i];
				Along(spark, time, 0.3f, out float x, out float y);
				Vector2 pos = new Vector2(art.X + x * art.Width, art.Y + y * art.Height) + CalamitasMenuParallax.ForDepth(spark.Depth);
				float heat = 0.5f + 0.5f * MathF.Sin(time * (2.2f + spark.Size) + spark.Phase);
				spriteBatch.Draw(glow, pos, null, Ember * (0.14f * heat * alpha), 0f, origin, spark.Size * 1.7f, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, Gold * (0.18f * heat * alpha), 0f, origin, spark.Size * 0.55f, SpriteEffects.None, 0f);
			}

			for (int i = 0; i < MoteCount; i++) {
				Spark spark = _motes[i];
				Along(spark, time, 0.18f, out float x, out float y);
				Vector2 pos = new Vector2(art.X + x * art.Width, art.Y + y * art.Height) + CalamitasMenuParallax.ForDepth(spark.Depth);
				float pulse = 0.5f + 0.5f * MathF.Sin(time * 1.8f + spark.Phase);
				float rot = spark.Phase * 0.15f + time * 0.12f;
				spriteBatch.Draw(glow, pos, null, Cyan * (0.14f * pulse * alpha), rot, origin, new Vector2(spark.Size * 2.6f, spark.Size * 0.55f), SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, Color.White * (0.12f * pulse * alpha), 0f, origin, spark.Size * 0.4f, SpriteEffects.None, 0f);
			}

			for (int i = 0; i < MagentaCount; i++) {
				Spark spark = _magenta[i];
				Along(spark, time, 0.16f, out float x, out float y);
				Vector2 pos = new Vector2(art.X + x * art.Width, art.Y + y * art.Height) + CalamitasMenuParallax.ForDepth(spark.Depth);
				float pulse = 0.45f + 0.55f * MathF.Sin(time * 2.1f + spark.Phase);
				spriteBatch.Draw(glow, pos, null, Magenta * (0.16f * pulse * alpha), 0f, origin, spark.Size * 1.45f, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, Color.White * (0.08f * pulse * alpha), 0f, origin, spark.Size * 0.32f, SpriteEffects.None, 0f);
			}
		}

		private static void DrawStars(SpriteBatch spriteBatch, Rectangle dest, Rectangle content, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			for (int i = 0; i < StarCount; i++) {
				Spark star = _stars[i];
				bool upper = star.Pos.Y < 0.5f;
				float fieldH = upper ? Math.Max(1, content.Y - dest.Y) : Math.Max(1, dest.Bottom - content.Bottom);
				float originY = upper ? dest.Y : content.Bottom;
				float x = (star.Pos.X + time * star.Drift.X) % 1f;
				if (x < 0f)
					x += 1f;
				float y = star.Pos.Y + MathF.Sin(time * star.Drift.Y + star.Phase) * 0.04f;
				float along = upper ? MathHelper.Clamp(y * 2f, 0f, 1f) : MathHelper.Clamp((y - 0.5f) * 2f, 0f, 1f);
				float drawY = originY + along * fieldH;
				if (upper && drawY > content.Y - 2)
					continue;
				if (!upper && drawY < content.Bottom + 2)
					continue;

				float twinkle = 0.35f + 0.65f * MathF.Sin(time * (1.3f + star.Size) + star.Phase);
				Color tint = Color.Lerp(star.Tint, Color.White, 0.35f);
				spriteBatch.Draw(
					glow,
					new Vector2(dest.X + x * dest.Width, drawY) + CalamitasMenuParallax.ForDepth(star.Depth),
					null,
					tint * (0.28f * twinkle * alpha),
					0f,
					origin,
					star.Size * 0.09f,
					SpriteEffects.None,
					0f);
			}
		}

		private static void EnsureSparks()
		{
			if (_spawned)
				return;

			_spawned = true;
			var rng = new Random(40002556);
			for (int i = 0; i < DustCount; i++) {
				_dust[i] = new Spark {
					Pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
					Drift = new Vector2((float)(rng.NextDouble() * 0.04 - 0.02), (float)(rng.NextDouble() * 0.03 - 0.01)),
					Size = (float)(0.04 + rng.NextDouble() * 0.07),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.2 + rng.NextDouble() * 0.7),
					Tint = Color.Lerp(new Color(180, 200, 255), Cyan, (float)rng.NextDouble())
				};
			}

			for (int i = 0; i < EmberCount; i++) {
				_embers[i] = new Spark {
					Pos = new Vector2(0.52f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble()),
					Drift = new Vector2((float)(rng.NextDouble() * 0.03 - 0.01), (float)(-0.06 - rng.NextDouble() * 0.05)),
					Size = (float)(0.06 + rng.NextDouble() * 0.12),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.35 + rng.NextDouble() * 0.9)
				};
			}

			for (int i = 0; i < MoteCount; i++) {
				_motes[i] = new Spark {
					Pos = new Vector2((float)rng.NextDouble() * 0.48f, (float)rng.NextDouble()),
					Drift = new Vector2((float)(0.015 + rng.NextDouble() * 0.03), (float)(rng.NextDouble() * 0.04 - 0.02)),
					Size = (float)(0.05 + rng.NextDouble() * 0.1),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.25 + rng.NextDouble() * 0.8)
				};
			}

			for (int i = 0; i < MagentaCount; i++) {
				_magenta[i] = new Spark {
					Pos = new Vector2((float)rng.NextDouble() * 0.38f, (float)rng.NextDouble() * 0.42f),
					Drift = new Vector2((float)(rng.NextDouble() * 0.03 - 0.01), (float)(-0.02 - rng.NextDouble() * 0.03)),
					Size = (float)(0.05 + rng.NextDouble() * 0.09),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.3 + rng.NextDouble() * 0.7)
				};
			}

			for (int i = 0; i < StarCount; i++) {
				bool cyan = rng.NextDouble() < 0.55;
				_stars[i] = new Spark {
					Pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
					Drift = new Vector2((float)(rng.NextDouble() * 0.012 - 0.006), (float)(0.08 + rng.NextDouble() * 0.12)),
					Size = (float)(0.35 + rng.NextDouble() * 0.7),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.4 + rng.NextDouble() * 0.7),
					Tint = cyan ? Cyan : Color.Lerp(Gold, Magenta, (float)rng.NextDouble())
				};
			}
		}

		private static void Along(Spark spark, float time, float speed, out float x, out float y)
		{
			x = spark.Pos.X + time * spark.Drift.X * speed + MathF.Sin(time * 0.55f + spark.Phase) * 0.03f;
			y = spark.Pos.Y + time * spark.Drift.Y * speed;
			x = Wrap(x);
			y = Wrap(y);
		}

		private static float Wrap(float value)
		{
			value %= 1f;
			if (value < 0f)
				value += 1f;
			return value;
		}
	}
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuWitch
	{
		private const string ArtPath = "DieWithASmile/Assets/Textures/Menu/WitchArt";
		private const float FadeSpeed = 0.018f;
		private const float OrbTexX = 2379f;
		private const float OrbTexY = 992f;
		private const int AshCount = 78;
		private const int EmberCount = 36;

		private struct Spark
		{
			public Vector2 Pos;
			public Vector2 Drift;
			public float Size;
			public float Phase;
			public float Depth;
			public float Heat;
			public Color Tint;
		}

		private static Asset<Texture2D> _art;
		private static float _scene;
		private static bool _spawned;
		private static readonly Spark[] _ash = new Spark[AshCount];
		private static readonly Spark[] _embers = new Spark[EmberCount];
		private static readonly Color Cover = new(16, 12, 14);
		private static readonly Color AshWhite = new(236, 228, 224);
		private static readonly Color AshRose = new(214, 168, 168);
		private static readonly Color EmberRed = new(196, 36, 48);
		private static readonly Color EmberHot = new(255, 120, 110);
		private static readonly Color NeonRed = new(255, 64, 72);

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
			float target = DieWithASmileSettings.UseWitchScene ? 1f : 0f;
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
			GetDestination(_art.Value, out Rectangle art);
			DrawCover(spriteBatch, alpha);
			spriteBatch.Draw(_art.Value, art, Color.White * alpha);
			DrawOrbGlow(spriteBatch, art, _art.Value, alpha);
			DrawAsh(spriteBatch, alpha);
			DrawEmbers(spriteBatch, alpha);
		}

		private static void GetDestination(Texture2D texture, out Rectangle art) =>
			art = CalamitasMenuDraw.CoverDestination(texture, 0.15f);

		private static void DrawCover(SpriteBatch spriteBatch, float alpha)
		{
			Point size = CalamitasMenuDraw.CoverSize;
			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				new Rectangle(-240, -240, size.X + 480, size.Y + 480),
				Cover * alpha);
		}

		private static void DrawOrbGlow(SpriteBatch spriteBatch, Rectangle art, Texture2D texture, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			float pulse = 0.72f + 0.28f * MathF.Sin(time * 1.15f);
			float breathe = 0.82f + 0.18f * MathF.Sin(time * 0.55f + 1.2f);
			Vector2 pos = new(
				art.X + OrbTexX / texture.Width * art.Width,
				art.Y + OrbTexY / texture.Height * art.Height);
			Vector2 origin = glow.Size() * 0.5f;
			float size = Math.Max(art.Width, art.Height) / 920f;

			spriteBatch.Draw(glow, pos, null, NeonRed * (0.18f * pulse * alpha), 0f, origin, size * 2.4f * breathe, SpriteEffects.None, 0f);
			spriteBatch.Draw(glow, pos, null, EmberRed * (0.28f * pulse * alpha), 0f, origin, size * 1.35f * breathe, SpriteEffects.None, 0f);
			spriteBatch.Draw(glow, pos, null, EmberHot * (0.32f * pulse * alpha), 0f, origin, size * 0.62f, SpriteEffects.None, 0f);
			spriteBatch.Draw(glow, pos, null, Color.White * (0.22f * pulse * alpha), 0f, origin, size * 0.28f, SpriteEffects.None, 0f);

			for (int i = 0; i < 3; i++) {
				float cycle = (time * 0.22f + i / 3f) % 1f;
				float ring = (0.55f + cycle * 1.35f) * size;
				float ringA = MathF.Sin(cycle * MathF.PI) * (1f - cycle) * 0.16f * alpha;
				spriteBatch.Draw(glow, pos, null, EmberHot * ringA, 0f, origin, ring, SpriteEffects.None, 0f);
			}
		}

		private static void EnsureSparks()
		{
			if (_spawned)
				return;

			_spawned = true;
			var rng = new Random(190823);
			for (int i = 0; i < AshCount; i++) {
				_ash[i] = new Spark {
					Pos = new Vector2((float)rng.NextDouble() * 1.1f, (float)rng.NextDouble()),
					Drift = new Vector2(
						(float)(-0.035 - rng.NextDouble() * 0.03),
						(float)(0.04 + rng.NextDouble() * 0.035)),
					Size = (float)(0.03 + rng.NextDouble() * 0.05),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.35 + rng.NextDouble() * 1.1),
					Heat = (float)rng.NextDouble(),
					Tint = Color.Lerp(AshWhite, AshRose, (float)rng.NextDouble())
				};
			}

			for (int i = 0; i < EmberCount; i++) {
				_embers[i] = new Spark {
					Pos = new Vector2(0.2f + (float)rng.NextDouble() * 0.95f, (float)rng.NextDouble()),
					Drift = new Vector2(
						(float)(-0.04 - rng.NextDouble() * 0.028),
						(float)(0.045 + rng.NextDouble() * 0.04)),
					Size = (float)(0.08 + rng.NextDouble() * 0.14),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.5 + rng.NextDouble() * 1.15),
					Heat = (float)(0.3 + rng.NextDouble() * 0.7)
				};
			}
		}

		private static void DrawAsh(SpriteBatch spriteBatch, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			for (int i = 0; i < AshCount; i++) {
				Spark spark = _ash[i];
				AlongWind(spark, time, 0.32f, out float x, out float y);
				float life = MathHelper.Clamp(1.02f - y, 0.12f, 1f) * MathHelper.Clamp((y + 0.08f) / 0.16f, 0f, 1f);
				float flicker = 0.55f + 0.45f * MathF.Sin(time * 4.6f + spark.Phase);
				Vector2 parallax = CalamitasMenuParallax.ForDepth(spark.Depth);
				spriteBatch.Draw(
					glow,
					new Vector2(x * CalamitasMenuDraw.CoverSize.X, y * CalamitasMenuDraw.CoverSize.Y) + parallax,
					null,
					spark.Tint * (0.28f * life * flicker * alpha),
					0f,
					origin,
					spark.Size,
					SpriteEffects.None,
					0f);
			}
		}

		private static void DrawEmbers(SpriteBatch spriteBatch, float alpha)
		{
			Texture2D glow = CalamitasMenuShine.Texture;
			if (glow == null)
				return;

			float time = Main.GlobalTimeWrappedHourly;
			Vector2 origin = glow.Size() * 0.5f;
			for (int i = 0; i < EmberCount; i++) {
				Spark spark = _embers[i];
				AlongWind(spark, time, 0.38f, out float x, out float y);
				float heat = 0.5f + 0.5f * MathF.Sin(time * (2.6f + spark.Heat) + spark.Phase);
				float life = MathHelper.Clamp((1.08f - y) * (0.4f + y * 0.75f), 0f, 1f);
				Color core = Color.Lerp(EmberRed, Color.White, heat * 0.35f);
				Vector2 pos = new Vector2(x * CalamitasMenuDraw.CoverSize.X, y * CalamitasMenuDraw.CoverSize.Y) + CalamitasMenuParallax.ForDepth(spark.Depth);
				spriteBatch.Draw(glow, pos, null, NeonRed * (0.12f * life * alpha), 0f, origin, spark.Size * 1.8f, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, core * (0.22f * life * heat * alpha), 0f, origin, spark.Size * 0.7f, SpriteEffects.None, 0f);
			}
		}

		private static void AlongWind(Spark spark, float time, float speed, out float x, out float y)
		{
			x = spark.Pos.X + time * spark.Drift.X * speed + MathF.Sin(time * (0.55f + spark.Heat) + spark.Phase) * 0.04f;
			y = spark.Pos.Y + time * spark.Drift.Y * speed;
			x = Wrap(x + 0.12f, 1.24f) - 0.12f;
			y = Wrap(y + 0.18f, 1.36f) - 0.18f;
		}

		private static float Wrap(float value, float span)
		{
			value %= span;
			if (value < 0f)
				value += span;
			return value;
		}
	}
}

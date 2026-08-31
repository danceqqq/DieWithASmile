using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuYharim
	{
		private const string ArtPath = "DieWithASmile/Assets/Textures/Menu/YharimArt";
		private const float FadeSpeed = 0.018f;
		private const int AshCount = 86;
		private const int EmberCount = 42;

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
		private static readonly Color Cover = new(18, 6, 12);
		private static readonly Color AshDark = new(52, 28, 28);
		private static readonly Color AshMid = new(86, 48, 42);
		private static readonly Color EmberRed = new(196, 28, 36);
		private static readonly Color EmberHot = new(255, 92, 64);
		private static readonly Color NeonRed = new(255, 48, 58);

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
			float target = DieWithASmileSettings.UseYharimScene ? 1f : 0f;
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
			DrawAsh(spriteBatch, alpha);
			DrawEmbers(spriteBatch, alpha);
		}

		private static void GetDestination(Texture2D texture, out Rectangle art) =>
			art = CalamitasMenuDraw.CoverDestination(texture, 0.16f);

		private static void DrawCover(SpriteBatch spriteBatch, float alpha)
		{
			Point size = CalamitasMenuDraw.CoverSize;
			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				new Rectangle(-240, -240, size.X + 480, size.Y + 480),
				Cover * alpha);
		}

		private static void EnsureSparks()
		{
			if (_spawned)
				return;

			_spawned = true;
			var rng = new Random(190819);
			for (int i = 0; i < AshCount; i++) {
				_ash[i] = new Spark {
					Pos = new Vector2((float)rng.NextDouble(), 0.35f + (float)rng.NextDouble() * 0.75f),
					Drift = new Vector2(
						(float)(rng.NextDouble() * 0.08 - 0.04),
						(float)(-0.045 - rng.NextDouble() * 0.07)),
					Size = (float)(0.035 + rng.NextDouble() * 0.06),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.35 + rng.NextDouble() * 1.1),
					Heat = (float)rng.NextDouble(),
					Tint = Color.Lerp(AshDark, AshMid, (float)rng.NextDouble())
				};
			}

			for (int i = 0; i < EmberCount; i++) {
				_embers[i] = new Spark {
					Pos = new Vector2(0.18f + (float)rng.NextDouble() * 0.64f, 0.55f + (float)rng.NextDouble() * 0.55f),
					Drift = new Vector2(
						(float)(rng.NextDouble() * 0.05 - 0.025),
						(float)(-0.06 - rng.NextDouble() * 0.09)),
					Size = (float)(0.09 + rng.NextDouble() * 0.16),
					Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
					Depth = (float)(0.55 + rng.NextDouble() * 1.2),
					Heat = (float)(0.35 + rng.NextDouble() * 0.65)
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
				Vector2 parallax = CalamitasMenuParallax.ForDepth(spark.Depth);
				float rise = time * spark.Drift.Y * -1f;
				float y = spark.Pos.Y - rise * 0.35f;
				y = ((y % 1.2f) + 1.2f) % 1.2f;
				float x = spark.Pos.X
					+ time * spark.Drift.X
					+ MathF.Sin(time * (0.7f + spark.Heat) + spark.Phase) * 0.045f;
				x = ((x % 1.08f) + 1.08f) % 1.08f - 0.04f;
				float life = MathHelper.Clamp(1.05f - y, 0.12f, 1f);
				float flicker = 0.55f + 0.45f * MathF.Sin(time * 5.4f + spark.Phase);
				Color color = spark.Tint * (0.42f * life * flicker * alpha);
				spriteBatch.Draw(
					glow,
					new Vector2(x * CalamitasMenuDraw.CoverSize.X, y * CalamitasMenuDraw.CoverSize.Y) + parallax,
					null,
					color,
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
				Vector2 parallax = CalamitasMenuParallax.ForDepth(spark.Depth);
				float rise = time * -spark.Drift.Y;
				float y = spark.Pos.Y - rise * 0.42f;
				y = ((y % 1.25f) + 1.25f) % 1.25f;
				float x = spark.Pos.X
					+ MathF.Sin(time * 0.55f + spark.Phase) * 0.06f
					+ time * spark.Drift.X;
				x = ((x % 1.1f) + 1.1f) % 1.1f - 0.05f;
				float heat = 0.5f + 0.5f * MathF.Sin(time * (3.2f + spark.Heat) + spark.Phase);
				float life = MathHelper.Clamp((1.15f - y) * (0.35f + y * 0.9f), 0f, 1f);
				Color core = Color.Lerp(EmberRed, EmberHot, heat);
				Vector2 pos = new Vector2(x * CalamitasMenuDraw.CoverSize.X, y * CalamitasMenuDraw.CoverSize.Y) + parallax;
				spriteBatch.Draw(glow, pos, null, NeonRed * (0.16f * life * alpha), 0f, origin, spark.Size * 1.85f, SpriteEffects.None, 0f);
				spriteBatch.Draw(glow, pos, null, core * (0.28f * life * heat * alpha), 0f, origin, spark.Size * 0.72f, SpriteEffects.None, 0f);
			}
		}
	}
}

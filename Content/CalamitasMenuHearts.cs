using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuHearts
	{
		private const string HeartPath = "DieWithASmile/Assets/Textures/Menu/SoulHeart";
		private const float ChestTexX = 1691f;
		private const float ChestTexY = 580f;
		private const float BodyTexX = 1760f;
		private const float BodyTexY = 720f;
		private const float BodyRadiusX = 155f;
		private const float BodyRadiusY = 310f;

		private static readonly Color[] Colors =
		{
			new(46, 92, 230),
			new(255, 138, 36),
			new(70, 214, 236),
			new(168, 72, 224),
			new(48, 214, 74),
			new(255, 214, 48)
		};

		private static Asset<Texture2D> _source;
		private static Texture2D _heart;
		private static float _scene;

		internal static float Scene => _scene;
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
			_source = ModContent.Request<Texture2D>(HeartPath);
		}

		internal static void Unload()
		{
			Texture2D tex = _heart;
			_heart = null;
			_source = null;
			if (tex == null || tex.IsDisposed)
				return;

			Main.QueueMainThreadAction(() => {
				try {
					if (!tex.IsDisposed)
						tex.Dispose();
				}
				catch {
				}
			});
		}

		internal static void Reset() => _scene = 0f;

		internal static void Update()
		{
			float target = DieWithASmileSettings.UseDontForgetScene ? 1f : 0f;
			const float fadeSpeed = 0.016f;
			if (_scene < target)
				_scene = MathHelper.Min(target, _scene + fadeSpeed);
			else if (_scene > target)
				_scene = MathHelper.Max(target, _scene - fadeSpeed);
		}

		internal static void DrawBehind(SpriteBatch spriteBatch, float fade)
		{
			DrawLayer(spriteBatch, fade, behind: true);
		}

		internal static void DrawFront(SpriteBatch spriteBatch, float fade)
		{
			DrawLayer(spriteBatch, fade, behind: false);
		}

		private static void DrawLayer(SpriteBatch spriteBatch, float fade, bool behind)
		{
			if (SceneEase <= 0.02f || fade <= 0f)
				return;

			Texture2D heart = GetHeart();
			Texture2D glow = CalamitasMenuShine.Texture;
			if (heart == null)
				return;

			Vector2 chest = CalamitasMenuBackgroundStyle.GetScreenPoint(ChestTexX, ChestTexY);
			Vector2 body = CalamitasMenuBackgroundStyle.GetScreenPoint(BodyTexX, BodyTexY);
			Vector2 bodySize = GetTexSize(BodyRadiusX, BodyRadiusY);
			float time = Main.GlobalTimeWrappedHourly;
			float beat = 0.92f + 0.08f * CalamitasMenuSpectrum.SmoothBeat;

			for (int i = 0; i < Colors.Length; i++) {
				float depth = GetDepth(i, time);
				bool isBehind = depth < 0f;
				if (isBehind != behind)
					continue;

				GetOrbit(i, time, chest, out Vector2 pos, out float scale);
				float occlude = isBehind ? Occlusion(pos, body, bodySize) : 1f;
				float alpha = SceneEase * fade * occlude * MathHelper.Lerp(0.55f, 1f, (depth + 1f) * 0.5f);
				if (alpha < 0.02f)
					continue;

				float drawScale = scale * beat * (0.034f + 0.01f * depth);
				Color color = Colors[i] * alpha;
				if (glow != null) {
					spriteBatch.Draw(
						glow,
						pos,
						null,
						color * 0.7f,
						0f,
						glow.Size() * 0.5f,
						drawScale * heart.Width * 1.35f / glow.Width,
						SpriteEffects.None,
						0f);
					spriteBatch.Draw(
						glow,
						pos,
						null,
						Color.White * (0.22f * alpha),
						0f,
						glow.Size() * 0.5f,
						drawScale * heart.Width * 0.45f / glow.Width,
						SpriteEffects.None,
						0f);
				}

				spriteBatch.Draw(
					heart,
					pos,
					null,
					color,
					0f,
					heart.Size() * 0.5f,
					drawScale,
					SpriteEffects.None,
					0f);
			}
		}

		private static void GetOrbit(int index, float time, Vector2 chest, out Vector2 pos, out float scale)
		{
			float phase = time * (0.55f + index * 0.07f) + index * MathHelper.TwoPi / Colors.Length;
			float depth = MathF.Sin(phase);
			float swing = 118f + 18f * MathF.Sin(time * 0.8f + index);
			float rise = 86f + 14f * MathF.Cos(time * 0.65f + index * 1.4f);
			float bob = 10f * MathF.Sin(time * 1.6f + index * 2.1f);
			pos = new Vector2(
				chest.X + MathF.Cos(phase) * swing,
				chest.Y - 18f + MathF.Cos(phase * 0.5f + index) * rise * 0.35f + depth * 22f + bob);
			scale = 1f;
		}

		private static float GetDepth(int index, float time)
		{
			float phase = time * (0.55f + index * 0.07f) + index * MathHelper.TwoPi / Colors.Length;
			return MathF.Sin(phase);
		}

		private static float Occlusion(Vector2 pos, Vector2 body, Vector2 radius)
		{
			float nx = (pos.X - body.X) / Math.Max(radius.X, 1f);
			float ny = (pos.Y - body.Y) / Math.Max(radius.Y, 1f);
			float dist = MathF.Sqrt(nx * nx + ny * ny);
			return MathHelper.SmoothStep(0f, 1f, (dist - 0.42f) / 0.55f);
		}

		private static Vector2 GetTexSize(float texW, float texH)
		{
			Vector2 a = CalamitasMenuBackgroundStyle.GetScreenPoint(0f, 0f);
			Vector2 b = CalamitasMenuBackgroundStyle.GetScreenPoint(texW, texH);
			return new Vector2(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
		}

		private static Texture2D GetHeart()
		{
			if (_heart != null && !_heart.IsDisposed)
				return _heart;

			if (_source?.Value == null)
				return null;

			Texture2D src = _source.Value;
			int w = src.Width;
			int h = src.Height;
			var data = new Color[w * h];
			src.GetData(data);
			for (int i = 0; i < data.Length; i++) {
				Color c = data[i];
				int mask = Math.Max(0, c.G - Math.Max(c.R, c.B));
				byte a = (byte)Math.Min(255, mask * 2);
				data[i] = new Color(a, a, a, a);
			}

			_heart = new Texture2D(Main.graphics.GraphicsDevice, w, h);
			_heart.SetData(data);
			return _heart;
		}
	}
}

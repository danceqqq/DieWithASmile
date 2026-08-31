using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuShine
	{
		private static Texture2D _shineTexture;

		internal static Texture2D Texture => GetTexture();

		internal static void Unload()
		{
			Texture2D tex = _shineTexture;
			_shineTexture = null;
			if (tex == null || tex.IsDisposed)
				return;

			Main.QueueMainThreadAction(() => {
				try {
					if (!tex.IsDisposed)
						tex.Dispose();
				}
				catch {
					// Graphics device may already be gone during unload.
				}
			});
		}

		internal static void Draw(SpriteBatch spriteBatch, Vector2 position, float size, float fade, float pulse)
		{
			if (fade <= 0f)
				return;

			Texture2D shine = GetTexture();
			if (shine == null)
				return;

			Vector2 origin = new(shine.Width * 0.5f, shine.Height * 0.5f);
			float time = Main.GlobalTimeWrappedHourly;
			pulse = MathHelper.Clamp(pulse, 0.15f, 1f);

			var rubyColor = new Color(196, 48, 78);
			var shineColor = new Color(255, 186, 198);

			spriteBatch.Draw(shine, position, null, rubyColor * (0.42f * pulse * fade), 0f, origin, 0.72f * size, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, position, null, shineColor * (0.38f * pulse * fade), 0f, origin, 0.28f * size * (0.85f + 0.2f * pulse), SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, position, null, Color.White * (0.22f * pulse * fade), 0f, origin, new Vector2(1.55f, 0.11f) * size, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, position, null, Color.White * (0.18f * pulse * fade), 0f, origin, new Vector2(0.11f, 1.35f) * size, SpriteEffects.None, 0f);

			const int rings = 4;
			for (int i = 0; i < rings; i++) {
				float cycle = (time * 0.48f + i / (float)rings) % 1f;
				float ringScale = (0.22f + cycle * 1.35f) * size;
				float ringAlpha = MathF.Sin(cycle * MathF.PI) * (1f - cycle) * 0.38f * fade;
				spriteBatch.Draw(shine, position, null, rubyColor * ringAlpha, 0f, origin, ringScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(shine, position, null, shineColor * (ringAlpha * 0.45f), 0f, origin, ringScale * 0.55f, SpriteEffects.None, 0f);
			}

			const int sparks = 5;
			for (int i = 0; i < sparks; i++) {
				float ang = time * 1.7f + i * MathHelper.TwoPi / sparks;
				float radius = (10f + 7f * MathF.Sin(time * 2.8f + i)) * (size / 1.05f);
				Vector2 pos = position + ang.ToRotationVector2() * radius;
				float sparkAlpha = (0.18f + 0.16f * MathF.Sin(time * 4.5f + i * 1.3f)) * fade;
				spriteBatch.Draw(shine, pos, null, shineColor * sparkAlpha, ang, origin, 0.08f * size, SpriteEffects.None, 0f);
			}
		}

		private static Texture2D GetTexture()
		{
			if (_shineTexture != null && !_shineTexture.IsDisposed)
				return _shineTexture;

			const int size = 128;
			_shineTexture = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			var data = new Color[size * size];
			float center = (size - 1) * 0.5f;
			float maxDist = size * 0.5f;

			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					float dx = x - center;
					float dy = y - center;
					float dist = MathF.Sqrt(dx * dx + dy * dy) / maxDist;
					float a = MathHelper.Clamp(1f - dist, 0f, 1f);
					a = a * a * (3f - 2f * a);
					a *= a;
					byte v = (byte)(a * 255f);
					data[y * size + x] = new Color(v, v, v, v);
				}
			}

			_shineTexture.SetData(data);
			return _shineTexture;
		}
	}
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuLetter
	{
		private const string HandsPath = "DieWithASmile/Assets/Textures/Menu/ComeAlongHands";
		private const float FadeSpeed = 0.016f;

		private static Asset<Texture2D> _hands;
		private static float _scene;

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
			_hands = ModContent.Request<Texture2D>(HandsPath);
		}

		internal static void Unload() => _hands = null;

		internal static void Reset() => _scene = 0f;

		internal static void Update()
		{
			float target = DieWithASmileSettings.UseComeAlongScene ? 1f : 0f;
			if (_scene < target)
				_scene = MathHelper.Min(target, _scene + FadeSpeed);
			else if (_scene > target)
				_scene = MathHelper.Max(target, _scene - FadeSpeed);
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			float alpha = SceneEase * fade;
			if (alpha <= 0.02f || _hands?.Value == null)
				return;

			Texture2D tex = _hands.Value;
			Point cover = CalamitasMenuDraw.CoverSize;
			float scale = MathHelper.Min(cover.X * 0.78f, 1480f) / tex.Width;
			Vector2 origin = new(tex.Width, tex.Height);
			Vector2 pos = new(cover.X, cover.Y);
			pos.X += CalamitasMenuParallax.ForDepth(-0.7f).X;

			spriteBatch.Draw(
				tex,
				pos,
				null,
				Color.White * alpha,
				0f,
				origin,
				scale,
				SpriteEffects.None,
				0f);
		}
	}
}

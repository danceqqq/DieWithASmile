using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuDraw
	{
		private static readonly RasterizerState ClipRast = new()
		{
			CullMode = CullMode.CullCounterClockwiseFace,
			ScissorTestEnable = true
		};

		internal static Rectangle? Scissor { get; private set; }

		internal static Point CoverSize
		{
			get
			{
				int w = Math.Max(1, Main.screenWidth);
				int h = Math.Max(1, Main.screenHeight);
				GraphicsDevice gd = Main.instance?.GraphicsDevice;
				if (gd != null) {
					if (gd.Viewport.Width > 0)
						w = Math.Max(w, gd.Viewport.Width);
					if (gd.Viewport.Height > 0)
						h = Math.Max(h, gd.Viewport.Height);
					PresentationParameters pp = gd.PresentationParameters;
					if (pp != null) {
						if (pp.BackBufferWidth > 0)
							w = Math.Max(w, pp.BackBufferWidth);
						if (pp.BackBufferHeight > 0)
							h = Math.Max(h, pp.BackBufferHeight);
					}
				}

				return new Point(w, h);
			}
		}

		internal static Rectangle CoverRect
		{
			get
			{
				Point size = CoverSize;
				return new Rectangle(0, 0, size.X, size.Y);
			}
		}

		internal static Rectangle CoverDestination(Texture2D tex, float parallaxDepth = 0f, float overdraw = 1.05f)
		{
			Point cover = CoverSize;
			float scale = Math.Max(
				cover.X / (float)Math.Max(1, tex.Width),
				cover.Y / (float)Math.Max(1, tex.Height)) * overdraw;
			Vector2 shift = Math.Abs(parallaxDepth) > 0.0001f ? CalamitasMenuParallax.ForDepth(parallaxDepth) : Vector2.Zero;
			int w = Math.Max(1, (int)(tex.Width * scale));
			int h = Math.Max(1, (int)(tex.Height * scale));
			return new Rectangle(
				(int)((cover.X - w) * 0.5f + shift.X),
				(int)((cover.Y - h) * 0.5f + shift.Y),
				w,
				h);
		}

		internal static Rectangle FitScreenDestination(Texture2D tex, float parallaxDepth = 0f)
		{
			Point cover = CoverSize;
			float scale = Math.Min(
				cover.X / (float)Math.Max(1, tex.Width),
				cover.Y / (float)Math.Max(1, tex.Height));
			Vector2 shift = Math.Abs(parallaxDepth) > 0.0001f ? CalamitasMenuParallax.ForDepth(parallaxDepth) : Vector2.Zero;
			int w = Math.Max(1, (int)(tex.Width * scale));
			int h = Math.Max(1, (int)(tex.Height * scale));
			int minX = Math.Min(0, cover.X - w);
			int maxX = Math.Max(0, cover.X - w);
			int minY = Math.Min(0, cover.Y - h);
			int maxY = Math.Max(0, cover.Y - h);
			float xShift = w >= cover.X - 1 ? 0f : shift.X;
			int x = (int)Math.Clamp((cover.X - w) * 0.5f + xShift, minX, maxX);
			int y = (int)Math.Clamp((cover.Y - h) * 0.5f + shift.Y, minY, maxY);
			return new Rectangle(x, y, w, h);
		}

		internal static void BeginUi(SpriteBatch spriteBatch, Matrix? matrix = null)
		{
			RasterizerState rast = Scissor.HasValue ? ClipRast : RasterizerState.CullCounterClockwise;
			spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.LinearClamp,
				DepthStencilState.None,
				rast,
				null,
				matrix ?? Main.UIScaleMatrix);
			if (Scissor.HasValue)
				Main.instance.GraphicsDevice.ScissorRectangle = Scissor.Value;
		}

		internal static void WithLinear(SpriteBatch spriteBatch, Action draw)
		{
			spriteBatch.End();
			BeginUi(spriteBatch);
			draw();
			spriteBatch.End();
			BeginUi(spriteBatch);
		}

		internal static void WithScreen(SpriteBatch spriteBatch, Action draw)
		{
			spriteBatch.End();
			spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.LinearClamp,
				DepthStencilState.None,
				RasterizerState.CullCounterClockwise,
				null,
				Matrix.Identity);
			draw();
			spriteBatch.End();
			BeginUi(spriteBatch);
		}

		internal static void WithClip(SpriteBatch spriteBatch, Rectangle uiClip, Action draw)
		{
			GraphicsDevice gd = Main.instance.GraphicsDevice;
			Rectangle scaled = ToNative(uiClip);
			scaled = Rectangle.Intersect(scaled, gd.Viewport.Bounds);
			if (scaled.Width < 2 || scaled.Height < 2)
				return;

			Rectangle old = gd.ScissorRectangle;
			spriteBatch.End();
			Scissor = scaled;
			gd.ScissorRectangle = scaled;
			BeginUi(spriteBatch);
			draw();
			spriteBatch.End();
			Scissor = null;
			gd.ScissorRectangle = old;
			BeginUi(spriteBatch);
		}

		private static Rectangle ToNative(Rectangle rect)
		{
			float scale = Main.UIScale;
			int x = (int)MathF.Floor(rect.X * scale);
			int y = (int)MathF.Floor(rect.Y * scale);
			int w = (int)MathF.Ceiling(rect.Width * scale);
			int h = (int)MathF.Ceiling(rect.Height * scale);
			return new Rectangle(x, y, Math.Max(1, w), Math.Max(1, h));
		}
	}
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public class CalamitasMenuBackgroundStyle : ModSurfaceBackgroundStyle
	{
		private const string BackgroundPath = "DieWithASmile/Assets/Textures/Menu/CalamitasBackground";
		private const string DeltaruneBackgroundPath = "DieWithASmile/Assets/Textures/Menu/DeltaruneHeartsBackground";
		private const string ComeAlongBackgroundPath = "DieWithASmile/Assets/Textures/Menu/ComeAlongBackground";
		private const float BackgroundOverdraw = 1.05f;
		private const float RubyTexX = 1702f;
		private const float RubyTexY = 581f;

		private static Asset<Texture2D> _backgroundTexture;
		private static Asset<Texture2D> _deltaruneTexture;
		private static Asset<Texture2D> _comeAlongTexture;

		internal static float FadeAlpha { get; private set; }

		internal static bool DrewThisFrame { get; set; }

		internal static Vector2 LastCoverExtra { get; private set; }

		internal static void ResetFade()
		{
			FadeAlpha = 0f;
			CalamitasMenuHearts.Reset();
			CalamitasMenuLetter.Reset();
			CalamitasMenuMeadow.Reset();
			CalamitasMenuYharim.Reset();
			CalamitasMenuWitch.Reset();
			CalamitasMenuSoul.Reset();
		}

		internal static void UpdateFade()
		{
			FadeAlpha = MathHelper.Clamp(FadeAlpha + 0.012f, 0f, 1f);
			CalamitasMenuHearts.Update();
			CalamitasMenuLetter.Update();
			CalamitasMenuMeadow.Update();
			CalamitasMenuYharim.Update();
			CalamitasMenuWitch.Update();
			CalamitasMenuSoul.Update();
		}

		public override void Load()
		{
			_backgroundTexture = ModContent.Request<Texture2D>(BackgroundPath);
			_deltaruneTexture = ModContent.Request<Texture2D>(DeltaruneBackgroundPath);
			_comeAlongTexture = ModContent.Request<Texture2D>(ComeAlongBackgroundPath);
			CalamitasMenuHearts.Load();
			CalamitasMenuLetter.Load();
			CalamitasMenuMeadow.Load();
			CalamitasMenuYharim.Load();
			CalamitasMenuWitch.Load();
			CalamitasMenuSoul.Load();
		}

		public override void Unload()
		{
			CalamitasMenuShine.Unload();
			CalamitasMenuHearts.Unload();
			CalamitasMenuLetter.Unload();
			CalamitasMenuMeadow.Unload();
			CalamitasMenuYharim.Unload();
			CalamitasMenuWitch.Unload();
			CalamitasMenuSoul.Unload();
			CalamitasMenuUserArt.Unload();
			CalamitasMenuForeign.Unload();
			CalamitasMenuPlayerUI.Unload();
			CalamitasMenuIcons.Unload();
			CalamitasMenuPlaylist.Unload();
		}

		public override void ModifyFarFades(float[] fades, float transitionSpeed)
		{
			for (int i = 0; i < fades.Length; i++)
				fades[i] -= transitionSpeed * 2f;

			fades[Slot] += transitionSpeed * 3f;

			if (fades[Slot] > 1f)
				fades[Slot] = 1f;
		}

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (DrewThisFrame)
				return;

			if (_backgroundTexture?.Value == null || FadeAlpha <= 0f)
				return;

			CalamitasMenuDraw.WithScreen(spriteBatch, () => {
				try {
					DrawScreen(spriteBatch);
				}
				catch {
					DieWithASmileSettings.AbandonBrokenWallpaper();
				}
			});
		}

		private static void DrawScreen(SpriteBatch spriteBatch)
		{
			if (DieWithASmileSettings.UsingPassthroughSky) {
				CalamitasMenuLayout.DrawBackgroundDim(spriteBatch);
				DrewThisFrame = true;
				return;
			}

			if (DieWithASmileSettings.UsingFileWallpaper && CalamitasMenuUserArt.TryGetSelectedWallpaper(out Texture2D custom)) {
				GetBackgroundDestination(custom, true, DieWithASmileSettings.LiveWallpaperPan, out Rectangle customDest, out _);
				spriteBatch.Draw(custom, customDest, Color.White * FadeAlpha);
				CalamitasMenuLayout.DrawBackgroundDim(spriteBatch);
				DrewThisFrame = true;
				return;
			}

			if (DieWithASmileSettings.UsingForeignWallpaper && CalamitasMenuForeign.TryDrawWallpaper(spriteBatch, FadeAlpha)) {
				CalamitasMenuLayout.DrawBackgroundDim(spriteBatch);
				DrewThisFrame = true;
				return;
			}

			if (DieWithASmileSettings.UsingOrphanWallpaper && CalamitasMenuForeign.TryDrawOrphan(spriteBatch, FadeAlpha)) {
				CalamitasMenuLayout.DrawBackgroundDim(spriteBatch);
				DrewThisFrame = true;
				return;
			}

			Texture2D texture = _backgroundTexture.Value;
			GetBackgroundDestination(texture, out Rectangle destination, out float scale);
			float hearts = CalamitasMenuHearts.SceneEase;
			float letter = CalamitasMenuLetter.SceneEase;
			float meadow = CalamitasMenuMeadow.SceneEase;
			float yharim = CalamitasMenuYharim.SceneEase;
			float witch = CalamitasMenuWitch.SceneEase;
			float soul = CalamitasMenuSoul.SceneEase;
			float special = MathHelper.Clamp(hearts + letter + meadow + yharim + witch + soul, 0f, 1f);
			spriteBatch.Draw(texture, destination, Color.White * (FadeAlpha * (1f - special)));

			if (hearts > 0.02f && _deltaruneTexture?.Value != null)
				spriteBatch.Draw(_deltaruneTexture.Value, destination, Color.White * (FadeAlpha * hearts));

			if (letter > 0.02f && _comeAlongTexture?.Value != null)
				spriteBatch.Draw(_comeAlongTexture.Value, destination, Color.White * (FadeAlpha * letter));

			CalamitasMenuMeadow.Draw(spriteBatch, FadeAlpha);
			CalamitasMenuYharim.Draw(spriteBatch, FadeAlpha);
			CalamitasMenuWitch.Draw(spriteBatch, FadeAlpha);
			CalamitasMenuSoul.Draw(spriteBatch, FadeAlpha);
			CalamitasMenuHearts.DrawBehind(spriteBatch, FadeAlpha);

			if (letter < 0.98f && meadow < 0.98f && yharim < 0.98f && witch < 0.98f && soul < 0.98f) {
				Vector2 ruby = GetScreenPoint(RubyTexX, RubyTexY);
				float pulse = 0.55f + 0.45f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.2f);
				CalamitasMenuShine.Draw(spriteBatch, ruby, scale * 1.05f, FadeAlpha * (1f - letter), pulse);
			}

			CalamitasMenuHearts.DrawFront(spriteBatch, FadeAlpha);
			CalamitasMenuLetter.Draw(spriteBatch, FadeAlpha);
			CalamitasMenuLayout.DrawBackgroundDim(spriteBatch);
			DrewThisFrame = true;
		}

		internal static Vector2 GetScreenPoint(float texX, float texY, bool parallax = true)
		{
			if (_backgroundTexture?.Value == null)
				return new Vector2(Main.screenWidth * 0.72f, Main.screenHeight * 0.28f);

			Texture2D texture = _backgroundTexture.Value;
			GetBackgroundDestination(texture, parallax, out Rectangle destination, out _);
			return new Vector2(
				destination.X + texX / texture.Width * destination.Width,
				destination.Y + texY / texture.Height * destination.Height);
		}

		public override bool PreDrawCloseBackground(SpriteBatch spriteBatch)
		{
			return DieWithASmileSettings.UsingPassthroughSky;
		}

		private static void GetBackgroundDestination(Texture2D texture, out Rectangle destination, out float scale) =>
			GetBackgroundDestination(texture, true, new Vector2(0.5f, 0.5f), out destination, out scale);

		private static void GetBackgroundDestination(Texture2D texture, bool parallax, out Rectangle destination, out float scale) =>
			GetBackgroundDestination(texture, parallax, new Vector2(0.5f, 0.5f), out destination, out scale);

		private static void GetBackgroundDestination(Texture2D texture, bool parallax, Vector2 pan, out Rectangle destination, out float scale)
		{
			Point cover = CalamitasMenuDraw.CoverSize;
			scale = MathHelper.Max(
				cover.X / (float)texture.Width,
				cover.Y / (float)texture.Height) * BackgroundOverdraw;

			Vector2 shift = parallax ? CalamitasMenuParallax.ForDepth(0.45f) : Vector2.Zero;
			int drawWidth = (int)(texture.Width * scale);
			int drawHeight = (int)(texture.Height * scale);
			float extraX = drawWidth - cover.X;
			float extraY = drawHeight - cover.Y;
			LastCoverExtra = new Vector2(Math.Max(0f, extraX), Math.Max(0f, extraY));
			float px = MathHelper.Clamp(pan.X, 0f, 1f);
			float py = MathHelper.Clamp(pan.Y, 0f, 1f);
			float x = extraX > 0f ? -extraX * px : (cover.X - drawWidth) * 0.5f;
			float y = extraY > 0f ? -extraY * py : (cover.Y - drawHeight) * 0.5f;
			destination = new Rectangle(
				(int)(x + shift.X),
				(int)(y + shift.Y),
				drawWidth,
				drawHeight);
		}
	}
}

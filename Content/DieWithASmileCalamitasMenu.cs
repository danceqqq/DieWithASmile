using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public class DieWithASmileCalamitasMenu : ModMenu
	{
		private const string EmptyTexturePath = "DieWithASmile/Assets/Textures/Menu/Empty";

		private Asset<Texture2D> _emptyTexture;
		private static bool _ticked;

		public override string DisplayName => Language.GetTextValue("Mods.DieWithASmile.DisplayName");

		public override ModSurfaceBackgroundStyle MenuBackgroundStyle
		{
			get
			{
				if (DieWithASmileSettings.UsingVanillaWallpaper)
					return null;

				if (DieWithASmileSettings.UsingTmlWallpaper)
					return CalamitasMenuForeign.TmlMenu?.MenuBackgroundStyle;

				if (DieWithASmileSettings.UsingForeignWallpaper) {
					ModMenu foreign = CalamitasMenuForeign.Find(DieWithASmileSave.Data.ForeignWallpaperId);
					return foreign?.MenuBackgroundStyle;
				}

				return ModContent.GetInstance<CalamitasMenuBackgroundStyle>();
			}
		}

		public override Asset<Texture2D> SunTexture =>
			DieWithASmileSettings.UsingPassthroughSky ? HostSun() : _emptyTexture;

		public override Asset<Texture2D> MoonTexture =>
			DieWithASmileSettings.UsingPassthroughSky ? HostMoon() : _emptyTexture;

		public override int Music => CalamitasMenuPlaylist.MenuMusicId;

		public override void Load()
		{
			_emptyTexture = ModContent.Request<Texture2D>(EmptyTexturePath);
			CalamitasMenuPlaylist.Load(Mod);
			CalamitasMenuSpectrum.Load();
			CalamitasMenuLogo.Load();
			CalamitasMenuIcons.Load();
			CalamitasMenuPanels.Load();
			CalamitasMenuForeign.Load();
		}

		public override void OnSelected()
		{
			CalamitasMenuPersist.OnOurMenuSelected();
			CalamitasMenuBackgroundStyle.ResetFade();
			CalamitasMenuSpectrum.Reset();
			CalamitasMenuPlayerUI.Reset();
			CalamitasMenuPanels.Reset();
			CalamitasMenuLayout.Reset();
			CalamitasMenuUserArt.Scan();
			CalamitasMenuForeign.SnapshotContent();
			CalamitasMenuForeign.DropMissing();
			CalamitasMenuPlaylist.OnThemeSelected();
		}

		public override void OnDeselected()
		{
			CalamitasMenuLayout.Cancel(restore: false);
			CalamitasMenuPersist.OnOurMenuDeselected();
		}

		public override void Update(bool isOnTitleScreen)
		{
			if (!Main.gameMenu)
				return;

			if (!isOnTitleScreen && !CoolerMenuCompat.OnTitleLike)
				return;

			Tick();
		}

		internal void Tick()
		{
			if (!Main.gameMenu || _ticked)
				return;

			_ticked = true;
			CalamitasMenuPlaylist.HandleMenuLifecycle();
			if (!CoolerMenuCompat.MenuBackdropActive)
				return;

			CalamitasMenuForeign.BeginFrame();
			if (DieWithASmileSettings.UsingVanillaWallpaper)
				Main.bgStyle = DieWithASmileSave.Data.VanillaBgStyle;
			CalamitasMenuBackgroundStyle.DrewThisFrame = false;
			CalamitasMenuBackgroundStyle.UpdateFade();
			CalamitasMenuSpectrum.Update(Mod);
			if (!CoolerMenuCompat.OnTitleLike)
				return;

			DieWithASmileSettings.TickScenes();
			CalamitasMenuLayout.Update();
			CalamitasMenuPanels.Update();
			CalamitasMenuPlayerUI.HandleTitleInput();
			CalamitasMenuLogo.HandleTitleInput();
			CalamitasMenuLayout.HandleTitleInput();
			CalamitasMenuPlayerUI.Update();
		}

		public override void PostDrawLogo(
			SpriteBatch spriteBatch,
			Vector2 logoDrawCenter,
			float logoRotation,
			float logoScale,
			Color drawColor)
		{
			Tick();
			if (!CoolerMenuCompat.MenuBackdropActive) {
				_ticked = false;
				return;
			}

			if (!CalamitasMenuBackgroundStyle.DrewThisFrame)
				CalamitasMenuBackgroundStyle.Draw(spriteBatch);

			CalamitasMenuLogo.Draw(spriteBatch, CalamitasMenuBackgroundStyle.FadeAlpha);
			if (CoolerMenuCompat.OnTitleLike) {
				CalamitasMenuPlayerUI.Draw(spriteBatch, CalamitasMenuBackgroundStyle.FadeAlpha);
				CalamitasMenuPanels.Draw(spriteBatch, CalamitasMenuBackgroundStyle.FadeAlpha);
			}

			CalamitasMenuPanels.EndFrame();
			CalamitasMenuLogo.EndFrame();
			CalamitasMenuLayout.EndFrame();
			CalamitasMenuPlayerUI.EndFrame();
			_ticked = false;
		}

		public override bool PreDrawLogo(
			SpriteBatch spriteBatch,
			ref Vector2 logoDrawCenter,
			ref float logoRotation,
			ref float logoScale,
			ref Color drawColor)
		{
			Tick();
			if (!CoolerMenuCompat.MenuBackdropActive)
				return false;

			CalamitasMenuBackgroundStyle.Draw(spriteBatch);
			return false;
		}

		private Asset<Texture2D> HostSun()
		{
			if (DieWithASmileSettings.UsingTmlWallpaper) {
				try {
					Asset<Texture2D> sun = CalamitasMenuForeign.TmlMenu?.SunTexture;
					if (sun != null)
						return sun;
				}
				catch {
				}
			}

			return base.SunTexture;
		}

		private Asset<Texture2D> HostMoon()
		{
			if (DieWithASmileSettings.UsingTmlWallpaper) {
				try {
					Asset<Texture2D> moon = CalamitasMenuForeign.TmlMenu?.MoonTexture;
					if (moon != null)
						return moon;
				}
				catch {
				}
			}

			return base.MoonTexture;
		}
	}
}

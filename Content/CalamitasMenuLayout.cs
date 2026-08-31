using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuLayout
	{
		private const int MenuRows = 7;
		internal const int MenuStep = 68;

		private static bool _editing;
		private static bool _draggingMenu;
		private static bool _draggingPan;
		private static bool _dragMoved;
		private static bool _dragBound;
		private static bool _frameInput;
		private static bool _mouseHeld;
		private static int _blockThemeSwap;
		private static float _dim;
		private static Vector2 _workLogo;
		private static Vector2 _workPlayer;
		private static Vector2 _workMenu;
		private static Vector2 _workPan = new(0.5f, 0.5f);
		private static float _workLogoScale = 1f;
		private static Vector2 _dragOffset;
		private static Vector2 _dragStartMouse;

		internal static bool Editing => _editing;

		internal static bool Busy => _draggingMenu || _draggingPan;

		internal static Vector2 WorkPan => _workPan;

		internal static bool CanPanWallpaper =>
			DieWithASmileSettings.UsingFileWallpaper && CalamitasMenuUserArt.TryGetSelectedWallpaper(out _);

		internal static bool ShouldBlockThemeSwap => _editing || _blockThemeSwap > 0;

		internal static float Dim => _dim;

		internal static Vector2 Logo
		{
			get => _editing ? _workLogo : CalamitasMenuLogo.SavedOrDefaultAnchor;
			set => _workLogo = value;
		}

		internal static Vector2 Player
		{
			get => _editing ? _workPlayer : CalamitasMenuPlayerUI.SavedOrDefaultAnchor;
			set => _workPlayer = value;
		}

		internal static float LogoScale
		{
			get => _editing ? _workLogoScale : DieWithASmileSettings.LogoScale;
			set => _workLogoScale = MathHelper.Clamp(value, 0.45f, 2f);
		}

		internal static Vector2 Menu
		{
			get => _editing ? _workMenu : SavedOrDefaultMenu;
			set => _workMenu = ClampMenu(value);
		}

		internal static Vector2 DefaultMenu
		{
			get
			{
				int last = (int)(Main.screenHeight * 0.82f);
				int first = last - (MenuRows - 1) * MenuStep;
				return new Vector2(CalamitasMenuButtonSystem.LeftMenuButtonX, Math.Max(250, first));
			}
		}

		internal static Vector2 SavedOrDefaultMenu =>
			DieWithASmileSettings.HasCustomMenuPosition
				? ClampMenu(DieWithASmileSettings.MenuAnchorPixels)
				: DefaultMenu;

		internal static void Reset()
		{
			Cancel(restore: false);
			_dim = 0f;
		}

		internal static void Update()
		{
			if (!Main.gameMenu || !CoolerMenuCompat.OnTitleLike) {
				Cancel(restore: false);
				_dim = 0f;
				return;
			}

			_dim = MathHelper.Lerp(_dim, _editing ? 1f : 0f, 0.18f);
			if (_blockThemeSwap > 0)
				_blockThemeSwap--;
			if (_dim < 0.004f && !_editing)
				_dim = 0f;
			else if (_dim > 0.996f && _editing)
				_dim = 1f;
		}

		internal static void HandleTitleInput()
		{
			if (_frameInput)
				return;

			_frameInput = true;
			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;

			if (!_editing) {
				FinishDrags();
				return;
			}

			if (_draggingMenu) {
				UpdateMenuDrag();
				return;
			}

			if (_draggingPan) {
				UpdatePanDrag();
				return;
			}

			if (CalamitasMenuPanels.OverlayOpen || CalamitasMenuPanels.SideButtonsHover) {
				FinishDrags();
				return;
			}

			if (CalamitasMenuLogo.HitRect().Contains(Main.mouseX, Main.mouseY) ||
			    (DieWithASmileSettings.PlayerEnabled && CalamitasMenuPlayerUI.HitRect().Contains(Main.mouseX, Main.mouseY))) {
				FinishDrags();
				return;
			}

			if (CalamitasMenuLogo.Busy || CalamitasMenuPlayerUI.Busy) {
				FinishDrags();
				return;
			}

			if (MenuHit().Contains(Main.mouseX, Main.mouseY)) {
				Main.blockMouse = true;
				if (!pressed)
					return;

				_draggingMenu = true;
				_dragMoved = false;
				_dragBound = false;
				Main.mouseLeftRelease = false;
				Main.blockMouse = true;
				return;
			}

			if (!CanPanWallpaper || !pressed)
				return;

			_draggingPan = true;
			_dragMoved = false;
			_dragBound = false;
			Main.mouseLeftRelease = false;
			Main.blockMouse = true;
		}

		private static void UpdateMenuDrag()
		{
			if (!Main.mouseLeft) {
				FinishDrags();
				return;
			}

			Vector2 mouse = Mouse();
			if (!_dragBound) {
				_dragBound = true;
				_dragStartMouse = mouse;
				_dragOffset = mouse - _workMenu;
				Main.blockMouse = true;
				return;
			}

			if (!_dragMoved && Vector2.DistanceSquared(mouse, _dragStartMouse) >= 16f)
				_dragMoved = true;

			_workMenu = ClampMenu(mouse - _dragOffset);
			Main.blockMouse = true;
		}

		private static void UpdatePanDrag()
		{
			if (!Main.mouseLeft) {
				FinishDrags();
				return;
			}

			Vector2 mouse = Mouse();
			if (!_dragBound) {
				_dragBound = true;
				_dragStartMouse = mouse;
				Main.blockMouse = true;
				return;
			}

			Vector2 extra = CalamitasMenuBackgroundStyle.LastCoverExtra;
			Vector2 delta = mouse - _dragStartMouse;
			_dragStartMouse = mouse;
			if (extra.X > 1f)
				_workPan.X = MathHelper.Clamp(_workPan.X - delta.X / extra.X, 0f, 1f);
			if (extra.Y > 1f)
				_workPan.Y = MathHelper.Clamp(_workPan.Y - delta.Y / extra.Y, 0f, 1f);

			if (delta.LengthSquared() >= 1f)
				_dragMoved = true;

			Main.blockMouse = true;
		}

		internal static void EndFrame() => _frameInput = false;

		internal static bool TryToggleFromClick()
		{
			if (_editing)
				Cancel(restore: true);
			else
				Begin();

			return true;
		}

		internal static void Begin()
		{
			CalamitasMenuPanels.CloseOverlays();
			_workLogo = CalamitasMenuLogo.SavedOrDefaultAnchor;
			_workPlayer = CalamitasMenuPlayerUI.SavedOrDefaultAnchor;
			_workMenu = SavedOrDefaultMenu;
			_workLogoScale = DieWithASmileSettings.LogoScale;
			_workPan = DieWithASmileSettings.SavedWallpaperPan;
			_editing = true;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		internal static void Save()
		{
			if (!_editing)
				return;

			FinishDrags();
			CalamitasMenuLogo.StopDrag();
			CalamitasMenuPlayerUI.StopDrag();
			DieWithASmileSettings.SaveLayout(_workLogo, _workPlayer, _workMenu, _workLogoScale, _workPan);
			_editing = false;
			_blockThemeSwap = 12;
			Main.mouseLeftRelease = false;
			Main.mouseRightRelease = false;
			Main.blockMouse = true;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		internal static void Cancel(bool restore)
		{
			FinishDrags();
			CalamitasMenuLogo.StopDrag();
			CalamitasMenuPlayerUI.StopDrag();
			if (_editing && restore)
				SoundEngine.PlaySound(SoundID.MenuClose);

			_editing = false;
		}

		internal static void DrawBackgroundDim(SpriteBatch spriteBatch)
		{
			if (_dim <= 0.01f)
				return;

			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				CalamitasMenuDraw.CoverRect,
				Color.Black * (0.55f * _dim));
		}

		internal static void DrawGuides(SpriteBatch spriteBatch, float fade)
		{
			if (!_editing || fade <= 0f)
				return;

			Color idle = CalamitasMenuAccent.Mid * (0.7f * fade);
			Color hot = CalamitasMenuButtonSystem.GetAnimatedHoverColor() * fade;
			DrawBox(spriteBatch, MenuHit(), MenuHit().Contains(Main.mouseX, Main.mouseY) || _draggingMenu ? hot : idle);
			DrawBox(spriteBatch, CalamitasMenuLogo.HitRect(), CalamitasMenuLogo.Busy ? hot : idle);
			Rectangle logo = CalamitasMenuLogo.HitRect();
			if (!logo.IsEmpty) {
				string scale = $"{MathF.Round(LogoScale * 100f)}%  ·  {CalamitasMenuText.UI("ScrollToResize")}";
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					FontAssets.MouseText.Value,
					scale,
					new Vector2(logo.X, logo.Bottom + 6),
					new Color(255, 236, 236) * fade,
					0f,
					Vector2.Zero,
					new Vector2(0.72f));
			}

			if (CanPanWallpaper) {
				string pan = CalamitasMenuText.UI("DragToPan");
				DynamicSpriteFont font = FontAssets.MouseText.Value;
				Vector2 panSize = font.MeasureString(pan) * 0.72f;
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					pan,
					new Vector2((Main.screenWidth - panSize.X) * 0.5f, 22f),
					new Color(255, 236, 236) * fade,
					0f,
					Vector2.Zero,
					new Vector2(0.72f));
			}

			if (!DieWithASmileSettings.PlayerEnabled)
				return;

			Rectangle player = CalamitasMenuPlayerUI.HitRect();
			DrawBox(spriteBatch, player, CalamitasMenuPlayerUI.Busy ? hot : idle);
		}

		internal static Rectangle MenuHit()
		{
			Rectangle drawn = CalamitasMenuButtonSystem.MenuDrawBounds;
			if (!drawn.IsEmpty) {
				drawn.Inflate(12, 8);
				return drawn;
			}

			Vector2 origin = Menu;
			return new Rectangle(
				(int)(origin.X - 250f),
				(int)(origin.Y - 28f),
				500,
				MenuRows * MenuStep + 24);
		}

		internal static Rectangle SaveHit()
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string text = CalamitasMenuText.UI("SaveLayout");
			Vector2 size = font.MeasureString(text) * 0.82f;
			int width = Math.Max(120, (int)size.X + 28);
			int height = 34;
			Rectangle edit = CalamitasMenuPanels.EditHit();
			return new Rectangle(
				edit.Right + 10,
				edit.Y + (edit.Height - height) / 2,
				width,
				height);
		}

		internal static void DrawSaveButton(SpriteBatch spriteBatch, float fade)
		{
			if (!_editing)
				return;

			Rectangle hit = SaveHit();
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Color fill = new Color(29, 27, 28) * ((hover ? 0.92f : 0.78f) * fade);
			Color edge = (hover ? CalamitasMenuButtonSystem.GetAnimatedHoverColor() : CalamitasMenuAccent.Mid) * fade;
			spriteBatch.Draw(pixel, new Rectangle(hit.X - 2, hit.Y - 2, hit.Width + 4, hit.Height + 4), edge * 0.85f);
			spriteBatch.Draw(pixel, hit, fill);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string text = CalamitasMenuText.UI("SaveLayout");
			Vector2 size = font.MeasureString(text) * 0.82f;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				text,
				new Vector2(hit.X + (hit.Width - size.X) * 0.5f, hit.Y + (hit.Height - size.Y) * 0.5f - 1f),
				new Color(255, 236, 236) * fade,
				0f,
				Vector2.Zero,
				new Vector2(0.82f));
		}

		private static void FinishDrags()
		{
			_draggingMenu = false;
			_draggingPan = false;
			_dragMoved = false;
			_dragBound = false;
		}

		private static void DrawBox(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			if (rect.IsEmpty)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), color);
		}

		private static Vector2 ClampMenu(Vector2 point)
		{
			point.X = MathHelper.Clamp(point.X, 120f, Main.screenWidth - 120f);
			point.Y = MathHelper.Clamp(point.Y, 40f, Main.screenHeight - MenuRows * MenuStep - 20f);
			return point;
		}

		private static Vector2 Mouse() => new(Main.mouseX, Main.mouseY);
	}
}

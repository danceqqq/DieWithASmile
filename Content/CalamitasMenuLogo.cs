using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public enum MenuLogo
	{
		Classic = 0,
		Gothic = 1,
		Orbit = 2,
		Hands = 3,
		Sticker = 4
	}

	internal static class CalamitasMenuLogo
	{
		private const float TargetWidth = 520f;
		private const float CenterY = 112f;
		private const float ClassicShineX = 1016f;
		private const float ClassicShineY = 398f;
		private const float ClassicDividerTexY = 698f;
		private const float GothicShineX = 1616f;
		private const float GothicShineY = 448f;
		private const float OrbitShineAx = 1841f;
		private const float OrbitShineAy = 759f;
		private const float OrbitShineBx = 1510f;
		private const float OrbitShineBy = 149f;
		private const float HandsLayerX = 241f;
		private const float HandsLayerY = 227f;
		private const float StickerCenterX = 261f;
		private const float StickerCenterY = 254f;
		private const float StickerSlotW = 455f;
		private const float StickerSlotH = 430f;

		private static readonly string[] BasePaths =
		{
			"DieWithASmile/Assets/Textures/Menu/Logos/Classic",
			"DieWithASmile/Assets/Textures/Menu/Logos/Gothic",
			"DieWithASmile/Assets/Textures/Menu/Logos/Orbit",
			"DieWithASmile/Assets/Textures/Menu/Logos/HandsBase",
			"DieWithASmile/Assets/Textures/Menu/Logos/StickerBase"
		};

		private static Asset<Texture2D>[] _bases;
		private static Asset<Texture2D> _handsLayer;
		private static Asset<Texture2D> _sticker;
		private static bool _dragging;
		private static bool _dragMoved;
		private static bool _hover;
		private static bool _frameInput;
		private static bool _dragBound;
		private static bool _mouseHeld;
		private static Vector2 _liveAnchor;
		private static Vector2 _dragOffset;
		private static Vector2 _dragStartMouse;

		private static int _lastWheel;

		internal static bool Busy => _dragging || (CalamitasMenuLayout.Editing && _hover);

		internal static Vector2 SavedOrDefaultAnchor =>
			ClampAnchor(DieWithASmileSettings.HasCustomLogoPosition
				? DieWithASmileSettings.LogoAnchorPixels
				: DefaultAnchor);

		internal static void Load()
		{
			_bases = new Asset<Texture2D>[BasePaths.Length];
			for (int i = 0; i < BasePaths.Length; i++)
				_bases[i] = ModContent.Request<Texture2D>(BasePaths[i]);

			_handsLayer = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/Menu/Logos/HandsLayer");
			_sticker = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/Menu/Logos/Sticker");
		}

		internal static void HandleTitleInput()
		{
			if (_frameInput)
				return;

			_frameInput = true;
			UpdateDrag();
		}

		internal static void EndFrame() => _frameInput = false;

		internal static Vector2 DefaultAnchor => new(CalamitasMenuButtonSystem.LeftMenuButtonX, CenterY);

		internal static Vector2 Anchor
		{
			get
			{
				Vector2 raw;
				if (_dragging)
					raw = _liveAnchor;
				else if (CalamitasMenuLayout.Editing)
					raw = CalamitasMenuLayout.Logo;
				else if (DieWithASmileSettings.HasCustomLogoPosition)
					raw = DieWithASmileSettings.LogoAnchorPixels;
				else
					raw = DefaultAnchor;

				return ClampAnchor(raw);
			}
		}

		internal static float MidY => Anchor.Y;

		internal static Texture2D CurrentTexture
		{
			get
			{
				if (DieWithASmileSettings.UsingFileLogo && CalamitasMenuUserArt.TryGetSelectedLogo(out Texture2D custom))
					return custom;

				return TextureOf(DieWithASmileSettings.Logo);
			}
		}

		internal static Texture2D TextureOf(MenuLogo logo)
		{
			int i = Math.Clamp((int)logo, 0, _bases.Length - 1);
			return _bases?[i]?.Value;
		}

		private static MenuLogo DrawKind =>
			DieWithASmileSettings.UsingFileLogo ? MenuLogo.Classic : DieWithASmileSettings.Logo;

		internal static float DrawScale => ScaleOf(DrawKind, CurrentTexture);

		private static float ScaleOf(MenuLogo kind, Texture2D logo)
		{
			if (logo == null)
				return 1f;

			float cap = MathHelper.Min(TargetWidth, Main.screenWidth * 0.38f);
			float source = kind switch
			{
				MenuLogo.Gothic => 2727f,
				MenuLogo.Orbit => 2897f,
				_ => logo.Width
			};
			return cap / source * CalamitasMenuLayout.LogoScale;
		}

		internal static float DividerScreenY
		{
			get
			{
				Texture2D logo = CurrentTexture;
				if (logo == null)
					return CenterY + 28f;

				float scale = ScaleOf(DrawKind, logo);
				float top = Anchor.Y - logo.Height * scale * 0.5f;
				float texY = !DieWithASmileSettings.UsingFileLogo && !DieWithASmileSettings.UsingForeignLogo && DieWithASmileSettings.Logo == MenuLogo.Classic
					? ClassicDividerTexY
					: logo.Height * 0.70f;
				return top + texY * scale;
			}
		}

		internal static float RightX
		{
			get
			{
				Texture2D logo = CurrentTexture;
				if (logo == null)
					return CalamitasMenuButtonSystem.LeftMenuButtonX + 220f;

				float scale = ScaleOf(DrawKind, logo);
				return Anchor.X + logo.Width * scale * 0.5f;
			}
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (fade <= 0f)
				return;

			if (DieWithASmileSettings.UsingForeignLogo) {
				CalamitasMenuDraw.WithLinear(spriteBatch, () =>
					CalamitasMenuForeign.TryDrawLogo(spriteBatch, Anchor, CalamitasMenuLayout.LogoScale, fade));
				return;
			}

			Texture2D logo = CurrentTexture;
			if (logo == null)
				return;

			float beat = CalamitasMenuSpectrum.SmoothBeat;
			float scale = ScaleOf(DrawKind, logo);
			bool custom = DieWithASmileSettings.UsingFileLogo;
			CalamitasMenuDraw.WithLinear(spriteBatch, () =>
				DrawLogo(spriteBatch, DrawKind, Anchor, scale, fade, beat, preview: false, custom));
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle dest, MenuLogo logo, float fade)
		{
			Texture2D tex = TextureOf(logo);
			if (tex == null || fade <= 0f)
				return;

			float scale = Math.Min(dest.Width / (float)tex.Width, dest.Height / (float)tex.Height) * 0.92f;
			Vector2 center = dest.Center.ToVector2();
			DrawLogo(spriteBatch, logo, center, scale, fade, beat: 0.35f, preview: true, custom: false);
		}

		internal static void DrawPreviewTexture(SpriteBatch spriteBatch, Rectangle dest, Texture2D tex, float fade)
		{
			if (tex == null || fade <= 0f)
				return;

			float scale = Math.Min(dest.Width / (float)tex.Width, dest.Height / (float)tex.Height) * 0.92f;
			Vector2 center = dest.Center.ToVector2();
			spriteBatch.Draw(tex, center, null, Color.White * fade, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
		}

		private static void DrawLogo(SpriteBatch spriteBatch, MenuLogo logo, Vector2 center, float scale, float fade, float beat, bool preview, bool custom)
		{
			Texture2D tex = custom ? CurrentTexture : TextureOf(logo);
			if (tex == null)
				return;

			if (!custom && logo is MenuLogo.Classic or MenuLogo.Gothic or MenuLogo.Orbit)
				DrawHalo(spriteBatch, center, tex, scale, fade, beat);

			spriteBatch.Draw(tex, center, null, Color.White * fade, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);

			if (custom)
				return;

			if (logo == MenuLogo.Hands)
				DrawHandsLayer(spriteBatch, center, tex, scale, fade, preview);
			else if (logo == MenuLogo.Sticker)
				DrawStickerLayer(spriteBatch, center, tex, scale, fade, preview);

			if (preview)
				return;

			Vector2 topLeft = center - tex.Size() * scale * 0.5f;
			float shineSize = scale * 5.8f;
			float pulse = 0.58f + 0.42f * beat;
			if (logo == MenuLogo.Classic)
				CalamitasMenuShine.Draw(spriteBatch, topLeft + new Vector2(ClassicShineX, ClassicShineY) * scale, shineSize, fade, pulse);
			else if (logo == MenuLogo.Gothic)
				CalamitasMenuShine.Draw(spriteBatch, topLeft + new Vector2(GothicShineX, GothicShineY) * scale, shineSize, fade, pulse);
			else if (logo == MenuLogo.Orbit) {
				CalamitasMenuShine.Draw(spriteBatch, topLeft + new Vector2(OrbitShineAx, OrbitShineAy) * scale, shineSize * 0.82f, fade, pulse);
				CalamitasMenuShine.Draw(spriteBatch, topLeft + new Vector2(OrbitShineBx, OrbitShineBy) * scale, shineSize * 0.82f, fade, pulse);
			}
		}

		private static void DrawHalo(SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, float beat)
		{
			Texture2D halo = CalamitasMenuShine.Texture;
			if (halo == null)
				return;

			float haloPulse = 0.07f + 0.12f * beat;
			Vector2 haloScale = new(
				logo.Width * scale * 1.15f / halo.Width,
				logo.Height * scale * 1.55f / halo.Height);
			spriteBatch.Draw(
				halo,
				center,
				null,
				CalamitasMenuAccent.Mid * (haloPulse * fade),
				0f,
				halo.Size() * 0.5f,
				haloScale,
				SpriteEffects.None,
				0f);
		}

		private static void DrawHandsLayer(SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, bool preview)
		{
			Texture2D hands = _handsLayer?.Value;
			if (hands == null)
				return;

			float bob = preview ? 0f : MathF.Sin(Main.GlobalTimeWrappedHourly * 1.05f) * 16f;
			Vector2 topLeft = center - logo.Size() * scale * 0.5f;
			Vector2 pos = topLeft + new Vector2(HandsLayerX, HandsLayerY + bob) * scale;
			spriteBatch.Draw(hands, pos, null, Color.White * fade, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private static void DrawStickerLayer(SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, bool preview)
		{
			Texture2D sticker = _sticker?.Value;
			if (sticker == null)
				return;

			float pulse = preview ? 1f : 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f);
			float fit = Math.Min(StickerSlotW / sticker.Width, StickerSlotH / sticker.Height);
			Vector2 topLeft = center - logo.Size() * scale * 0.5f;
			Vector2 stickerCenter = topLeft + new Vector2(StickerCenterX, StickerCenterY) * scale;
			spriteBatch.Draw(
				sticker,
				stickerCenter,
				null,
				Color.White * fade,
				0f,
				sticker.Size() * 0.5f,
				scale * fit * pulse,
				SpriteEffects.None,
				0f);
		}

		private static void UpdateDrag()
		{
			int wheel = Mouse.GetState().ScrollWheelValue;
			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;
			if (!Main.gameMenu || !CoolerMenuCompat.OnTitleLike) {
				StopDrag();
				_hover = false;
				_lastWheel = wheel;
				return;
			}

			if (!CalamitasMenuLayout.Editing || CalamitasMenuPanels.OverlayOpen || CalamitasMenuPanels.SideButtonsHover || CalamitasMenuLayout.Busy) {
				StopDrag();
				_hover = false;
				_lastWheel = wheel;
				return;
			}

			if (_dragging) {
				if (!Main.mouseLeft) {
					FinishDrag();
					return;
				}

				Vector2 mouse = new(Main.mouseX, Main.mouseY);
				if (!_dragBound) {
					_dragBound = true;
					_dragStartMouse = mouse;
					_dragOffset = mouse - _liveAnchor;
					Main.blockMouse = true;
					return;
				}

				if (!_dragMoved && Vector2.DistanceSquared(mouse, _dragStartMouse) >= 16f)
					_dragMoved = true;

				_liveAnchor = ClampAnchor(mouse - _dragOffset);
				CalamitasMenuLayout.Logo = _liveAnchor;
				Main.blockMouse = true;
				return;
			}

			_hover = HitRect().Contains(Main.mouseX, Main.mouseY);
			if (_hover)
				Main.blockMouse = true;

			if (_hover) {
				float notches = (wheel - _lastWheel) / 120f;
				if (Math.Abs(notches) >= 0.5f)
					CalamitasMenuLayout.LogoScale += notches * 0.08f;
			}

			_lastWheel = wheel;

			if (_hover && pressed) {
				_liveAnchor = Anchor;
				_dragging = true;
				_dragMoved = false;
				_dragBound = false;
				Main.mouseLeftRelease = false;
				Main.blockMouse = true;
			}
		}

		private static void FinishDrag()
		{
			if (!_dragging)
				return;

			CalamitasMenuLayout.Logo = ClampAnchor(_liveAnchor);
			StopDrag();
		}

		internal static void StopDrag()
		{
			_dragging = false;
			_dragMoved = false;
			_dragBound = false;
		}

		internal static Rectangle HitRect()
		{
			if (DieWithASmileSettings.UsingForeignLogo)
				return CalamitasMenuForeign.LogoHit(Anchor, CalamitasMenuLayout.LogoScale);

			Texture2D logo = CurrentTexture;
			if (logo == null)
				return Rectangle.Empty;

			float scale = ScaleOf(DrawKind, logo);
			Vector2 size = logo.Size() * scale * 0.82f;
			Vector2 center = Anchor;
			return new Rectangle(
				(int)(center.X - size.X * 0.5f),
				(int)(center.Y - size.Y * 0.5f),
				(int)size.X,
				(int)size.Y);
		}

		private static Vector2 ClampAnchor(Vector2 point)
		{
			float padX = 80f;
			float padY = 40f;
			point.X = MathHelper.Clamp(point.X, padX, Main.screenWidth - padX);
			point.Y = MathHelper.Clamp(point.Y, padY, Main.screenHeight - padY);
			return point;
		}

		internal static string CurrentKey()
		{
			if (DieWithASmileSettings.UsingFileLogo)
				return "custom:" + DieWithASmileSave.Data.CustomLogoId;
			if (DieWithASmileSettings.UsingForeignLogo)
				return "foreign:" + DieWithASmileSave.Data.ForeignLogoId;
			return "logo:" + (int)DieWithASmileSettings.Logo;
		}

		internal static void Reroll(bool save)
		{
			List<string> pool = Pool();
			if (pool.Count == 0)
				return;

			string current = CurrentKey();
			string next = pool[0];
			if (pool.Count > 1) {
				int guard = 0;
				do
					next = pool[NextRand(pool.Count)];
				while (next == current && ++guard < 16);
			}

			DieWithASmileSettings.ApplyLogo(next, keepShuffle: true);
			if (save)
				DieWithASmileSave.Save();
		}

		internal static List<string> Pool()
		{
			var list = new List<string>();
			for (int i = 0; i < 5; i++)
				list.Add("logo:" + i);

			try {
				foreach (ModMenu menu in CalamitasMenuForeign.LogoMenus()) {
					if (menu != null && !string.IsNullOrEmpty(menu.FullName))
						list.Add("foreign:" + menu.FullName);
				}
			}
			catch {
			}

			foreach (CustomArtRecord record in CalamitasMenuUserArt.Logos) {
				if (record != null && !string.IsNullOrEmpty(record.Id))
					list.Add("custom:" + record.Id);
			}

			return list;
		}

		private static int NextRand(int count)
		{
			if (count <= 1)
				return 0;
			return Main.rand != null ? Main.rand.Next(count) : Random.Shared.Next(count);
		}
	}
}

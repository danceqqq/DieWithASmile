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
	internal static class CalamitasMenuPlayerUI
	{
		private static readonly Color PanelColor = new Color(29, 27, 28) * 0.78f;
		private static Color Neon => CalamitasMenuAccent.Mid;
		private static readonly Color TextMain = new Color(255, 236, 236);
		private static readonly Color TextSub = new Color(210, 190, 190);

		private const float CollapsedRadius = 28f;
		private const float CardWidth = 420f;
		private const float CardHeight = 148f;
		private const float TitleY = 14f;
		private const float ArtistY = 34f;
		private const float CoverY = 50f;
		private const float SeekY = 76f;
		private const float ControlsY = 118f;
		private const float SkipOffset = 40f;
		private const float ExtraOffset = 80f;
		private const float PlayRadius = 17f;
		private const float SkipRadius = 13f;
		private const float LoopRadius = 12f;

		private static float _expand;
		private static bool _hover;
		private static bool _draggingSeek;
		private static bool _draggingPlayer;
		private static bool _dragMoved;
		private static bool _dragBound;
		private static Vector2 _liveAnchor;
		private static Vector2 _dragOffset;
		private static Vector2 _dragStartMouse;
		private static Texture2D _circle;

		private static bool _frameInput;
		private static bool _mouseHeld;

		internal static float Expand => _expand;
		internal static float BaselineY => Main.screenHeight - 78f;
		internal static bool CoolerLayout => CoolerMenuCompat.CoreActive;
		internal static bool Busy => _draggingPlayer || (CalamitasMenuLayout.Editing && _hover);

		internal static Vector2 SavedOrDefaultAnchor => ClampAnchor(
			DieWithASmileSettings.HasCustomPlayerPosition
				? DieWithASmileSettings.PlayerAnchorPixels
				: DefaultAnchor);

		internal static Vector2 Anchor
		{
			get
			{
				Vector2 raw;
				if (_draggingPlayer)
					raw = _liveAnchor;
				else if (CalamitasMenuLayout.Editing)
					raw = CalamitasMenuLayout.Player;
				else if (DieWithASmileSettings.HasCustomPlayerPosition)
					raw = DieWithASmileSettings.PlayerAnchorPixels;
				else
					raw = DefaultAnchor;

				return ClampAnchor(raw);
			}
		}

		private static Vector2 DefaultAnchor =>
			CoolerLayout
				? GetCoolerAnchor()
				: new Vector2(CalamitasMenuButtonSystem.LeftMenuButtonX, BaselineY - CollapsedRadius);

		private static Vector2 GetCoolerAnchor()
		{
			return ClampAnchor(CalamitasMenuBackgroundStyle.GetScreenPoint(1622f, 290f, parallax: false));
		}

		private static Vector2 ClampAnchor(Vector2 point)
		{
			if (CalamitasMenuLayout.Editing) {
				point.X = MathHelper.Clamp(point.X, 40f, Main.screenWidth - 40f);
				point.Y = MathHelper.Clamp(point.Y, 40f, Main.screenHeight - 40f);
				return point;
			}

			float minX = CardWidth * 0.5f + 16f;
			float maxX = Main.screenWidth - CardWidth * 0.5f - 16f;
			float minY = CardHeight - CollapsedRadius + 16f;
			float maxY = Main.screenHeight - CollapsedRadius - 24f;
			if (maxX < minX)
				point.X = Main.screenWidth * 0.5f;
			else
				point.X = MathHelper.Clamp(point.X, minX, maxX);

			if (maxY < minY)
				point.Y = Main.screenHeight * 0.5f;
			else
				point.Y = MathHelper.Clamp(point.Y, minY, maxY);

			return point;
		}

		internal static void Reset()
		{
			_expand = 0f;
			_hover = false;
			_draggingSeek = false;
			StopDrag();
		}

		internal static void HandleTitleInput()
		{
			if (_frameInput)
				return;

			_frameInput = true;
			UpdateDrag();
		}

		internal static void EndFrame() => _frameInput = false;

		internal static void Update()
		{
			CalamitasMenuPlaylist.Update();
			if (!DieWithASmileSettings.PlayerEnabled)
				return;

			if (!_frameInput)
				UpdateDrag();
		}

		private static void UpdateDrag()
		{
			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;
			if (!Main.gameMenu || !CoolerMenuCompat.OnTitleLike) {
				StopDrag();
				_hover = false;
				return;
			}

			if (CalamitasMenuPanels.OverlayOpen || CalamitasMenuLayout.Busy) {
				StopDrag();
				return;
			}

			if (_draggingPlayer) {
				HandlePlayerDrag();
				_hover = true;
				if (!CalamitasMenuLayout.Editing)
					_expand = 1f;
				Main.blockMouse = true;
				return;
			}

			Vector2 center = Anchor;
			Rectangle collapsed = CircleHit(center, CollapsedRadius + 12f);
			Rectangle expanded = GetCardRect(center, 1f);
			Rectangle hit = _expand > 0.42f || _draggingSeek ? expanded : collapsed;

			if (CalamitasMenuLayout.Editing) {
				_expand = MathHelper.Lerp(_expand, 0f, 0.22f);
				_hover = collapsed.Contains(Main.mouseX, Main.mouseY);
				if (_hover)
					Main.blockMouse = true;
				if (_hover && pressed)
					BeginPlayerDrag();
				return;
			}

			_hover = hit.Contains(Main.mouseX, Main.mouseY);
			float target = _hover || _draggingSeek ? 1f : 0f;
			_expand = MathHelper.Lerp(_expand, target, 0.17f);
			if (Math.Abs(_expand - target) < 0.004f)
				_expand = target;

			if (_hover || _draggingSeek)
				Main.blockMouse = true;

			if (!_hover && !_draggingSeek)
				return;

			if (Ease > 0.72f)
				HandleExpandedClicks(GetCardRect(center, 1f));
			else if (Clicked(collapsed)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuPlaylist.TogglePause();
				Main.mouseLeftRelease = false;
			}
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (fade <= 0f || !DieWithASmileSettings.PlayerEnabled || !CoolerMenuCompat.OnTitleLike)
				return;

			Vector2 center = Anchor;
			float ease = Ease;
			float pulse = 1f + CalamitasMenuSpectrum.SmoothBeat * 0.1f;
			Rectangle card = GetCardRect(center, ease);
			CalamitasMenuDraw.WithLinear(spriteBatch, () => {
				CalamitasMenuSpectrum.Draw(spriteBatch, fade, ease, center, CollapsedRadius * pulse, card, pulse);
				DrawCollapsedButton(spriteBatch, center, fade, 1f - ease, pulse);
				if (ease > 0.02f)
					DrawCard(spriteBatch, card, fade * ease, ease);
			});
		}

		internal static void Unload()
		{
			FinishPlayerDrag();
			Texture2D tex = _circle;
			_circle = null;
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

		private static float Ease
		{
			get
			{
				float t = MathHelper.Clamp(_expand, 0f, 1f);
				return t * t * (3f - 2f * t);
			}
		}

		private static Rectangle GetCardRect(Vector2 center, float ease)
		{
			float width = MathHelper.Lerp(CollapsedRadius * 2f, CardWidth, ease);
			float height = MathHelper.Lerp(CollapsedRadius * 2f, CardHeight, ease);
			float baseline = center.Y + CollapsedRadius;
			return new Rectangle(
				(int)(center.X - width * 0.5f),
				(int)(baseline - height),
				(int)width,
				(int)height);
		}

		private static void HandleExpandedClicks(Rectangle card)
		{
			Vector2 controls = new(card.X + card.Width * 0.5f, card.Y + ControlsY);
			Rectangle play = CircleHit(controls, PlayRadius);
			Rectangle prev = CircleHit(controls + new Vector2(-SkipOffset, 0f), SkipRadius);
			Rectangle next = CircleHit(controls + new Vector2(SkipOffset, 0f), SkipRadius);
			Rectangle edit = CircleHit(controls + new Vector2(-ExtraOffset, 0f), SkipRadius);
			Rectangle upload = CircleHit(controls + new Vector2(ExtraOffset, 0f), SkipRadius);
			Rectangle loop = CircleHit(new Vector2(card.Right - 28f, controls.Y), LoopRadius);
			Rectangle shuffle = CircleHit(new Vector2(card.X + 28f, controls.Y), LoopRadius);
			var seek = new Rectangle(card.X + 18, card.Y + (int)SeekY - 8, card.Width - 36, 20);

			if (_draggingSeek) {
				if (Main.mouseLeft) {
					float t = MathHelper.Clamp((Main.mouseX - seek.X) / (float)seek.Width, 0f, 1f);
					CalamitasMenuPlaylist.Seek01(t);
				}
				else {
					_draggingSeek = false;
				}

				return;
			}

			if (!Main.mouseLeft || !Main.mouseLeftRelease)
				return;

			if (seek.Contains(Main.mouseX, Main.mouseY)) {
				_draggingSeek = true;
				float t = MathHelper.Clamp((Main.mouseX - seek.X) / (float)seek.Width, 0f, 1f);
				CalamitasMenuPlaylist.Seek01(t);
				Main.mouseLeftRelease = false;
				return;
			}

			if (play.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuPlaylist.TogglePause();
			}
			else if (prev.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuPlaylist.Previous();
			}
			else if (next.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuPlaylist.Next();
			}
			else if (edit.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuOpen);
				CalamitasMenuPanels.OpenPlaylist();
			}
			else if (upload.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				if (CalamitasMenuLibrary.TryPickAudioFile(out string path))
					CalamitasMenuLibrary.StartImport(path);
			}
			else if (loop.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuPlaylist.ToggleLoop();
			}
			else if (shuffle.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuPlaylist.ToggleShuffle();
			}
			else {
				if (CalamitasMenuLayout.Editing)
					BeginPlayerDrag();
			}

			Main.mouseLeftRelease = false;
		}

		private static void BeginPlayerDrag()
		{
			if (!CalamitasMenuLayout.Editing)
				return;

			Vector2 start = Anchor;
			_draggingPlayer = true;
			_dragMoved = false;
			_dragBound = false;
			_liveAnchor = start;
			Main.mouseLeftRelease = false;
			Main.blockMouse = true;
		}

		private static void HandlePlayerDrag()
		{
			if (!Main.mouseLeft) {
				FinishPlayerDrag();
				return;
			}

			Vector2 mouse = Mouse();
			if (!_dragBound) {
				_dragBound = true;
				_dragStartMouse = mouse;
				_dragOffset = mouse - _liveAnchor;
				return;
			}

			if (!_dragMoved && Vector2.DistanceSquared(mouse, _dragStartMouse) >= 16f)
				_dragMoved = true;

			_liveAnchor = ClampAnchor(mouse - _dragOffset);
			CalamitasMenuLayout.Player = _liveAnchor;
		}

		private static void FinishPlayerDrag()
		{
			if (!_draggingPlayer)
				return;

			CalamitasMenuLayout.Player = ClampAnchor(_liveAnchor);
			StopDrag();
		}

		internal static void StopDrag()
		{
			_draggingPlayer = false;
			_dragMoved = false;
			_dragBound = false;
		}

		internal static Rectangle HitRect()
		{
			return CircleHit(Anchor, CollapsedRadius + 14f);
		}

		private static Vector2 Mouse() => new(Main.mouseX, Main.mouseY);

		private static bool Clicked(Rectangle hit)
		{
			return Main.mouseLeft && Main.mouseLeftRelease && hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static Rectangle CircleHit(Vector2 center, float radius)
		{
			return new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));
		}

		private static void DrawCollapsedButton(SpriteBatch spriteBatch, Vector2 center, float fade, float visible, float pulse)
		{
			if (visible < 0.04f)
				return;

			Texture2D circle = GetCircle();
			float alpha = fade * visible;
			float diameter = CollapsedRadius * 2f * pulse;
			spriteBatch.Draw(circle, center, null, Neon * (0.28f * alpha), 0f, circle.Size() * 0.5f, (diameter * 1.22f) / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center, null, Neon * (0.9f * alpha), 0f, circle.Size() * 0.5f, (diameter + 4f) / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center, null, PanelColor * alpha, 0f, circle.Size() * 0.5f, (diameter - 2f) / circle.Width, SpriteEffects.None, 0f);
			DrawPlayPause(spriteBatch, center, 11f * pulse, CalamitasMenuAccent.Glyph(_hover) * alpha, CalamitasMenuPlaylist.IsPaused);
		}

		private static void DrawCard(SpriteBatch spriteBatch, Rectangle card, float alpha, float ease)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(card.X - 3, card.Y - 3, card.Width + 6, card.Height + 6), Neon * (0.16f * alpha));
			spriteBatch.Draw(pixel, card, PanelColor * alpha);

			var neon = Neon * alpha;
			spriteBatch.Draw(pixel, new Rectangle(card.X, card.Y, card.Width, 2), neon);
			spriteBatch.Draw(pixel, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), neon);
			spriteBatch.Draw(pixel, new Rectangle(card.X, card.Y, 2, card.Height), neon);
			spriteBatch.Draw(pixel, new Rectangle(card.Right - 2, card.Y, 2, card.Height), neon);

			if (ease < 0.38f)
				return;

			float textAlpha = MathHelper.Clamp((ease - 0.38f) / 0.4f, 0f, 1f) * alpha;
			MenuTrack track = CalamitasMenuPlaylist.Current;
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			DrawCentered(spriteBatch, font, track.Title, new Vector2(card.X + card.Width * 0.5f, card.Y + TitleY), TextMain * textAlpha, 0.92f);
			DrawCentered(spriteBatch, font, track.Artist, new Vector2(card.X + card.Width * 0.5f, card.Y + ArtistY), TextSub * textAlpha, 0.72f);
			if (!string.IsNullOrEmpty(track.CoverArtist))
				DrawCentered(spriteBatch, font, track.CoverArtist, new Vector2(card.X + card.Width * 0.5f, card.Y + CoverY), Neon * (0.95f * textAlpha), 0.62f);

			float duration = Math.Max(CalamitasMenuPlaylist.GetDuration(), 0.01f);
			float time = MathHelper.Clamp(CalamitasMenuPlaylist.GetDisplayTime(), 0f, duration);
			float progress = time / duration;
			var bar = new Rectangle(card.X + 22, card.Y + (int)SeekY, card.Width - 44, 4);
			Texture2D circle = GetCircle();
			spriteBatch.Draw(pixel, bar, Color.White * (0.18f * textAlpha));
			spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, Math.Max(1, (int)(bar.Width * progress)), bar.Height), Neon * textAlpha);
			spriteBatch.Draw(circle, new Vector2(bar.X + bar.Width * progress, bar.Y + 2f), null, TextMain * textAlpha, 0f, circle.Size() * 0.5f, 8f / circle.Width, SpriteEffects.None, 0f);

			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, CalamitasMenuPlaylist.FormatTime(time), new Vector2(bar.X, bar.Y + 8f), TextSub * textAlpha, 0f, Vector2.Zero, new Vector2(0.7f));
			string end = CalamitasMenuPlaylist.FormatTime(duration);
			Vector2 endSize = font.MeasureString(end) * 0.7f;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, end, new Vector2(bar.Right - endSize.X, bar.Y + 8f), TextSub * textAlpha, 0f, Vector2.Zero, new Vector2(0.7f));

			Vector2 controls = new(card.X + card.Width * 0.5f, card.Y + ControlsY);
			Vector2 editPos = controls + new Vector2(-ExtraOffset, 0f);
			Vector2 prevPos = controls + new Vector2(-SkipOffset, 0f);
			Vector2 nextPos = controls + new Vector2(SkipOffset, 0f);
			Vector2 uploadPos = controls + new Vector2(ExtraOffset, 0f);
			Vector2 shufflePos = new(card.X + 28f, controls.Y);
			Vector2 loopPos = new(card.Right - 28f, controls.Y);

			DrawRoundButton(spriteBatch, editPos, SkipRadius, textAlpha);
			DrawIcon(spriteBatch, CalamitasMenuIcons.AsControlIcon(CalamitasMenuIcons.EditPlaylist), editPos, 15f, Paint(editPos, SkipRadius, textAlpha));
			DrawRoundButton(spriteBatch, prevPos, SkipRadius, textAlpha);
			DrawSkip(spriteBatch, prevPos, -1, Paint(prevPos, SkipRadius, textAlpha));
			DrawRoundButton(spriteBatch, controls, PlayRadius, textAlpha);
			DrawPlayPause(spriteBatch, controls, 10f, Paint(controls, PlayRadius, textAlpha), CalamitasMenuPlaylist.IsPaused);
			DrawRoundButton(spriteBatch, nextPos, SkipRadius, textAlpha);
			DrawSkip(spriteBatch, nextPos, 1, Paint(nextPos, SkipRadius, textAlpha));
			DrawRoundButton(spriteBatch, uploadPos, SkipRadius, textAlpha);
			DrawIcon(spriteBatch, CalamitasMenuIcons.AsControlIcon(CalamitasMenuIcons.UploadSong), uploadPos, 15f, Paint(uploadPos, SkipRadius, textAlpha));

			DrawRoundButton(spriteBatch, shufflePos, LoopRadius, textAlpha, CalamitasMenuPlaylist.ShuffleEnabled);
			DrawIcon(
				spriteBatch,
				CalamitasMenuIcons.AsControlIcon(CalamitasMenuIcons.Shuffle),
				shufflePos,
				14f,
				Paint(shufflePos, LoopRadius, textAlpha, CalamitasMenuPlaylist.ShuffleEnabled));

			DrawRoundButton(spriteBatch, loopPos, LoopRadius, textAlpha, CalamitasMenuPlaylist.LoopEnabled);
			DrawIcon(
				spriteBatch,
				CalamitasMenuIcons.AsControlIcon(CalamitasMenuPlaylist.LoopEnabled ? CalamitasMenuIcons.UnlockSong : CalamitasMenuIcons.LockSong),
				loopPos,
				15f,
				Paint(loopPos, LoopRadius, textAlpha, CalamitasMenuPlaylist.LoopEnabled));
		}

		internal static void DrawRoundButtonPublic(SpriteBatch spriteBatch, Vector2 center, float radius, float alpha, bool active = false) =>
			DrawRoundButton(spriteBatch, center, radius, alpha, active);

		internal static void DrawIcon(SpriteBatch spriteBatch, Texture2D tex, Vector2 center, float size, Color color)
		{
			if (tex == null)
				return;

			spriteBatch.Draw(tex, center, null, color, 0f, tex.Size() * 0.5f, size / Math.Max(tex.Width, 1), SpriteEffects.None, 0f);
		}

		private static void DrawCentered(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 center, Color color, float scale)
		{
			Vector2 size = font.MeasureString(text) * scale;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, center - new Vector2(size.X * 0.5f, 0f), color, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static Color Paint(Vector2 center, float radius, float alpha, bool on = false) =>
			CalamitasMenuAccent.Glyph(CircleHit(center, radius).Contains(Main.mouseX, Main.mouseY), on) * alpha;

		private static void DrawRoundButton(SpriteBatch spriteBatch, Vector2 center, float radius, float alpha, bool active = false)
		{
			Texture2D circle = GetCircle();
			bool hover = CircleHit(center, radius).Contains(Main.mouseX, Main.mouseY);
			Color accent = CalamitasMenuAccent.Glyph(hover, active);
			Color fill = hover || active ? accent * (0.35f * alpha) : Color.White * (0.08f * alpha);
			spriteBatch.Draw(circle, center, null, fill, 0f, circle.Size() * 0.5f, radius * 2f / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center, null, accent * ((hover || active ? 0.95f : 0.45f) * alpha), 0f, circle.Size() * 0.5f, (radius * 2f + 3f) / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center, null, PanelColor * (0.2f * alpha), 0f, circle.Size() * 0.5f, (radius * 2f - 3f) / circle.Width, SpriteEffects.None, 0f);
		}

		private static void DrawPlayPause(SpriteBatch spriteBatch, Vector2 center, float size, Color color, bool paused)
		{
			if (paused)
				DrawPlayTriangle(spriteBatch, center, size, color);
			else
				DrawPauseBars(spriteBatch, center, size, color);
		}

		private static void DrawPlayTriangle(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int height = Math.Max(6, (int)(size * 1.35f));
			int originX = (int)(center.X - size * 0.38f);
			int originY = (int)center.Y;
			for (int dy = -height; dy <= height; dy++) {
				int width = Math.Max(1, (int)((height - Math.Abs(dy)) * 0.85f));
				spriteBatch.Draw(pixel, new Rectangle(originX, originY + dy, width, 1), color);
			}
		}

		private static void DrawPauseBars(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int height = (int)(size * 1.4f);
			int y = (int)(center.Y - height * 0.5f);
			spriteBatch.Draw(pixel, new Rectangle((int)(center.X - 6f), y, 3, height), color);
			spriteBatch.Draw(pixel, new Rectangle((int)(center.X + 2f), y, 3, height), color);
		}

		private static void DrawSkip(SpriteBatch spriteBatch, Vector2 center, int direction, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			const int halfH = 6;
			const int triW = 8;
			int cy = (int)center.Y;
			int baseX = (int)center.X - direction * 2;

			for (int dy = -halfH; dy <= halfH; dy++) {
				float t = 1f - Math.Abs(dy) / (float)halfH;
				int width = Math.Max(1, (int)MathF.Round(triW * t));
				int x = direction > 0 ? baseX : baseX - width + 1;
				spriteBatch.Draw(pixel, new Rectangle(x, cy + dy, width, 1), color);
			}

			int barX = direction > 0 ? baseX + triW + 1 : baseX - triW - 2;
			spriteBatch.Draw(pixel, new Rectangle(barX, cy - halfH, 2, halfH * 2 + 1), color);
		}

		private static Texture2D GetCircle()
		{
			if (_circle != null && !_circle.IsDisposed)
				return _circle;

			const int size = 64;
			_circle = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			var data = new Color[size * size];
			float c = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					float dist = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size * 0.5f);
					float a = MathHelper.Clamp((1f - dist) * 8f, 0f, 1f);
					byte v = (byte)(a * 255f);
					data[y * size + x] = new Color(v, v, v, v);
				}
			}

			_circle.SetData(data);
			return _circle;
		}
	}
}

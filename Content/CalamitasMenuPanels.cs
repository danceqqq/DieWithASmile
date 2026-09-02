using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuPanels
	{
		private static readonly Color Panel = new Color(29, 27, 28) * 0.94f;
		private static Color Neon => CalamitasMenuAccent.Mid;
		private static readonly Color TextMain = new Color(255, 236, 236);
		private static readonly Color TextSub = new Color(210, 190, 190);
		private static readonly string[] ScenePaths =
		{
			"DieWithASmile/Assets/Textures/Menu/CalamitasBackground",
			"DieWithASmile/Assets/Textures/Menu/DeltaruneHeartsBackground",
			"DieWithASmile/Assets/Textures/Menu/ComeAlongBackground",
			"DieWithASmile/Assets/Textures/Menu/MeadowArt1",
			"DieWithASmile/Assets/Textures/Menu/YharimArt",
			"DieWithASmile/Assets/Textures/Menu/WitchArt",
			"DieWithASmile/Assets/Textures/Menu/SoulOfTheUniverse"
		};

		private static string L(string key) => CalamitasMenuText.UI(key);

		private static string SceneLabel(int i) => i switch
		{
			0 => L("SceneCalamitas"),
			1 => L("SceneDontForget"),
			2 => L("SceneComeAlong"),
			3 => L("SceneMeadow"),
			4 => L("SceneYharim"),
			5 => L("SceneWitch"),
			6 => L("SceneSoul"),
			_ => ""
		};

		private static Asset<Texture2D>[] _previews;
		private static bool _playlistOpen;
		private static bool _galleryOpen;
		private static bool _logoOpen;
		private static float _playlistScroll;
		private static float _galleryScroll;
		private static float _logoScroll;
		private static string _pendingDeleteId;
		private static string _pendingDeleteArtId;
		private static string _editArtistId;
		private static string _editArtistBuffer = "";
		private static string _tooltip;
		private static int _lastWheel;
		private static bool _enterHeld;
		private static bool _rawLeft;
		private static bool _rawRight;
		private static bool _leftClick;
		private static bool _rightClick;
		private static bool _draggingInterval;
		private static bool _frameInput;
		private static int _artPulse;
		private static IReadOnlyList<ModMenu> _foreignWalls = Array.Empty<ModMenu>();
		private static IReadOnlyList<ModMenu> _foreignLogos = Array.Empty<ModMenu>();
		private static IReadOnlyList<ModSurfaceBackgroundStyle> _tmlStyles = Array.Empty<ModSurfaceBackgroundStyle>();
		private static bool _showHidden;

		private enum GKind
		{
			Follow,
			Shuffle,
			Scene,
			Vanilla,
			Tml,
			Orphan,
			Foreign,
			Custom,
			Upload
		}

		private readonly struct GItem
		{
			internal readonly GKind Kind;
			internal readonly int Index;
			internal readonly string Key;

			internal GItem(GKind kind, int index = 0, string key = "")
			{
				Kind = kind;
				Index = index;
				Key = key;
			}
		}

		internal static bool PlaylistOpen => _playlistOpen;
		internal static bool EditingArtist => !string.IsNullOrEmpty(_editArtistId);
		internal static bool OverlayOpen =>
			CalamitasMenuConflict.OverlayActive ||
			_playlistOpen ||
			_galleryOpen ||
			_logoOpen ||
			CalamitasMenuLibrary.Importing ||
			!string.IsNullOrEmpty(CalamitasMenuLibrary.ImportError);

		internal static void Load()
		{
			_previews = new Asset<Texture2D>[ScenePaths.Length];
			for (int i = 0; i < ScenePaths.Length; i++)
				_previews[i] = ModContent.Request<Texture2D>(ScenePaths[i]);
		}

		internal static void Reset()
		{
			_playlistOpen = false;
			_galleryOpen = false;
			_logoOpen = false;
			_playlistScroll = 0f;
			_galleryScroll = 0f;
			_logoScroll = 0f;
			_pendingDeleteId = null;
			_pendingDeleteArtId = null;
			_draggingInterval = false;
			_showHidden = false;
			_frameInput = false;
			CancelArtistEdit();
			CalamitasMenuLayout.Reset();
		}

		internal static void CloseOverlays()
		{
			_playlistOpen = false;
			_galleryOpen = false;
			_logoOpen = false;
			_pendingDeleteId = null;
			_pendingDeleteArtId = null;
			_showHidden = false;
			CancelArtistEdit();
		}

		internal static void OpenPlaylist()
		{
			_galleryOpen = false;
			_logoOpen = false;
			_playlistOpen = true;
			_pendingDeleteId = null;
			_pendingDeleteArtId = null;
			CancelArtistEdit();
			_lastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			CalamitasMenuLibrary.ScanIntoSave();
		}

		internal static void OpenGallery()
		{
			_playlistOpen = false;
			_logoOpen = false;
			CalamitasMenuUserArt.Scan();
			RefreshForeign();
			_galleryScroll = 0f;
			_showHidden = false;
			_lastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			_galleryOpen = true;
		}

		internal static void OpenLogoGallery()
		{
			_playlistOpen = false;
			_galleryOpen = false;
			CalamitasMenuUserArt.Scan();
			RefreshForeign();
			_logoScroll = 0f;
			_lastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			_logoOpen = true;
		}

		internal static bool StealVanillaClicks =>
			OverlayOpen ||
			SideButtonsHover ||
			CalamitasMenuLayout.ShouldBlockThemeSwap ||
			CalamitasMenuLayout.Busy ||
			CalamitasMenuLogo.Busy ||
			CalamitasMenuPlayerUI.Busy;

		internal static bool SideButtonsHover
		{
			get
			{
				if (EditHit().Contains(Main.mouseX, Main.mouseY))
					return true;
				if (CalamitasMenuLayout.Editing)
					return CalamitasMenuLayout.SaveHit().Contains(Main.mouseX, Main.mouseY);
				return GalleryHit().Contains(Main.mouseX, Main.mouseY) || LogoHit().Contains(Main.mouseX, Main.mouseY);
			}
		}

		internal static void HandleTitleInput()
		{
			if (_frameInput)
				return;

			_frameInput = true;
			RunUpdate();
		}

		internal static void Update()
		{
			if (!_frameInput)
				RunUpdate();
		}

		internal static void EndFrame() => _frameInput = false;

		private static void RunUpdate()
		{
			_tooltip = null;
			if (CalamitasMenuConflict.OverlayActive) {
				_playlistOpen = false;
				_galleryOpen = false;
				_logoOpen = false;
				Main.blockMouse = true;
				return;
			}

			if (!Main.gameMenu || !CoolerMenuCompat.OnTitleLike)
				return;

			CaptureClicks();

			if (OverlayOpen)
				Main.blockMouse = true;

			if (EditingArtist)
				PlayerInput.WritingText = true;

			if (CalamitasMenuLibrary.Importing || !string.IsNullOrEmpty(CalamitasMenuLibrary.ImportError)) {
				UpdateImport();
				return;
			}

			if (_playlistOpen) {
				UpdatePlaylist();
				return;
			}

			if (_galleryOpen) {
				UpdateGallery();
				return;
			}

			if (_logoOpen) {
				UpdateLogoGallery();
				return;
			}

			UpdateSideButtons();
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (fade <= 0f || !CoolerMenuCompat.OnTitleLike)
				return;

			if (!OverlayOpen) {
				if (!CalamitasMenuLayout.Editing) {
					DrawGalleryButton(spriteBatch, fade);
					DrawLogoButton(spriteBatch, fade);
				}

				DrawEditButton(spriteBatch, fade);
				CalamitasMenuLayout.DrawGuides(spriteBatch, fade);
				CalamitasMenuLayout.DrawSaveButton(spriteBatch, fade);
			}

			if (OverlayOpen)
				DrawDim(spriteBatch, fade * 0.72f);

			if (CalamitasMenuLibrary.Importing || !string.IsNullOrEmpty(CalamitasMenuLibrary.ImportError))
				DrawImport(spriteBatch, fade);
			else if (_playlistOpen)
				DrawPlaylist(spriteBatch, fade);
			else if (_galleryOpen)
				DrawGallery(spriteBatch, fade);
			else if (_logoOpen)
				DrawLogoGallery(spriteBatch, fade);

			if (!string.IsNullOrEmpty(_tooltip))
				DrawTooltip(spriteBatch, _tooltip);
		}

		private static void UpdateSideButtons()
		{
			if (CalamitasMenuLayout.Editing) {
				Rectangle save = CalamitasMenuLayout.SaveHit();
				if (save.Contains(Main.mouseX, Main.mouseY)) {
					Main.blockMouse = true;
					_tooltip = L("SaveLayout");
					if (Clicked())
						CalamitasMenuLayout.Save();
					return;
				}

				Rectangle edit = EditHit();
				if (edit.Contains(Main.mouseX, Main.mouseY)) {
					Main.blockMouse = true;
					_tooltip = L("EditPosition");
					if (Clicked())
						CalamitasMenuLayout.TryToggleFromClick();
				}

				return;
			}

			Rectangle gallery = GalleryHit();
			if (gallery.Contains(Main.mouseX, Main.mouseY)) {
				Main.blockMouse = true;
				_tooltip = CalamitasMenuSkyCover.HasHint ? CalamitasMenuSkyCover.HintText : L("MenuBackgrounds");
				if (Clicked()) {
					SoundEngine.PlaySound(SoundID.MenuOpen);
					OpenGallery();
				}

				return;
			}

			Rectangle logos = LogoHit();
			if (logos.Contains(Main.mouseX, Main.mouseY)) {
				Main.blockMouse = true;
				_tooltip = L("MenuLogos");
				if (Clicked()) {
					SoundEngine.PlaySound(SoundID.MenuOpen);
					OpenLogoGallery();
				}

				return;
			}

			Rectangle layout = EditHit();
			if (!layout.Contains(Main.mouseX, Main.mouseY))
				return;

			Main.blockMouse = true;
			_tooltip = L("EditPosition");
			if (Clicked())
				CalamitasMenuLayout.TryToggleFromClick();
		}

		private static void UpdateImport()
		{
			Rectangle panel = Centered(420, 188);
			Rectangle cancel = new(panel.X + 28, panel.Bottom - 46, 120, 28);
			Rectangle close = new(panel.Right - 148, panel.Bottom - 46, 120, 28);
			if (!string.IsNullOrEmpty(CalamitasMenuLibrary.ImportError)) {
				if (Clicked(close) || RightClicked()) {
					CalamitasMenuLibrary.ImportError = "";
					SoundEngine.PlaySound(SoundID.MenuClose);
				}

				return;
			}

			if (Clicked(cancel)) {
				CalamitasMenuLibrary.CancelImport();
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		private static void UpdatePlaylist()
		{
			Rectangle panel = PlaylistRect();
			if (RightClicked() || Clicked(CloseHit(panel))) {
				if (EditingArtist)
					CommitArtistEdit();
				_playlistOpen = false;
				_pendingDeleteId = null;
				SoundEngine.PlaySound(SoundID.MenuClose);
				return;
			}

			if (EditingArtist) {
				PlayerInput.WritingText = true;
				try {
					Main.instance.HandleIME();
				}
				catch {
				}

				_editArtistBuffer = Main.GetInputText(_editArtistBuffer ?? "");
				if (_editArtistBuffer.Length > 48)
					_editArtistBuffer = _editArtistBuffer[..48];

				bool enterDown = Main.inputTextEnter || Keyboard.GetState().IsKeyDown(Keys.Enter);
				if (enterDown && !_enterHeld) {
					CommitArtistEdit();
					SoundEngine.PlaySound(SoundID.MenuTick);
					_enterHeld = enterDown;
					return;
				}

				_enterHeld = enterDown;

				if (Main.inputTextEscape) {
					CancelArtistEdit();
					SoundEngine.PlaySound(SoundID.MenuClose);
					return;
				}
			}

			if (!string.IsNullOrEmpty(CalamitasMenuLibrary.LastNotice)) {
				Rectangle notice = new(panel.X + 16, panel.Y + 42, panel.Width - 32, 36);
				if (Clicked(notice)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					CalamitasMenuLibrary.OpenBrokenFolder();
					CalamitasMenuLibrary.LastNotice = "";
				}
			}

			Rectangle folder = new(panel.X + 20, panel.Bottom - 42, 210, 24);
			if (Clicked(folder)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuLibrary.OpenMusicFolder();
			}

			IReadOnlyList<MenuTrack> catalog = CalamitasMenuPlaylist.Catalog();
			int rowH = 46;
			int viewTop = panel.Y + 58 + (string.IsNullOrEmpty(CalamitasMenuLibrary.LastNotice) ? 0 : 36);
			int viewH = panel.Height - 110 - (string.IsNullOrEmpty(CalamitasMenuLibrary.LastNotice) ? 0 : 36);
			float maxScroll = Math.Max(0f, catalog.Count * rowH - viewH);
			int wheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			float wheel = (wheelValue - _lastWheel) / 120f;
			_lastWheel = wheelValue;
			_playlistScroll = MathHelper.Clamp(_playlistScroll - wheel * 18f, 0f, maxScroll);

			for (int i = 0; i < catalog.Count; i++) {
				var row = new Rectangle(panel.X + 16, viewTop + i * rowH - (int)_playlistScroll, panel.Width - 32, rowH - 6);
				if (row.Bottom < viewTop || row.Y > viewTop + viewH)
					continue;

				MenuTrack track = catalog[i];
				Rectangle toggle = new(row.X + 8, row.Y + 10, 22, 22);
				Rectangle del = new(row.Right - 78, row.Y + 8, 66, 24);
				Rectangle rename = new(row.Right - 156, row.Y + 8, 70, 24);
				Rectangle artistHit = new(row.X + 40, row.Y + 20, Math.Max(40, rename.X - row.X - 48), 18);

				if (EditingArtist) {
					if (track.Id == _editArtistId && Clicked(rename)) {
						CommitArtistEdit();
						SoundEngine.PlaySound(SoundID.MenuTick);
						return;
					}

					if (Clicked(row) && track.Id != _editArtistId)
						CommitArtistEdit();
					continue;
				}

				if (Clicked(toggle)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					CalamitasMenuPlaylist.SetEnabled(track, !track.Enabled);
				}
				else if (track.IsCustom && Clicked(del)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					if (_pendingDeleteId == track.Id) {
						CalamitasMenuPlaylist.DeleteCustom(track);
						_pendingDeleteId = null;
					}
					else {
						_pendingDeleteId = track.Id;
					}
				}
				else if (track.IsCustom && (Clicked(rename) || Clicked(artistHit))) {
					BeginArtistEdit(track);
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
				else if (track.IsCustom && (rename.Contains(Main.mouseX, Main.mouseY) || artistHit.Contains(Main.mouseX, Main.mouseY)))
					_tooltip = L("ClickRenameArtist");
			}
		}

		private static void BeginArtistEdit(MenuTrack track)
		{
			_editArtistId = track.Id;
			_editArtistBuffer = string.Equals(track.Artist, "Custom", StringComparison.OrdinalIgnoreCase) ? "" : track.Artist ?? "";
			Main.clrInput();
			PlayerInput.WritingText = true;
			_enterHeld = true;
		}

		private static void CommitArtistEdit()
		{
			if (string.IsNullOrEmpty(_editArtistId))
				return;

			MenuTrack track = CalamitasMenuPlaylist.Catalog().FirstOrDefault(item => item.Id == _editArtistId);
			if (track != null)
				CalamitasMenuPlaylist.SetArtist(track, _editArtistBuffer);

			CancelArtistEdit();
		}

		private static void CancelArtistEdit()
		{
			_editArtistId = null;
			_editArtistBuffer = "";
		}

		private static void UpdateGallery()
		{
			PulseArtScan();
			Rectangle panel = GalleryRect();
			if (RightClicked() || Clicked(CloseHit(panel))) {
				if (EditingArtist)
					CommitArtistEdit();
				_galleryOpen = false;
				_draggingInterval = false;
				_pendingDeleteArtId = null;
				SoundEngine.PlaySound(SoundID.MenuClose);
				return;
			}

			if (UpdateAccentClicks(panel))
				return;

			var view = GalleryView(panel);
			ApplyOverlayWheel(ref _galleryScroll, GalleryMaxScroll(view));
			List<GItem> items = GalleryItems();
			Rectangle[] cards = SceneCards(panel, items.Count);
			for (int i = 0; i < items.Count; i++) {
				GItem item = items[i];
				Rectangle card = cards[i];
				if (item.Kind == GKind.Custom) {
					if (!CardInView(card, view) || !view.Contains(Main.mouseX, Main.mouseY))
						continue;
					CustomArtRecord record = CalamitasMenuUserArt.Wallpapers[item.Index];
					if (Clicked(ArtDeleteHit(card))) {
						SoundEngine.PlaySound(SoundID.MenuTick);
						if (_pendingDeleteArtId == record.Id) {
							CalamitasMenuUserArt.DeleteWallpaper(record);
							_pendingDeleteArtId = null;
						}
						else {
							_pendingDeleteArtId = record.Id;
						}

						return;
					}
				}

				if (HandleCardTools(card, item.Key, view)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					_pendingDeleteArtId = null;
					return;
				}

				if (!ClickedVisible(card, view))
					continue;

				SoundEngine.PlaySound(SoundID.MenuTick);
				_pendingDeleteArtId = null;
				switch (item.Kind) {
					case GKind.Follow:
						DieWithASmileSettings.SetFollowMusic();
						break;
					case GKind.Shuffle:
						DieWithASmileSettings.SetShuffleScenes();
						break;
					case GKind.Upload:
						if (CalamitasMenuUserArt.TryImportWallpaper())
							SoundEngine.PlaySound(SoundID.MenuOpen);
						break;
					default:
						CalamitasMenuWallpaper.Apply(item.Key, keepShuffle: false);
						break;
				}
			}

			if (DieWithASmileSettings.ShuffleScenes)
				UpdateIntervalSlider(panel);
			else
				_draggingInterval = false;

			Rectangle hide = HideHit(panel);
			if (!_draggingInterval && Clicked(hide)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				DieWithASmileSettings.SetPlayerEnabled(!DieWithASmileSettings.PlayerEnabled);
			}

			Rectangle hiddenBtn = HiddenHit(panel);
			if (!_draggingInterval && (CalamitasMenuWallpaper.HiddenCount() > 0 || _showHidden) && Clicked(hiddenBtn)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				_showHidden = !_showHidden;
				if (_showHidden && CalamitasMenuWallpaper.HiddenCount() == 0)
					_showHidden = false;
				_galleryScroll = 0f;
			}
		}

		private static void UpdateIntervalSlider(Rectangle panel)
		{
			Rectangle bar = IntervalBar(panel);
			Rectangle hit = IntervalHit(panel);
			if (_rawLeft && (_draggingInterval || hit.Contains(Main.mouseX, Main.mouseY))) {
				_draggingInterval = true;
				_leftClick = false;
				Main.mouseLeftRelease = false;
				Main.blockMouse = true;
				float t = MathHelper.Clamp((Main.mouseX - bar.X) / (float)Math.Max(1, bar.Width), 0f, 1f);
				DieWithASmileSettings.SetShuffleSceneSeconds(t * 15f, save: false);
				return;
			}

			if (_draggingInterval && !_rawLeft) {
				_draggingInterval = false;
				DieWithASmileSave.Save();
			}
		}

		private static void DrawIntervalSlider(SpriteBatch spriteBatch, Rectangle panel, float fade)
		{
			Rectangle bar = IntervalBar(panel);
			float t = DieWithASmileSettings.ShuffleSceneSeconds / 15f;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			bool hover = IntervalHit(panel).Contains(Main.mouseX, Main.mouseY) || _draggingInterval;
			spriteBatch.Draw(pixel, bar, Color.White * ((hover ? 0.22f : 0.14f) * fade));
			spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, Math.Max(2, (int)(bar.Width * t)), bar.Height), Neon * fade);
			DrawRound(spriteBatch, new Vector2(bar.X + bar.Width * t, bar.Y + bar.Height * 0.5f), 7f, fade, hover);

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			int seconds = (int)DieWithASmileSettings.ShuffleSceneSeconds;
			string label = seconds <= 0 ? L("ShuffleOnLeave") : CalamitasMenuText.UI("ShuffleEvery", seconds);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				label,
				new Vector2(bar.Right + 12, bar.Y - 6),
				TextMain * fade,
				0f,
				Vector2.Zero,
				new Vector2(0.72f));
		}

		private static void DrawGalleryButton(SpriteBatch spriteBatch, float fade)
		{
			Rectangle hit = GalleryHit();
			Vector2 center = hit.Center.ToVector2();
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			DrawRound(spriteBatch, center, hit.Width * 0.5f, fade, hover);
			DrawTintedIcon(spriteBatch, CalamitasMenuIcons.Gallery, center, 22f, hover, fade);
		}

		private static void DrawLogoButton(SpriteBatch spriteBatch, float fade)
		{
			Rectangle hit = LogoHit();
			Vector2 center = hit.Center.ToVector2();
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			DrawRound(spriteBatch, center, hit.Width * 0.5f, fade, hover);
			DrawTintedIcon(spriteBatch, CalamitasMenuIcons.ChangeLogo, center, 22f, hover, fade);
		}

		private static void DrawEditButton(SpriteBatch spriteBatch, float fade)
		{
			Rectangle hit = EditHit();
			Vector2 center = hit.Center.ToVector2();
			bool hover = hit.Contains(Main.mouseX, Main.mouseY) || CalamitasMenuLayout.Editing;
			DrawRound(spriteBatch, center, hit.Width * 0.5f, fade, hover);
			DrawTintedIcon(spriteBatch, CalamitasMenuIcons.ChangePosition, center, 22f, hover, fade);
		}

		private static void UpdateLogoGallery()
		{
			PulseArtScan();
			Rectangle panel = LogoRect();
			if (RightClicked() || Clicked(CloseHit(panel))) {
				_logoOpen = false;
				_pendingDeleteArtId = null;
				SoundEngine.PlaySound(SoundID.MenuClose);
				return;
			}

			if (UpdateAccentClicks(panel))
				return;

			var view = LogoView(panel);
			ApplyOverlayWheel(ref _logoScroll, LogoMaxScroll(view));
			var cards = LogoCards(panel);
			const int builtin = 5;
			int logoStart = 1;
			if (ClickedVisible(cards[0], view)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				_pendingDeleteArtId = null;
				DieWithASmileSettings.SetShuffleLogos();
				return;
			}

			for (int i = 0; i < builtin; i++) {
				if (ClickedVisible(cards[logoStart + i], view)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					_pendingDeleteArtId = null;
					DieWithASmileSettings.SetLogo((MenuLogo)i);
				}
			}

			for (int i = 0; i < _foreignLogos.Count; i++) {
				if (ClickedVisible(cards[logoStart + builtin + i], view)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					_pendingDeleteArtId = null;
					DieWithASmileSettings.SetForeignLogo(_foreignLogos[i].FullName);
				}
			}

			IReadOnlyList<CustomArtRecord> logos = CalamitasMenuUserArt.Logos;
			int customStart = logoStart + builtin + _foreignLogos.Count;
			for (int i = 0; i < logos.Count; i++) {
				Rectangle card = cards[customStart + i];
				if (!CardInView(card, view) || !view.Contains(Main.mouseX, Main.mouseY))
					continue;

				Rectangle del = ArtDeleteHit(card);
				if (Clicked(del)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					if (_pendingDeleteArtId == logos[i].Id) {
						CalamitasMenuUserArt.DeleteLogo(logos[i]);
						_pendingDeleteArtId = null;
					}
					else {
						_pendingDeleteArtId = logos[i].Id;
					}

					return;
				}

				if (Clicked(card)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					_pendingDeleteArtId = null;
					DieWithASmileSettings.SetCustomLogo(logos[i].Id);
				}
			}

			if (ClickedVisible(cards[^1], view)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				_pendingDeleteArtId = null;
				if (CalamitasMenuUserArt.TryImportLogo())
					SoundEngine.PlaySound(SoundID.MenuOpen);
			}
		}

		private static void DrawLogoGallery(SpriteBatch spriteBatch, float fade)
		{
			Rectangle panel = LogoRect();
			DrawPanel(spriteBatch, panel, fade);
			var view = LogoView(panel);
			var cards = LogoCards(panel);
			CalamitasMenuDraw.WithClip(spriteBatch, view, () => {
				const int builtin = 5;
				int logoStart = 1;
				if (CardInView(cards[0], view))
					DrawLogoCard(spriteBatch, cards[0], MenuLogo.Orbit, L("ShuffleLogos"), DieWithASmileSettings.ShuffleLogos, fade);

				for (int i = 0; i < builtin; i++) {
					if (!CardInView(cards[logoStart + i], view))
						continue;
					var logo = (MenuLogo)i;
					bool selected = !DieWithASmileSettings.UsingCustomLogo && DieWithASmileSettings.Logo == logo;
					DrawLogoCard(spriteBatch, cards[logoStart + i], logo, LogoLabel(logo), selected, fade);
				}

				for (int i = 0; i < _foreignLogos.Count; i++) {
					if (!CardInView(cards[logoStart + builtin + i], view))
						continue;
					ModMenu menu = _foreignLogos[i];
					bool selected = DieWithASmileSettings.UsingForeignLogo && DieWithASmileSave.Data.ForeignLogoId == menu.FullName;
					DrawModArtCard(spriteBatch, cards[logoStart + builtin + i], CalamitasMenuForeign.PreviewLogo(menu), menu.DisplayName, selected, fade, cover: false);
				}

				IReadOnlyList<CustomArtRecord> logos = CalamitasMenuUserArt.Logos;
				int customStart = logoStart + builtin + _foreignLogos.Count;
				for (int i = 0; i < logos.Count; i++) {
					if (!CardInView(cards[customStart + i], view))
						continue;
					CustomArtRecord record = logos[i];
					bool selected = DieWithASmileSettings.UsingFileLogo && DieWithASmileSave.Data.CustomLogoId == record.Id;
					DrawCustomArtCard(
						spriteBatch,
						cards[customStart + i],
						CalamitasMenuUserArt.TextureOf(record, logo: true),
						ArtLabel(record),
						selected,
						fade,
						record.Id);
				}

				if (CardInView(cards[^1], view))
					DrawUploadCard(spriteBatch, cards[^1], L("UploadLogo"), fade);

				DrawOverlayScroll(spriteBatch, view, _logoScroll, LogoMaxScroll(view), fade);
			});
			CoverOverlayChrome(spriteBatch, panel, view, fade);
			DrawCentered(spriteBatch, FontAssets.MouseText.Value, L("MenuLogo"), new Vector2(panel.Center.X, panel.Y + 16), TextMain * fade, 1f);
			DrawAccentSwatches(spriteBatch, panel, fade);
			DrawClose(spriteBatch, CloseHit(panel), fade);
		}

		private static string LogoLabel(MenuLogo logo) => logo switch
		{
			MenuLogo.Classic => L("LogoClassic"),
			MenuLogo.Gothic => L("LogoGothic"),
			MenuLogo.Orbit => L("LogoOrbit"),
			MenuLogo.Hands => L("LogoHands"),
			MenuLogo.Sticker => L("LogoSticker"),
			_ => ""
		};

		private static void DrawLogoCard(SpriteBatch spriteBatch, Rectangle rect, MenuLogo logo, string label, bool selected, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Color(20, 18, 18) * fade);
			CalamitasMenuLogo.DrawPreview(spriteBatch, new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 24), logo, fade);

			Color border = selected ? Neon * fade : rect.Contains(Main.mouseX, Main.mouseY) ? Color.White * (0.8f * fade) : new Color(80, 70, 70) * fade;
			DrawBorder(spriteBatch, rect, border, selected ? 3 : 2);
			float textScale = 0.7f;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * textScale;
			if (size.X > rect.Width - 8f)
				textScale *= (rect.Width - 8f) / size.X;
			size = FontAssets.MouseText.Value.MeasureString(label) * textScale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				FontAssets.MouseText.Value,
				label,
				new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Bottom - 20f),
				TextMain * fade,
				0f,
				Vector2.Zero,
				new Vector2(textScale));
		}

		private static void DrawCustomArtCard(SpriteBatch spriteBatch, Rectangle rect, Texture2D tex, string label, bool selected, float fade, string id)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Color(20, 18, 18) * fade);
			if (tex != null) {
				Rectangle dest = new(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 24);
				if (id.StartsWith("wall:", StringComparison.OrdinalIgnoreCase)) {
					GetCoverSource(tex, dest, DieWithASmileSettings.WallpaperPanOf(id), out Rectangle src);
					CalamitasMenuDraw.WithLinear(spriteBatch, () =>
						spriteBatch.Draw(tex, dest, src, Color.White * fade));
				}
				else {
					CalamitasMenuDraw.WithLinear(spriteBatch, () =>
						CalamitasMenuLogo.DrawPreviewTexture(spriteBatch, dest, tex, fade));
				}
			}

			Color border = selected ? Neon * fade : rect.Contains(Main.mouseX, Main.mouseY) ? Color.White * (0.8f * fade) : new Color(80, 70, 70) * fade;
			DrawBorder(spriteBatch, rect, border, selected ? 3 : 2);
			DrawCardLabel(spriteBatch, rect, label, fade);
			DrawTextButton(spriteBatch, ArtDeleteHit(rect), _pendingDeleteArtId == id ? L("Sure") : L("Delete"), fade, true);
		}

		private static void DrawUploadCard(SpriteBatch spriteBatch, Rectangle rect, string label, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			bool hover = rect.Contains(Main.mouseX, Main.mouseY);
			spriteBatch.Draw(pixel, rect, new Color(20, 18, 18) * fade);
			spriteBatch.Draw(pixel, new Rectangle(rect.Center.X - 14, rect.Y + rect.Height / 2 - 18, 28, 4), Neon * ((hover ? 0.95f : 0.7f) * fade));
			spriteBatch.Draw(pixel, new Rectangle(rect.Center.X - 2, rect.Y + rect.Height / 2 - 30, 4, 28), Neon * ((hover ? 0.95f : 0.7f) * fade));
			DrawBorder(spriteBatch, rect, hover ? Color.White * (0.8f * fade) : Neon * (0.55f * fade), hover ? 3 : 2);
			DrawCardLabel(spriteBatch, rect, label, fade);
		}

		private static void DrawCardLabel(SpriteBatch spriteBatch, Rectangle rect, string label, float fade)
		{
			float textScale = 0.7f;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * textScale;
			if (size.X > rect.Width - 8f)
				textScale *= (rect.Width - 8f) / size.X;
			size = FontAssets.MouseText.Value.MeasureString(label) * textScale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				FontAssets.MouseText.Value,
				label,
				new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Bottom - 20f),
				TextMain * fade,
				0f,
				Vector2.Zero,
				new Vector2(textScale));
		}

		private static Rectangle ArtDeleteHit(Rectangle card) =>
			_showHidden
				? new Rectangle(card.Right - 58, card.Y + 28, 54, 18)
				: new Rectangle(card.Right - 58, card.Y + 4, 54, 18);

		private static string ArtLabel(CustomArtRecord record)
		{
			string name = Path.GetFileNameWithoutExtension(record?.FileName ?? "");
			if (string.IsNullOrWhiteSpace(name))
				return "PNG";
			return name.Length > 18 ? name[..16] + "..." : name;
		}

		private static void DrawModArtCard(SpriteBatch spriteBatch, Rectangle rect, Texture2D tex, string label, bool selected, float fade, bool cover)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Color(20, 18, 18) * fade);
			if (tex != null) {
				Rectangle dest = new(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 24);
				if (cover) {
					GetCoverSource(tex, dest, out Rectangle src);
					CalamitasMenuDraw.WithLinear(spriteBatch, () =>
						spriteBatch.Draw(tex, dest, src, Color.White * fade));
				}
				else {
					CalamitasMenuDraw.WithLinear(spriteBatch, () =>
						CalamitasMenuLogo.DrawPreviewTexture(spriteBatch, dest, tex, fade));
				}
			}

			Color border = selected ? Neon * fade : rect.Contains(Main.mouseX, Main.mouseY) ? Color.White * (0.8f * fade) : new Color(80, 70, 70) * fade;
			DrawBorder(spriteBatch, rect, border, selected ? 3 : 2);
			DrawCardLabel(spriteBatch, rect, label, fade);
		}

		private static void PulseArtScan()
		{
			if (++_artPulse < 40)
				return;

			_artPulse = 0;
			CalamitasMenuUserArt.Scan();
			RefreshForeign();
		}

		private static void RefreshForeign()
		{
			_foreignWalls = CalamitasMenuForeign.WallpaperMenus();
			_foreignLogos = CalamitasMenuForeign.LogoMenus();
			_tmlStyles = CalamitasMenuForeign.ReplacementStyles();
		}

		private static Rectangle[] LogoCards(Rectangle panel)
		{
			const float gap = 8f;
			const int cols = 5;
			int total = 6 + _foreignLogos.Count + CalamitasMenuUserArt.Logos.Count + 1;
			return WrapCards(panel, total, cols, 44f, 128f, gap, _logoScroll);
		}

		private static void DrawImport(SpriteBatch spriteBatch, float fade)
		{
			Rectangle panel = Centered(420, 188);
			DrawPanel(spriteBatch, panel, fade);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			DrawCentered(spriteBatch, font, L("UploadingSong"), new Vector2(panel.Center.X, panel.Y + 18), TextMain * fade, 0.95f);
			DrawCentered(spriteBatch, font, CalamitasMenuLibrary.ImportTitle, new Vector2(panel.Center.X, panel.Y + 44), Neon * fade, 0.78f);
			string status = string.IsNullOrEmpty(CalamitasMenuLibrary.ImportError)
				? CalamitasMenuLibrary.ImportStatus
				: CalamitasMenuLibrary.ImportError;
			DrawCentered(spriteBatch, font, status, new Vector2(panel.Center.X, panel.Y + 68), TextSub * fade, 0.72f);

			var bar = new Rectangle(panel.X + 28, panel.Y + 98, panel.Width - 56, 10);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, bar, Color.White * (0.12f * fade));
			float progress = MathHelper.Clamp(CalamitasMenuLibrary.ImportProgress, 0f, 1f);
			spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, Math.Max(2, (int)(bar.Width * progress)), bar.Height), Neon * fade);
			DrawCentered(spriteBatch, font, $"{progress * 100f:0}%", new Vector2(panel.Center.X, bar.Bottom + 8), TextSub * fade, 0.7f);

			if (string.IsNullOrEmpty(CalamitasMenuLibrary.ImportError))
				DrawTextButton(spriteBatch, new Rectangle(panel.X + 28, panel.Bottom - 46, 120, 28), L("Cancel"), fade);
			else
				DrawTextButton(spriteBatch, new Rectangle(panel.Right - 148, panel.Bottom - 46, 120, 28), L("Close"), fade);
		}

		private static void DrawPlaylist(SpriteBatch spriteBatch, float fade)
		{
			Rectangle panel = PlaylistRect();
			DrawPanel(spriteBatch, panel, fade);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			DrawCentered(spriteBatch, font, L("Playlist"), new Vector2(panel.Center.X, panel.Y + 16), TextMain * fade, 1f);
			DrawClose(spriteBatch, CloseHit(panel), fade);

			bool hasNotice = !string.IsNullOrEmpty(CalamitasMenuLibrary.LastNotice);
			if (hasNotice) {
				var notice = new Rectangle(panel.X + 16, panel.Y + 42, panel.Width - 32, 36);
				Texture2D noticePixel = TextureAssets.MagicPixel.Value;
				spriteBatch.Draw(noticePixel, notice, Neon * (0.22f * fade));
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					CalamitasMenuLibrary.LastNotice,
					new Vector2(notice.X + 8, notice.Y + 4),
					TextMain * fade,
					0f,
					Vector2.Zero,
					new Vector2(0.58f));
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					L("ClickBrokenFolder"),
					new Vector2(notice.X + 8, notice.Y + 20),
					Neon * fade,
					0f,
					Vector2.Zero,
					new Vector2(0.56f));
			}

			IReadOnlyList<MenuTrack> catalog = CalamitasMenuPlaylist.Catalog();
			int rowH = 46;
			int viewTop = panel.Y + 58 + (hasNotice ? 36 : 0);
			int viewH = panel.Height - 110 - (hasNotice ? 36 : 0);
			var clip = new Rectangle(panel.X + 12, viewTop, panel.Width - 24, viewH);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, clip, new Color(18, 16, 16) * (0.55f * fade));

			for (int i = 0; i < catalog.Count; i++) {
				var row = new Rectangle(panel.X + 16, viewTop + i * rowH - (int)_playlistScroll, panel.Width - 32, rowH - 6);
				if (row.Bottom < viewTop || row.Y > viewTop + viewH)
					continue;

				MenuTrack track = catalog[i];
				bool current = track.Id == CalamitasMenuPlaylist.Current.Id;
				spriteBatch.Draw(pixel, row, (current ? Neon : Color.White) * ((current ? 0.16f : 0.05f) * fade));
				Rectangle toggle = new(row.X + 8, row.Y + 10, 22, 22);
				DrawToggle(spriteBatch, toggle, track.Enabled, fade);
				Color title = (track.Enabled ? TextMain : TextSub) * fade;
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, track.Title, new Vector2(row.X + 40, row.Y + 6), title, 0f, Vector2.Zero, new Vector2(0.8f));
				if (track.IsCustom && track.Id == _editArtistId) {
					string shown = string.IsNullOrEmpty(_editArtistBuffer) ? L("ArtistPlaceholder") : _editArtistBuffer + (((int)(Main.GlobalTimeWrappedHourly * 2f) & 1) == 0 ? "|" : "");
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, shown, new Vector2(row.X + 40, row.Y + 24), Neon * fade, 0f, Vector2.Zero, new Vector2(0.62f));
				}
				else {
					string sub = track.Artist;
					bool artistHover = track.IsCustom && new Rectangle(row.X + 40, row.Y + 22, Math.Max(40, row.Width - 130), 16).Contains(Main.mouseX, Main.mouseY);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, sub, new Vector2(row.X + 40, row.Y + 24), (artistHover ? Color.White : TextSub) * (0.85f * fade), 0f, Vector2.Zero, new Vector2(0.62f));
				}

				if (track.IsCustom) {
					Rectangle rename = new(row.Right - 156, row.Y + 8, 70, 24);
					Rectangle del = new(row.Right - 78, row.Y + 8, 66, 24);
					DrawTextButton(spriteBatch, rename, track.Id == _editArtistId ? L("Done") : L("Rename"), fade);
					DrawTextButton(spriteBatch, del, _pendingDeleteId == track.Id ? L("Sure") : L("Delete"), fade, true);
				}
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				L("OpenMusicFolder"),
				new Vector2(panel.X + 22, panel.Bottom - 38),
				folderHover() ? Color.White * fade : Neon * fade,
				0f,
				Vector2.Zero,
				new Vector2(0.72f));
			DrawCentered(spriteBatch, font, L("RenameHint"), new Vector2(panel.Center.X, panel.Bottom - 16), TextSub * (0.8f * fade), 0.54f);

			bool folderHover() => new Rectangle(panel.X + 20, panel.Bottom - 42, 210, 24).Contains(Main.mouseX, Main.mouseY);
		}

		private static void DrawGallery(SpriteBatch spriteBatch, float fade)
		{
			Rectangle panel = GalleryRect();
			DrawPanel(spriteBatch, panel, fade);
			var view = GalleryView(panel);
			List<GItem> items = GalleryItems();
			var cards = SceneCards(panel, items.Count);
			CalamitasMenuDraw.WithClip(spriteBatch, view, () => DrawGalleryCards(spriteBatch, view, cards, items, fade));
			CoverOverlayChrome(spriteBatch, panel, view, fade);

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			DrawCentered(spriteBatch, font, _showHidden ? L("HiddenBackgrounds") : L("MenuBackground"), new Vector2(panel.Center.X, panel.Y + 16), TextMain * fade, 1f);
			if (CalamitasMenuSkyCover.HasHint) {
				string hint = CalamitasMenuSkyCover.HintText;
				float hintScale = 0.56f;
				Vector2 hintSize = font.MeasureString(hint) * hintScale;
				if (hintSize.X > panel.Width - 40f)
					hintScale *= (panel.Width - 40f) / hintSize.X;
				DrawCentered(spriteBatch, font, hint, new Vector2(panel.Center.X, panel.Y + 34), TextSub * fade, hintScale);
			}

			if (DieWithASmileSettings.ShuffleScenes) {
				DrawCentered(spriteBatch, font, L("ShufflePickHint"), new Vector2(panel.Center.X, panel.Bottom - 114), TextSub * fade, 0.56f);
				DrawIntervalSlider(spriteBatch, panel, fade);
			}

			Rectangle hide = HideHit(panel);
			bool on = DieWithASmileSettings.PlayerEnabled;
			DrawRound(spriteBatch, new Vector2(hide.X + 14, hide.Center.Y), 13f, fade, hide.Contains(Main.mouseX, Main.mouseY));
			DrawTintedIcon(
				spriteBatch,
				on ? CalamitasMenuIcons.PlayerOn : CalamitasMenuIcons.PlayerOff,
				new Vector2(hide.X + 14, hide.Center.Y),
				16f,
				hide.Contains(Main.mouseX, Main.mouseY),
				fade,
				on);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				on ? L("HideMusicPlayer") : L("ShowMusicPlayer"),
				new Vector2(hide.X + 36, hide.Y + 6),
				TextMain * fade,
				0f,
				Vector2.Zero,
				new Vector2(0.78f));
			if (CalamitasMenuWallpaper.HiddenCount() > 0 || _showHidden) {
				Rectangle hiddenBtn = HiddenHit(panel);
				bool hiddenHover = hiddenBtn.Contains(Main.mouseX, Main.mouseY);
				DrawRound(spriteBatch, new Vector2(hiddenBtn.X + 14, hiddenBtn.Center.Y), 13f, fade, hiddenHover);
				DrawTintedIcon(
					spriteBatch,
					_showHidden ? CalamitasMenuIcons.Hide : CalamitasMenuIcons.Show,
					new Vector2(hiddenBtn.X + 14, hiddenBtn.Center.Y),
					16f,
					hiddenHover,
					fade,
					_showHidden);
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					_showHidden ? L("ShowVisibleBackgrounds") : L("ShowHiddenBackgrounds"),
					new Vector2(hiddenBtn.X + 36, hiddenBtn.Y + 6),
					TextMain * fade,
					0f,
					Vector2.Zero,
					new Vector2(0.78f));
			}
			DrawAccentSwatches(spriteBatch, panel, fade);
			DrawClose(spriteBatch, CloseHit(panel), fade);
		}

		private static void DrawGalleryCards(SpriteBatch spriteBatch, Rectangle view, Rectangle[] cards, List<GItem> items, float fade)
		{
			string current = CalamitasMenuWallpaper.CurrentKey();
			bool shuffleOn = DieWithASmileSettings.ShuffleScenes;
			for (int i = 0; i < items.Count; i++) {
				if (!CardInView(cards[i], view))
					continue;
				GItem item = items[i];
				bool selected = item.Kind switch {
					GKind.Follow => DieWithASmileSettings.FollowMusic && !DieWithASmileSettings.UsingCustomWallpaper,
					GKind.Shuffle => shuffleOn,
					_ => !string.IsNullOrEmpty(item.Key) && item.Key == current
				};
				float cardFade = shuffleOn && !string.IsNullOrEmpty(item.Key) && !CalamitasMenuWallpaper.InShuffle(item.Key)
					? fade * 0.42f
					: fade;
				switch (item.Kind) {
					case GKind.Follow:
						DrawSceneCard(spriteBatch, cards[i], 0, L("FollowMusic"), selected, fade, true);
						break;
					case GKind.Shuffle:
						DrawSceneCard(spriteBatch, cards[i], 4, L("ShuffleBackgrounds"), selected, fade, true);
						break;
					case GKind.Scene:
						DrawSceneCard(spriteBatch, cards[i], item.Index, SceneLabel(item.Index), selected, cardFade, false);
						DrawCardTools(spriteBatch, cards[i], item.Key, fade, shuffleOn);
						break;
					case GKind.Vanilla:
						VanillaMenuScene scene = CalamitasMenuVanilla.Scenes[item.Index];
						DrawIconCard(spriteBatch, cards[i], CalamitasMenuForeign.TmlLogoPreview, L(scene.Key), selected, cardFade);
						DrawCardTools(spriteBatch, cards[i], item.Key, fade, shuffleOn);
						break;
					case GKind.Tml:
						DrawIconCard(spriteBatch, cards[i], CalamitasMenuForeign.TmlLogoPreview, L("TmlBackground"), selected, cardFade);
						DrawCardTools(spriteBatch, cards[i], item.Key, fade, shuffleOn);
						break;
					case GKind.Orphan: {
						ModSurfaceBackgroundStyle style = _tmlStyles[item.Index];
						Texture2D art = CalamitasMenuForeign.GalleryThumb(style, out bool cover);
						DrawModArtCard(spriteBatch, cards[i], art, CalamitasMenuForeign.StyleLabel(style), selected, cardFade, cover);
						DrawCardTools(spriteBatch, cards[i], item.Key, fade, shuffleOn);
						break;
					}
					case GKind.Foreign: {
						ModMenu menu = _foreignWalls[item.Index];
						Texture2D art = CalamitasMenuForeign.GalleryThumb(menu, out bool cover);
						DrawModArtCard(spriteBatch, cards[i], art, menu.DisplayName, selected, cardFade, cover);
						DrawCardTools(spriteBatch, cards[i], item.Key, fade, shuffleOn);
						break;
					}
					case GKind.Custom: {
						CustomArtRecord record = CalamitasMenuUserArt.Wallpapers[item.Index];
						DrawCustomArtCard(
							spriteBatch,
							cards[i],
							CalamitasMenuUserArt.TextureOf(record, logo: false),
							ArtLabel(record),
							selected,
							cardFade,
							record.Id);
						DrawCardTools(spriteBatch, cards[i], item.Key, fade, shuffleOn);
						break;
					}
					case GKind.Upload:
						DrawUploadCard(spriteBatch, cards[i], L("UploadBackground"), fade);
						break;
				}
			}

			DrawOverlayScroll(spriteBatch, view, _galleryScroll, GalleryMaxScroll(view), fade);
		}

		private static void DrawSceneCard(SpriteBatch spriteBatch, Rectangle rect, int previewIndex, string label, bool selected, float fade, bool follow)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Color(20, 18, 18) * fade);
			Texture2D tex = _previews?[previewIndex]?.Value;
			if (tex != null) {
				GetCoverSource(tex, rect, out Rectangle src);
				spriteBatch.Draw(tex, rect, src, Color.White * ((follow ? 0.72f : 1f) * fade));
			}

			if (follow)
				spriteBatch.Draw(pixel, rect, Neon * (0.18f * fade));

			Color border = selected ? Neon * fade : rect.Contains(Main.mouseX, Main.mouseY) ? Color.White * (0.8f * fade) : new Color(80, 70, 70) * fade;
			DrawBorder(spriteBatch, rect, border, selected ? 3 : 2);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float textScale = 0.7f;
			Vector2 size = font.MeasureString(label) * textScale;
			if (size.X > rect.Width - 8f)
				textScale *= (rect.Width - 8f) / size.X;
			size = font.MeasureString(label) * textScale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				label,
				new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Bottom - 20f),
				TextMain * fade,
				0f,
				Vector2.Zero,
				new Vector2(textScale));
		}

		private static void DrawIconCard(SpriteBatch spriteBatch, Rectangle rect, Texture2D tex, string label, bool selected, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, rect, new Color(20, 18, 18) * fade);
			if (tex != null) {
				Rectangle dest = new(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 24);
				float scale = Math.Min(dest.Width / (float)tex.Width, dest.Height / (float)tex.Height) * 0.72f;
				Vector2 center = dest.Center.ToVector2();
				CalamitasMenuDraw.WithLinear(spriteBatch, () =>
					spriteBatch.Draw(tex, center, null, Color.White * fade, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f));
			}

			Color border = selected ? Neon * fade : rect.Contains(Main.mouseX, Main.mouseY) ? Color.White * (0.8f * fade) : new Color(80, 70, 70) * fade;
			DrawBorder(spriteBatch, rect, border, selected ? 3 : 2);
			DrawCardLabel(spriteBatch, rect, label, fade);
		}

		private static List<GItem> GalleryItems()
		{
			var list = new List<GItem>();
			bool hiddenView = _showHidden;
			void add(GKind kind, int index, string key)
			{
				if (CalamitasMenuWallpaper.IsHidden(key) == hiddenView)
					list.Add(new GItem(kind, index, key));
			}

			if (!hiddenView)
				list.Add(new GItem(GKind.Follow));

			for (int i = 0; i < ScenePaths.Length; i++)
				add(GKind.Scene, i, CalamitasMenuWallpaper.Scene(i));

			if (!hiddenView)
				list.Add(new GItem(GKind.Shuffle));

			for (int i = 0; i < CalamitasMenuVanilla.Count; i++) {
				VanillaMenuScene scene = CalamitasMenuVanilla.Scenes[i];
				add(GKind.Vanilla, i, CalamitasMenuWallpaper.Vanilla(scene.Style));
			}

			add(GKind.Tml, 0, CalamitasMenuWallpaper.Tml);

			for (int i = 0; i < _tmlStyles.Count; i++)
				add(GKind.Orphan, i, CalamitasMenuWallpaper.Orphan(CalamitasMenuForeign.StyleKey(_tmlStyles[i])));

			for (int i = 0; i < _foreignWalls.Count; i++)
				add(GKind.Foreign, i, CalamitasMenuWallpaper.Foreign(_foreignWalls[i].FullName));

			IReadOnlyList<CustomArtRecord> walls = CalamitasMenuUserArt.Wallpapers;
			for (int i = 0; i < walls.Count; i++)
				add(GKind.Custom, i, CalamitasMenuWallpaper.Custom(walls[i].Id));

			if (!hiddenView)
				list.Add(new GItem(GKind.Upload));

			return list;
		}

		private static bool HandleCardTools(Rectangle card, string key, Rectangle view)
		{
			if (string.IsNullOrEmpty(key) || !CardInView(card, view) || !view.Contains(Main.mouseX, Main.mouseY))
				return false;
			if (Clicked(EyeHit(card))) {
				CalamitasMenuWallpaper.ToggleHidden(key);
				if (_showHidden && CalamitasMenuWallpaper.HiddenCount() == 0)
					_showHidden = false;
				return true;
			}

			if (!_showHidden && DieWithASmileSettings.ShuffleScenes && Clicked(CheckHit(card))) {
				CalamitasMenuWallpaper.ToggleShuffle(key);
				return true;
			}

			return false;
		}

		private static void DrawCardTools(SpriteBatch spriteBatch, Rectangle card, string key, float fade, bool shuffleOn)
		{
			if (string.IsNullOrEmpty(key))
				return;

			if (!_showHidden && shuffleOn) {
				Rectangle check = CheckHit(card);
				bool on = CalamitasMenuWallpaper.InShuffle(key);
				bool hover = check.Contains(Main.mouseX, Main.mouseY);
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				spriteBatch.Draw(pixel, check, new Color(12, 10, 10) * fade);
				DrawBorder(spriteBatch, check, (hover || on ? Neon : new Color(80, 70, 70)) * fade, on ? 2 : 1);
				if (on)
					spriteBatch.Draw(pixel, new Rectangle(check.X + 4, check.Y + 4, check.Width - 8, check.Height - 8), Neon * fade);
			}

			Rectangle eye = EyeHit(card);
			bool eyeHover = eye.Contains(Main.mouseX, Main.mouseY);
			DrawRound(spriteBatch, eye.Center.ToVector2(), 11f, fade, eyeHover);
			DrawTintedIcon(
				spriteBatch,
				_showHidden ? CalamitasMenuIcons.Show : CalamitasMenuIcons.Hide,
				eye.Center.ToVector2(),
				14f,
				eyeHover,
				fade,
				true);
		}

		private static Rectangle EyeHit(Rectangle card)
		{
			const int s = 22;
			return _showHidden
				? new Rectangle(card.Right - s - 4, card.Y + 4, s, s)
				: new Rectangle(card.X + (DieWithASmileSettings.ShuffleScenes ? 26 : 4), card.Y + 4, s, s);
		}

		private static Rectangle CheckHit(Rectangle card)
		{
			Rectangle eye = EyeHit(card);
			return new Rectangle(eye.X - 22, eye.Y + 2, 18, 18);
		}

		private static Rectangle[] SceneCards(Rectangle panel, int total)
		{
			const float gap = 8f;
			const int cols = 4;
			return WrapCards(panel, Math.Max(1, total), cols, GalleryHead(), 108f, gap, _galleryScroll);
		}

		private static Rectangle[] WrapCards(Rectangle panel, int total, int cols, float topOffset, float cardH, float gap, float scroll = 0f)
		{
			float width = (panel.Width - 40f - gap * (cols - 1)) / cols;
			float top = panel.Y + topOffset - scroll;
			var cards = new Rectangle[total];
			for (int i = 0; i < total; i++) {
				int row = i / cols;
				int col = i % cols;
				int rowCount = Math.Min(cols, total - row * cols);
				float rowPad = (cols - rowCount) * (width + gap) * 0.5f;
				cards[i] = new Rectangle(
					(int)(panel.X + 20 + rowPad + col * (width + gap)),
					(int)(top + row * (cardH + gap)),
					(int)width,
					(int)cardH);
			}

			return cards;
		}

		internal static Rectangle GalleryHit() => SideButtonHit(0);

		internal static Rectangle LogoHit() => SideButtonHit(1);

		internal static Rectangle EditHit() => SideButtonHit(2);

		private static Rectangle SideButtonHit(int index)
		{
			int size = 36;
			int pad = 16;
			int gap = 10;
			int y = Main.screenHeight - pad - size;
			return new Rectangle(pad + index * (size + gap), y, size, size);
		}

		private static Rectangle LogoRect()
		{
			int content = LogoContentHeight();
			int height = Math.Clamp(52 + content + 36, 280, Main.screenHeight - 80);
			return Centered(Math.Min(980, Main.screenWidth - 60), height);
		}

		private static Rectangle PlaylistRect() => Centered(620, Math.Min(420, Main.screenHeight - 80));

		private static Rectangle GalleryRect()
		{
			int footer = GalleryFooter();
			int content = GalleryContentHeight();
			int height = Math.Clamp(GalleryHead() + content + footer, 280, Main.screenHeight - 80);
			return Centered(Math.Min(980, Main.screenWidth - 60), height);
		}

		private static int GalleryFooter() => DieWithASmileSettings.ShuffleScenes ? 132 : 64;

		private static int GalleryCardCount() => Math.Max(1, GalleryItems().Count);

		private static int LogoCardCount() =>
			6 + _foreignLogos.Count + CalamitasMenuUserArt.Logos.Count + 1;

		private static int GalleryContentHeight()
		{
			int rows = CardRows(GalleryCardCount(), 4);
			return (int)(rows * 116f - 8f);
		}

		private static int LogoContentHeight()
		{
			int rows = CardRows(LogoCardCount(), 5);
			return (int)(rows * 136f - 8f);
		}

		private static int GalleryHead() => CalamitasMenuSkyCover.HasHint ? 58 : 44;

		private static Rectangle GalleryView(Rectangle panel) =>
			new(panel.X + 8, panel.Y + GalleryHead(), panel.Width - 16, Math.Max(40, panel.Height - GalleryHead() - GalleryFooter()));

		private static Rectangle LogoView(Rectangle panel) =>
			new(panel.X + 8, panel.Y + 44, panel.Width - 16, Math.Max(40, panel.Height - 80));

		private static float GalleryMaxScroll(Rectangle view) =>
			Math.Max(0f, GalleryContentHeight() - view.Height);

		private static float LogoMaxScroll(Rectangle view) =>
			Math.Max(0f, LogoContentHeight() - view.Height);

		private static void ApplyOverlayWheel(ref float scroll, float max)
		{
			int wheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			float wheel = (wheelValue - _lastWheel) / 120f;
			_lastWheel = wheelValue;
			scroll = MathHelper.Clamp(scroll - wheel * 48f, 0f, max);
		}

		private static bool CardInView(Rectangle card, Rectangle view) => card.Intersects(view);

		private static bool ClickedVisible(Rectangle card, Rectangle view) =>
			view.Contains(Main.mouseX, Main.mouseY) && CardInView(card, view) && Clicked(card);

		private static void DrawOverlayScroll(SpriteBatch spriteBatch, Rectangle view, float scroll, float max, float fade)
		{
			if (max <= 1f)
				return;

			int trackW = 4;
			var track = new Rectangle(view.Right - 10, view.Y + 4, trackW, view.Height - 8);
			float thumbH = MathHelper.Clamp(view.Height * view.Height / (view.Height + max), 28f, track.Height);
			float t = scroll / max;
			int thumbY = track.Y + (int)((track.Height - thumbH) * t);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, track, Color.White * (0.08f * fade));
			spriteBatch.Draw(pixel, new Rectangle(track.X, thumbY, track.Width, (int)thumbH), Neon * (0.85f * fade));
		}

		private static int CardRows(int total, int cols) => Math.Max(1, (total + cols - 1) / cols);

		private static Rectangle HideHit(Rectangle panel) =>
			new(panel.X + 24, panel.Bottom - 48, 210, 28);

		private static Rectangle HiddenHit(Rectangle panel) =>
			new(panel.X + 240, panel.Bottom - 48, 260, 28);

		private static Rectangle IntervalHit(Rectangle panel)
		{
			Rectangle bar = IntervalBar(panel);
			return new Rectangle(bar.X - 8, bar.Y - 12, bar.Width + 16, bar.Height + 24);
		}

		private static Rectangle IntervalBar(Rectangle panel) =>
			new(panel.X + 24, panel.Bottom - 88, Math.Max(80, panel.Width - 200), 8);

		private static Rectangle Centered(int width, int height) =>
			new((Main.screenWidth - width) / 2, (Main.screenHeight - height) / 2, width, height);

		private static Rectangle CloseHit(Rectangle panel) => new(panel.Right - 34, panel.Y + 10, 22, 22);

		private static void CoverOverlayChrome(SpriteBatch spriteBatch, Rectangle panel, Rectangle view, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Color fill = Panel * fade;
			int head = Math.Max(0, view.Y - panel.Y);
			int foot = Math.Max(0, panel.Bottom - view.Bottom);
			if (head > 0)
				spriteBatch.Draw(pixel, new Rectangle(panel.X, panel.Y, panel.Width, head), fill);
			if (foot > 0)
				spriteBatch.Draw(pixel, new Rectangle(panel.X, view.Bottom, panel.Width, foot), fill);
			DrawBorder(spriteBatch, panel, Neon * fade, 2);
		}

		private static bool UpdateAccentClicks(Rectangle panel)
		{
			Rectangle[] hits = AccentHits(panel);
			for (int i = 0; i < hits.Length; i++) {
				if (!Clicked(hits[i]))
					continue;
				SoundEngine.PlaySound(SoundID.MenuTick);
				CalamitasMenuAccent.Set(i);
				return true;
			}

			return false;
		}

		private static void DrawAccentSwatches(SpriteBatch spriteBatch, Rectangle panel, float fade)
		{
			Rectangle[] hits = AccentHits(panel);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			for (int i = 0; i < hits.Length; i++) {
				Rectangle hit = hits[i];
				bool selected = CalamitasMenuAccent.Index == i;
				bool hover = hit.Contains(Main.mouseX, Main.mouseY);
				AccentSwatch swatch = CalamitasMenuAccent.Palettes[i];
				spriteBatch.Draw(pixel, hit, swatch.Mid * fade);
				Color border = selected ? Color.White * fade : hover ? Color.White * (0.8f * fade) : swatch.Dark * fade;
				DrawBorder(spriteBatch, hit, border, selected ? 3 : 1);
				if (hover)
					_tooltip = L(swatch.Key);
			}
		}

		private static Rectangle[] AccentHits(Rectangle panel)
		{
			int count = CalamitasMenuAccent.Palettes.Length;
			int size = 16;
			int gap = 5;
			int total = count * size + (count - 1) * gap;
			int x = panel.Right - 18 - total;
			int y = panel.Bottom - 28;
			var hits = new Rectangle[count];
			for (int i = 0; i < count; i++)
				hits[i] = new Rectangle(x + i * (size + gap), y, size, size);
			return hits;
		}

		private static void DrawPanel(SpriteBatch spriteBatch, Rectangle panel, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(panel.X - 4, panel.Y - 4, panel.Width + 8, panel.Height + 8), Neon * (0.2f * fade));
			spriteBatch.Draw(pixel, panel, Panel * fade);
			DrawBorder(spriteBatch, panel, Neon * fade, 2);
		}

		private static void DrawDim(SpriteBatch spriteBatch, float fade)
		{
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * fade);
		}

		private static void DrawClose(SpriteBatch spriteBatch, Rectangle hit, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Color color = hit.Contains(Main.mouseX, Main.mouseY) ? Color.White * fade : TextSub * fade;
			for (int i = 0; i < 12; i++) {
				spriteBatch.Draw(pixel, new Rectangle(hit.X + 5 + i, hit.Y + 5 + i, 2, 2), color);
				spriteBatch.Draw(pixel, new Rectangle(hit.X + 15 - i, hit.Y + 5 + i, 2, 2), color);
			}
		}

		private static void DrawToggle(SpriteBatch spriteBatch, Rectangle hit, bool on, float fade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, hit, (on ? Neon : Color.White) * ((on ? 0.55f : 0.08f) * fade));
			DrawBorder(spriteBatch, hit, Neon * fade, 1);
			if (on) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					FontAssets.MouseText.Value,
					"+",
					new Vector2(hit.X + 5, hit.Y + 1),
					TextMain * fade,
					0f,
					Vector2.Zero,
					new Vector2(0.8f));
			}
		}

		private static void DrawTextButton(SpriteBatch spriteBatch, Rectangle hit, string text, float fade, bool danger = false)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			spriteBatch.Draw(pixel, hit, (danger ? CalamitasMenuAccent.Deep : Neon) * ((hover ? 0.55f : 0.28f) * fade));
			DrawBorder(spriteBatch, hit, Neon * fade, 1);
			Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.68f;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				FontAssets.MouseText.Value,
				text,
				new Vector2(hit.X + (hit.Width - size.X) * 0.5f, hit.Y + 4),
				TextMain * fade,
				0f,
				Vector2.Zero,
				new Vector2(0.68f));
		}

		private static void DrawRound(SpriteBatch spriteBatch, Vector2 center, float radius, float fade, bool hover)
		{
			CalamitasMenuPlayerUI.DrawRoundButtonPublic(spriteBatch, center, radius, fade, hover);
		}

		private static void DrawIcon(SpriteBatch spriteBatch, Texture2D tex, Vector2 center, float size, Color color)
		{
			if (tex == null)
				return;

			spriteBatch.Draw(tex, center, null, color, 0f, tex.Size() * 0.5f, size / tex.Width, SpriteEffects.None, 0f);
		}

		private static void DrawTintedIcon(SpriteBatch spriteBatch, Asset<Texture2D> asset, Vector2 center, float size, bool hover, float fade, bool on = false)
		{
			DrawIcon(
				spriteBatch,
				CalamitasMenuIcons.AsControlIcon(asset),
				center,
				size,
				CalamitasMenuAccent.Glyph(hover, on) * fade);
		}

		private static void DrawCentered(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 center, Color color, float scale)
		{
			Vector2 size = font.MeasureString(text) * scale;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, center - new Vector2(size.X * 0.5f, 0f), color, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private static void DrawTooltip(SpriteBatch spriteBatch, string text)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.72f;
			var box = new Rectangle(Main.mouseX + 16, Main.mouseY + 16, (int)size.X + 12, (int)size.Y + 8);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, new Color(29, 27, 28) * 0.92f);
			DrawBorder(spriteBatch, box, Neon * 0.8f, 1);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, new Vector2(box.X + 6, box.Y + 3), TextMain, 0f, Vector2.Zero, new Vector2(0.72f));
		}

		private static void GetCoverSource(Texture2D tex, Rectangle dest, out Rectangle src) =>
			GetCoverSource(tex, dest, new Vector2(0.5f, 0.5f), out src);

		private static void GetCoverSource(Texture2D tex, Rectangle dest, Vector2 pan, out Rectangle src)
		{
			float px = MathHelper.Clamp(pan.X, 0f, 1f);
			float py = MathHelper.Clamp(pan.Y, 0f, 1f);
			if (tex.Width / (float)tex.Height > dest.Width / (float)dest.Height) {
				int srcH = tex.Height;
				int srcW = Math.Max(1, (int)(tex.Height * (dest.Width / (float)dest.Height)));
				int extra = Math.Max(0, tex.Width - srcW);
				src = new Rectangle((int)(extra * px), 0, srcW, srcH);
			}
			else {
				int srcW = tex.Width;
				int srcH = Math.Max(1, (int)(tex.Width * (dest.Height / (float)dest.Width)));
				int extra = Math.Max(0, tex.Height - srcH);
				src = new Rectangle(0, (int)(extra * py), srcW, srcH);
			}
		}

		private static void CaptureClicks()
		{
			MouseState mouse = Mouse.GetState();
			bool left = mouse.LeftButton == ButtonState.Pressed;
			bool right = mouse.RightButton == ButtonState.Pressed;
			_leftClick = left && !_rawLeft;
			_rightClick = right && !_rawRight;
			_rawLeft = left;
			_rawRight = right;
		}

		private static bool Clicked()
		{
			if (!_leftClick)
				return false;

			_leftClick = false;
			Main.mouseLeftRelease = false;
			return true;
		}

		private static bool Clicked(Rectangle hit) => hit.Contains(Main.mouseX, Main.mouseY) && Clicked();

		private static bool RightClicked()
		{
			if (!_rightClick)
				return false;

			_rightClick = false;
			Main.mouseRightRelease = false;
			return true;
		}
	}
}

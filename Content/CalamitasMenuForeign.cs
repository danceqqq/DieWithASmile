using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuForeign
	{
		private enum DrawMode
		{
			Off,
			Logo,
			Background
		}

		private enum DrawAction
		{
			Skip,
			Keep,
			Remap
		}

		private static FieldInfo _menus;
		private static FieldInfo _currentMenu;
		private static readonly Dictionary<string, Texture2D> _bgPreview = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, Texture2D> _stylePreview = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, Texture2D> _modIcon = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, bool> _blitLogo = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, bool> _wallpaper = new(StringComparer.Ordinal);
		private static readonly Dictionary<int, bool> _emptyTex = new();
		private static Texture2D _tmlLogo;
		private static Texture2D _vanillaLogo;
		private static ModMenu _tmlMenu;
		private static ModSurfaceBackgroundStyle[] _styleSnap = Array.Empty<ModSurfaceBackgroundStyle>();
		private static GlobalBackgroundStyle[] _globalSnap = Array.Empty<GlobalBackgroundStyle>();
		private static bool _hooksReady;

		private static DrawMode _mode;
		private static bool _insideDraw;
		private static Texture2D _logoTex;
		private static Vector2 _vanillaCenter;
		private static Vector2 _ourAnchor;
		private static Vector2 _liveCenter;
		private static Vector2 _hover;
		private static float _liveScale = 1f;
		private static float _liveRot;
		private static float _intendedScale = 1f;
		private static float _fade = 1f;
		private static Color _liveColor = Color.White;
		private static bool _sawLogoDraw;
		private static bool _sawCover;
		private static float _passScaleX = 1f;
		private static float _passScaleY = 1f;
		private static float _passShiftX;
		private static float _passShiftY;
		private static bool _gardenSkipRenderer;
		private static Type _shaderManager;
		private static MethodInfo _tryGetShader;
		private static IDictionary _shaderDict;
		private static int _swapDepth;
		private static ModMenu _savedCurrent;

		internal static bool HoldingCurrent => _swapDepth > 0;

		internal static void Load()
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			_menus = typeof(MenuLoader).GetField("menus", flags);
			_currentMenu = typeof(MenuLoader).GetField("currentMenu", flags);
			InstallDrawHooks();
			On_Main.DoUpdate += DoUpdateHook;
			On_Main.DoDraw += DoDrawHook;
			MethodInfo inner = typeof(MenuLoader).GetMethod("UpdateAndDrawModMenuInner", flags);
			if (inner != null)
				MonoModHooks.Add(inner, RestoreBeforeOurMenuDraw);
		}

		private static void RestoreBeforeOurMenuDraw(
			Action<SpriteBatch, GameTime, Color, float, float> orig,
			SpriteBatch spriteBatch,
			GameTime gameTime,
			Color color,
			float logoRotation,
			float logoScale)
		{
			PopAll();
			orig(spriteBatch, gameTime, color, logoRotation, logoScale);
		}

		internal static void Unload()
		{
			PopAll();
			_mode = DrawMode.Off;
			Refresh();
			_menus = null;
			_currentMenu = null;
			_tmlLogo = null;
			_vanillaLogo = null;
			_tmlMenu = null;
			_logoTex = null;
			_savedCurrent = null;
			_styleSnap = Array.Empty<ModSurfaceBackgroundStyle>();
			_globalSnap = Array.Empty<GlobalBackgroundStyle>();
		}

		internal static ModMenu TmlMenu
		{
			get
			{
				if (_tmlMenu != null)
					return _tmlMenu;

				object raw = _menus?.GetValue(null);
				if (raw is IEnumerable<ModMenu> menus)
					EnsureDefaults(menus);

				return _tmlMenu;
			}
		}

		internal static Texture2D TmlLogoPreview
		{
			get
			{
				_ = TmlMenu;
				return _tmlLogo;
			}
		}

		internal static void PushCurrentForWallpaper()
		{
			if (!Main.gameMenu || !CoolerMenuCompat.MenuBackdropActive || _currentMenu == null)
				return;

			ModMenu target = null;
			if (DieWithASmileSettings.UsingForeignWallpaper)
				target = Find(DieWithASmileSave.Data.ForeignWallpaperId);
			else if (DieWithASmileSettings.UsingTmlWallpaper)
				target = TmlMenu;

			if (target == null)
				return;

			if (_swapDepth++ == 0)
				_savedCurrent = _currentMenu.GetValue(null) as ModMenu;

			_currentMenu.SetValue(null, target);
		}

		internal static void PushMenu(ModMenu menu)
		{
			if (menu == null || _currentMenu == null)
				return;

			if (_swapDepth++ == 0)
				_savedCurrent = _currentMenu.GetValue(null) as ModMenu;

			_currentMenu.SetValue(null, menu);
		}

		internal static void PopCurrentForWallpaper()
		{
			if (_swapDepth <= 0 || _currentMenu == null)
				return;

			_swapDepth--;
			if (_swapDepth == 0) {
				_currentMenu.SetValue(null, _savedCurrent);
				_savedCurrent = null;
			}
		}

		private static void DoUpdateHook(On_Main.orig_DoUpdate orig, Main self, ref GameTime gameTime)
		{
			float volume = Main.musicVolume;
			CalamitasMenuPlaylist.PrepareFrameAudio();
			if (!Main.gameMenu || !CoolerMenuCompat.MenuBackdropActive)
				PopAll();
			else if (!CalamitasMenuConflict.Blocking)
				PushCurrentForWallpaper();
			else
				PopAll();
			try {
				orig(self, ref gameTime);
			}
			finally {
				CalamitasMenuPlaylist.RestoreIfStolen(volume);
				PopAll();
				if (Main.gameMenu && MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu)
					CalamitasMenuPlaylist.AssertTitleMusic();
			}
		}

		private static void DoDrawHook(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
		{
			float volume = Main.musicVolume;
			if (Main.gameMenu && CoolerMenuCompat.MenuBackdropActive && !CalamitasMenuConflict.Blocking)
				PushCurrentForWallpaper();
			try {
				orig(self, gameTime);
			}
			finally {
				CalamitasMenuPlaylist.RestoreIfStolen(volume);
				PopAll();
				if (Main.gameMenu && MenuLoader.CurrentMenu is DieWithASmileCalamitasMenu)
					CalamitasMenuPlaylist.AssertTitleMusic();
			}
		}

		internal static void PopAll()
		{
			while (_swapDepth > 0)
				PopCurrentForWallpaper();
		}

		internal static void BeginFrame()
		{
			_mode = DrawMode.Off;
			_sawLogoDraw = false;
			_insideDraw = false;
		}

		internal static void Refresh()
		{
			_bgPreview.Clear();
			_stylePreview.Clear();
			_modIcon.Clear();
			_blitLogo.Clear();
			_wallpaper.Clear();
			_emptyTex.Clear();
		}

		internal static Texture2D IconOf(Mod mod)
		{
			if (mod == null)
				return null;

			if (_modIcon.TryGetValue(mod.Name, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			Texture2D tex = LoadModIcon(mod);
			if (tex != null)
				_modIcon[mod.Name] = tex;
			return tex;
		}

		internal static Texture2D IconOf(ModMenu menu) => IconOf(menu?.Mod);

		internal static bool NeedsManualOrphan(ModSurfaceBackgroundStyle style)
		{
			if (style == null || IsEternalGardenStyle(style))
				return false;

			try {
				float scale = 1f;
				double parallax = 0d;
				float a = 0f;
				float b = 0f;
				style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b);
				return b > 10000f;
			}
			catch {
				return false;
			}
		}

		internal static bool IsSkippedStyleKey(string key)
		{
			if (string.IsNullOrEmpty(key))
				return false;
			return key.Contains("AstralDesert", StringComparison.OrdinalIgnoreCase) ||
			       key.Contains("AstralSnow", StringComparison.OrdinalIgnoreCase) ||
			       key.Contains("Astral Desert", StringComparison.OrdinalIgnoreCase) ||
			       key.Contains("Astral Snow", StringComparison.OrdinalIgnoreCase) ||
			       key.Contains("PissSea", StringComparison.OrdinalIgnoreCase) ||
			       key.Contains("Piss Sea", StringComparison.OrdinalIgnoreCase) ||
			       key.Contains("Piss_Sea", StringComparison.OrdinalIgnoreCase);
		}

		internal static bool IsSkippedWallpaperKey(string key)
		{
			if (string.IsNullOrEmpty(key))
				return false;
			if (key.StartsWith("orphan:", StringComparison.Ordinal))
				return IsSkippedStyleKey(key) || IsSkippedStyleKey(key[7..]);
			return IsSkippedStyleKey(key);
		}

		internal static bool IsSkippedStyle(ModSurfaceBackgroundStyle style) =>
			style != null && (IsSkippedStyleKey(style.Name) || IsSkippedStyleKey(StyleLabel(style)));

		internal static bool TryDrawOrphan(SpriteBatch spriteBatch, float fade)
		{
			ModSurfaceBackgroundStyle style = FindStyle(DieWithASmileSave.Data.OrphanStyleKey);
			if (style == null || IsSkippedStyle(style))
				return false;

			if (NeedsManualOrphan(style)) {
				DieWithASmileSettings.AbandonBrokenWallpaper();
				return false;
			}

			try {
				bool drew = IsEternalGardenStyle(style)
					? TryDrawEternalGardenSafe(spriteBatch, style)
					: DrawStyle(spriteBatch, style);
				if (!drew)
					return false;
			}
			catch {
				DieWithASmileSettings.AbandonBrokenWallpaper();
				return false;
			}

			RestoreBatch(spriteBatch, Matrix.Identity);
			if (fade < 0.999f)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, CalamitasMenuDraw.CoverRect, Color.Black * (1f - fade));
			return true;
		}

		internal static void SnapshotContent()
		{
			try {
				var styles = new List<ModSurfaceBackgroundStyle>();
				foreach (ModSurfaceBackgroundStyle style in ModContent.GetContent<ModSurfaceBackgroundStyle>())
					styles.Add(style);
				_styleSnap = styles.ToArray();
			}
			catch {
			}

			try {
				var globals = new List<GlobalBackgroundStyle>();
				foreach (GlobalBackgroundStyle global in ModContent.GetContent<GlobalBackgroundStyle>())
					globals.Add(global);
				_globalSnap = globals.ToArray();
			}
			catch {
			}
		}

		internal static ModSurfaceBackgroundStyle StyleBySlot(int slot)
		{
			for (int i = 0; i < _styleSnap.Length; i++) {
				ModSurfaceBackgroundStyle style = _styleSnap[i];
				if (style != null && style.Slot == slot)
					return style;
			}

			return null;
		}

		internal static void DropMissing()
		{
			if (!string.IsNullOrEmpty(DieWithASmileSave.Data.ForeignLogoId) && !CanUseForeignLogo(Find(DieWithASmileSave.Data.ForeignLogoId)))
				DieWithASmileSave.Data.ForeignLogoId = "";
			if (!string.IsNullOrEmpty(DieWithASmileSave.Data.ForeignWallpaperId) && !HasWallpaper(Find(DieWithASmileSave.Data.ForeignWallpaperId)))
				DieWithASmileSave.Data.ForeignWallpaperId = "";
			if (!string.IsNullOrEmpty(DieWithASmileSave.Data.OrphanStyleKey)) {
				ModSurfaceBackgroundStyle orphan = FindStyle(DieWithASmileSave.Data.OrphanStyleKey);
				if (orphan == null || NeedsManualOrphan(orphan) || IsSkippedStyle(orphan))
					DieWithASmileSave.Data.OrphanStyleKey = "";
			}
		}

		internal static string StyleKey(ModSurfaceBackgroundStyle style) =>
			style == null ? "" : (style.Mod?.Name ?? "") + "/" + style.Name;

		internal static ModSurfaceBackgroundStyle FindStyle(string key)
		{
			if (string.IsNullOrEmpty(key))
				return null;

			for (int i = 0; i < _styleSnap.Length; i++) {
				ModSurfaceBackgroundStyle style = _styleSnap[i];
				if (style is CalamitasMenuBackgroundStyle || style == null)
					continue;
				if (StyleKey(style) == key)
					return style;
			}

			SnapshotContent();
			for (int i = 0; i < _styleSnap.Length; i++) {
				ModSurfaceBackgroundStyle style = _styleSnap[i];
				if (style is CalamitasMenuBackgroundStyle || style == null)
					continue;
				if (StyleKey(style) == key)
					return style;
			}

			return null;
		}

		internal static Texture2D PreviewStyle(ModSurfaceBackgroundStyle style) => StyleArt(style);

		internal static Texture2D GalleryThumb(ModSurfaceBackgroundStyle style, out bool cover)
		{
			cover = false;
			if (style == null)
				return null;

			Texture2D art = StyleArt(style);
			if (art != null) {
				cover = PreferCover(art);
				return art;
			}

			return IconOf(style.Mod);
		}

		internal static Texture2D GalleryThumb(ModMenu menu, out bool cover)
		{
			cover = false;
			if (menu == null)
				return null;

			Texture2D logo = UniqueLogo(menu);
			Texture2D style = StyleArt(menu.MenuBackgroundStyle);
			Texture2D wotg = WotGMenuArt(menu);

			if (logo != null && !IsSceneTexture(logo)) {
				cover = false;
				return logo;
			}

			if (style != null && PreferCover(style)) {
				cover = true;
				return style;
			}

			if (wotg != null) {
				cover = PreferCover(wotg);
				return wotg;
			}

			if (logo != null) {
				cover = PreferCover(logo);
				return logo;
			}

			if (style != null) {
				cover = PreferCover(style);
				return style;
			}

			return IconOf(menu);
		}

		internal static string StyleLabel(ModSurfaceBackgroundStyle style)
		{
			if (style == null)
				return "";

			string modName = style.Mod?.DisplayName ?? style.Mod?.Name ?? "Mod";
			string raw = style.Name ?? "";
			raw = raw.Replace("BackgroundStyle", "", StringComparison.Ordinal)
				.Replace("BGStyle", "", StringComparison.Ordinal)
				.Replace("Surface", "", StringComparison.Ordinal)
				.Trim();
			if (string.IsNullOrEmpty(raw) || string.Equals(raw, modName, StringComparison.OrdinalIgnoreCase))
				return modName;

			return modName + " · " + raw;
		}

		internal static IReadOnlyList<ModSurfaceBackgroundStyle> ReplacementStyles()
		{
			SnapshotContent();
			var usedByMenus = new HashSet<int>();
			foreach (ModMenu menu in All()) {
				ModSurfaceBackgroundStyle menuStyle = menu.MenuBackgroundStyle;
				if (menuStyle != null)
					usedByMenus.Add(menuStyle.Slot);
			}

			var result = new List<ModSurfaceBackgroundStyle>();
			var seen = new HashSet<int>();
			for (int i = 0; i < _styleSnap.Length; i++) {
				ModSurfaceBackgroundStyle style = _styleSnap[i];
				if (style is CalamitasMenuBackgroundStyle || style == null || usedByMenus.Contains(style.Slot))
					continue;

				try {
					if (IsSkippedStyle(style) || NeedsManualOrphan(style) || !HasDrawableStyle(style))
						continue;
				}
				catch {
					continue;
				}

				result.Add(style);
				seen.Add(style.Slot);
			}

			ProbeTmlGlobals(result, seen, usedByMenus);
			return result;
		}

		private static void ProbeTmlGlobals(
			List<ModSurfaceBackgroundStyle> result,
			HashSet<int> seen,
			HashSet<int> usedByMenus)
		{
			if (_currentMenu == null || HoldingCurrent)
				return;

			ModMenu tml = TmlMenu;
			if (tml == null)
				return;

			float volume = Main.musicVolume;
			if (_swapDepth++ == 0)
				_savedCurrent = _currentMenu.GetValue(null) as ModMenu;

			_currentMenu.SetValue(null, tml);
			try {
				for (int i = 0; i < _globalSnap.Length; i++) {
					GlobalBackgroundStyle global = _globalSnap[i];
					if (global is CalamitasMenuSkyCover || global == null)
						continue;
					if (!Overrides(global, nameof(GlobalBackgroundStyle.ChooseSurfaceBackgroundStyle)))
						continue;

					int style = SurfaceBackgroundID.Forest1;
					try {
						global.ChooseSurfaceBackgroundStyle(ref style);
					}
					catch {
						continue;
					}

					if (style == SurfaceBackgroundID.Forest1)
						continue;

					ModSurfaceBackgroundStyle found = StyleBySlot(style);
					if (found == null || found is CalamitasMenuBackgroundStyle || IsSkippedStyle(found) || NeedsManualOrphan(found))
						continue;
					if (usedByMenus.Contains(found.Slot) || !seen.Add(found.Slot))
						continue;

					result.Add(found);
				}
			}
			finally {
				PopCurrentForWallpaper();
				CalamitasMenuPlaylist.RestoreIfStolen(volume);
			}
		}

		private static ModSurfaceBackgroundStyle FindStyleBySlot(int slot) => StyleBySlot(slot);

		internal static IReadOnlyList<ModMenu> LogoMenus()
		{
			var list = new List<ModMenu>();
			foreach (ModMenu menu in All()) {
				if (CanUseForeignLogo(menu))
					list.Add(menu);
			}

			return list;
		}

		internal static IReadOnlyList<ModMenu> WallpaperMenus()
		{
			var list = new List<ModMenu>();
			foreach (ModMenu menu in All()) {
				if (HasWallpaper(menu))
					list.Add(menu);
			}

			return list;
		}

		internal static ModMenu Find(string fullName)
		{
			if (string.IsNullOrEmpty(fullName))
				return null;

			foreach (ModMenu menu in All()) {
				if (menu.FullName == fullName)
					return menu;
			}

			return null;
		}

		internal static bool TryGetLogoTexture(out Texture2D texture)
		{
			texture = PreviewLogo(Find(DieWithASmileSave.Data.ForeignLogoId));
			return texture != null;
		}

		internal static Texture2D PreviewLogo(ModMenu menu)
		{
			if (menu == null || !CanUseForeignLogo(menu))
				return null;
			return UniqueLogo(menu) ?? SafeLogo(menu);
		}

		internal static Texture2D PreviewWallpaper(ModMenu menu)
		{
			if (menu == null)
				return null;

			if (_bgPreview.TryGetValue(menu.FullName, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			Texture2D fromStyle = StyleArt(menu.MenuBackgroundStyle);
			if (fromStyle != null) {
				_bgPreview[menu.FullName] = fromStyle;
				return fromStyle;
			}

			Texture2D logo = SafeLogo(menu);
			if (logo != null && !IsBlank(logo))
				return logo;

			return null;
		}

		internal static bool TryDrawWallpaper(SpriteBatch spriteBatch, float fade) =>
			TryDrawWallpaper(spriteBatch, fade, Find(DieWithASmileSave.Data.ForeignWallpaperId));

		internal static bool TryDrawWallpaper(SpriteBatch spriteBatch, float fade, ModMenu menu)
		{
			if (!HasWallpaper(menu))
				return false;

			try {
				float volume = Main.musicVolume;
				PreparePass(menu, DrawMode.Background, new Vector2(Main.screenWidth * 0.5f, 100f), 1f, fade);
				bool tookOver = false;
				if (HasLogoHooks(menu))
					tookOver = RunLogoHooks(spriteBatch, menu);

				EndPass();
				if (!tookOver || !_sawCover)
					DrawStyle(spriteBatch, menu.MenuBackgroundStyle);
				if (!_sawCover) {
					Texture2D preview = PreviewWallpaper(menu);
					if (preview != null && IsSceneTexture(preview)) {
						DrawCover(spriteBatch, preview);
						_sawCover = true;
					}
				}

				RestoreBatch(spriteBatch, Matrix.Identity);
				if (fade < 0.999f)
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, CalamitasMenuDraw.CoverRect, Color.Black * (1f - fade));
				CalamitasMenuPlaylist.RestoreIfStolen(volume);
			}
			catch {
				DieWithASmileSettings.AbandonBrokenWallpaper();
				return false;
			}

			return true;
		}

		internal static bool TryDrawLogo(SpriteBatch spriteBatch, Vector2 center, float userScale, float fade)
		{
			ModMenu menu = Find(DieWithASmileSave.Data.ForeignLogoId);
			if (!CanUseForeignLogo(menu))
				return false;

			Texture2D tex = SafeLogo(menu);
			if (tex == null)
				return false;

			float cap = MathHelper.Min(520f, Main.screenWidth * 0.38f);
			float intended = cap / Math.Max(1, tex.Width) * userScale;
			float volume = Main.musicVolume;
			if (!HasLogoHooks(menu) || PaintsSkyOnly(menu)) {
				_hover = Vector2.Zero;
				spriteBatch.Draw(tex, center, null, Color.White * fade, 0f, tex.Size() * 0.5f, intended, SpriteEffects.None, 0f);
				return true;
			}

			PreparePass(menu, DrawMode.Logo, center, intended, fade);
			bool drewDefault = RunLogoHooks(spriteBatch, menu);
			EndPass();
			CalamitasMenuPlaylist.RestoreIfStolen(volume);
			Vector2 visual = center + _hover;
			if (drewDefault || !_sawLogoDraw)
				spriteBatch.Draw(tex, visual, null, _liveColor, _liveRot, tex.Size() * 0.5f, intended, SpriteEffects.None, 0f);

			RestoreBatch(spriteBatch);
			return true;
		}

		internal static Rectangle LogoHit(Vector2 center, float userScale)
		{
			if (!TryGetLogoTexture(out Texture2D tex))
				return Rectangle.Empty;

			float cap = MathHelper.Min(520f, Main.screenWidth * 0.38f);
			float scale = cap / tex.Width * userScale;
			Vector2 visual = center + _hover;
			Vector2 size = tex.Size() * scale * 0.82f;
			return new Rectangle(
				(int)(visual.X - size.X * 0.5f),
				(int)(visual.Y - size.Y * 0.5f),
				(int)size.X,
				(int)size.Y);
		}

		private static void PreparePass(ModMenu menu, DrawMode mode, Vector2 ourAnchor, float intendedScale, float fade)
		{
			_mode = mode;
			_logoTex = SafeLogo(menu);
			_vanillaCenter = new Vector2(Main.screenWidth * 0.5f, 100f);
			_ourAnchor = ourAnchor;
			_liveCenter = _vanillaCenter;
			_liveScale = 1f;
			_liveRot = 0f;
			_liveColor = Color.White * fade;
			_intendedScale = Math.Max(0.05f, intendedScale);
			_fade = fade;
			_sawLogoDraw = false;
			_sawCover = false;
			_hover = Vector2.Zero;
			_passScaleX = 1f;
			_passScaleY = 1f;
			_passShiftX = 0f;
			_passShiftY = 0f;
		}

		private static void EndPass()
		{
			_hover = _liveCenter - _vanillaCenter;
			_mode = DrawMode.Off;
			_insideDraw = false;
		}

		private static bool RunLogoHooks(SpriteBatch spriteBatch, ModMenu menu)
		{
			float volume = Main.musicVolume;
			bool drawDefault = true;
			try {
				drawDefault = menu.PreDrawLogo(spriteBatch, ref _liveCenter, ref _liveRot, ref _liveScale, ref _liveColor);
			}
			catch {
			}

			try {
				menu.PostDrawLogo(spriteBatch, _liveCenter, _liveRot, _liveScale, _liveColor);
			}
			catch {
			}

			CalamitasMenuPlaylist.RestoreIfStolen(volume);
			return drawDefault;
		}

		private static IEnumerable<ModMenu> All()
		{
			object raw = _menus?.GetValue(null);
			if (raw is not IEnumerable<ModMenu> menus)
				yield break;

			EnsureDefaults(menus);
			foreach (ModMenu menu in menus) {
				if (menu == null)
					continue;
				if (menu is DieWithASmileCalamitasMenu)
					continue;
				Type type = menu.GetType();
				if (type.Namespace != null && type.Namespace.StartsWith("Terraria.ModLoader", StringComparison.Ordinal))
					continue;

				yield return menu;
			}
		}

		private static void EnsureDefaults(IEnumerable<ModMenu> menus)
		{
			if (_vanillaLogo == null)
				_vanillaLogo = TextureAssets.Logo?.Value;

			if (_tmlMenu != null)
				return;

			foreach (ModMenu menu in menus) {
				Type type = menu.GetType();
				if (type.Namespace == null || !type.Namespace.StartsWith("Terraria.ModLoader", StringComparison.Ordinal))
					continue;

				if (type.Name == "MenutML")
					_tmlMenu = menu;

				_tmlLogo ??= SafeLogo(menu);
			}

			if (_tmlMenu != null)
				return;

			foreach (ModMenu menu in menus) {
				Type type = menu.GetType();
				if (type.Namespace != null && type.Namespace.StartsWith("Terraria.ModLoader", StringComparison.Ordinal)) {
					_tmlMenu = menu;
					_tmlLogo ??= SafeLogo(menu);
					return;
				}
			}
		}

		internal static bool HasBlitLogo(ModMenu menu)
		{
			if (menu == null)
				return false;

			if (_blitLogo.TryGetValue(menu.FullName, out bool known))
				return known;

			Texture2D tex = SafeLogo(menu);
			if (tex == null || tex.IsDisposed)
				return false;

			bool has = IsBlitLogo(menu, tex);
			_blitLogo[menu.FullName] = has;
			return has;
		}

		private static bool CanUseForeignLogo(ModMenu menu)
		{
			if (HasBlitLogo(menu))
				return true;
			Texture2D unique = UniqueLogo(menu);
			return unique != null && !IsSceneTexture(unique);
		}

		internal static bool HasWallpaper(ModMenu menu)
		{
			if (menu == null)
				return false;

			if (_wallpaper.TryGetValue(menu.FullName, out bool known))
				return known;

			bool has = HasDrawableStyle(menu.MenuBackgroundStyle) ||
			           IsSceneTexture(SafeLogo(menu)) ||
			           SubstantialHook(menu, nameof(ModMenu.PreDrawLogo)) ||
			           SubstantialHook(menu, nameof(ModMenu.PostDrawLogo)) ||
			           Overrides(menu, nameof(ModMenu.IsAvailable));
			_wallpaper[menu.FullName] = has;
			return has;
		}

		private static bool IsBlitLogo(ModMenu menu, Texture2D tex)
		{
			if (!Overrides(menu, nameof(ModMenu.Logo)))
				return false;
			if (tex == null || tex.IsDisposed)
				return false;
			if (tex == _tmlLogo || tex == _vanillaLogo)
				return false;
			if (LooksPlaceholder(menu.Logo?.Name, tex))
				return false;
			if (IsSceneTexture(tex) && !HasLogoHooks(menu) && !Overrides(menu, nameof(ModMenu.IsAvailable)))
				return false;

			return true;
		}

		private static bool HasLogoHooks(ModMenu menu) =>
			Overrides(menu, nameof(ModMenu.PreDrawLogo)) || Overrides(menu, nameof(ModMenu.PostDrawLogo));

		private static bool PaintsSkyOnly(ModMenu menu)
		{
			if (!HasWallpaper(menu) || !HasLogoHooks(menu))
				return false;

			MethodInfo method = menu.GetType().GetMethod(nameof(ModMenu.PreDrawLogo), BindingFlags.Public | BindingFlags.Instance);
			int length = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
			return length > 0 && length < 220;
		}

		private static Texture2D LoadModIcon(Mod mod)
		{
			Texture2D placeholder = PlaceholderIcon(mod);
			try {
				foreach (string name in new[] { "ModIcon", "SmallModIcon", "Icon" }) {
					PropertyInfo prop = typeof(Mod).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
					if (prop?.GetValue(mod) is not Asset<Texture2D> fromProp)
						continue;
					Texture2D value = fromProp.Value;
					if (value == null || value.IsDisposed || value == placeholder || IsBlank(value))
						continue;
					return value;
				}
			}
			catch {
			}

			try {
				Asset<Texture2D> fromFile = mod.Assets?.Request<Texture2D>("icon", AssetRequestMode.ImmediateLoad);
				Texture2D tex = fromFile?.Value;
				if (tex != null && !tex.IsDisposed && tex != placeholder && !IsBlank(tex))
					return tex;
			}
			catch {
			}

			return null;
		}

		private static Texture2D PlaceholderIcon(Mod mod)
		{
			try {
				PropertyInfo prop = typeof(Mod).GetProperty("PlaceholderModIcon", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
				if (prop?.GetValue(mod) is Asset<Texture2D> asset)
					return asset.Value;
			}
			catch {
			}

			return null;
		}

		private static bool IsEternalGardenStyle(ModSurfaceBackgroundStyle style) =>
			style?.Mod?.Name == "NoxusBoss" &&
			style.Name != null &&
			style.Name.Contains("EternalGarden", StringComparison.OrdinalIgnoreCase);

		private static bool TryDrawEternalGardenSafe(SpriteBatch spriteBatch, ModSurfaceBackgroundStyle style)
		{
			Mod mod = style?.Mod;
			if (mod == null)
				return false;

			if (!_gardenSkipRenderer) {
				try {
					if (GardenEffectsReady(mod) && TryDrawEternalGardenSky(mod))
						return true;
				}
				catch {
					_gardenSkipRenderer = true;
					RestoreBatch(spriteBatch, Matrix.Identity);
				}
			}

			try {
				return DrawEternalGardenLayers(spriteBatch, mod);
			}
			catch {
				return false;
			}
		}

		private static bool TryDrawEternalGardenSky(Mod mod)
		{
			if (!HasGardenShaders())
				return false;

			Type sky = mod?.Code?.GetType("NoxusBoss.Core.World.Subworlds.EternalGardenSky");
			MethodInfo method = sky?.GetMethod("RenderAtCameraPosition", BindingFlags.Public | BindingFlags.Static);
			if (method == null)
				return false;

			Point cover = CalamitasMenuDraw.CoverSize;
			method.Invoke(null, new object[] {
				1,
				Vector2.UnitY * -1200f,
				new Vector2(cover.X, cover.Y),
				-1f,
				1f,
				1f
			});
			return true;
		}

		private static bool GardenEffectsReady(Mod mod)
		{
			if (_gardenSkipRenderer || !FindShaderManager())
				return false;

			if (!ShadersFinishedLoading())
				return false;

			if (!HasGardenShaders()) {
				if (_tryGetShader != null || _shaderDict != null)
					_gardenSkipRenderer = true;
				return false;
			}

			try {
				Type sky = mod?.Code?.GetType("NoxusBoss.Core.World.Subworlds.EternalGardenSky");
				if (sky == null)
					return false;
				if (sky.GetProperty("ReflectionTarget", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) == null)
					return false;
				if (sky.GetProperty("AuroraTarget", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) == null)
					return false;
				if (sky.GetProperty("LakeTarget", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) == null)
					return false;

				Type stars = mod.Code.GetType("NoxusBoss.Core.World.Subworlds.EternalGardenSkyStarRenderer");
				const BindingFlags intern = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
				if (stars?.GetField("StarVertexBuffer", intern)?.GetValue(null) == null)
					return false;
				if (stars.GetField("StarIndexBuffer", intern)?.GetValue(null) == null)
					return false;
			}
			catch {
				return false;
			}

			return true;
		}

		private static bool HasGardenShaders() =>
			HasLuminanceShader("NoxusBoss.EternalGardenStarPrimitiveShader") &&
			HasLuminanceShader("NoxusBoss.AuroraShader") &&
			HasLuminanceShader("NoxusBoss.LakeReflectionShader");

		private static bool FindShaderManager()
		{
			if (_shaderManager == null) {
				try {
					if (ModLoader.TryGetMod("Luminance", out Mod lum))
						_shaderManager = lum.Code?.GetType("Luminance.Core.Graphics.ShaderManager");
				}
				catch {
				}

				if (_shaderManager == null) {
					foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
						try {
							Type type = assembly.GetType("Luminance.Core.Graphics.ShaderManager");
							if (type == null)
								continue;
							_shaderManager = type;
							break;
						}
						catch {
						}
					}
				}
			}

			if (_shaderManager == null)
				return false;

			const BindingFlags stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			if (_tryGetShader == null) {
				foreach (MethodInfo method in _shaderManager.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
					if (method.Name != "TryGetShader")
						continue;
					ParameterInfo[] args = method.GetParameters();
					if (args.Length == 2 && args[0].ParameterType == typeof(string) && args[1].IsOut) {
						_tryGetShader = method;
						break;
					}
				}
			}

			if (_shaderDict == null) {
				try {
					_shaderDict = _shaderManager.GetField("shaders", stat)?.GetValue(null) as IDictionary;
				}
				catch {
				}
			}

			return true;
		}

		private static bool ShadersFinishedLoading()
		{
			try {
				object raw = _shaderManager?.GetProperty("HasFinishedLoading", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
				if (raw is bool done)
					return done;
			}
			catch {
			}

			return true;
		}

		private static bool HasLuminanceShader(string name)
		{
			try {
				if (_tryGetShader != null) {
					object[] args = { name, null };
					return _tryGetShader.Invoke(null, args) is true;
				}

				return _shaderDict != null && _shaderDict.Contains(name);
			}
			catch {
				return false;
			}
		}

		private static bool DrawEternalGardenLayers(SpriteBatch spriteBatch, Mod mod)
		{
			Point cover = CalamitasMenuDraw.CoverSize;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, CalamitasMenuDraw.CoverRect, new Color(12, 29, 48));

			int frame = (int)(Main.GameUpdateCount / 10U) % 4;
			Texture2D forest = GardenSkyFrame(mod, "BackgroundFrameTextures", frame) ?? GardenVanillaFrame(frame);
			Texture2D lake = GardenSkyFrame(mod, "LakeFrameTextures", frame) ??
			                 ModTexture(mod, "Assets/Textures/Skies/EternalGarden/GardenLake" + (frame + 1));

			const float cameraY = -1200f;
			const float parallax = 0.2f;
			int y = (int)(cameraY * parallax * 0.5f);
			Vector2 size = new(cover.X, cover.Y);
			if (forest != null) {
				float layerY = size.Y - y + parallax * 100f;
				for (int i = -2; i <= 2; i++) {
					Vector2 layer = new(size.X * 0.5f + forest.Width * i, layerY);
					spriteBatch.Draw(forest, layer - forest.Size() * 0.5f, Color.White);
				}
			}

			if (lake != null) {
				float lakeY = size.Y + (parallax * 100f - y);
				for (int i = -2; i <= 2; i++) {
					Vector2 layer = new(size.X * 0.5f + lake.Width * i, lakeY);
					spriteBatch.Draw(lake, layer - lake.Size() * 0.5f, Color.SkyBlue);
				}
			}

			return forest != null || lake != null;
		}

		private static Texture2D GardenSkyFrame(Mod mod, string property, int frame)
		{
			try {
				Type sky = mod?.Code?.GetType("NoxusBoss.Core.World.Subworlds.EternalGardenSky");
				object raw = sky?.GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
				if (raw is not Array arr || arr.Length == 0)
					return null;

				object lazy = arr.GetValue(Math.Clamp(frame, 0, arr.Length - 1));
				if (lazy == null)
					return null;

				if (lazy is Texture2D direct && !direct.IsDisposed)
					return direct;

				PropertyInfo value = lazy.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
				if (value?.GetValue(lazy) is Texture2D tex && !tex.IsDisposed)
					return tex;
			}
			catch {
			}

			return null;
		}

		private static Texture2D GardenVanillaFrame(int frame)
		{
			int id = 251 + Math.Clamp(frame, 0, 3);
			try {
				Main.instance.LoadBackground(id);
			}
			catch {
			}

			try {
				Texture2D tex = TextureAssets.Background?[id]?.Value;
				if (tex != null && !tex.IsDisposed && !IsBlank(tex))
					return tex;
			}
			catch {
			}

			return null;
		}

		private static Texture2D UniqueLogo(ModMenu menu)
		{
			if (menu == null || !Overrides(menu, nameof(ModMenu.Logo)))
				return null;

			Texture2D tex = SafeLogo(menu);
			if (tex == null || tex == _tmlLogo || tex == _vanillaLogo || IsBlank(tex))
				return null;
			if (LooksPlaceholder(menu.Logo?.Name, tex))
				return null;
			return tex;
		}

		private static Texture2D StyleArt(ModSurfaceBackgroundStyle style)
		{
			if (style == null)
				return null;

			string key = StyleKey(style);
			if (_stylePreview.TryGetValue(key, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			Texture2D art = IsEternalGardenStyle(style) ? GardenThumb(style.Mod) : StyleSlotArt(style);
			if (art != null)
				_stylePreview[key] = art;
			return art;
		}

		private static Texture2D StyleSlotArt(ModSurfaceBackgroundStyle style)
		{
			Texture2D fromField = FirstStyleTexture(style);
			if (fromField != null)
				return fromField;

			try {
				Texture2D far = TextureOfSlot(style.ChooseFarTexture());
				if (far != null)
					return far;
				Texture2D mid = TextureOfSlot(style.ChooseMiddleTexture());
				if (mid != null)
					return mid;

				float scale = 1f;
				double parallax = 0d;
				float a = 0f;
				float b = 0f;
				int close = style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b);
				if (b <= 10000f)
					return TextureOfSlot(close);
			}
			catch {
			}

			return null;
		}

		private static Texture2D GardenThumb(Mod mod) =>
			ModTexture(mod, "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Garden") ??
			ModTexture(mod, "Assets/Textures/BiomeIcons/EternalGardenBiome") ??
			ModTexture(mod, "Assets/Textures/Map/EternalGardenBG") ??
			ModTexture(mod, "Assets/Textures/MainMenuThemes/NamelessDeityLogo");

		private static Texture2D WotGMenuArt(ModMenu menu)
		{
			if (menu?.Mod?.Name != "NoxusBoss")
				return null;

			string type = menu.GetType().Name ?? "";
			string display = menu.DisplayName ?? "";
			string path = type switch {
				"XNamelessDeityDimensionMainMenu" => "Assets/Textures/MainMenuThemes/NamelessDeityLogo",
				"AvatarRiftSkyMainMenu" => "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Rift",
				"AvatarWindMainMenu" => "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Avatar",
				"XAscentMainNenu" => "Assets/Textures/Map/AvatarUniverseExplorationMapBackground",
				_ => null
			};

			if (path == null) {
				if (NameHas(display, "Paradise", "Nameless"))
					path = "Assets/Textures/MainMenuThemes/NamelessDeityLogo";
				else if (NameHas(display, "Carmine", "Insouciant", "Rift"))
					path = "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Rift";
				else if (NameHas(display, "Turbulent", "Expanse", "Emptiness"))
					path = "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Avatar";
				else if (NameHas(display, "Ascent", "Terminus", "Stair"))
					path = "Assets/Textures/Map/AvatarUniverseExplorationMapBackground";
			}

			return ModTexture(menu.Mod, path);
		}

		private static bool NameHas(string hay, params string[] needles)
		{
			if (string.IsNullOrEmpty(hay))
				return false;
			foreach (string needle in needles) {
				if (hay.Contains(needle, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private static bool PreferCover(Texture2D tex) =>
			tex != null && (IsSceneTexture(tex) || (tex.Width >= 400 && tex.Height >= 220));

		private static Texture2D ModTexture(Mod mod, string path)
		{
			if (mod == null || string.IsNullOrEmpty(path))
				return null;

			string trimmed = path.Replace('\\', '/').TrimStart('/');
			string prefix = mod.Name + "/";
			if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				trimmed = trimmed[prefix.Length..];

			try {
				Asset<Texture2D> asset = mod.Assets?.Request<Texture2D>(trimmed, AssetRequestMode.ImmediateLoad);
				Texture2D tex = asset?.Value;
				if (tex != null && !tex.IsDisposed)
					return tex;
			}
			catch {
			}

			try {
				Asset<Texture2D> full = ModContent.Request<Texture2D>(mod.Name + "/" + trimmed, AssetRequestMode.ImmediateLoad);
				Texture2D tex = full?.Value;
				if (tex != null && !tex.IsDisposed)
					return tex;
			}
			catch {
			}

			return null;
		}

		private static bool SubstantialHook(object instance, string name)
		{
			if (!Overrides(instance, name))
				return false;

			MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
			int length = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
			return length > 64;
		}

		private static bool HasDrawableStyle(ModSurfaceBackgroundStyle style)
		{
			if (style == null)
				return false;
			if (IsEternalGardenStyle(style))
				return true;
			if (FirstStyleTexture(style) != null)
				return true;

			try {
				if (RealSlot(style.ChooseFarTexture()))
					return true;
				if (RealSlot(style.ChooseMiddleTexture()))
					return true;

				float scale = 1f;
				double parallax = 0d;
				float a = 0f;
				float b = 0f;
				if (RealSlot(style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b)))
					return true;
			}
			catch {
			}

			return Overrides(style, nameof(ModSurfaceBackgroundStyle.PreDrawCloseBackground)) &&
			       !LooksLikeNullBackground(style);
		}

		private static bool LooksLikeNullBackground(ModSurfaceBackgroundStyle style)
		{
			try {
				float scale = 1f;
				double parallax = 0d;
				float a = 0f;
				float b = 0f;
				return !RealSlot(style.ChooseFarTexture()) &&
				       !RealSlot(style.ChooseMiddleTexture()) &&
				       !RealSlot(style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b));
			}
			catch {
				return false;
			}
		}

		internal static bool DrawStylePreview(SpriteBatch spriteBatch, ModSurfaceBackgroundStyle style) =>
			DrawStyle(spriteBatch, style);

		private static bool DrawStyle(SpriteBatch spriteBatch, ModSurfaceBackgroundStyle style)
		{
			if (IsEternalGardenStyle(style))
				return TryDrawEternalGardenSafe(spriteBatch, style);

			if (style == null || LooksLikeNullBackground(style) || NeedsManualOrphan(style))
				return false;

			bool drew = false;
			try {
				drew |= DrawBackgroundSlot(spriteBatch, style.ChooseFarTexture(), horizon: false);
				drew |= DrawBackgroundSlot(spriteBatch, style.ChooseMiddleTexture(), horizon: false);

				bool wantClose = true;
				if (Overrides(style, nameof(ModSurfaceBackgroundStyle.PreDrawCloseBackground)))
					wantClose = style.PreDrawCloseBackground(spriteBatch);

				if (wantClose) {
					float scale = 1f;
					double parallax = 0d;
					float a = 0f;
					float b = 0f;
					drew |= DrawBackgroundSlot(spriteBatch, style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b), horizon: true);
				}
				else {
					drew = true;
				}

				if (!drew) {
					Texture2D preview = FirstStyleTexture(style);
					if (preview != null) {
						DrawCover(spriteBatch, preview);
						drew = true;
					}
				}
			}
			catch {
			}

			return drew;
		}

		private static bool DrawBackgroundSlot(SpriteBatch spriteBatch, int slot, bool horizon)
		{
			Texture2D tex = TextureOfSlot(slot);
			if (tex == null)
				return false;

			if (horizon)
				DrawHorizon(spriteBatch, tex);
			else
				DrawCover(spriteBatch, tex);
			return true;
		}

		private static bool RealSlot(int slot) => TextureOfSlot(slot) != null;

		private static Texture2D TextureOfSlot(int slot)
		{
			if (slot < 0 || TextureAssets.Background == null || slot >= TextureAssets.Background.Length)
				return null;

			try {
				Main.instance.LoadBackground(slot);
			}
			catch {
			}

			Texture2D tex = TextureAssets.Background[slot]?.Value;
			if (tex == null || tex.IsDisposed || IsBlank(tex))
				return null;

			return tex;
		}

		private static void DrawCover(SpriteBatch spriteBatch, Texture2D tex)
		{
			Point cover = CalamitasMenuDraw.CoverSize;
			float scale = MathHelper.Max(cover.X / (float)tex.Width, cover.Y / (float)tex.Height);
			int w = (int)(tex.Width * scale);
			int h = (int)(tex.Height * scale);
			spriteBatch.Draw(
				tex,
				new Rectangle((cover.X - w) / 2, (cover.Y - h) / 2, w, h),
				Color.White);
		}

		private static void DrawHorizon(SpriteBatch spriteBatch, Texture2D tex)
		{
			Point cover = CalamitasMenuDraw.CoverSize;
			float scale = cover.X / (float)Math.Max(1, tex.Width);
			int w = (int)(tex.Width * scale);
			int h = (int)(tex.Height * scale);
			spriteBatch.Draw(
				tex,
				new Rectangle((cover.X - w) / 2, cover.Y - h, w, h),
				Color.White);
		}

		private static void RestoreBatch(SpriteBatch spriteBatch) =>
			RestoreBatch(spriteBatch, Main.UIScaleMatrix);

		private static void RestoreBatch(SpriteBatch spriteBatch, Matrix matrix)
		{
			try {
				spriteBatch.End();
			}
			catch {
			}

			spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.LinearClamp,
				DepthStencilState.None,
				RasterizerState.CullCounterClockwise,
				null,
				matrix);
		}

		private static Texture2D SafeLogo(ModMenu menu)
		{
			try {
				Texture2D tex = menu.Logo?.Value;
				return tex == null || tex.IsDisposed ? null : tex;
			}
			catch {
				return null;
			}
		}

		private static Texture2D FirstStyleTexture(ModSurfaceBackgroundStyle style)
		{
			if (style == null)
				return null;

			try {
				foreach (FieldInfo field in style.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
					object value = field.GetValue(style);
					if (TryArt(value, out Texture2D tex))
						return tex;
				}

				foreach (PropertyInfo prop in style.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
					if (prop.GetIndexParameters().Length > 0)
						continue;
					object value = prop.GetValue(style);
					if (TryArt(value, out Texture2D tex))
						return tex;
				}
			}
			catch {
			}

			return null;
		}

		private static bool TryArt(object value, out Texture2D tex)
		{
			tex = null;
			if (value is Asset<Texture2D> asset && asset.IsLoaded)
				tex = asset.Value;
			else if (value is Texture2D direct)
				tex = direct;

			return tex != null && !tex.IsDisposed && !IsBlank(tex) && !IsSceneTooSmall(tex);
		}

		private static bool IsSceneTooSmall(Texture2D tex) => tex.Width < 64 || tex.Height < 64;

		private static bool LooksPlaceholder(string name, Texture2D tex)
		{
			if (tex.Width < 24 || tex.Height < 24)
				return true;
			if (IsBlank(tex))
				return true;
			if (string.IsNullOrEmpty(name))
				return false;

			return name.Contains("Blank", StringComparison.OrdinalIgnoreCase) ||
			       name.Contains("Empty", StringComparison.OrdinalIgnoreCase) ||
			       name.Contains("Null", StringComparison.OrdinalIgnoreCase) ||
			       name.Contains("Placeholder", StringComparison.OrdinalIgnoreCase) ||
			       (name.Contains("Pixel", StringComparison.OrdinalIgnoreCase) && tex.Width <= 32);
		}

		private static bool IsSceneTexture(Texture2D tex)
		{
			if (tex == null || tex.IsDisposed || IsBlank(tex))
				return false;

			float aspect = tex.Width / (float)tex.Height;
			bool wideScene = tex.Width >= 960 && tex.Height >= 540 && aspect is >= 1.35f and <= 2.15f;
			if (wideScene)
				return true;

			return tex.Width >= 640 && tex.Height >= 360 && CornersOpaque(tex);
		}

		private static bool CornersOpaque(Texture2D tex)
		{
			try {
				var pixel = new Color[1];
				int w = tex.Width - 1;
				int h = tex.Height - 1;
				int[] xs = { 1, w };
				int[] ys = { 1, h };
				foreach (int y in ys) {
					foreach (int x in xs) {
						tex.GetData(0, new Rectangle(x, y, 1, 1), pixel, 0, 1);
						if (pixel[0].A < 200)
							return false;
					}
				}

				return true;
			}
			catch {
				return false;
			}
		}

		private static bool IsBlank(Texture2D tex)
		{
			int key = tex.GetHashCode();
			if (_emptyTex.TryGetValue(key, out bool known))
				return known;

			bool empty = LooksEmpty(tex);
			_emptyTex[key] = empty;
			return empty;
		}

		private static bool LooksEmpty(Texture2D tex)
		{
			if (tex.Width <= 4 && tex.Height <= 4)
				return true;

			try {
				int w = tex.Width;
				int h = tex.Height;
				int opaque = 0;
				int samples = 0;
				if (w * h <= 256 * 256) {
					var data = new Color[w * h];
					tex.GetData(data);
					int step = Math.Max(1, data.Length / 4096);
					for (int i = 0; i < data.Length; i += step) {
						samples++;
						if (data[i].A > 20)
							opaque++;
					}
				}
				else {
					var pixel = new Color[1];
					for (int y = 0; y < 8; y++) {
						for (int x = 0; x < 8; x++) {
							int px = (int)((x + 0.5f) / 8f * (w - 1));
							int py = (int)((y + 0.5f) / 8f * (h - 1));
							tex.GetData(0, new Rectangle(px, py, 1, 1), pixel, 0, 1);
							samples++;
							if (pixel[0].A > 20)
								opaque++;
						}
					}
				}

				return samples == 0 || opaque < 3 || opaque / (float)samples < 0.01f;
			}
			catch {
				return tex.Width <= 8 && tex.Height <= 8;
			}
		}

		private static bool Overrides(object instance, string name)
		{
			if (instance == null)
				return false;

			MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
			if (method == null) {
				PropertyInfo prop = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
				method = prop?.GetGetMethod();
			}

			if (method == null)
				return false;

			Type declared = method.DeclaringType;
			Type root = instance switch {
				ModMenu => typeof(ModMenu),
				ModSurfaceBackgroundStyle => typeof(ModSurfaceBackgroundStyle),
				GlobalBackgroundStyle => typeof(GlobalBackgroundStyle),
				_ => instance.GetType().BaseType
			};
			return declared != null && declared != root;
		}

		private static void InstallDrawHooks()
		{
			if (_hooksReady)
				return;

			TryHookDraw(new[] { typeof(Texture2D), typeof(Vector2), typeof(Color) }, DrawVecColor);
			TryHookDraw(new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color) }, DrawVecRectColor);
			TryHookDraw(
				new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) },
				DrawVecFloatScale);
			TryHookDraw(
				new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float) },
				DrawVecVecScale);
			TryHookDraw(new[] { typeof(Texture2D), typeof(Rectangle), typeof(Color) }, DrawDestColor);
			TryHookDraw(new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color) }, DrawDestRectColor);
			TryHookDraw(
				new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(SpriteEffects), typeof(float) },
				DrawDestFull);
			TryHookBegin();
			_hooksReady = true;
		}

		private static void TryHookBegin()
		{
			foreach (MethodInfo method in typeof(SpriteBatch).GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
				if (method.Name != nameof(SpriteBatch.Begin))
					continue;
				ParameterInfo[] pars = method.GetParameters();
				if (pars.Length != 7 || pars[6].ParameterType != typeof(Matrix))
					continue;
				try {
					MonoModHooks.Add(method, BeginMatrix);
				}
				catch {
				}

				break;
			}
		}

		private static void BeginMatrix(
			Action<SpriteBatch, SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Matrix> orig,
			SpriteBatch sb,
			SpriteSortMode sort,
			BlendState blend,
			SamplerState samp,
			DepthStencilState depth,
			RasterizerState rast,
			Effect effect,
			Matrix matrix)
		{
			if (_mode == DrawMode.Background && !_insideDraw) {
				_passScaleX = (float)Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
				_passScaleY = (float)Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);
				if (_passScaleX < 0.05f)
					_passScaleX = 1f;
				if (_passScaleY < 0.05f)
					_passScaleY = 1f;
				_passShiftX = matrix.M41;
				_passShiftY = matrix.M42;
				matrix = Matrix.Identity;
			}
			orig(sb, sort, blend, samp, depth, rast, effect, matrix);
		}

		private static void TryHookDraw(Type[] types, Delegate hook)
		{
			MethodInfo method = FindDraw(types);
			if (method == null)
				return;

			try {
				MonoModHooks.Add(method, hook);
			}
			catch {
			}
		}

		private static MethodInfo FindDraw(Type[] types)
		{
			foreach (MethodInfo method in typeof(SpriteBatch).GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
				if (method.Name != nameof(SpriteBatch.Draw))
					continue;

				ParameterInfo[] pars = method.GetParameters();
				if (pars.Length != types.Length)
					continue;

				bool match = true;
				for (int i = 0; i < pars.Length; i++) {
					if (pars[i].ParameterType != types[i]) {
						match = false;
						break;
					}
				}

				if (match)
					return method;
			}

			return null;
		}

		private static void DrawVecColor(Action<SpriteBatch, Texture2D, Vector2, Color> orig, SpriteBatch sb, Texture2D tex, Vector2 pos, Color color)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, pos, color);
				return;
			}

			if (!FilterVec(tex, ref pos, Vector2.Zero, ref color, Vector2.One, null, out Vector2 _, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, pos, color));
		}

		private static void DrawVecRectColor(Action<SpriteBatch, Texture2D, Vector2, Rectangle?, Color> orig, SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? src, Color color)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, pos, src, color);
				return;
			}

			if (!FilterVec(tex, ref pos, Vector2.Zero, ref color, Vector2.One, src, out Vector2 _, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, pos, src, color));
		}

		private static void DrawVecFloatScale(
			Action<SpriteBatch, Texture2D, Vector2, Rectangle?, Color, float, Vector2, float, SpriteEffects, float> orig,
			SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? src, Color color, float rot, Vector2 origin, float scale, SpriteEffects fx, float depth)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, pos, src, color, rot, origin, scale, fx, depth);
				return;
			}

			Vector2 sc = new(scale, scale);
			if (!FilterVec(tex, ref pos, origin, ref color, sc, src, out sc, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, pos, src, color, rot, origin, sc.X, fx, depth));
		}

		private static void DrawVecVecScale(
			Action<SpriteBatch, Texture2D, Vector2, Rectangle?, Color, float, Vector2, Vector2, SpriteEffects, float> orig,
			SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? src, Color color, float rot, Vector2 origin, Vector2 scale, SpriteEffects fx, float depth)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, pos, src, color, rot, origin, scale, fx, depth);
				return;
			}

			if (!FilterVec(tex, ref pos, origin, ref color, scale, src, out scale, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, pos, src, color, rot, origin, scale, fx, depth));
		}

		private static void DrawDestColor(Action<SpriteBatch, Texture2D, Rectangle, Color> orig, SpriteBatch sb, Texture2D tex, Rectangle dest, Color color)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, dest, color);
				return;
			}

			if (!FilterRect(tex, ref dest, ref color, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, dest, color));
		}

		private static void DrawDestRectColor(Action<SpriteBatch, Texture2D, Rectangle, Rectangle?, Color> orig, SpriteBatch sb, Texture2D tex, Rectangle dest, Rectangle? src, Color color)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, dest, src, color);
				return;
			}

			if (!FilterRect(tex, ref dest, ref color, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, dest, src, color));
		}

		private static void DrawDestFull(
			Action<SpriteBatch, Texture2D, Rectangle, Rectangle?, Color, float, Vector2, SpriteEffects, float> orig,
			SpriteBatch sb, Texture2D tex, Rectangle dest, Rectangle? src, Color color, float rot, Vector2 origin, SpriteEffects fx, float depth)
		{
			if (_mode == DrawMode.Off || _insideDraw) {
				orig(sb, tex, dest, src, color, rot, origin, fx, depth);
				return;
			}

			if (!FilterRect(tex, ref dest, ref color, out bool skip) || skip)
				return;

			Pass(() => orig(sb, tex, dest, src, color, rot, origin, fx, depth));
		}

		private static void Pass(Action draw)
		{
			_insideDraw = true;
			try {
				draw();
			}
			finally {
				_insideDraw = false;
			}
		}

		private static bool FilterVec(
			Texture2D tex,
			ref Vector2 pos,
			Vector2 origin,
			ref Color color,
			Vector2 scale,
			Rectangle? src,
			out Vector2 newScale,
			out bool skip)
		{
			newScale = scale;
			skip = false;
			DrawAction action = Classify(tex, pos, origin, scale, src, null);
			if (action == DrawAction.Skip) {
				skip = true;
				return false;
			}

			if (_mode == DrawMode.Background && IsScreenCover(tex, pos, origin, scale, src)) {
				Point size = CalamitasMenuDraw.CoverSize;
				float sx = size.X / (float)Math.Max(1, Main.screenWidth);
				float sy = size.Y / (float)Math.Max(1, Main.screenHeight);
				pos = new Vector2(pos.X * sx, pos.Y * sy);
				newScale = new Vector2(scale.X * sx, scale.Y * sy);
				color *= _fade;
				_sawCover = true;
				return true;
			}

			if (_mode == DrawMode.Background) {
				ScaleForeignObject(ref pos, ref newScale);
				color *= _fade;
				return true;
			}

			if (action == DrawAction.Remap) {
				Remap(tex, ref pos, ref newScale, ref color);
				_sawLogoDraw = true;
			}
			else if (_mode == DrawMode.Logo) {
				color *= _fade;
			}

			return true;
		}

		private static bool FilterRect(Texture2D tex, ref Rectangle dest, ref Color color, out bool skip)
		{
			skip = false;
			Vector2 center = dest.Center.ToVector2();
			DrawAction action = Classify(tex, center, Vector2.Zero, Vector2.One, null, dest);
			if (action == DrawAction.Skip) {
				skip = true;
				return false;
			}

			if (_mode == DrawMode.Background && IsScreenCover(dest)) {
				dest = CalamitasMenuDraw.CoverRect;
				color *= _fade;
				_sawCover = true;
				return true;
			}

			if (_mode == DrawMode.Background) {
				ScaleForeignObject(ref dest);
				color *= _fade;
				return true;
			}

			if (action == DrawAction.Remap) {
				Vector2 pos = center;
				Vector2 scale = Vector2.One;
				Remap(tex, ref pos, ref scale, ref color);
				int w = Math.Max(1, (int)(dest.Width * scale.X));
				int h = Math.Max(1, (int)(dest.Height * scale.Y));
				dest = new Rectangle((int)(pos.X - w * 0.5f), (int)(pos.Y - h * 0.5f), w, h);
				_sawLogoDraw = true;
			}

			return true;
		}

		private static void ScaleForeignObject(ref Vector2 pos, ref Vector2 scale)
		{
			pos = new Vector2(pos.X * _passScaleX + _passShiftX, pos.Y * _passScaleY + _passShiftY);
			scale = new Vector2(scale.X * _passScaleX, scale.Y * _passScaleY);
			CoverFit(out float sx, out float sy);
			pos = new Vector2(pos.X * sx, pos.Y * sy);
			scale = new Vector2(scale.X * sx, scale.Y * sy);
		}

		private static void ScaleForeignObject(ref Rectangle dest)
		{
			float x = dest.X * _passScaleX + _passShiftX;
			float y = dest.Y * _passScaleY + _passShiftY;
			float w = dest.Width * _passScaleX;
			float h = dest.Height * _passScaleY;
			CoverFit(out float sx, out float sy);
			dest = new Rectangle(
				(int)Math.Round(x * sx),
				(int)Math.Round(y * sy),
				Math.Max(1, (int)Math.Round(w * sx)),
				Math.Max(1, (int)Math.Round(h * sy)));
		}

		private static void CoverFit(out float sx, out float sy)
		{
			Point size = CalamitasMenuDraw.CoverSize;
			sx = size.X / (float)Math.Max(1, Main.screenWidth);
			sy = size.Y / (float)Math.Max(1, Main.screenHeight);
		}

		private static void Remap(Texture2D tex, ref Vector2 pos, ref Vector2 scale, ref Color color)
		{
			pos += _ourAnchor - _vanillaCenter;
			if (IsLogoSized(tex)) {
				float mul = _intendedScale / Math.Max(0.05f, _liveScale);
				scale *= mul;
			}

			color *= _fade;
		}

		private static DrawAction Classify(Texture2D tex, Vector2 pos, Vector2 origin, Vector2 scale, Rectangle? src, Rectangle? dest)
		{
			if (tex == null || tex.IsDisposed)
				return DrawAction.Skip;

			bool cover = dest.HasValue ? IsScreenCover(dest.Value) : IsScreenCover(tex, pos, origin, scale, src);
			bool logo = IsLogoish(tex, pos, scale);
			if (_mode == DrawMode.Background) {
				if (cover)
					return DrawAction.Keep;
				if (_logoTex != null && ReferenceEquals(tex, _logoTex))
					return DrawAction.Skip;
				return logo ? DrawAction.Skip : DrawAction.Keep;
			}

			if (_mode == DrawMode.Logo) {
				if (cover)
					return DrawAction.Skip;
				if (logo || NearLogo(pos))
					return DrawAction.Remap;
				return DrawAction.Skip;
			}

			return DrawAction.Keep;
		}

		private static bool IsScreenCover(Rectangle dest) =>
			dest.Width >= Main.screenWidth * 0.42f && dest.Height >= Main.screenHeight * 0.42f;

		private static bool IsScreenCover(Texture2D tex, Vector2 pos, Vector2 origin, Vector2 scale, Rectangle? src)
		{
			int tw = src?.Width ?? tex.Width;
			int th = src?.Height ?? tex.Height;
			float w = tw * Math.Abs(scale.X);
			float h = th * Math.Abs(scale.Y);
			if (w >= Main.screenWidth * 0.42f && h >= Main.screenHeight * 0.42f)
				return true;

			Vector2 screen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
			return Vector2.Distance(pos, screen) < 96f && (w >= Main.screenWidth * 0.28f || h >= Main.screenHeight * 0.28f);
		}

		private static bool IsLogoish(Texture2D tex, Vector2 pos, Vector2 scale)
		{
			if (_logoTex != null && ReferenceEquals(tex, _logoTex))
				return true;
			if (!NearLogo(pos))
				return false;
			if (_logoTex == null)
				return !IsTiny(tex, scale);

			return IsLogoSized(tex) || Vector2.Distance(pos, _liveCenter) < Math.Max(48f, _logoTex.Width * 0.35f);
		}

		private static bool IsLogoSized(Texture2D tex)
		{
			if (_logoTex == null || tex == null)
				return false;
			if (ReferenceEquals(tex, _logoTex))
				return true;

			float lw = Math.Max(_logoTex.Width, _logoTex.Height);
			float tw = Math.Max(tex.Width, tex.Height);
			return tw >= lw * 0.4f && tw <= lw * 2.5f;
		}

		private static bool IsTiny(Texture2D tex, Vector2 scale) =>
			tex.Width * Math.Abs(scale.X) < 48f && tex.Height * Math.Abs(scale.Y) < 48f;

		private static bool NearLogo(Vector2 pos)
		{
			float r = _logoTex != null ? Math.Max(150f, _logoTex.Width * 0.9f) : 180f;
			return Vector2.Distance(pos, _liveCenter) < r ||
			       Vector2.Distance(pos, _vanillaCenter) < r ||
			       Vector2.Distance(pos, _ourAnchor) < r;
		}
	}
}

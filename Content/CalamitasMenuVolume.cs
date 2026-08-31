using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public class CalamitasMenuVolume : ModSystem
	{
		private const int TitleLockId = 5;
		private const int TitleVolumeMenuMode = 26;
		private const int DummyLockId = 19;

		private static bool _optionsVolumePage;
		private static bool _wasTitleVolumePage;
		private static bool _wasOptionsVolumePage;
		private static bool _titleArmed;
		private static bool _optionsArmed;
		private static bool _dirty;
		private static bool _injecting;
		private static int _saveDelay;
		private static int _bars;
		private static int _lastPercentIndex = -1;
		private static float _savedMusic = 1f;
		private static float _savedSound = 1f;
		private static float _savedAmbient = 1f;
		private static Vector2 _anchor;
		private static Vector2 _offset;

		public override void Load()
		{
			DieWithASmileSave.EnsureLoaded();
			On_IngameOptions.Draw += DrawOptionsHook;
			On_IngameOptions.DrawRightSide += DrawRightSideHook;
			On_IngameOptions.DrawValueBar += DrawValueBarHook;
			On_Main.DrawMenu += DrawMenuHook;
		}

		public override void Unload()
		{
			ForceSave();
		}

		private static void DrawMenuHook(On_Main.orig_DrawMenu orig, Main self, GameTime time)
		{
			_bars = 0;
			bool volumePage = Main.gameMenu && Main.menuMode == TitleVolumeMenuMode;
			if (volumePage) {
				if (!_wasTitleVolumePage) {
					_titleArmed = false;
					CaptureVanillaVolumes();
				}

				if (!Main.mouseLeft)
					_titleArmed = true;

				if (!_titleArmed)
					IngameOptions.rightLock = DummyLockId;
			}

			orig(self, time);

			if (volumePage) {
				if (!_titleArmed)
					RestoreVanillaVolumes();
			}
			else if (_wasTitleVolumePage) {
				ForceSave();
			}

			_wasTitleVolumePage = volumePage;
			FlushSave();
		}

		private static void DrawOptionsHook(On_IngameOptions.orig_Draw orig, Main main, SpriteBatch sb)
		{
			_optionsVolumePage = false;
			_lastPercentIndex = -1;
			_bars = 0;
			bool likelyVolume = IngameOptions.category == 0;
			if (likelyVolume) {
				if (!_wasOptionsVolumePage) {
					_optionsArmed = false;
					CaptureVanillaVolumes();
				}

				if (!Main.mouseLeft)
					_optionsArmed = true;

				if (!_optionsArmed)
					IngameOptions.rightLock = DummyLockId;
			}

			orig(main, sb);

			if (_optionsVolumePage) {
				if (!_optionsArmed)
					RestoreVanillaVolumes();
			}
			else if (_wasOptionsVolumePage) {
				ForceSave();
			}

			_wasOptionsVolumePage = _optionsVolumePage;
			FlushSave();
		}

		private static bool DrawRightSideHook(
			On_IngameOptions.orig_DrawRightSide orig,
			SpriteBatch sb,
			string txt,
			int i,
			Vector2 anchor,
			Vector2 offset,
			float scale,
			float colorScale,
			Color over)
		{
			bool result = orig(sb, txt, i, anchor, offset, scale, colorScale, over);
			if (string.IsNullOrEmpty(txt) || !txt.Contains('%'))
				return result;

			if (txt.StartsWith(Lang.menu[99].Value, StringComparison.Ordinal) ||
			    txt.StartsWith(Lang.menu[98].Value, StringComparison.Ordinal) ||
			    txt.StartsWith(Lang.menu[119].Value, StringComparison.Ordinal))
				_optionsVolumePage = true;

			if (_optionsVolumePage) {
				_anchor = anchor;
				_offset = offset;
				_lastPercentIndex = i;
			}

			return result;
		}

		private static float DrawValueBarHook(On_IngameOptions.orig_DrawValueBar orig, SpriteBatch sb, float scale, float perc, int lockState, Utils.ColorLerpMethod colorMethod)
		{
			Vector2 row = IngameOptions.valuePosition;
			float result = orig(sb, scale, perc, lockState, colorMethod);
			if (_injecting)
				return result;

			bool titleVolume = Main.gameMenu && Main.menuMode == TitleVolumeMenuMode;
			if (!titleVolume && !_optionsVolumePage)
				return result;

			_bars++;
			if (_bars != 3)
				return result;

			_injecting = true;
			try {
				DrawMenuMusicBar(sb, scale, row);
			}
			finally {
				_injecting = false;
			}

			return result;
		}

		private static void DrawMenuMusicBar(SpriteBatch sb, float scale, Vector2 row)
		{
			float volume = DieWithASmileSettings.MenuMusicVolume;
			string label = CalamitasMenuText.UI("MainMenuMusic") + " " + Math.Round(volume * 100.0) + "%";
			float dy = _offset.Y > 1f ? _offset.Y : 40f;
			bool armed = Main.gameMenu ? _titleArmed : _optionsArmed;

			if (_optionsVolumePage) {
				int i = _lastPercentIndex + 1;
				if (i >= 0 && i < IngameOptions.rightScale.Length) {
					float colorScale = (IngameOptions.rightScale[i] - 1f) / 0.001f;
					int previousHover = IngameOptions.rightHover;
					IngameOptions.noSound = true;
					if (IngameOptions.DrawRightSide(sb, label, i, _anchor, _offset, IngameOptions.rightScale[i], colorScale)) {
						if (IngameOptions.rightLock == -1)
							IngameOptions.notBar = true;
						IngameOptions.noSound = true;
					}

					Vector2 size = new(IngameOptions.width, IngameOptions.height);
					Vector2 panel = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f - size * 0.5f;
					IngameOptions.valuePosition.X = panel.X + size.X - 30f;
					IngameOptions.valuePosition.Y -= 3f;
					float value = IngameOptions.DrawValueBar(sb, scale, volume);
					if (armed && IngameOptions.inBar && Main.mouseLeft && (IngameOptions.rightLock == -1 || IngameOptions.rightLock == i) && !IngameOptions.notBar) {
						IngameOptions.rightLock = i;
						IngameOptions.noSound = true;
						SetVolume(value);
					}

					IngameOptions.rightHover = previousHover;
					IngameOptions.noSound = true;
				}

				return;
			}

			Color text = Color.White;
			text.R = (byte)((255 + text.R) / 2);
			text.G = (byte)((255 + text.R) / 2);
			text.B = (byte)((255 + text.R) / 2);
			float labelX = 370f + Main.screenWidth * 0.5f - 400f;
			Utils.DrawBorderStringFourWay(sb, FontAssets.DeathText.Value, label, labelX, row.Y + dy - 10f, text, Color.Black, Vector2.Zero, 0.5f);

			int prevHover = IngameOptions.rightHover;
			IngameOptions.noSound = true;
			IngameOptions.valuePosition = new Vector2(row.X, row.Y + dy);
			float dragged = IngameOptions.DrawValueBar(sb, scale, volume);
			bool hovering = IngameOptions.inBar;
			bool dragging = armed &&
			                hovering &&
			                Main.mouseLeft &&
			                (IngameOptions.rightLock == -1 || IngameOptions.rightLock == TitleLockId);
			if (dragging) {
				IngameOptions.rightLock = TitleLockId;
				SetVolume(dragged);
			}

			IngameOptions.rightHover = hovering ? -1 : prevHover;
			IngameOptions.noSound = true;
		}

		private static void CaptureVanillaVolumes()
		{
			_savedMusic = Main.musicVolume;
			_savedSound = Main.soundVolume;
			_savedAmbient = Main.ambientVolume;
		}

		private static void RestoreVanillaVolumes()
		{
			Main.musicVolume = _savedMusic;
			Main.soundVolume = _savedSound;
			Main.ambientVolume = _savedAmbient;
		}

		private static void SetVolume(float value)
		{
			value = Math.Clamp(value, 0f, 1f);
			if (Math.Abs(DieWithASmileSave.Data.MenuMusicVolume - value) < 0.0005f)
				return;

			DieWithASmileSave.Data.MenuMusicVolume = value;
			_dirty = true;
			_saveDelay = 20;
		}

		private static void FlushSave()
		{
			if (!_dirty)
				return;

			if (_saveDelay > 0)
				_saveDelay--;

			if (Main.mouseLeft && _saveDelay > 0)
				return;

			ForceSave();
		}

		private static void ForceSave()
		{
			if (!_dirty)
				return;

			_dirty = false;
			_saveDelay = 0;
			DieWithASmileSave.Save();
		}
	}
}

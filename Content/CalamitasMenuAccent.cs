using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace DieWithASmile.Content
{
	internal readonly struct AccentSwatch
	{
		internal readonly string Key;
		internal readonly Color Mid;
		internal readonly Color Dark;
		internal readonly Color Light;
		internal readonly Color Deep;

		internal AccentSwatch(string key, Color mid, Color dark, Color light, Color deep)
		{
			Key = key;
			Mid = mid;
			Dark = dark;
			Light = light;
			Deep = deep;
		}
	}

	internal static class CalamitasMenuAccent
	{
		internal static readonly AccentSwatch[] Palettes =
		{
			new("AccentCrimson", new Color(138, 63, 64), new Color(98, 43, 42), new Color(168, 72, 67), new Color(90, 30, 32)),
			new("AccentPurple", new Color(132, 78, 186), new Color(78, 42, 128), new Color(186, 132, 232), new Color(72, 32, 110)),
			new("AccentRose", new Color(186, 72, 118), new Color(118, 36, 78), new Color(232, 128, 168), new Color(96, 28, 64)),
			new("AccentGold", new Color(196, 148, 64), new Color(128, 88, 28), new Color(236, 196, 96), new Color(96, 64, 20)),
			new("AccentTeal", new Color(48, 148, 148), new Color(24, 88, 92), new Color(96, 204, 196), new Color(16, 64, 68)),
			new("AccentIce", new Color(88, 140, 196), new Color(40, 72, 128), new Color(152, 196, 236), new Color(28, 48, 96)),
			new("AccentEmber", new Color(196, 96, 48), new Color(128, 52, 24), new Color(236, 148, 88), new Color(96, 36, 16)),
			new("AccentMint", new Color(72, 168, 112), new Color(32, 96, 64), new Color(128, 216, 160), new Color(24, 72, 48))
		};

		internal static int Index
		{
			get
			{
				int index = DieWithASmileSave.Data.AccentIndex;
				if (index < 0 || index >= Palettes.Length)
					return 0;
				return index;
			}
		}

		internal static AccentSwatch Current => Palettes[Index];

		internal static Color Mid => Current.Mid;

		internal static Color Dark => Current.Dark;

		internal static Color Light => Current.Light;

		internal static Color Deep => Current.Deep;

		internal static Color Hover
		{
			get
			{
				float wave = (MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f) + 1f) * 0.5f;
				return Color.Lerp(Dark, Light, wave);
			}
		}

		internal static Color Glyph(bool hover, bool on = false) =>
			hover ? Light : on ? Mid : Dark;

		internal static void Set(int index)
		{
			DieWithASmileSettings.SetAccent(index);
		}
	}
}

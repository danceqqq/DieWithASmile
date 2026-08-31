using Microsoft.Xna.Framework;
using Terraria.ID;

namespace DieWithASmile.Content
{
	internal readonly struct VanillaMenuScene
	{
		internal readonly int Style;
		internal readonly string Key;

		internal VanillaMenuScene(int style, string key)
		{
			Style = style;
			Key = key;
		}
	}

	internal static class CalamitasMenuVanilla
	{
		internal static readonly VanillaMenuScene[] Scenes =
		{
			new(SurfaceBackgroundID.Forest1, "VanillaBackground")
		};

		internal static int Count => Scenes.Length;

		internal static bool IsKnown(int style)
		{
			for (int i = 0; i < Scenes.Length; i++) {
				if (Scenes[i].Style == style)
					return true;
			}

			return false;
		}
	}
}

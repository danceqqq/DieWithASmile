using Microsoft.Xna.Framework;
using Terraria;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuParallax
	{
		internal static Vector2 MouseOffset =>
			new Vector2(
				(Main.mouseX - Main.screenWidth * 0.5f) * 0.028f,
				(Main.mouseY - Main.screenHeight * 0.5f) * 0.018f);

		internal static Vector2 ForDepth(float depth) => MouseOffset * depth;
	}
}

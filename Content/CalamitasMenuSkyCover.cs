using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	public class CalamitasMenuSkyCover : GlobalBackgroundStyle
	{
		internal static string CoveringModName { get; private set; } = "";

		internal static bool HasHint =>
			CalamitasMenuChrome.Active && !string.IsNullOrEmpty(CoveringModName);

		internal static string HintText =>
			HasHint ? CalamitasMenuText.UI("SkyCoveredBy", CoveringModName) : "";

		internal static void BeginFrame() => CoveringModName = "";

		internal static void NoteForeignStyle(int slot)
		{
			string name = FindForeignStyleName(slot);
			if (!string.IsNullOrEmpty(name))
				CoveringModName = name;
		}

		public override void ChooseSurfaceBackgroundStyle(ref int style)
		{
			if (!Main.gameMenu || MenuLoader.CurrentMenu is not DieWithASmileCalamitasMenu)
				return;

			if (DieWithASmileSettings.UsingVanillaWallpaper) {
				style = DieWithASmileSave.Data.VanillaBgStyle;
				return;
			}

			if (DieWithASmileSettings.UsingForeignWallpaper || DieWithASmileSettings.UsingTmlWallpaper)
				return;

			int ours = ModContent.GetInstance<CalamitasMenuBackgroundStyle>().Slot;
			if (style == ours)
				return;

			NoteForeignStyle(style);
			style = ours;
		}

		private static string FindForeignStyleName(int slot)
		{
			try {
				ModSurfaceBackgroundStyle other = CalamitasMenuForeign.StyleBySlot(slot);
				if (other is null or CalamitasMenuBackgroundStyle)
					return "";
				return other.Mod?.DisplayName ?? other.Name;
			}
			catch {
				return "";
			}
		}
	}

	public class CalamitasMenuSkyCoverSystem : ModSystem
	{
		public override void Load() => On_Main.DrawBG += DrawBGHook;

		private static void DrawBGHook(On_Main.orig_DrawBG orig, Main self)
		{
			if (CalamitasMenuChrome.Active)
				CalamitasMenuSkyCover.BeginFrame();

			if (DieWithASmileSettings.UsingVanillaWallpaper)
				Main.bgStyle = DieWithASmileSave.Data.VanillaBgStyle;

			orig(self);
			if ((!CalamitasMenuChrome.Active && !CalamitasMenuForeign.HoldingCurrent) ||
			    DieWithASmileSettings.UsingPassthroughSky)
				return;

			if (ModContent.GetInstance<CalamitasMenuBackgroundStyle>() is { } ours && Main.bgStyle != ours.Slot)
				CalamitasMenuSkyCover.NoteForeignStyle(Main.bgStyle);
		}
	}
}

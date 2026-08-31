using Terraria.Localization;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuText
	{
		internal static string UI(string key)
		{
			return Language.GetTextValue("Mods.DieWithASmile.UI." + key);
		}

		internal static string UI(string key, object arg)
		{
			return Language.GetTextValue("Mods.DieWithASmile.UI." + key, arg);
		}
	}
}

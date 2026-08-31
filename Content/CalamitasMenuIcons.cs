using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace DieWithASmile.Content
{
	internal static class CalamitasMenuIcons
	{
		private static readonly Dictionary<Texture2D, Texture2D> _white = new();

		internal static Asset<Texture2D> EditPlaylist;
		internal static Asset<Texture2D> UploadSong;
		internal static Asset<Texture2D> LockSong;
		internal static Asset<Texture2D> UnlockSong;
		internal static Asset<Texture2D> Gallery;
		internal static Asset<Texture2D> ChangeLogo;
		internal static Asset<Texture2D> ChangePosition;
		internal static Asset<Texture2D> Shuffle;
		internal static Asset<Texture2D> PlayerOn;
		internal static Asset<Texture2D> PlayerOff;
		internal static Asset<Texture2D> Show;
		internal static Asset<Texture2D> Hide;

		internal static void Load()
		{
			EditPlaylist = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/EditPlaylist");
			UploadSong = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/UploadSong");
			LockSong = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/LockSong");
			UnlockSong = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/UnlockSong");
			Gallery = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/Gallery");
			ChangeLogo = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/ChangeLogo");
			ChangePosition = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/ChangePosition");
			Shuffle = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/Shuffle");
			PlayerOn = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/PlayerOn");
			PlayerOff = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/PlayerOff");
			Show = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/Show");
			Hide = ModContent.Request<Texture2D>("DieWithASmile/Assets/Textures/UI/Hide");
		}

		internal static Texture2D AsControlIcon(Asset<Texture2D> asset)
		{
			Texture2D source = asset?.Value;
			if (source == null || source.IsDisposed)
				return null;

			if (_white.TryGetValue(source, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			int count = source.Width * source.Height;
			var data = new Color[count];
			source.GetData(data);
			for (int i = 0; i < data.Length; i++) {
				byte a = data[i].A;
				if (a == 0)
					continue;

				data[i] = new Color((byte)255, (byte)255, (byte)255, a);
			}

			var copy = new Texture2D(Main.graphics.GraphicsDevice, source.Width, source.Height);
			copy.SetData(data);
			_white[source] = copy;
			return copy;
		}

		internal static void Unload()
		{
			foreach (Texture2D tex in _white.Values) {
				Texture2D local = tex;
				Main.QueueMainThreadAction(() => {
					try {
						if (local != null && !local.IsDisposed)
							local.Dispose();
					}
					catch {
					}
				});
			}

			_white.Clear();
		}
	}
}

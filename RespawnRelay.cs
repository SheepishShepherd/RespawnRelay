using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace RespawnRelay
{
	public class RespawnRelay : Mod
	{

	}

	public class RespawnTeamSystem : ModSystem
	{
		public IMultiplayerClosePlayersOverlay ActiveClosePlayersTeamOverlay = new DeathPlayer();
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			// Draws the respawn time for dead players on the same team
			int layerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: MP Player Names"));
			if (layerIndex != -1) {
				layers.Insert(layerIndex + 1, new LegacyGameInterfaceLayer(
					"RespawnRelay: MP Player Names (respawn)",
					delegate {
						ActiveClosePlayersTeamOverlay.Draw();
						return true;
					}
				));
				//layers.RemoveAll(x => x.Name == "Vanilla: Death Text"); // debug purposes
			}
		}
	}

	public class DeathPlayer : IMultiplayerClosePlayersOverlay
	{
		public DeathPlayer() {

		}

		private struct PlayerRespawnCache(string name, Vector2 pos, Color color, Vector2 npDistPos, string npDist)
		{
			private string nameToShow = name;
			private Vector2 namePlatePos = pos.Floor();
			private Color namePlateColor = color;
			private Vector2 timeDrawPos = npDistPos.Floor();
			private string timeRemaining = npDist;
			private readonly DynamicSpriteFont font = FontAssets.MouseText.Value;

			public void DrawPlayerName(SpriteBatch spriteBatch) {
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, this.nameToShow, this.namePlatePos + new Vector2(0f, -40f), this.namePlateColor, 0f, Vector2.Zero, Vector2.One);
			}

			public void DrawTombstone(SpriteBatch spritebatch) {
				Main.instance.LoadItem(ItemID.Tombstone);
				Asset<Texture2D> tomb = TextureAssets.Item[ItemID.Tombstone];
				Vector2 vector = new Vector2(this.namePlatePos.X, this.namePlatePos.Y - 20f);
				vector.X = this.timeDrawPos.X - tomb.Value.Width - 4f;
				vector.Y += 4f;
				vector = vector.Floor();
				spritebatch.Draw(tomb.Value, vector, tomb.Value.Bounds, Color.White, 0f, default, 0.65f, SpriteEffects.None, 0f);
			}

			public void DrawPlayerDistance(SpriteBatch spriteBatch) {
				float scale = 0.85f;
				DynamicSpriteFontExtensionMethods.DrawString(spriteBatch, font, this.timeRemaining, new Vector2(this.timeDrawPos.X - 2f, this.timeDrawPos.Y), Color.Black, 0f, default, scale, SpriteEffects.None, 0f);
				DynamicSpriteFontExtensionMethods.DrawString(spriteBatch, font, this.timeRemaining, new Vector2(this.timeDrawPos.X + 2f, this.timeDrawPos.Y), Color.Black, 0f, default, scale, SpriteEffects.None, 0f);
				DynamicSpriteFontExtensionMethods.DrawString(spriteBatch, font, this.timeRemaining, new Vector2(this.timeDrawPos.X, this.timeDrawPos.Y - 2f), Color.Black, 0f, default, scale, SpriteEffects.None, 0f);
				DynamicSpriteFontExtensionMethods.DrawString(spriteBatch, font, this.timeRemaining, new Vector2(this.timeDrawPos.X, this.timeDrawPos.Y + 2f), Color.Black, 0f, default, scale, SpriteEffects.None, 0f);
				DynamicSpriteFontExtensionMethods.DrawString(spriteBatch, font, this.timeRemaining, this.timeDrawPos, Color.DarkGray, 0f, default, scale, SpriteEffects.None, 0f);
			}
		}

		private List<PlayerRespawnCache> _playerRespawnCache = new List<PlayerRespawnCache>();

		public void Draw() {
			if (Main.teamNamePlateDistance <= 0)
				return;

			this._playerRespawnCache.Clear();
			PlayerInput.SetZoom_World();
			Vector2 screenPosition = Main.screenPosition;
			PlayerInput.SetZoom_UI();
			_ = Main.screenPosition;
			float num2 = (float)(int)Main.mouseTextColor / 255f;
			if (Main.LocalPlayer.team == 0)
				return; // draw code does not run if the client player is not on a team

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			foreach (Player deadPlayer in Main.ActivePlayers) {
				if (deadPlayer.whoAmI == Main.myPlayer || !deadPlayer.dead || deadPlayer.team != Main.LocalPlayer.team)
					continue; // skip self and any players that are alive or not on the same team

				GetDistance(Main.screenWidth, Main.screenHeight, screenPosition, Main.LocalPlayer, font, deadPlayer, out var namePlatePos, out var namePlateDist, out var nameSize);
				Color teamColor = Main.teamColor[deadPlayer.team];
				Color color = new Color((byte)((float)(int)teamColor.R * num2), (byte)((float)(int)teamColor.G * num2), (byte)((float)(int)teamColor.B * num2), Main.mouseTextColor);
				string time = Language.GetTextValue("Mods.RespawnRelay.Respawn", ((int)((float)deadPlayer.respawnTimer / 60) + 1).ToString("0"));

				float num5 = -27f;
				num5 -= (nameSize.X - 85f) / 2f;
				Vector2 timePos = font.MeasureString(time);
				timePos.X = namePlatePos.X - num5;
				timePos.Y = namePlatePos.Y + nameSize.Y / 2f - timePos.Y / 2f - 16f;

				if (namePlateDist > 0f) {
					float distance = Vector2.Distance(deadPlayer.lastDeathPostion, Main.LocalPlayer.position);
					if (!(distance > (float)(Main.teamNamePlateDistance * 8))) {
						this._playerRespawnCache.Add(new PlayerRespawnCache(deadPlayer.name, namePlatePos, color, timePos, time));
					}
				}
				else {
					this._playerRespawnCache.Add(new PlayerRespawnCache(deadPlayer.name, namePlatePos, color, timePos, time));
				}
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
			this._playerRespawnCache.ForEach(x => x.DrawPlayerName(Main.spriteBatch));
			this._playerRespawnCache.ForEach(x => x.DrawPlayerDistance(Main.spriteBatch));
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, Main.UIScaleMatrix);
			this._playerRespawnCache.ForEach(x => x.DrawTombstone(Main.spriteBatch));
		}

		private static void GetDistance(int testWidth, int testHeight, Vector2 testPosition, Player localPlayer, DynamicSpriteFont font, Player player, out Vector2 namePlatePos, out float namePlateDist, out Vector2 measurement) {
			float uIScale = Main.UIScale;
			namePlatePos = font.MeasureString(player.name);
			float num = 0f;
			if (player.chatOverhead.timeLeft > 0 || player.emoteTime > 0) {
				num = (0f - namePlatePos.Y) * uIScale;
			}
			Vector2 value = new Vector2((float)(testWidth / 2) + testPosition.X, (float)(testHeight / 2) + testPosition.Y);
			Vector2 position = player.lastDeathPostion;
			position += (position - value) * (Main.GameViewMatrix.Zoom - Vector2.One);
			namePlateDist = 0f;
			float num2 = position.X + (float)(player.width / 2) - value.X;
			float num3 = position.Y - namePlatePos.Y - 2f + num - value.Y;
			float num4 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
			int num5 = testHeight;
			if (testHeight > testWidth) {
				num5 = testWidth;
			}
			num5 = num5 / 2 - 50;
			if (num5 < 100) {
				num5 = 100;
			}
			if (num4 < (float)num5) {
				namePlatePos.X = position.X + (float)(player.width / 2) - namePlatePos.X / 2f - testPosition.X;
				namePlatePos.Y = position.Y - namePlatePos.Y - 2f + num - testPosition.Y;
			}
			else {
				namePlateDist = num4;
				num4 = (float)num5 / num4;
				namePlatePos.X = (float)(testWidth / 2) + num2 * num4 - namePlatePos.X / 2f;
				namePlatePos.Y = (float)(testHeight / 2) + num3 * num4 + 40f * uIScale;
			}
			measurement = font.MeasureString(player.name);
			namePlatePos += measurement / 2f;
			namePlatePos *= 1f / uIScale;
			namePlatePos -= measurement / 2f;
			if (localPlayer.gravDir == -1f) {
				namePlatePos.Y = (float)testHeight - namePlatePos.Y;
			}
		}
	}
}

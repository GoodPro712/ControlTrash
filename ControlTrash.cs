using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.ID;

namespace ControlTrash
{
	public class ControlTrash : Mod
	{
		public static bool ControlInUse => PressingControl(Main.keyState);

		public static FieldInfo canFavoriteAt = null;
		public static MethodBase overrideLeftClick = null;
		public static MethodBase sellOrTrash = null;

		public override void Load()
		{
			try
			{
				canFavoriteAt = typeof(ItemSlot).GetField("canFavoriteAt", BindingFlags.NonPublic | BindingFlags.Static);
				overrideLeftClick = typeof(ItemSlot).GetMethod("OverrideLeftClick", BindingFlags.NonPublic | BindingFlags.Static);
				sellOrTrash = typeof(ItemSlot).GetMethod("SellOrTrash", BindingFlags.NonPublic | BindingFlags.Static);
			}
			catch (Exception) { }

			On.Terraria.UI.ItemSlot.LeftClick_ItemArray_int_int += On_LeftClick;
			IL.Terraria.UI.ItemSlot.OverrideHover += IL_OverrideHover;
		}

		public override void Unload()
		{
			canFavoriteAt = null;
			overrideLeftClick = null;
			sellOrTrash = null;
		}

		public static bool PressingControl(KeyboardState kb) => !kb.IsKeyDown(Keys.LeftControl) ? kb.IsKeyDown(Keys.RightControl) : true;

		private void IL_OverrideHover(ILContext il)
		{
			ILCursor c = new ILCursor(il);

			//IL_00cd: ldc.i4.6
			//IL_00ce: stsfld int32 Terraria.Main::cursorOverride

			if (c.TryGotoNext(MoveType.Before, e => e.MatchLdcI4(6), e => e.MatchStsfld(typeof(Main).GetField(nameof(Main.cursorOverride)))))
			{
				c.Index++;
				c.Emit(OpCodes.Pop); //Remove 6 from the stack
				c.Emit(OpCodes.Ldc_I4, -1); //Replace 6 with -1, which is the value for normal cursor
			}

			c.Goto(0);

			//IL_010b: ldsflda valuetype[Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.KeyboardState Terraria.Main::keyState
			//IL_0110: ldsfld valuetype[Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.Keys Terraria.Main::FavoriteKey
			//IL_0115: call instance bool[Microsoft.Xna.Framework] Microsoft.Xna.Framework.Input.KeyboardState::IsKeyDown(valuetype[Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.Keys)

			/*if (c.TryGotoNext(MoveType.Before,
				e => e.MatchLdsflda(typeof(Main).GetField(nameof(Main.keyState))),
				e => e.MatchLdsfld(typeof(Main).GetField(nameof(Main.FavoriteKey))),
				e => e.MatchCall(typeof(KeyboardState).GetMethod(nameof(KeyboardState.IsKeyDown)))))
			{
				c.Emit(OpCodes.Ldloc_0); //ldloc.0, item
				c.Emit(OpCodes.Ldarg_0); //ldarg.0, inv
				c.Emit(OpCodes.Ldarg_2); //ldarg.2, slot

				c.EmitDelegate<Action<Item, Item[], int>>((item, inv, slot) =>
				{
					if (ControlInUse && item.type > ItemID.None && item.stack > 0 && !inv[slot].favorited)
						Main.cursorOverride = 6;
				});
			}*/


			//IL_0000: ldarg.0
			//IL_0001: ldarg.2
			//IL_0002: ldelem.ref
			//IL_0003: stloc.0

			if(c.TryGotoNext(MoveType.After,
				e => e.MatchLdarg(0),
				e => e.MatchLdarg(2),
				e => e.MatchLdelemRef(),
				e => e.MatchStloc(0)))
			{
				c.Emit(OpCodes.Ldloc_0); //ldloc.0, item
				c.Emit(OpCodes.Ldarg_0); //ldarg.0, inv
				c.Emit(OpCodes.Ldarg_2); //ldarg.2, slot

				c.EmitDelegate<Action<Item, Item[], int>>((item, inv, slot) =>
				{
					if (ControlInUse && item.type > ItemID.None && item.stack > 0 && !inv[slot].favorited)
						Main.cursorOverride = 6; //The trash cursor override 
				});
			}
		}

		//TODO make IL
		//I method swapped because I could not get IL working
		private void On_LeftClick(On.Terraria.UI.ItemSlot.orig_LeftClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
		{
			Main.NewText(Main.cursorOverride);
			if (!(bool)overrideLeftClick.Invoke(null, new object[] { inv, context, slot }))
			{
				inv[slot].newAndShiny = false;
				Player player = Main.player[Main.myPlayer];
				bool flag = false;
				if ((uint)context <= 4u)
				{
					flag = (player.chest == -1);
				}
				if (ControlInUse & flag)
				{
					sellOrTrash.Invoke(null, new object[] { inv, context, slot });
				}
				else if (player.itemAnimation == 0 && player.itemTime == 0)
				{
					switch (ItemSlot.PickItemMovementAction(inv, context, slot, Main.mouseItem))
					{
						case 0:
							if (context == 6 && Main.mouseItem.type != ItemID.None)
							{
								inv[slot].SetDefaults(0, false);
							}
							Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
							if (inv[slot].stack > 0)
							{
								if (context != 0)
								{
									if ((uint)(context - 8) <= 4u || (uint)(context - 16) <= 1u)
									{
										AchievementsHelper.HandleOnEquip(player, inv[slot], context);
									}
								}
								else
								{
									AchievementsHelper.NotifyItemPickup(player, inv[slot]);
								}
							}
							if (inv[slot].type == ItemID.None || inv[slot].stack < 1)
							{
								inv[slot] = new Item();
							}
							if (Main.mouseItem.IsTheSameAs(inv[slot]))
							{
								Utils.Swap<bool>(ref inv[slot].favorited, ref Main.mouseItem.favorited);
								if (inv[slot].stack != inv[slot].maxStack && Main.mouseItem.stack != Main.mouseItem.maxStack)
								{
									if (Main.mouseItem.stack + inv[slot].stack <= Main.mouseItem.maxStack)
									{
										inv[slot].stack += Main.mouseItem.stack;
										Main.mouseItem.stack = 0;
									}
									else
									{
										int num3 = Main.mouseItem.maxStack - inv[slot].stack;
										inv[slot].stack += num3;
										Main.mouseItem.stack -= num3;
									}
								}
							}
							if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack < 1)
							{
								Main.mouseItem = new Item();
							}
							if (Main.mouseItem.type > ItemID.None || inv[slot].type > ItemID.None)
							{
								Recipe.FindRecipes();
								Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
							}
							if (context == 3 && Main.netMode == NetmodeID.MultiplayerClient)
							{
								NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, player.chest, slot, 0f, 0f, 0, 0, 0);
							}
							break;
						case 1:
							if (Main.mouseItem.stack == 1 && Main.mouseItem.type > ItemID.None && inv[slot].type > ItemID.None && inv[slot].IsNotTheSameAs(Main.mouseItem))
							{
								Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
								Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
								if (inv[slot].stack > 0)
								{
									if (context != 0)
									{
										if ((uint)(context - 8) <= 4u || (uint)(context - 16) <= 1u)
										{
											AchievementsHelper.HandleOnEquip(player, inv[slot], context);
										}
									}
									else
									{
										AchievementsHelper.NotifyItemPickup(player, inv[slot]);
									}
								}
							}
							else if (Main.mouseItem.type == ItemID.None && inv[slot].type > ItemID.None)
							{
								Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
								if (inv[slot].type == ItemID.None || inv[slot].stack < 1)
								{
									inv[slot] = new Item();
								}
								if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack < 1)
								{
									Main.mouseItem = new Item();
								}
								if (Main.mouseItem.type <= ItemID.None && inv[slot].type <= ItemID.None)
								{
									break;
								}
								Recipe.FindRecipes();
								Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
							}
							else if (Main.mouseItem.type > ItemID.None && inv[slot].type == ItemID.None)
							{
								if (Main.mouseItem.stack == 1)
								{
									Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
									if (inv[slot].type == ItemID.None || inv[slot].stack < 1)
									{
										inv[slot] = new Item();
									}
									if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack < 1)
									{
										Main.mouseItem = new Item();
									}
									if (Main.mouseItem.type > ItemID.None || inv[slot].type > ItemID.None)
									{
										Recipe.FindRecipes();
										Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
									}
								}
								else
								{
									Main.mouseItem.stack--;
									inv[slot].SetDefaults(Main.mouseItem.type, false);
									Recipe.FindRecipes();
									Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
								}
								if (inv[slot].stack > 0)
								{
									if (context != 0)
									{
										if ((uint)(context - 8) <= 4u || (uint)(context - 16) <= 1u)
										{
											AchievementsHelper.HandleOnEquip(player, inv[slot], context);
										}
									}
									else
									{
										AchievementsHelper.NotifyItemPickup(player, inv[slot]);
									}
								}
							}
							break;
						case 2:
							if (Main.mouseItem.stack == 1 && Main.mouseItem.dye > 0 && inv[slot].type > ItemID.None && inv[slot].type != Main.mouseItem.type)
							{
								Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
								Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
								if (inv[slot].stack > 0)
								{
									if (context != 0)
									{
										if ((uint)(context - 8) <= 4u || (uint)(context - 16) <= 1u)
										{
											AchievementsHelper.HandleOnEquip(player, inv[slot], context);
										}
									}
									else
									{
										AchievementsHelper.NotifyItemPickup(player, inv[slot]);
									}
								}
							}
							else if (Main.mouseItem.type == ItemID.None && inv[slot].type > ItemID.None)
							{
								Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
								if (inv[slot].type == ItemID.None || inv[slot].stack < 1)
								{
									inv[slot] = new Item();
								}
								if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack < 1)
								{
									Main.mouseItem = new Item();
								}
								if (Main.mouseItem.type <= ItemID.None && inv[slot].type <= ItemID.None)
								{
									break;
								}
								Recipe.FindRecipes();
								Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
							}
							else if (Main.mouseItem.dye > 0 && inv[slot].type == ItemID.None)
							{
								if (Main.mouseItem.stack == 1)
								{
									Utils.Swap<Item>(ref inv[slot], ref Main.mouseItem);
									if (inv[slot].type == ItemID.None || inv[slot].stack < 1)
									{
										inv[slot] = new Item();
									}
									if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack < 1)
									{
										Main.mouseItem = new Item();
									}
									if (Main.mouseItem.type > ItemID.None || inv[slot].type > ItemID.None)
									{
										Recipe.FindRecipes();
										Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
									}
								}
								else
								{
									Main.mouseItem.stack--;
									inv[slot].SetDefaults(Main.mouseItem.type, false);
									Recipe.FindRecipes();
									Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
								}
								if (inv[slot].stack > 0)
								{
									if (context != 0)
									{
										if ((uint)(context - 8) <= 4u || (uint)(context - 16) <= 1u)
										{
											AchievementsHelper.HandleOnEquip(player, inv[slot], context);
										}
									}
									else
									{
										AchievementsHelper.NotifyItemPickup(player, inv[slot]);
									}
								}
							}
							break;
						case 3:
							if (PlayerHooks.CanBuyItem(player, Main.npc[player.talkNPC], inv, inv[slot]))
							{
								Main.mouseItem = inv[slot].Clone();
								Main.mouseItem.stack = 1;
								if (inv[slot].buyOnce)
								{
									Main.mouseItem.value *= 5;
								}
								else
								{
									Main.mouseItem.Prefix(-1);
								}
								Main.mouseItem.position = player.Center - new Vector2(Main.mouseItem.width, Main.mouseItem.headSlot) / 2f;
								ItemText.NewText(Main.mouseItem, Main.mouseItem.stack, false, false);
								if (inv[slot].buyOnce && --inv[slot].stack <= 0)
								{
									inv[slot].SetDefaults(0, false);
								}
								if (inv[slot].value > 0)
								{
									Main.PlaySound(SoundID.Coins, -1, -1, 1, 1f, 0f);
								}
								else
								{
									Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
								}
								PlayerHooks.PostBuyItem(player, Main.npc[player.talkNPC], inv, Main.mouseItem);
							}
							break;
						case 4:
							if (PlayerHooks.CanSellItem(player, Main.npc[player.talkNPC], inv, Main.mouseItem))
							{
								Chest chest = Main.instance.shop[Main.npcShop];
								if (player.SellItem(Main.mouseItem.value, Main.mouseItem.stack))
								{
									int num = chest.AddShop(Main.mouseItem);
									Main.mouseItem.SetDefaults(0, false);
									Main.PlaySound(SoundID.Coins, -1, -1, 1, 1f, 0f);
									PlayerHooks.PostSellItem(player, Main.npc[player.talkNPC], chest.item, chest.item[num]);
								}
								else if (Main.mouseItem.value == 0)
								{
									int num2 = chest.AddShop(Main.mouseItem);
									Main.mouseItem.SetDefaults(0, false);
									Main.PlaySound(SoundID.Grab, -1, -1, 1, 1f, 0f);
									PlayerHooks.PostSellItem(player, Main.npc[player.talkNPC], chest.item, chest.item[num2]);
								}
								Recipe.FindRecipes();
							}
							break;
					}
					if ((uint)context > 2u && context != 5)
					{
						inv[slot].favorited = false;
					}
				}
			}
		}
	}
}
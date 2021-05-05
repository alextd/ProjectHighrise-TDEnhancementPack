using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using HarmonyLib;

using Game.Session.Input;
using Game.Session.Entities;
using Game.Session.Entities.Components;
using Game.Session.Board;
using Game.Session.Sim;
using Game.Systems.Events;

//Paint Whole Floor was implemented for wires, why not all rooms?

namespace BetterPlacement
{
	public struct WholeFloorSize
	{
		public GridPos left, right; public bool success, startLeft;

		public static implicit operator WholeFloorSize(bool b)
		{
			return new WholeFloorSize() { success = b};
		}

		public int Count() => right.x - left.x + 1;
	}
	[HarmonyPatch(typeof(AbstractPaintInputMode), "TryPaint")]
	public static class PaintWholeFloor
	{
		//class AbstractPaintInputMode
		//protected override void DoMouseUp(Vector2 scrpos, bool wasDragging)
		public static bool Prefix(AbstractPaintInputMode __instance, bool dragging)
		{
			if (__instance is BuyEntityInputMode buyMode)
			{
				if (KeyboardShortcutManager.shift)
				{
					TryPaintWholeFloor(buyMode, dragging: dragging);
					return false;
				}
			}
			return true;
		}

		public static void TryPaintWholeFloor(BuyEntityInputMode buyMode, bool dragging)
		{
			//Basically a copy of the same from AddPipeInputMode: protected void TryPaintWholeFloor(bool dragging)
			buyMode._successful = true;
			if (!buyMode._isDown || GUIUtility.hotControl != 0 ||
				!buyMode._cursor.config.placement.multiplace)
				buyMode._successful = false;

			WholeFloorSize floorSize = buyMode.CanPaintWholeFloor();
			if (!floorSize.success)
				buyMode._successful = false;

			if(!buyMode.CanPayWholeFloor(floorSize))
				buyMode._successful = false;

			if (buyMode._successful)
			{
				buyMode.UpdateCursor();
				buyMode.PayAndPaintWholeFloor(floorSize);//CreateCursor sets _successful = false
				buyMode._paintcount++;
			}
			else
			{
				buyMode.OnFailedPaint(dragging);
			}
			if (!dragging)
			{
				buyMode._mouseoverStarted = false;
			}
		}

		public static WholeFloorSize CanPaintWholeFloor(this BuyEntityInputMode buyMode)
		{
			if (!buyMode.IsBuildable(buyMode._cursorpos))
				return false;

			WholeFloorSize result = true;
			result.left = buyMode.FindEnd(buyMode._cursorpos, -1);
			result.right = buyMode.FindEnd(buyMode._cursorpos, 1);
			//Start at whichever end the cursor is closer to.
			result.startLeft = (buyMode._cursorpos.x - result.left.x) < (result.right.x - buyMode._cursorpos.x);
			//Adjust right end for room width.
			result.right.x += buyMode.Width() - 1;

			int cellCount = result.Count();
			int buildCount = cellCount / buyMode.Width();
			if (buildCount == 0)
				return false;
			
			return result;
		}
		private static GridPos FindEnd(this BuyEntityInputMode buyMode, GridPos start, int dx)
		{
			GridPos end,gridPos = start;
			do
			{
				end = gridPos;
				gridPos = end.Add(dx, 0);
			}
			while (buyMode.IsBuildable(gridPos));
			return end;
		}


		public static void PayAndPaintWholeFloor(this BuyEntityInputMode buyMode, WholeFloorSize wholeFloor)
		{
			int width = buyMode.Width();
			int numPossible = wholeFloor.Count() / width;
			Game.Game.ctx.sim.player.DoAdd(buyMode.GetBuyCost() * numPossible, Reason.BuildCost, (GridPosF)buyMode._cursorpos);

			GridPos cursorPos = buyMode._cursorpos;//The left side of the mouse-placement unit. Good enough to use.

			for (GridPos buildPos = wholeFloor.startLeft ? wholeFloor.left : new GridPos(wholeFloor.right.x - width + 1, wholeFloor.right.y);
				buildPos.x + width - 1 <= wholeFloor.right.x && buildPos.x >= wholeFloor.left.x;
				buildPos.x += wholeFloor.startLeft ? width:-width)
			{
				GridCell buildCell = Game.Game.ctx.board.grid.FindGridCell(buildPos);
				//So much relies on _cursor so let's set the position there. It gets rebuilt after placement anyway

				buyMode.DoMoveCursor(buildPos, buyMode._grid.GridPosToWorldPos(buildPos));
				buyMode._cursorpos = buildPos;

				VfxUtil.MaybeSpawnBuildFx(buyMode._cursor.config, buildPos);

				if (buyMode._cursor.config.placement.lobbycandidate)
				{
					buyMode.ClearLobby(buildCell);
				}

				//PlaceCursorEntity();
				PlacementComponent placement = buyMode._cursor.components.placement;
				placement.Insert(buildPos);
				buyMode._cursor.enabled = true;
				buyMode._cursor.components.sprite.ClearLayerOverride();
				placement.StartBuilding(placement.NeedsBuiltInstantly());

				//HandleLobby doesn't really need to be done until they're all placed.
				//Plus, only elevators and closets have lobbycandidate and those aren't Whole-Floor placed anyway.
				if (!buyMode._cursor.config.placement.lobbycandidate || !buyMode.HandleLobby(buildCell))
				{
					Game.Game.ctx.events.Send(GameEventType.EntityAfterCreatedByPlayer, buyMode._cursor.id, buildPos);
				}
				buyMode.CreateCursor();
			}

			buyMode.UpdateCursor();
			buyMode.PlayPaintSound(success: true);
		}


		public static bool CanPayWholeFloor(this BuyEntityInputMode buyMode, WholeFloorSize wholeFloor) =>
			Game.Game.ctx.sim.player.CanSpend(buyMode.GetBuyCost() * (wholeFloor.Count() / buyMode.Width()));

		//Can the room be placed here:
		public static bool IsBuildable(this BuyEntityInputMode buyMode, GridPos pos)
		{
			if (Game.Game.serv.globals.settings.cheats.allowplacementanywhere)
				return true;

			return buyMode._grid.CanInsertIntoFootprint(pos, buyMode._cursor.config, buyMode._cursor.config.placement.reqs) == GridCell.GridAddResult.Success;
		}

		public static int Width(this BuyEntityInputMode buyMode) =>
			buyMode._cursor.config.placement.size.x;

		//Todo: CanPay with shift checks whole floor.
		/*
		protected override bool CanPay()
		{
			return Game.ctx.sim.player.CanSpend(GetBuyCost());
		}
		//ShouldShowPositiveCursor check shift for whole floor
		*/
	}
}



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
		public GridCell left, right; public bool success, startLeft;

		public static implicit operator WholeFloorSize(bool b)
		{
			return new WholeFloorSize() { success = b};
		}
	}
	[HarmonyPatch(typeof(AbstractPaintInputMode), "DoMouseUp")]
	public static class PaintWholeFloor
	{
		//class AbstractPaintInputMode
		//protected override void DoMouseUp(Vector2 scrpos, bool wasDragging)
		public static bool Prefix(AbstractPaintInputMode __instance, Vector2 scrpos, bool wasDragging)
		{
			if (__instance is BuyEntityInputMode buyMode)
			{
				if (KeyboardShortcutManager.shift)
				{
					__instance.UpdateCursorPosition(scrpos);
					Log.Debug("Doing TryPaintWholeFloor!");
					TryPaintWholeFloor(buyMode, dragging: false);
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

			if (buyMode._successful)
			{
				WholeFloorSize floorSize = buyMode.CanPaintWholeFloor();
				if (floorSize.success)
				{
					buyMode.UpdateCursor();
					buyMode.PayAndPaintWholeFloor(floorSize);
					buyMode._paintcount++;
				}
				else
					buyMode._successful = false;
			}
			else
			{
				Log.Debug("Failed PaintWholeFloor");
				buyMode.OnFailedPaint(dragging);
			}
			if (!dragging)
			{
				buyMode._mouseoverStarted = false;
			}
		}

		public static WholeFloorSize CanPaintWholeFloor(this BuyEntityInputMode buyMode)
		{
			Log.Debug("CanPaintWholeFloor?");
			GridCell gridCell = Game.Game.ctx.board.grid.FindGridCellOrNull(buyMode._cursorpos);
			if (!gridCell.IsEmptyFloor())
				return false;

			WholeFloorSize result = true;
			result.left = FindEnd(gridCell, -1);
			result.right = FindEnd(gridCell, 1);

			int cellCount = result.right.pos.x - result.left.pos.x + 1;
			int buildCount = cellCount / buyMode.Width();
			if (buildCount == 0)
				return false;
			result.startLeft = true;//TODO
			Log.Debug($"CanPaintWholeFloor: {result.left.pos.x}-{result.right.pos.x} fits {buildCount}");
			return result;
		}
		private static GridCell FindEnd(GridCell start, int dx)
		{
			GridCell end = start;
			for (GridCell gridCell = start; gridCell.IsEmptyFloor(); gridCell = Game.Game.ctx.board.grid.FindGridCellOrNull(end.pos.Add(dx, 0)))
				end = gridCell;
			return end;
		}


		public static void PayAndPaintWholeFloor(this BuyEntityInputMode buyMode, WholeFloorSize wholeFloor)
		{
			int numPossible = (wholeFloor.right.pos.x - wholeFloor.left.pos.x + 1) / buyMode.Width();
			Game.Game.ctx.sim.player.DoAdd(buyMode.GetBuyCost() * numPossible, Reason.BuildCost, (GridPosF)buyMode._cursorpos);

			GridPos cursorPos = buyMode._cursorpos;//The left side of the mouse-placement unit. Good enough to use.
			VfxUtil.MaybeSpawnBuildFx(buyMode._cursor.config, cursorPos);

			Log.Debug($"PayAndPaintWholeFloor: {wholeFloor.left.pos.x}-{wholeFloor.right.pos.x} fits {numPossible} costs {buyMode.GetBuyCost() * numPossible}");
			//todo:use wholeFloor.startLeft
			for (GridPos buildPos = wholeFloor.left.pos; buildPos.x + buyMode.Width()-1 <= wholeFloor.right.pos.x; buildPos.x += buyMode.Width())
			{
				Log.Debug($"Building at {buildPos}");
				GridCell buildCell = Game.Game.ctx.board.grid.FindGridCell(buildPos);
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


		public static bool CanPay(this BuyEntityInputMode buyMode, int count) =>
			Game.Game.ctx.sim.player.CanSpend(buyMode.GetBuyCost() * count);

		public static bool IsEmptyFloor(this GridCell cell) =>
			cell != null && cell.hasFloor && !cell.hasUnit;

		public static int Width(this BuyEntityInputMode buyMode) =>
			buyMode._cursor.config.placement.size.x;

		//Todo: CanPay with shift checks whole floor.
		/*
		protected override bool CanPay()
		{
			return Game.ctx.sim.player.CanSpend(GetBuyCost());
		}
		*/
	}
}



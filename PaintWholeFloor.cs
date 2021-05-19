using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using HarmonyLib;

using Game.Session.Input;
using Game.Session.Entities;
using Game.Session.Entities.Config;
using Game.Session.Entities.Components;
using Game.Session.Board;
using Game.Session.Sim;
using Game.Systems.Events;

//Paint Whole Floor was implemented for wires, why not all rooms?

namespace BetterPlacement
{
	public struct WholeFloorSize
	{
		public GridPos left, right; public bool success, startLeft, vertical; public int width;

		public static implicit operator WholeFloorSize(bool b)
		{
			return new WholeFloorSize() { success = b};
		}

		public int Count() => (right.x - left.x + 1)/width;
	}
	[HarmonyPatch(typeof(AbstractPaintInputMode), "TryPaint")]
	public static class PaintWholeFloor
	{
		//class AbstractPaintInputMode
		//protected override void DoMouseUp(Vector2 scrpos, bool wasDragging)
		public static bool Prefix(AbstractPaintInputMode __instance, bool dragging)
		{
			if (__instance is BuyEntityInputMode buyMode && !(buyMode is MoveInUnitInputMode))
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
			Log.Debug($"trying whole floor for {buyMode}:{buyMode.cursorTemplateName}");
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

		public static bool VerticalFilling(Entity e)
		{
			return e.config.closet?.type == Game.Session.Entities.Config.ClosetType.Closet ||
				(e.config.ident.template is string template && (
				template == "path-escalator" ||//wtf are those doing in the game?
				template == "path-stairs" ||
				template == "path-elevator-wide" ||
				template == "path-elevator-fancy" ||
				template == "path-elevator"));
		}

		public static WholeFloorSize CanPaintWholeFloor(this BuyEntityInputMode buyMode)
		{
			if (!buyMode.IsBuildable(buyMode._cursorpos))
				return false;

			if (!buyMode.CanStackElevator())
				return false;

			WholeFloorSize result = true;

			result.vertical = VerticalFilling(buyMode._cursor);
			Log.Debug($"Vertical : {result.vertical} for {buyMode._cursor.config.ident.name}");
			result.left = buyMode.FindEnd(buyMode._cursorpos, -1, result.vertical);
			result.right = buyMode.FindEnd(buyMode._cursorpos, 1, result.vertical);
			if (result.vertical)
			{
				//Start closest to cursor. Theoretically there could be 2 or 3-height things taht layer vertical so why not
				result.startLeft = (buyMode._cursorpos.y - result.left.y) < (result.right.y - buyMode._cursorpos.y);
				//Adjust right end for room height.
				result.width = buyMode.Height();
				result.right.x += result.width - 1;
			}
			else
			{
				//Start at whichever end the cursor is closer to.
				result.startLeft = (buyMode._cursorpos.x - result.left.x) < (result.right.x - buyMode._cursorpos.x);
				//Adjust right end for room width.
				result.width = buyMode.Width();
				result.right.x += result.width - 1;
			}

			if (result.Count() == 0)
				return false;
			
			return result;
		}

		//You know I thought I'd extend this to allow clicking above elevators by a few floors, but requiring you to click next to an existing elevator isn't so bad.
		//This is not checking when planning since elevators don't exist YET so it would be false - but the plan is to build up to the point so it should work
		//This is not checked when building, which isn't a big problem, but technically for some reason, you can't PLACE a fancy elevator right above a normal elevator
		//... but you can EXTEND it down to attach to a normal elevator. So the fact you can't place it is moot. That would be stupid to do anyway.
		public static bool CanStackElevator(this BuyEntityInputMode buyMode)
		{
			if (buyMode is BuyElevatorInputMode elevBuyMode)
			{
				switch (elevBuyMode._cursortemplate?.path.stacking)
				{
					case PathConfig.Stacking.ElevatorFromGroundFloor:
						return elevBuyMode._cursorpos.y == 0 || elevBuyMode.IsStacked(-1) || elevBuyMode.IsStacked(1);
					case PathConfig.Stacking.ElevatorFromAnyFloor:
						return elevBuyMode.IsStacked(-1) || elevBuyMode.IsStacked(1) || (elevBuyMode.NotTouchingElevator(1) && elevBuyMode.NotTouchingElevator(-1));
				}
			}
			return true;
		}
		private static GridPos FindEnd(this BuyEntityInputMode buyMode, GridPos start, int dx, bool vertical = false)
		{
			GridPos end,gridPos = start;
			do
			{
				end = gridPos;
				gridPos = end.Add(vertical?0:dx, vertical?dx:0);
			}
			while (buyMode.IsBuildable(gridPos));
			return end;
		}


		public static void PayAndPaintWholeFloor(this BuyEntityInputMode buyMode, WholeFloorSize wholeFloor)
		{
			int numPossible = wholeFloor.Count();
			int width = wholeFloor.width;
			Game.Game.ctx.sim.player.DoAdd(buyMode.GetBuyCost() * numPossible, Reason.BuildCost, (GridPosF)buyMode._cursorpos);

			GridPos cursorPos = buyMode._cursorpos;//The left side of the mouse-placement unit. Good enough to use.

			if (wholeFloor.vertical)
			{
				if (wholeFloor.startLeft)
					for (GridPos buildPos = wholeFloor.left; buildPos.y + width - 1 <= wholeFloor.right.y; buildPos.y += width)
						buyMode.PaintOne(buildPos);
				else
					for (GridPos buildPos = wholeFloor.right.Add(0, 1- width); buildPos.y >= wholeFloor.left.y; buildPos.y -= width)
						buyMode.PaintOne(buildPos);
			}
			else
			{
				if (wholeFloor.startLeft)
					for (GridPos buildPos = wholeFloor.left ; buildPos.x + width - 1 <= wholeFloor.right.x; buildPos.x += width)
						buyMode.PaintOne(buildPos);
				else
					for (GridPos buildPos = wholeFloor.right.Add(1-width,0); buildPos.x >= wholeFloor.left.x; buildPos.x -= width)
						buyMode.PaintOne(buildPos);
			}

			buyMode.UpdateCursor();
			buyMode.PlayPaintSound(success: true);
		}

		public static void PaintOne(this BuyEntityInputMode buyMode, GridPos buildPos)
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


		public static bool CanPayWholeFloor(this BuyEntityInputMode buyMode, WholeFloorSize wholeFloor) =>
			Game.Game.ctx.sim.player.CanSpend(buyMode.GetBuyCost() * wholeFloor.Count());

		//Can the room be placed here ( aka CanPaint ). Doesn't consider elevator stacking.
		public static bool IsBuildable(this BuyEntityInputMode buyMode, GridPos pos)
		{
			if (Game.Game.serv.globals.settings.cheats.allowplacementanywhere)
				return true;

			return buyMode._grid.CanInsertIntoFootprint(pos, buyMode._cursor.config, buyMode._cursor.config.placement.reqs) == GridCell.GridAddResult.Success;
		}

		public static int Width(this BuyEntityInputMode buyMode) =>
			buyMode._cursor.config.placement.size.x;
		public static int Height(this BuyEntityInputMode buyMode) =>
			buyMode._cursor.config.placement.size.y;

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



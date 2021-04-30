using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using HarmonyLib;

using Game.Session.Input;
using Game.Session.Entities;
using Game.Session.Board;
using Game.Session.Sim;

//Paint Whole Floor was implemented for wires, why not all rooms?
/*
namespace BetterPlacement
{
	[HarmonyPatch(typeof(AbstractPaintInputMode), "DoMouseUp")]
	class PaintWholeFloor
	{
		//class AbstractPaintInputMode
		//protected override void DoMouseUp(Vector2 scrpos, bool wasDragging)
		public static bool Prefix(AbstractPaintInputMode __instance, Vector2 scrpos, bool wasDragging)
		{
			if(__instance is BuyEntityInputMode buyMode)
			{
				if (KeyboardShortcutManager.shift)
				{
					__instance.UpdateCursorPosition(scrpos);
					//TODO: TryPaintWholeFloor(dragging: false);
					return false;
				}
			}
			return true;
		}

		public static void TryPaintWholeFloorBuyEntity(BuyEntityInputMode buyMode, bool dragging)
		{
			//Basically a copy of the same from AddPipeInputMode: protected void TryPaintWholeFloor(bool dragging)
			buyMode._successful = true;
			if (!buyMode._isDown || GUIUtility.hotControl != 0)
			{
				buyMode._successful = false;
			}
			if (!CanPaintWholeFloor(buyMode, out GridCell left, out GridCell right))
			{
				buyMode._successful = false;
			}
			if (buyMode._successful)
			{
				buyMode.UpdateCursor();
				PayAndPaintWholeFloor(buyMode, left, right);
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

		public static void PayAndPaintWholeFloor(BuyEntityInputMode buyMode, GridCell leftEnd, GridCell rightEnd)
		{
			int numPossible = (rightEnd.pos.x - leftEnd.pos.x + 1) / buyMode._cursor.config.placement.size.x;
			Money income = (int)(Money)buyMode._cursor.components.unit.GetBuildCost() * numPossible;
			Game.Game.ctx.sim.player.DoAdd(income, Reason.BuildCost, (GridPosF)buyMode._cursorpos);
			GridPos pos = leftEnd.pos;
			do
			{
				Game.Game.ctx.sim.infra.DoPlacePipe(type, pos);
			}
			while (pos.x++ != rightEnd.pos.x);
			buyMode.UpdateCursor();
			buyMode.PlayPaintSound(success: true);
		}

		public static bool CanPaintWholeFloor(BuyEntityInputMode buyMode)
		{
			GridCell left;
			return CanPaintWholeFloor(buyMode, out left, out left);
		}

		protected static bool CanPay(BuyEntityInputMode buyMode, int count)
		{
			return Game.Game.ctx.sim.player.CanSpend(buyMode.GetBuyCost()* count);
		}

		public static bool CanPaintWholeFloor(BuyEntityInputMode buyMode, out GridCell left, out GridCell right)
		{
			left = (right = null);
			RangeUtil.FloorPaintingResult floorPaintingResult = RangeUtil.CanPaintWholeFloor(_def.type, _cursorpos);
			if (!floorPaintingResult.success)
			{
				return false;
			}
			int num = floorPaintingResult.right.pos.x - floorPaintingResult.left.pos.x + 1;
			if (num > 0 && (floorPaintingResult.connectorLeft || floorPaintingResult.connectorRight))
			{
				Money cost = (int)(Money)_def.buildcost.Evaluate(_cursor) * num;
				left = floorPaintingResult.left;
				right = floorPaintingResult.right;
				return Game.Game.ctx.sim.player.CanSpend(cost);
			}
			return false;
		}
	}
	}
}


*/
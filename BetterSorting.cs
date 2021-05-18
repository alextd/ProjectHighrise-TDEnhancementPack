using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Sim;
using static Game.Session.Sim.PerformerRecord.UIState;
using Game.UI.Session.Hotels;

namespace BetterPlacement
{
	//Performer sorting. It was sorted in two places? Let's do one.
	[HarmonyPatch(typeof(PerformerRecord), nameof(PerformerRecord.SortByBookedThenCashCost))]
	class PerformerSorting
	{
		//public static int SortByBookedThenCashCost(PerformerRecord a, PerformerRecord b)
		public static bool Prefix(ref int __result, PerformerRecord a, PerformerRecord b)
		{
			__result = ActualCompare(a, b);
			return false;
		}
		public static int ActualCompare(PerformerRecord a, PerformerRecord b)
		{
			PerformerRecord.UIState aS = a.GetBookingState();
			PerformerRecord.UIState bS = b.GetBookingState();
			if (aS == Locked && bS != Locked)
			{
				return 1;
			}
			if (bS == Locked && aS != Locked)
			{
				return -1;
			}
			if (a.isBooked && !b.isBooked)
			{
				return 1;	//flipped the booked order. Booked go below. I don't need to see things I've booked at the top of the list.
			}
			if (b.isBooked && !a.isBooked)
			{
				return -1;
			}
			if (a.cashcost != b.cashcost)
			{
				return a.cashcost - b.cashcost;
			}
			return string.Compare(a.name, b.name);//Lucky Aaron A. Aaronson always gets first pick on gigs
		}
	}

	//Sort elsewhere, filter here:
	[HarmonyPatch(typeof(PerformerListPopup), nameof(PerformerListPopup.GetFilteredSortedPerformers))]
	public static class DontDoubleSort
	{
		//private List<PerformerRecord> GetFilteredSortedPerformers()
		public static bool Prefix(ref List<PerformerRecord> __result, PerformerListPopup __instance)
		{
			Game.Game.ctx.sim.eventspaces.SortPerformers();
			__result = Game.Game.ctx.sim.eventspaces.data.performers
				.FindAll(rec => __instance.RecMatchesAllTagLists(rec, __instance.MakeTagLists()));

			return false;
		}
	}
}

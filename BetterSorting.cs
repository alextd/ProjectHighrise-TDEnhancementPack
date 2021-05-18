using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Sim;
using static Game.Session.Sim.PerformerRecord.UIState;
using Game.UI.Session.Hotels;
using Game.UI.Session.MoveIns;
using Game.Services.Settings;

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

	//I guess 3.5 has no tuple
	public class TypeServiceDef
	{
		public bool running, needed, wanted;
		public ServicesDefinitionDetail def;
	}
	public class TSDComparer : IComparer<TypeServiceDef>
	{
		public int Compare(TypeServiceDef a, TypeServiceDef b)
		{
			if (a.wanted && !b.wanted) return -1;
			if (!a.wanted && b.wanted) return 1;
			if (a.needed && !b.needed) return -1;
			if (!a.needed && b.needed) return 1;
			if (a.running && !b.running) return 1;
			if (!a.running && b.running) return -1;
			return 0;//Keep original sorting?
		}
	}

	[HarmonyPatch(typeof(MoveInServicesDialog), nameof(MoveInServicesDialog.Refresh))]
	public static class SortServicesByNeed
	{
		//private void Refresh()
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
		{
			LocalBuilder tempListLB = ilGen.DeclareLocal(typeof(List<ServicesDefinitionDetail>));

			FieldInfo entriesInfo = AccessTools.Field(typeof(ServicesDefinition), nameof(ServicesDefinition.entries));

			foreach(var inst in instructions)
			{
				yield return inst;
				if(inst.LoadsField(entriesInfo))
				{
					//entries
					yield return new CodeInstruction(OpCodes.Ldloca, tempListLB);//entries, ref sorted
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SortServicesByNeed), nameof(SortItFFS)));//SortITFFS(entries, ref sorted) => sorted
				}
			}
		}

		public static List<ServicesDefinitionDetail> SortItFFS(List<ServicesDefinitionDetail> list, ref List<ServicesDefinitionDetail> sorted)
		{
			if (sorted != null)
				return sorted;
			List<TypeServiceDef> keyedList = new List<TypeServiceDef>();
			foreach(ServicesDefinitionDetail def in list)
			{
				SupportTaskType supportTaskType = Game.Game.ctx.sim.support.FindSingleSupportTaskProvidedByService(Game.Game.ctx.entityman.FindTemplate(def.template));

				bool running = Game.Game.ctx.sim.support.IsServiceUnitAvailable(supportTaskType);
				bool needed = Game.Game.ctx.sim.support.IsServiceNeededAndTimedOut(supportTaskType, yesterday: true, today: true);
				bool wanted = Game.Game.ctx.entityman.MakeListOfAllUnits().Any(e => e.components.unit.DoesNeedService(supportTaskType));

				keyedList.Add(new TypeServiceDef() { running = running, needed = needed, wanted = wanted,def = def });
			}

			sorted = new List<ServicesDefinitionDetail>(keyedList.OrderBy(x => x, new TSDComparer()).Select(t => t.def));
			return sorted;
		}

	}
}

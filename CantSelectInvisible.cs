using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

using Game.Session.Board;
using Game.Session.Entities;

namespace TDEnhancementPack
{
	[HarmonyPatch(typeof(PeepTracker), nameof(PeepTracker.FindPeepUnderCursor))]
	public static class CantSelectInvisible
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase mb, ILGenerator ilg)
		{
			int localIndex = mb.GetMethodBody().LocalVariables.First(lvi => lvi.LocalType == typeof(PeepTracker.FloorInfo)).LocalIndex;
			foreach (var inst in instructions)
			{
				yield return inst;
				if(inst.opcode == OpCodes.Ldloc_3)//There's no Harmony.IsLdloc(int) :/
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CantSelectInvisible), nameof(FilterVisible)));
			}
		}

		public static IEnumerable<Entity> FilterVisible(PeepTracker.FloorInfo floorInfo)
		{
			PeepTracker.FloorInfo filtered = new PeepTracker.FloorInfo();
			foreach (var x in floorInfo.Where(e => e.components.sprite?.IsVisible ?? true))
				filtered.Add(x);
			return filtered;
		}
	}
}

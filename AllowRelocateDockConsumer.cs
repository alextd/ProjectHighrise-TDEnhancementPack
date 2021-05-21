using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Input;
using Game.Session.Entities.Config;
using Game.Systems.Requirements;

namespace BetterPlacement
{
	//relocating e.g. a store when you're at max storage capacity isn't allowed because you can't add more stores, you're at max capacity!! Uh huh.
	[HarmonyPatch(typeof(RelocateEntityInputMode), nameof(RelocateEntityInputMode.CanPaint))]
	public static class AllowRelocateDockConsumer
	{
		//protected override bool CanPaint()
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			FieldInfo reqsInfo = AccessTools.Field(typeof(PlacementConfig), nameof(PlacementConfig.reqs));

			foreach (var inst in instructions)
			{
				yield return inst;
				if (inst.LoadsField(reqsInfo))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AllowRelocateDockConsumer), nameof(RemoveDockRequirement)));
				}
			}
		}

		public static PlacementRequirements RemoveDockRequirement(PlacementRequirements reqs)
		{
			var removed = new PlacementRequirements();
			removed.AddRange(reqs.Where(r => !(r is DockBayRequirement)));
			return removed;
		}
	}
}

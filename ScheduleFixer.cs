using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Services;
using Game.Services.Settings;

//Okay this one is technically a game design choice but why do IT services go sit in their office for 1.5 hours instead of working like every other service?
//They go to lunch anyway so that's their break time
namespace BetterPlacement
{
  [HarmonyPatch(typeof(GlobalSettingsService), nameof(GlobalSettingsService.OnSettingsLoaded))]
	public static class ScheduleFixer
	{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
      MethodInfo TryInfo = AccessTools.Method(typeof(GlobalSettingsService), nameof(GlobalSettingsService.TryCompletion));
      foreach (var inst in instructions)
			{
        if (inst.Calls(TryInfo))
          yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ScheduleFixer), nameof(PostSettingsLoaded)));
        yield return inst;
			}

		}

    public static void PostSettingsLoaded()
		{
      Game.Game.serv.globals.settings.schedules.definitions
        .Find(d => d.name == "schedule-service-equipment")
        .blocks = new List<ScheduleBlock>()
        { new ScheduleBlock() {from = 8, to=9, tasks = new List<string>() {"go-work-at-workstation" } },
          new ScheduleBlock() {from = 9, to=16, tasks = new List<string>() { "go-work-tasks" } },
          new ScheduleBlock() {from = 16, to=17, tasks = new List<string>() {"go-work-at-workstation" } },
          new ScheduleBlock() {from = 17, to=8, tasks = new List<string>() { "go-stay-offsite" } } };
		}

/*
            name "schedule-service-equipment"
            blocks [
                { from 8 to 9 tasks [ go-work-at-workstation ] }
                { from 9 to 12 tasks [ go-work-tasks ] }
                { from 12 to 13.5 tasks [ go-work-at-workstation ] }   <-- KILL ME
                { from 13.5 to 16 tasks [ go-work-tasks ] }
                { from 16 to 17 tasks [ go-work-at-workstation ] }
                { from 17 to 8 tasks [ go-stay-offsite ] }
            ]
*/
}
}

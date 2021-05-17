using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Sim;
using Game.UI.Session;

namespace BetterPlacement
{
	[HarmonyPatch(typeof(GameClock), nameof(GameClock.Initialize))]
	public static class PauseAfterLoad
	{
		//IsPausedBy(UI_PAUSE_SENTINEL)
		//Game.ctx.clock.ToggleSpeedPaused
		//public override void Initialize(SessionContext ctx)
		public static void Postfix(GameClock __instance)
		{
			__instance.Pause(GameClock.UI_PAUSE_SENTINEL);
		}
	}

	//Seems that the clock HUD depends on a tick to set the display so force it after load (new games are 10:00AM anyway)
	[HarmonyPatch(typeof(HUDManager), nameof(HUDManager.Load))]
	public static class ClockAfterLoad
	{
		//public void Load(Hashtable data)
		public static void Postfix(HUDManager __instance)
		{
			__instance.hudclock.RefreshTimeDisplay(true);
		}
	}

	//TODO other things don't change: UI numbers e.g. money.
}

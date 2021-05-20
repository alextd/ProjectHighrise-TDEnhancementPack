using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Tools;

namespace BetterPlacement
{
	[BepInPlugin("TD.ProjectHighrise.BetterPlacement.main", "Better Placement", "0.1.0.0")]
	public class BetterPlacement : BaseUnityPlugin
	{
		// Awake is called once when both the game and the plug-in are loaded
		void Awake()
		{
			Log.logger = Logger;
#if DEBUG
			HarmonyFileLog.Enabled = true;
#endif
			Log.Message($"Wake up! Time is: {DateTime.Now}");
			new Harmony("TD.ProjectHighrise.BetterPlacement.main").PatchAll();
		}
	}

	public static class Log
	{
		public static ManualLogSource logger;
		public static void Message(object data) => logger.LogMessage(data);
		public static void Error(object data) => logger.LogError(data);
		public static void Debug(object data) => logger.LogDebug(data);
		public static void Warning(object data) => logger.LogWarning(data);
	}

	//TODO: Check move in all parking?

	//TOOD: Spacebar clicks the all tenants toggle?
	//TODO: preventative maintenance on elevators

	//TODO: Sort placing services by need
	//TODO: Button to draw utils to entity as needed.

	//TODO: Plan ahead, build over construction floors + rubble
	//TODO: Build and flace floors
	//TODO: Measure # of cells

	//TODO: Actual loan interest

	//TODO: Smarter services taking closest request
	//TODO: interupt people going to office with tasks (builders leaving constructed floors not building the thing you placed right on top of them)

	//TODO: Track time for seated people in ServiceComponent after load

	//TODO: POpup/pause on no service warning.

	//TODO: Ability to view ad list without a building
}


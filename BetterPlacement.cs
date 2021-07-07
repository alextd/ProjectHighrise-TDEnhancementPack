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

	//FIX
	//TODO: Build whole floor going over 2k limit

	//BETTER:
	//TODO: Track time for seated people in ServiceComponent after load
	//TODO: Place whole floor from floor outward so it builds in that direction

	//UI IMPROVEMENTS:
	//TODO: Autosave and quit popup
	//TODO: Measure # of cells
	//TODO: POpup/pause on no service warning.
	//TODO: Ability to view ad list without emptyspace
	//TODO: Util view shows who needs what
	//TODO: Show util usage # when placing wires

	//NEW GAME FEATURES:

	//TODO: Workers re-try tasks that were unpathable - e.g. new floors, all cells tried before elevator was built are pushed to end of queue
	//TODO: Button to buy utils to entity as needed.
	//TODO: Plan ahead - build over construction floors + rubble, place floors as needed
	//TODO: Actual loan interest
	//TODO: Smarter services taking closest request
	//TODO: interupt people going to office with tasks (builders leaving constructed floors not building the thing you placed right on top of them)
	//TODO: preventative maintenance on elevators
	//TODO: Moveouts don't require re-build. Why would someone leaving an apartment mean it has to be rebuit
	//TODO: unocupied apartments don't consume utilites, etc
	//TODO: Refuse contracts (e.g. no offices)
}


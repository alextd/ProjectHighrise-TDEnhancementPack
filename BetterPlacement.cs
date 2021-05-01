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
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Services;

namespace TDEnhancementPack
{
	[HarmonyPatch(typeof(LocalizationService), nameof(LocalizationService.GetValue), typeof(string), typeof(SomaSim.Math.IRandom), typeof(object[]))]
	public static class StringFixes
	{
		public static Dictionary<string, string> fixes = new Dictionary<string, string>()
		{
			//among the sea of 'services', there is one 'store' that is requested. If you try the find the store, you won't find it, BECAUSE THAT'S SUPPOSED TO BE 'SERVICE' NOT A 'STORE'.
			{ "Office supplies store",
				"Office Supplies Services" },
			{ "Expects an office supplies store",
				"Expects office supplies services" }
		};
		//public string GetValue(string key, IRandom rng, params object[] replacements)
		public static string Postfix(string result)
		{
			if (fixes.TryGetValue(result, out string fix))
				return fix;

			return result;
		}
	}
}

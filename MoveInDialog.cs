using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

using Game.Util;
using Game.UI.Session.MoveIns;
using UnityEngine;
using UnityEngine.UI;

namespace BetterPlacement
{
	[HarmonyPatch(typeof(UIUtil), nameof(UIUtil.SetScrollRectElasticity), new Type[] { typeof(GameObject)})]
	//public static void SetScrollRectElasticity(GameObject scrollView)
	public static class ScrollFaster
	{
		public static void Postfix(GameObject scrollView)
		{
			//No clue what why how this should be, but it was too low.
			scrollView.GetComponent<ScrollRect>().scrollSensitivity = 50;
		}
	}

	[HarmonyPatch(typeof(MoveInAdDialog), nameof(MoveInAdDialog.InitializeGameObject))]
	public static class LargerWindowAd
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			UIUtil.ForceRectSizeHack(____go, ____go.GetComponent<RectTransform>().rect.width, 950);
		}
	}
	[HarmonyPatch(typeof(MoveInTenantsDialog), nameof(MoveInTenantsDialog.InitializeGameObject))]
	public static class LargerWindowTenant
	{
		public static void Postfix(GameObject ____go) => LargerWindowAd.Postfix(____go);
	}
}

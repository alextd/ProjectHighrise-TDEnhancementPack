using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.UI.Session;
using Game.UI.Session.MoveIns;
using Game.UI.Session.Economy;
using Game.UI.Session.Quests;
using Game.UI.Session.Hotels;
using Game.Util;
using UnityEngine;

namespace BetterPlacement
{
	public static class OverrideWindowHeight
	{
		public static int h = 950;
	}
	[HarmonyPatch(typeof(MoveInServicesDialog), nameof(MoveInServicesDialog.InitializeGameObject))]
	public static class LargerMoveInServicesDialog
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			//____go is actually a window-sized black cover, within is a panel:
			var windowGO = UIUtil.GetChild(____go, "Panel");
			var tr = windowGO.GetComponent<RectTransform>();
			UIUtil.ForceRectSizeHack(windowGO, tr.rect.width, OverrideWindowHeight.h);
		}
	}

	[HarmonyPatch(typeof(LoansPopup), nameof(LoansPopup.InitializeGameObject))]
	public static class LargerLoansPopup
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			//____go is actually a window-sized black cover, within is a panel:
			var windowGO = UIUtil.GetChild(____go, "Panel");
			var tr = windowGO.GetComponent<RectTransform>();
			UIUtil.ForceRectSizeHack(windowGO, tr.rect.width, OverrideWindowHeight.h);
		}
	}

	[HarmonyPatch(typeof(QuestDialog), nameof(QuestDialog.InitializeGameObject))]
	public static class LargerQuestDialog
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			//____go is actually a window-sized black cover, within is a panel:
			var windowGO = UIUtil.GetChild(____go, "Panel");
			var tr = windowGO.GetComponent<RectTransform>();
			UIUtil.ForceRectSizeHack(windowGO, tr.rect.width, OverrideWindowHeight.h);
		}
	}

	[HarmonyPatch(typeof(HotelRoomsPopup), nameof(HotelRoomsPopup.InitializeGameObject))]
	public static class LargerHotelRoomsPopup
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			//____go is actually a window-sized black cover, within is a panel:
			var windowGO = UIUtil.GetChild(____go, "Panel");
			var tr = windowGO.GetComponent<RectTransform>();
			UIUtil.ForceRectSizeHack(windowGO, tr.rect.width, OverrideWindowHeight.h);
		}
	}

	[HarmonyPatch(typeof(PerformerListPopup), nameof(PerformerListPopup.InitializeGameObject))]
	public static class LargerPerformerListPopup
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			//____go is actually a window-sized black cover, within is a panel:
			var windowGO = UIUtil.GetChild(____go, "Panel");
			var tr = windowGO.GetComponent<RectTransform>();
			UIUtil.ForceRectSizeHack(windowGO, tr.rect.width, OverrideWindowHeight.h);
		}
	}

	[HarmonyPatch(typeof(PerformerRoomPopup), nameof(PerformerRoomPopup.InitializeGameObject))]
	public static class LargerPerformerRoomPopup
	{
		//This seems to be a static value inside Unity resource files so I'm going to statically increase it.
		public static void Postfix(GameObject ____go)
		{
			//____go is actually a window-sized black cover, within is a panel:
			var windowGO = UIUtil.GetChild(____go, "Panel");
			var tr = windowGO.GetComponent<RectTransform>();
			UIUtil.ForceRectSizeHack(windowGO, tr.rect.width, OverrideWindowHeight.h);
		}
	}
}

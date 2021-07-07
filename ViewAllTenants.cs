using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

using Game.UI.Session.MoveIns;
using Game.Session.Sim;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Entities.Data;
using Game.Services.Settings;
using Game.Services;
using Game.Util;

using UnityEngine;
using UnityEngine.UI;

namespace TDEnhancementPack
{
	//View all tenants for all ads, not just one tenant type
	// Rewrite RefreshTenantList for 'all' - toggle on/off somewhere.
	// 	MoveInsDefinition moveInsDefinition = moveins.FindDefByEntryId(_entryid);
	// foreach moveInDef, 
	//	MoveInsEntryStatus moveInsEntryStatus = moveins.FindSavedStatus(_entryid);
	// foreach status,
	//  Accumulate all candidates
	// Continue as in RefreshTenantList with more candidates.
	
	[HarmonyPatch(typeof(MoveInTenantsDialog), nameof(MoveInTenantsDialog.RefreshTenantList))]
	public static class ViewAllTenants
	{
		//private void RefreshTenantList()
		public static bool Prefix(MoveInTenantsDialog __instance, GameObject ____go)
		{
			if(!ToggleButton.doViewAll) return true;
			
			string entryid = __instance._entryid;
			if (entryid == null) return true;

			MoveInsManager moveins = Game.Game.ctx.sim.moveins;

			//Find the def for all the possible moveins:
			MoveInsDefinition moveInsDefinition = moveins.FindDefByEntryId(entryid);

			if (moveInsDefinition.instant != null)
				return true;//This method only handled non-instant, ie, ad-based tenants.

			//Here we fork from the original method:

			Entity entity = __instance.GetEntity();

			//Title (Just "All")
			string text = "All";
			UIUtil.GetTextMesh(____go, "Title").text = Loc.Get("ui.moveins.adready", "type", text);
			//TODO: add toggle button on this (also in base method)

			////Todo: allow decline  all?
			UIUtil.GetChild(____go, "Decline Button").SetActive(false);

			//Do the same thing as original, but for each MoveInsEntry, accumulating into one big list.
			List<UnitInstanceData> candidates = new List<UnitInstanceData>();
			foreach (MoveInsEntry entry in moveInsDefinition.entries)
			{
				MoveInsEntryStatus moveInsEntryStatus = moveins.FindSavedStatus(entry.id);

				if (moveInsEntryStatus.IsNotAdvertising) continue;

				candidates.AddRange(moveInsEntryStatus.candidates);
			}

			GameObject scrollView = UIUtil.GetChild(____go, "Scroll View/Contents");

			int count = candidates.Count;
			UIUtil.EnsureEntryCount(scrollView, count, () => __instance.MakeEntry());
			for (int i = 0; i < count; i++)
				__instance.UpdateEntry(entity, moveInsDefinition, scrollView, candidates, i);
			UIUtil.RepositionScrolledContainerEntries(scrollView);
			UIUtil.ShowOrHideScrollbar(scrollView, UIUtil.GetChild(____go, "Scroll View/Scrollbar"));
			__instance.UpdateImages(moveInsDefinition);

			return false;

		}
	}

	[HarmonyPatch(typeof(MoveInTenantsDialog), nameof(MoveInTenantsDialog.InitializeGameObject))]
	public static class ToggleButton
	{
		public static bool doViewAll = false;

		//protected override void InitializeGameObject()
		public static void Prefix(MoveInTenantsDialog __instance, GameObject ____go)
		{
			Button toggleButton = UIUtil.GetChild(____go, "Title")
				.AddComponent<UnityEngine.UI.Button>();
			var n = toggleButton.navigation;
			n.mode = Navigation.Mode.None;//Don't focus after clicking, which would mean spacebar clicks it ? okay.
			toggleButton.navigation = n;//there has to be a better way :/

			toggleButton.onClick.AddListener(delegate () { 
				doViewAll = !doViewAll;
				__instance.RefreshTenantList();
			}) ;
		}
	}
}


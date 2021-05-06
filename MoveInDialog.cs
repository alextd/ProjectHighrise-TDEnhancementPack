using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

using Assets.Code.Music;
using Game.Util;
using Game.UI.Session.MoveIns;
using Game.Session.Entities;
using Game.Session.Entities.Config;
using Game.Session.Entities.Data;
using Game.Session.Sim;
using Game.Session.Board;
using Game.Services.Settings;
using Game.Systems.Events;
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
	public static class LargerWindow
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
		public static void Postfix(GameObject ____go) => LargerWindow.Postfix(____go);
	}

	[HarmonyPatch(typeof(MoveInTenantsDialog), nameof(MoveInTenantsDialog.OnEntryClick))]
	public static class MoveInAllTenants
	{
		//private void OnEntryClick(TenantEntryContext ctx)
		public static bool Prefix(MoveInTenantsDialog __instance, MoveInTenantsDialog.TenantEntryContext ctx)
		{
			if (KeyboardShortcutManager.shift)
			{
				//MoveInsDefinition def = ctx.def;	//Why does PerformMoveIn have this argument?
				string entryid = __instance._entryid;//TODO: Different entryid each click?
				UnitInstanceData unit = ctx.unitdata;
				Log.Message($"Moving in all {ctx} ({entryid}, {unit})");

				bool kaching = false;
				MoveInsManager manager = Game.Game.ctx.sim.moveins;
				EntityTemplate template = Game.Game.ctx.entityman.FindTemplate(unit.template);

				foreach (Entity emptyspace in Game.Game.ctx.entityman._templateEntityCache[__instance.GetEntity().config.template].ToList())
				{
					Log.Message($"Moving in {emptyspace}:{emptyspace.id}");
					try
					{
						if (!manager.CanMoveIn(unit, emptyspace))
						{
							break;//TODO: Warning.
						}
						manager.RemoveFromSavedResults(entryid, unit);

						Money moveInCost = manager.GetMoveInCost(unit, emptyspace);
						if (moveInCost != 0) kaching = true;
						manager._manager.player.DoAdd(moveInCost, Reason.BuildCost, emptyspace);

						GridPos gridpos = emptyspace.data.placement.gridpos;
						Game.Game.ctx.board.DestroyEntity(emptyspace, playerInitiated: true, "move-in");
						Entity entity = Game.Game.ctx.board.CreateEntity(template, gridpos, enabled: true);

						entity.components.unit.PopulateFromRecipe(unit);
						entity.components.unit.SaveStatsOnMoveIn();
						entity.components.placement.StartBuilding(entity.components.placement.NeedsBuiltInstantly());
						Game.Game.ctx.events.Send(GameEventType.EntityAfterCreatedByPlayer, entity.id, gridpos);
					}
					catch(Exception e)
					{
						Log.Error($"Failed move in {emptyspace}:{emptyspace.id}: {e};{e.StackTrace}");
					}
					Log.Message($"Moved in!");
				}
				if (kaching)
				{
					Game.Game.serv.audio.PlayUISFX(UIEffectType.Purchase);
				}
				Log.Message($"Done moving in");

				return false;
			}
			else return true;
		}
	}
}

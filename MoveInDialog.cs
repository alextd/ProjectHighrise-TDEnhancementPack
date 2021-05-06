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
		public static int DistanceToSelection(Entity selected, Entity e)
		{
			int dx = Math.Abs(e.data.placement.gridpos.x - selected.data.placement.gridpos.x);
			int dy = e.data.placement.gridpos.y - selected.data.placement.gridpos.y;
			if (dy == 0)
				return dx;
			if (dy < 0)
				return -dy * 1000 - 500 + dx;
			return dy * 1000 + dx;
		}
		//private void OnEntryClick(TenantEntryContext ctx)
		public static bool Prefix(MoveInTenantsDialog __instance, MoveInTenantsDialog.TenantEntryContext ctx)
		{
			if (KeyboardShortcutManager.shift)
			{
				Entity selected = __instance.GetEntity();
				MoveInsDefinition def = ctx.def;	//Why does PerformMoveIn have this argument?
				string entryid = __instance._entryid;

				UnitInstanceData unit = ctx.unitdata;//Universal for residences, unique for offices. See 'instant'
				Log.Message($"Moving in all {entryid}");

				bool kaching = false;
				MoveInsManager manager = Game.Game.ctx.sim.moveins;
				EntityTemplate template = Game.Game.ctx.entityman.FindTemplate(unit.template);//Don't understand how selected.config doesn't work here.

				List<Entity> emptySpaces = Game.Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(selected.config.template).ToList();

				emptySpaces.Sort( (Entity a, Entity b) => DistanceToSelection(selected, a) - DistanceToSelection(selected, b));

				foreach (Entity emptyspace in emptySpaces)
				{
					Log.Message($"Moving in {emptyspace}:{emptyspace.id} - {unit}");
					try
					{
						if(unit == null)//probably out of candidates
						{
							break;
						}
						if (!manager.CanMoveIn(unit, emptyspace))
						{
							break;//TODO: Warning.
						}

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

						//It seems to me "instant" means "generic + always available, clicking means prepare" e.g. apartment types.
						//Non-instant means "must have interested individual, clicking means accept"
						//Which is the opposite of instant, since apartments do NOT move in instantly, but offices do.
						//Perhaps it is supposed to mean 'instantly available'
						if (def.instant == null)
						{
							manager.RemoveFromSavedResults(entryid, unit);//Pointless for instant as it refills.
							unit = manager.FindSavedStatus(entryid).candidates.FirstOrDefault();
						}
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

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
			var tr = ____go.GetComponent<RectTransform>();
			var anchor = tr.anchoredPosition;
			anchor.y /= 10;//move closer to top of screen
			tr.anchoredPosition = anchor;
			UIUtil.ForceRectSizeHack(____go, tr.rect.width, 950);
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
			if (!KeyboardShortcutManager.shift) return true;

			PerformMoveInAll(Game.Game.ctx.sim.moveins, __instance.GetEntity(), ctx.def, __instance._entryid, ctx.unitdata);

			return false;
		}

		public static Entity PerformMoveInAll(MoveInsManager manager, Entity selected, MoveInsDefinition def, string entryid, UnitInstanceData unit = null)
		{
			bool kaching = false;

			List<Entity> emptySpaces = Game.Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(selected.config.template).ToList();

			emptySpaces.Sort((Entity a, Entity b) => DistanceToSelection(selected, a) - DistanceToSelection(selected, b));

			Entity nextEntity = null;
			foreach (Entity emptyspace in emptySpaces)
			{
				if (unit == null)//probably out of candidates
				{
					unit = manager.FindSavedStatus(entryid).candidates.FirstOrDefault();
					if (unit == null)//probably out of candidates
					{
						nextEntity = emptyspace;
						break;
					}
				}

				//This might be dangerously close to PerformMoveIn. Thought it would change more. Basically need to not audio.PlayUISFX so much.
				if (!manager.CanMoveIn(unit, emptyspace))
				{
					break;//TODO: Warning.
				}

				Money moveInCost = manager.GetMoveInCost(unit, emptyspace);
				if (moveInCost != 0) kaching = true;
				manager._manager.player.DoAdd(moveInCost, Reason.BuildCost, emptyspace);

				GridPos gridpos = emptyspace.data.placement.gridpos;
				Game.Game.ctx.board.DestroyEntity(emptyspace, playerInitiated: true, "move-in");

				EntityTemplate template = Game.Game.ctx.entityman.FindTemplate(unit.template);//This is a DIFFERENT USAGE OF TEMPLATE from config.template.
																																											//This is the graphical template. not the room type template.
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
					unit = null;
				}
			}

			if (kaching)
				Game.Game.serv.audio.PlayUISFX(UIEffectType.Purchase);

			return nextEntity;
		}
	}

	[HarmonyPatch(typeof(MoveInsManager), nameof(MoveInsManager.OnAdSelected))]
	public static class AcceptAllAds
	{
		//public void OnAdSelected(Entity emptyspace, string entryid)
		public static bool Prefix(MoveInsManager __instance, Entity emptyspace, string entryid)
		{
			if (!KeyboardShortcutManager.shift) return true;

			//This is probably redundant since 'Ad selected' implies not instant.

			MoveInsDefinition moveInsDefinition = __instance.FindDefinitionGivenSpaceTypeAndSize(emptyspace);
			Entity nextEntity = MoveInAllTenants.PerformMoveInAll(__instance, emptyspace, moveInsDefinition, entryid);

			if (nextEntity != null)
			{
				Game.Game.ctx.board.DoSelect(nextEntity);
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(MoveInsManager), nameof(MoveInsManager.StartAdvertising))]
	public static class StartAllAds
	{
		//Technicaly I'd want to change the call to StartAdvertising to check shift and call a new method but that'd take a transpiler and this works fine.
		public static bool recursive = false;

		//public void StartAdvertising(string entryid, Entity e)
		public static bool Prefix(Entity e)
		{
			if (!KeyboardShortcutManager.shift || recursive) return true;

			MoveInsDefinition def = Game.Game.ctx.sim.moveins.FindDefinitionGivenSpaceTypeAndSize(e);
			recursive = true;
			foreach (MoveInsEntry entry in Game.Game.ctx.sim.moveins.FindMoveInsEntryList(def).Where(me => me.IsEntryVisible()))
			{
				MoveInsEntryStatus moveInsEntryStatus = Game.Game.ctx.sim.moveins.FindSavedStatus(entry.id);
				if (moveInsEntryStatus.IsNotAdvertising)
				{
					Game.Game.ctx.sim.moveins.StartAdvertising(entry.id, e);
				}
			}
			recursive = false;
			return false;
		}
	}
}

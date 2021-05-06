using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Entities.Components;
using Game.Session.Entities;
using Game.Session.Entities.Data;
using Game.Session.Board;
using Game.Components;
using SomaSim.AI;
using Game.AI;

namespace BetterPlacement
{
	[HarmonyPatch(typeof(PeepComponent), nameof(PeepComponent.PickFromSchedule))]
	public static class SaveScriptState
	{
		public static void LogScriptQueue(Entity e)
		{
			Log.Debug($"Queue for {e}:{e.id}");
			if (e.components?.script?.queue is GameScriptQueue q && !q.IsEmpty)
			{
				foreach (Script s in q)
					Log.Debug($"- {s.Name}");
			}
			else Log.Debug("- (empty)");
		}
		//PickFromSchedule
		public static void Prefix(PeepComponent __instance, Entity ____entity)
		{
			Log.Debug($"Scheduling {____entity}:{____entity.id}");
			LogScriptQueue(____entity);
		}
		public static void Postfix(PeepComponent __instance, Entity ____entity)
		{
			Log.Debug($"Scheduled {____entity}:{____entity.id}");
			LogScriptQueue(____entity);
		}
	}


	[HarmonyPatch(typeof(ScheduleManager), nameof(ScheduleManager.InBuilding))]
	public static class LogInBuilding
	{
		//private static bool InBuilding(Entity entity)
		public static void Postfix(bool __result, Entity entity)
		{
			Log.Debug($"InBuilding({entity}:{entity.id}) -> {__result}");
			Log.Debug($"entity.components.sprite.IsVisible ({entity.components.sprite.IsVisible})");
			Log.Debug($"Game.ctx.board.grid.FindGridCellOrNull(entity) ({Game.Game.ctx.board.grid.FindGridCellOrNull(entity)})");
			Log.Debug($"Game.ctx.board.grid.FindGridCellOrNull(entity).hasFloor ({Game.Game.ctx.board.grid.FindGridCellOrNull(entity).hasFloor})");
		}
	}

	//private void InitNewGameBoard(GameEvent gev)
	[HarmonyPatch(typeof(StartupLevelGenerator), nameof(StartupLevelGenerator.InitNewGameBoard))]
	public static class TESTLOG
	{
		public static void Prefix()
		{
			Log.Debug(":::INITNEWGAMEBOARD");
		}
	}
	//internal void AddFloor(FloorType ft = FloorType.Default)
	[HarmonyPatch(typeof(GridCell), nameof(GridCell.AddFloor))]
	public static class TESTLOG2
	{
		public static void Prefix(GridCell __instance)
		{
			Log.Debug($":::AddFloor({__instance}");
		}
	}

	//Does Loaded peeps update before Grid is loaded?
	[HarmonyPatch(typeof(PeepComponent), nameof(PeepComponent.Update))]
	public static class TESTLOG3
	{
		public static void Prefix(Entity ____entity)
		{
			Log.Debug($"Update({____entity}:{____entity.id})");
		}
	}

	//public void Load(Hashtable data)
	[HarmonyPatch(typeof(Grid), nameof(Grid.Load))]
	public static class TESTLOG4
	{
		public static void Prefix()
		{
			Log.Debug($":::GRID LOADING");
		}
	}

	[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.CreateByTemplate))]
	public static class DeactivateWhileLoading
	{
	//public Entity CreateByTemplate(EntityTemplate configs, int id = 0, EntityData savedata = null)
		public static void Postfix(Entity __result, EntityData savedata)
		{
			if (savedata != null)  //Loaded
				__result.go?.SetActive(false);
		}
	}

	[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.OnAfterLoad))]
	public static class ActivateAfterLoad
	{
	//public void OnAfterLoad()
		public static void Postfix(EntityManager __instance)
		{
			foreach (Entity e in __instance.GetAllEntities())
				e.go?.SetActive(true);
		}
	}
}

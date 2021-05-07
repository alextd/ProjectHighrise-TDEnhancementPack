using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Entities.Components;
using Game.Session.Entities;
using Game.Session.Entities.Data;
using Game.Session.Board;
using Game.Session.Sim;
using Game.Components;
using SomaSim.AI;
using Game.AI;
using Game.AI.Actions;

namespace BetterPlacement
{
	//People would go to their office when the game was loaded.
	//This was because they though they weren't in the building
	//This is because the building wasn't loaded
	//This is because the Unity GameObjects created during loading were 'active' and therefore could call 'Update' before loading finished

	//So - simply set them inactive as they are loaded, then activate them all later.
	//TODO - all unity game objects? Oh well this is enough.


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



	//#2 problem is that task progress wasn't saved.
	//Simply enough, you just need to lower the duration of the task while it is being worked on.
	[HarmonyPatch(typeof(ActionPauseForTask), nameof(ActionPauseForTask.OnUpdate))]
	public static class SaveProgress
	{
		//internal override void OnUpdate()
		public static void Prefix(ActionPauseForTask __instance)
		{
			SupportTask task = __instance.context.agent.components.peep.GetTaskUnsafe();
			if (task == null) return;

			float dt = Game.Game.ctx.clock.deltaTimeSim;
			ConsultantManager consultants = Game.Game.ctx.sim.consultants;
			if ((task.isCategoryBuilding && consultants.IsUpgradeActive("cons-c-4")) || (task.isCategoryService && consultants.IsUpgradeActive("cons-c-5")))
			{
				dt *= 2f;
			}
			task.durationsec -= dt;
		}
	}
}

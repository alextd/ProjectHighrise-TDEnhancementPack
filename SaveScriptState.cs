using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
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
using SomaSim;
using SomaSim.AI;
using SomaSim.Serializer;
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

	/* Obsolete now that current actions are saved:
	 * Other Pause actions didn't have a place to save remaining time globally anyway.


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
	*/

	[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.SerializeEntity))]
	public static class SaveEntityScript
	{
		public static void SerAdd(this Hashtable hash, string key, object value) =>
			hash[key] = Game.Game.serv.serializer.Serialize(value);

		//private Hashtable SerializeEntity(Entity e)
		public static void Postfix(Hashtable __result, Entity e)
		{
			if (e.components.script?.queue is GameScriptQueue queue)
			{
				Log.Debug($"---Saving {e}:{e.id}");
				Hashtable script = new Hashtable();
				GameActionContext ctx = queue.context;
				if (ctx.targetid != 0)
					script.SerAdd("targetid", ctx.targetid);
				if (ctx.targetpos.HasValue)
					script.SerAdd("targetpos", ctx.targetpos.Value);
				if (ctx.onfail != null)
					script.SerAdd("onfail", ctx.onfail);
				
				script.SerAdd("agent_id", ctx.agent.id);
				//On load, resolve GameScriptQueue.agent from agent_id


				Log.Debug($"---Try save {queue}");
				try
				{
					//Serializing queue itself will be handled as an enumerable
					//( which assumes the enumerable has no other data to save, so it is saved above)
					script.SerAdd("script_queue", queue);
				}
				catch (Exception ex)
				{
					Log.Debug($"---FAIL: {ex}\n{ex.StackTrace}");
				}
				Log.Debug("---saved!");

				__result["script"] = script;
			}
		}
	}


	[HarmonyPatch(typeof(Serializer), nameof(Serializer.SerializeEnumerable))]
	public static class SaveScript
	{
		//private ArrayList SerializeEnumerable(IEnumerable list, bool specifyValueTypes)

		public static bool Prefix(ref object __result, EntityManager __instance, IEnumerable list)
		{
			Log.Debug($"SerializeEnumerable:: {list} ::: {list.GetType()}");
			if (list is Script script)
			{
				Log.Debug($"---Serializing {script} ::: {script.GetType()}");
				Hashtable hash = new Hashtable();

				if (script is GameScript gameScript)
					hash["task"] = gameScript.task;
				hash["name"] = script.Name;
				//hash["action_queue"] = $"DEBUG TEST {script._queue.ToArray().Length} actions";
				hash["action_queue"] = Game.Game.serv.serializer.Serialize(script._queue, true);

				__result = hash;//SerializeEnumerable returns ArrayList even though the place that uses it only needs an object so this should work? 

				Log.Debug($"---Serialed!");
				return false;
			}
			return true;
		}
	}


	[HarmonyPatch(typeof(Serializer), nameof(Serializer.SerializeClassOrStruct))]
	public static class SaveAction
	{
		//private object SerializeClassOrStruct(object value, bool specifyType)
		public static bool Prefix(ref object __result, object value, ref bool specifyType)
		{
			Log.Debug($"SerializeClassOrStruct:: {value} ::: {value.GetType()}");
			if (value is SomaSim.AI.Action action)
			{
				 //Jesus blimey for real - Enumerable serialization only writes their element's type if the enumerable itself is not generic.
				 //Well Actions are stored in a Deque<Action> - so it's not saving what subclass of action
				 // AS IF whether or not the ENUMERABLE is generic should tell us whether or not the ELEMENTS have subclass types?
				specifyType = true;
				Log.Debug($"---Serializing {action} : {action.GetType()}");

				if (HandleAction(action) is Hashtable hash)
				{
					Log.Debug($"---Handled {action} : {string.Join(", ", (from object x in hash.Keys select x.ToString()).ToArray())}");

					//Something special was done in HandleAction. But here we handle this:
					hash[Game.Game.serv.serializer.TYPEKEY] = action.GetType().FullName;

					if (action._updatedOnce && !hash.ContainsKey("_updatedOnce"))//Don't set it if it was handled.
						hash["_updatedOnce"] = action._updatedOnce;//else default false

					__result = hash;

					//Don't continue SerializeClassOrStruct:
					return false;
				}
			}
			//All other Actions that gave null from HandleAction can use the normal SerializeClassOrStruct
			//albeit with private fields, no properties, via GetMembersForActions below
			return true;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo GetMembersInfo = AccessTools.Method(typeof(TypeUtils), nameof(TypeUtils.GetMembers), new Type[] { typeof(object) });

			MethodInfo GetMembersForActionsInfo = AccessTools.Method(typeof(SaveAction), nameof(SaveAction.GetMembersForActionsInfo));

			foreach(CodeInstruction inst in instructions)
			{
				if (inst.Calls(GetMembersInfo))
				{
					yield return new CodeInstruction(OpCodes.Call, GetMembersForActionsInfo);
				}
				else
					yield return inst;
			}
		}

		//Omg seriously, did the game NOT SAVE DATA because the field was PRIVATE?
		//yeeeahhhh and it DID save const values - but checked the value against the default, which of course, was the same, so no value was saved in the end.
		//And it saved Properties - which nothing being saved had properties anyway.
		//public static List<MemberInfo> GetMembers(object obj)
		public static List<MemberInfo> GetMembersForActionsInfo(object obj)
		{
			if (obj is SomaSim.AI.Action action)
			{
				List<MemberInfo> fields = action.GetType().GetMembers(
					BindingFlags.Public
			| BindingFlags.NonPublic
			| BindingFlags.Instance).Where(mi => mi is FieldInfo).ToList();
				Log.Debug($"Fields ({fields.Count}) for {obj.GetType()} are {string.Join(", ", fields.Select(f => f.Name).ToArray())}");
				return fields;
			}
			return TypeUtils.GetMembers(obj);
		}

		//Some Actions work with SerializeClassOrStruct, but need one tweak:
		public static void Postfix(object __result, object value)
		{
			//These actions need to be restarted to set up callbacks.
			if (value is ActionWaitSeated || value is ActionWaitInLine)
			{
				Log.Debug($"---Resetting _updatedOnce {value} : {value.GetType()}");
				if (__result is Hashtable hash)
					hash.Remove("_updatedOnce");//or just hash["_updatedOnce"] = false but that doesn't NEED to be written.

				//No special loading - their OnStarted just needs a patch use the loaded _endTime
			}
		}

		public static Hashtable HandleAction(SomaSim.AI.Action action)
		{
			Hashtable hash = null;

			if (action is SomaSim.AI.ActionsTest.TestAction)
			{
				Log.Warning("Holy cow, a test action?");
			}

			//List of actions autogenerated:	
			/*
			Log.Message(
			string.Concat(
			Assembly.GetAssembly(typeof(SomaSim.AI.Action)).GetTypes()
			.Where((Type x) => x.IsSubclassOf(typeof(SomaSim.AI.Action)))
			.Select(t => $"\n			else if (action is {t} action{t.Name})\n			{{\n				\n			}}").ToArray()));

			(then remove all "actionAction" names)
			*/

			//There's actually not a whole lot that need special handling.


			//On load, an ActionFollowPath should be discarded, and the ActionNavigate (that should come next) will create a new path when run.
			//ActionNavigate sets up things that ActionFollowPath needs, which aren't saved.
			//That's maybe a one-frame hicucp, but easier than figuring what to save for pathing.
			else if (action is Game.AI.Actions.ActionFollowPath actionFollowPath)
			{
				return new Hashtable();

				//#TYPE will be set. On load, just toss this out.
			}
			//ActionNavigate needs to be re-run to re-generate ActionFollowPath
			else if (action is Game.AI.Actions.ActionNavigate actionNavigate)
			{
				hash = new Hashtable();

				//Re-do this action from the start, so it creates a new ActionFollowPath
				//(It is otherwise expecting a result from FollowPath.)
				hash["_updatedOnce"] = false;

				//All the other "_" members are set in OnStarted and don't need to be saved!
				//Luckily the GridCell doesn't need to be saved by reference at all!
				hash["delivery"] = actionNavigate.delivery;

				//No special loading is needed!
			}

			//Waiting for Elevator has a reference to Entity. So this needs a big manual step.
			else if (action is Game.AI.Actions.ActionWaitForElevator actionWaitForElevator)
			{
				hash = new Hashtable();
				//Could do some default checking here. oh well.
				hash["_startTime"] = actionWaitForElevator._startTime;
				hash["_timeoutTime"] = actionWaitForElevator._timeoutTime;
				hash["_complained"] = actionWaitForElevator._complained;
				hash["entry"] = actionWaitForElevator.entry;

				hash["_elevator_id"] = actionWaitForElevator._elevator.id;// <- this here, why this action can't be just saved.

				//hash["_updatedOnce"] = actionWaitForElevator._updatedOnce;//automatically handled above. Of course this turned out to be the only action that would need this.

				//This cannot simply be restarted, since _timeoutTime would get reset.
				//That's similar to WaitInLine/Seated, but then we still need to skip saving _elavator here anyway, so we might as well just resolve that reference on load

				//ON load, resolve reference to _elevator.
			}

			/*
			 * These Wait actions have OnStarted actions that set up something(callbacks)
			 * This can actually be saved normally, loaded normally
			 * But its OnStarted needs a patch to use _endTime if it exists instead of resetting it
			
			else if (action is Game.AI.Actions.ActionWaitInLine actionWaitInLine)
			{
			}
			else if (action is Game.AI.Actions.ActionWaitSeated actionWaitSeated)
			{

			}
			*/

			/* 
			 * Above Actions some reason to be handled specifically
			 * Commented out actions below can be handled by the normal SerializeClass, so they'll return null
			 * They will NOT have their OnStarted called again, as _updatedOnce will be saved.
			 * 
			 * 
			else if (action is Game.AI.Actions.ActionChangeVisibility actionChangeVisibility)
			{
				//This is what it would look like, but the normal SerializeClass method can do this
				hash = new Hashtable();
				hash["visible"] = actionChangeVisibility.visible;
			}
			else if (action is Game.AI.Actions.ActionDebugText actionDebugText)
			{
			}
			else if (action is Game.AI.Actions.ActionDie actionDie)
			{

			}
			else if (action is Game.AI.Actions.ActionEventspaceNotify actionEventspaceNotify)
			{

			}
			else if (action is Game.AI.Actions.ActionSetEventAttendeeState actionSetEventAttendeeState)
			{

			}
			else if (action is Game.AI.Actions.ActionFinishTask actionFinishTask)
			{

			}
			
			else if (action is Game.AI.Actions.ActionHotelCheckIn actionHotelCheckIn)
			{

			}
			else if (action is Game.AI.Actions.ActionHotelDirtyRoom actionHotelDirtyRoom)
			{

			}
			else if (action is Game.AI.Actions.ActionHotelCheckOutOrAbort actionHotelCheckOutOrAbort)
			{

			}
			else if (action is Game.AI.Actions.ActionLogThought actionLogThought)
			{

			}
			else if (action is Game.AI.Actions.ActionLogTaskThought actionLogTaskThought)
			{

			}
			else if (action is Game.AI.Actions.ActionMaybeGenerateBuzz actionMaybeGenerateBuzz)
			{

			}
			else if (action is Game.AI.Actions.ActionMaybePayForVisit actionMaybePayForVisit)
			{

			}
			else if (action is Game.AI.Actions.ActionPause actionPause)
			{

			}
			else if (action is Game.AI.Actions.ActionPauseForTask actionPauseForTask)
			{

			}
			else if (action is Game.AI.Actions.ActionReactToGrime actionReactToGrime)
			{

			}
			else if (action is Game.AI.Actions.ActionReactToHotelRoomUtilities actionReactToHotelRoomUtilities)
			{

			}
			else if (action is Game.AI.Actions.ActionSetAnimation actionSetAnimation)
			{

			}
			else if (action is Game.AI.Actions.ActionSetSpecialAnimationAbstract actionSetSpecialAnimationAbstract)
			{

			}
			else if (action is Game.AI.Actions.ActionSetSpecialHomeAnimation actionSetSpecialHomeAnimation)
			{

			}
			else if (action is Game.AI.Actions.ActionWaitVending actionWaitVending)
			{

			}
			else if (action is Game.AI.Actions.ActionChangeReservation actionChangeReservation)
			{

			}
			else if (action is Game.AI.Actions.ActionChangeApartmentReservation actionChangeApartmentReservation)
			{

			}
			else if (action is Game.AI.Actions.ActionChangePatronReservation actionChangePatronReservation)
			{

			}
			else if (action is Game.AI.Actions.ActionSetContextualAnimation actionSetContextualAnimation)
			{

			}
			else if (action is Game.AI.Actions.ActionSetOnFailure actionSetOnFailure)
			{

			}
			else if (action is Game.AI.Actions.ActionSetMemory actionSetMemory)
			{

			}
			else if (action is Game.AI.Actions.ActionClearPoi actionClearPoi)
			{

			}
			else if (action is Game.AI.Actions.ActionSetTarget actionSetTarget)
			{

			}
			else if (action is Game.AI.Actions.ActionUpdateFavorites actionUpdateFavorites)
			{

			}
			else if (action is Game.AI.Actions.ActionCheckBrokenPathElement actionCheckBrokenPathElement)
			{

			}

			//Also tutorial actions, sorry not gonna do those:
			/*
			
			else if (action is Game.AI.Actions.Tutorial.TutPanCamera actionTutPanCamera)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutForceUnpause actionTutForceUnpause)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutForceRemovePopup actionTutForceRemovePopup)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutForceDefaultInputMode actionTutForceDefaultInputMode)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutForceDeselect actionTutForceDeselect)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutForceHideBuildRibbons actionTutForceHideBuildRibbons)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutForceUtilInputMode actionTutForceUtilInputMode)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutExit actionTutExit)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutShowArrow actionTutShowArrow)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutHideArrow actionTutHideArrow)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutHideLayers actionTutHideLayers)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutShowHighlight actionTutShowHighlight)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutHideHighlight actionTutHideHighlight)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutShowInfo actionTutShowInfo)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutHideInfo actionTutHideInfo)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutShowOkPopup actionTutShowOkPopup)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutHideButtonHighlights actionTutHideButtonHighlights)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutSkipFrames actionTutSkipFrames)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForCamera actionTutWaitForCamera)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForBuildRibbon actionTutWaitForBuildRibbon)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForEntityCount actionTutWaitForEntityCount)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForPopulation actionTutWaitForPopulation)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForPrestige actionTutWaitForPrestige)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForAllUnitConstructionDone actionTutWaitForAllUnitConstructionDone)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForDialog actionTutWaitForDialog)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForHeatmap actionTutWaitForHeatmap)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForGameSpeed actionTutWaitForGameSpeed)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitRealTime actionTutWaitRealTime)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForBuyEntityInputMode actionTutWaitForBuyEntityInputMode)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForDefaultInputMode actionTutWaitForDefaultInputMode)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForAddFloorInputMode actionTutWaitForAddFloorInputMode)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForAddUtilityInputMode actionTutWaitForAddUtilityInputMode)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForFloor actionTutWaitForFloor)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForUtilities actionTutWaitForUtilities)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForUpgradeActive actionTutWaitForUpgradeActive)
			{

			}
			else if (action is Game.AI.Actions.Tutorial.TutWaitForServicePending actionTutWaitForServicePending)
			{

			}
			 * no data to save here anyway:
			else if (action is Game.AI.GameAction actionGameAction)
			{

			}
			*/
			return hash;
		}
	}
}

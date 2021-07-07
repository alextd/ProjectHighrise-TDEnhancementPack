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
using Game.Session.Entities.Config;
using Game.Session.Entities.Data;
using Game.Session.Board;
using Game.Session.Sim;
using Game.Services;
using Game.Components;
using SomaSim;
using SomaSim.Collections;
using SomaSim.AI;
using SomaSim.Serializer;
using Game.AI;
using Game.AI.Actions;

namespace TDEnhancementPack
{
	//People would go to their office when the game was loaded.
	//This was because they thought they weren't in the building
	//This is because the building wasn't loaded
	//This is because the Unity GameObjects created during loading were 'active' and therefore could call 'Update' before loading finished

	//(Also I just learnt a week later)
	//People who were going offsite would disappear - and be invisible - but on screen and selectable. So this fixes all that too.
	//It's funny loading up a game, wondering where this bug is, and realizing, oh hey, my patches aren't applied - I fixed that bug.

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
				Hashtable scriptHash = new Hashtable();

				//This would be easy if there wasn't a reference to Agent. Seems like this is the one place in code it's not an id but an actual Entity reference.
				Hashtable context = new Hashtable();
				GameActionContext ctx = queue.context;
				if (ctx.targetid != 0)
					context.SerAdd("targetid", ctx.targetid);
				if (ctx.targetpos.HasValue)
					context.SerAdd("targetpos", ctx.targetpos.Value);
				if (ctx.onfail != null)
					context.SerAdd("onfail", ctx.onfail);
				scriptHash["context"] = context;


				try
				{
					//Serializing queue itself will be handled as an enumerable
					//( which assumes the enumerable has no other data to save, so it is saved above)
					scriptHash.SerAdd("script_queue", queue);
				}
				catch (Exception ex)
				{
					Log.Error($"---FAIL saving script queue for Saving {e}:{e.id}\n{ex}");
				}

				__result["script"] = scriptHash;
			}
		}
	}


	//Serialize would catch Script as a IEnumeramble and skip right over the other fields.
	[HarmonyPatch(typeof(Serializer), nameof(Serializer.SerializeEnumerable))]
	public static class SaveScript
	{
		//private ArrayList SerializeEnumerable(IEnumerable list, bool specifyValueTypes)

		public static bool Prefix(ref object __result, EntityManager __instance, IEnumerable list)
		{
			if (list is Script script)
			{
				Hashtable hash = new Hashtable();

				//so that the deserializer knows how to deserialize it inside the ScriptQueue
				//That being said, still have to override it so it deserializes all the fields and not just the enumerable part.
				hash[Game.Game.serv.serializer.TYPEKEY] = script.GetType().FullName;

				if (script is GameScript gameScript)
					hash["task"] = gameScript.task;
				hash["Name"] = script.Name;
				hash["_queue"] = Game.Game.serv.serializer.Serialize(script._queue, true);

				__result = hash;//SerializeEnumerable returns ArrayList even though the place that uses it only needs an object so this should work? 
												// I do have to override deserialize - but otherwise it would only deserialize the enumerable part, not thinking about other members.

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
			if (value is SomaSim.AI.Action action)
			{
				//Jesus blimey for real - Enumerable serialization only writes their element's type if the enumerable itself is not generic.
				//Well Actions are stored in a Deque<Action> - so it's not saving what subclass of action
				// AS IF whether or not the ENUMERABLE is generic should tell us whether or not the ELEMENTS have subclass types?
				specifyType = true;


				if (HandleAction(action) is Hashtable hash)
				{
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

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyLib.Transpilers.MethodReplacer(instructions,
				AccessTools.Method(typeof(TypeUtils), nameof(TypeUtils.GetMembers), new Type[] { typeof(object) }),
				AccessTools.Method(typeof(SaveAction), nameof(SaveAction.GetMembersForActionsObj)));

		//Omg seriously, did the game NOT SAVE DATA because the field was PRIVATE?
		//yeeeahhhh and it DID save const values - but checked the value against the default, which of course, was the same, so no value was saved in the end.
		//And it saved Properties - which nothing being saved had properties anyway.
		//public static List<MemberInfo> GetMembers(object obj)
		public static List<MemberInfo> GetMembersForActionsObj(object obj) =>
			GetMembersForActionsType(obj.GetType());

		public static List<MemberInfo> GetMembersForActionsType(Type type)
		{
			if (typeof(SomaSim.AI.Action).IsAssignableFrom(type))
				return type.GetMembers(
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
					.Where(mi => mi is FieldInfo).ToList();

			return TypeUtils.GetMembers(type);
		}

		//Some Actions work with SerializeClassOrStruct, but need one tweak:
		public static void Postfix(object __result, object value)
		{
			//These actions need to be restarted to set up callbacks.
			if (value is ActionWaitSeated || value is ActionWaitInLine || value is ActionWaitForElevator)
			{
				if (__result is Hashtable hash)
					hash["_updatedOnce"] = false; //force OnStarted to run

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


			else if (action is Game.AI.Actions.ActionNavigate actionNavigate)
			{
				//Okay this script can be in a few states:
				//Not run. Nothing special.
				//Pending (aka !IsDone). Needs to be restarted since path creation component needs to be restarted
				//IsDone + Success - Waiting on interrupted script to finish
				//IsDone + Failed - that is, Path creation failed - it will immediately need to stop the script in OnUpdate
				// (which probably happened already but there's a slight possiblity it got the result but didn't update)

				//But if it's pending, it needs to be restarted to call Game.ctx.board.nav.AddSearch.
				hash = new Hashtable();

				//"IsDone" Means the path creation task is done.
				//Otherwise, it's pending, but the callback it's waiting on is not saved
				//So the action needs to be restarted to call Game.ctx.board.nav.AddSearch.
				//OnUpdate otherwise would be waiting for that path processing callback that'll never come
				if (!actionNavigate.IsDone)
					hash["_updatedOnce"] = false;//force restart. Otherwise, _updatedOnce is handled automatically after HandleAction
				else
					hash["_done"] = Game.Game.serv.serializer.Serialize(actionNavigate._done);//_done is Pending by default

				//Of course this is just a configuration so save it:
				hash["delivery"] = actionNavigate.delivery;


				//Let's consider the members:

				//private GridCell _target;
				//private GridPosF _targetpos;
				//private int _totalstairs;
				//private int _totalescalators;

				//They are only used inside OnStarted, or the callback ProcessNavResult, which won't be saved anyway.
				//They will be re-made when status is pending; They won't be needed when status is not pending.
				//Verdict: no reason to save these. Good because _target is a reference anyway.

				//No special loading is needed!
				//What about the cart anim without restarting? Anim loading should hopefully be handled elsewhere
			}

			//ActionFollowPath has no default constructor.
			//So the serializer has nothing to compare to determine which default values to not write
			//And it explodes. Meh. Okay just do it here.
			else if (action is Game.AI.Actions.ActionFollowPath actionFollowPath)
			{
				hash = new Hashtable();

				hash.SerAdd("speedwobble", actionFollowPath.speedwobble);
				hash.SerAdd("_path", actionFollowPath._path);
				hash.SerAdd("_type", actionFollowPath._type);
				hash.SerAdd("_delivery", actionFollowPath._delivery);

				return hash;
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

				//this here, why this action can't be just saved. _elevator is a reference to an Entity.
				//hash["_elevator_id"] = actionWaitForElevator._elevator.id;
				//Of course, dang it, how would I resolve this reference? I'd need to save that ID somewhere until after everything is loaded.
				//Okay, just make sure _timeoutTime is used in OnStarted like other Wait actions. But do have to Handle it here to NOT save _elevator.

				//No special loading needed. OnStarted should check _timeoutTime.
			}

			/*
			 * These Wait actions have OnStarted actions that set up something(callbacks)
			 * saving it will be handled in the Postfix - as everything else works fine (albeit with private members allowed)
			 * Loading will work normal (albeit with private members allowed)
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


	// ----------------
	//  --- LOADING ---
	// ----------------

	[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.LoadAndCreateEntity))]
	public static class LoadEntityScript
	{
		//public LoadEntityResult LoadAndCreateEntity(Hashtable data, List<string> mods);
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo CreateByTemplateInfo = AccessTools.Method(typeof(EntityManager), nameof(EntityManager.CreateByTemplate));
			MethodInfo AfterCreateByTemplate = AccessTools.Method(typeof(LoadEntityScript), nameof(AfterCreateByTemplate));

			foreach (var inst in instructions)
			{
				yield return inst;
				if (inst.Calls(CreateByTemplateInfo))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_1);//Hashtable data
					yield return new CodeInstruction(OpCodes.Call, AfterCreateByTemplate);//AfterCreateByTemplate(entity, data) => entity
				}
			}
		}

		//public Entity CreateByTemplate(EntityTemplate configs, int id = 0, EntityData savedata = null)
		public static Entity AfterCreateByTemplate(Entity e, Hashtable entityHash)
		{
			if (!entityHash.ContainsKey("script")) return LoadEntitySprite.AfterCreateByTemplate(e, entityHash);

			if (e.components.script?.queue is GameScriptQueue queue)
			{
				if (entityHash["script"] is Hashtable scriptHash)
				{
					//QueueContext has been created by the component and agent set, now load in other values:
					//Can't DeserializeIntoClassOrStruct as it hit null refs - TypeUtils.GetMemberType(memberInfo) for type GridPos? being null I guess?
					if (scriptHash.ContainsKey("context"))
					{
						Hashtable contextHash = scriptHash["context"] as Hashtable;
						GameActionContext ctx = queue.context;

						SaveLoadUtils.DeserializeSingleKey(contextHash, "targetid", delegate (int x) { ctx.targetid = x; });
						SaveLoadUtils.DeserializeSingleKey(contextHash, "targetpos", delegate (GridPosF x) { ctx.targetpos = x; });
						SaveLoadUtils.DeserializeSingleKey(contextHash, "onfail", delegate (string x) { ctx.onfail = x; });
					}

					//Scripts!
					if (scriptHash.ContainsKey("script_queue"))
					{
						try
						{
							List<Script> scripts = Game.Game.serv.serializer.Deserialize(scriptHash["script_queue"], typeof(List<Script>)) as List<Script>;
							foreach (Script script in scripts)
								queue.Add(script);
						}
						catch (Exception ex)
						{
							Log.Error($"---FAIL LOADING Q?!??!: {ex}");
						}
					}
				}
			}
			else
				Log.Error($"{e}:{e.id} has no ScriptComponent Queue to load into!");

			return LoadEntitySprite.AfterCreateByTemplate(e, entityHash);
		}
	}

	[HarmonyPatch(typeof(Serializer), nameof(Serializer.Deserialize), new Type[] { typeof(object), typeof(Type)})]
	public static class LoadScript
	{
		//public object Deserialize(object value, Type targettype = null)
		public static bool Prefix(ref object __result, object value, Type targettype)
		{

			//So if we let this go on its own way, it'll deserialize Script as an enumerable and skip over the other fields.
			if (value is Hashtable hash && hash.ContainsKey(Game.Game.serv.serializer.TYPEKEY) &&
				Game.Game.serv.serializer.FindTypeByName(hash[Game.Game.serv.serializer.TYPEKEY] as string) is Type type &&
				typeof(Script).IsAssignableFrom(type))
			{
				//FFFUUUUUUUU No default constructor for GameScript.
				//Script script = Activator.CreateInstance(type) as Script;
				Script script;
				if (type == typeof(GameScript))
					script = new GameScript(new List<SomaSim.AI.Action>());
				else
					script = new Script();


				//I could ALMOST use this:
				//Game.Game.serv.serializer.DeserializeIntoClassOrStruct(value, obj); 
				//This should read fields ---
				//But _queue is private and it'll not load that. So just do it manual.

				if (script is GameScript gameScript)
					SaveLoadUtils.DeserializeSingleKey(hash, "task", delegate (string x) { gameScript.task = x; });
				SaveLoadUtils.DeserializeSingleKey(hash, "Name", delegate (string x) { script.Name = x; });

				if (hash.ContainsKey("_queue"))
				{
					List<SomaSim.AI.Action> actions = Game.Game.serv.serializer.Deserialize(hash["_queue"], typeof(List<SomaSim.AI.Action>)) as List<SomaSim.AI.Action>;
					foreach (var action in actions)
						script.Add(action);
				}

				__result = script;

				return false;
			}
			return true;
		}
	}


	//DeserializeIntoClassOrStruct would only used public fields to deterine what data to load
	//So even though the data to load included a value for a field - it would just not use that.
	//So let's get private fields in there for Actions.
	//DeserializeIntoClassOrStruct calls Serializer.GetMemberInfos, and is the only one to call it, so patch that to get all fields
	[HarmonyPatch(typeof(Serializer), nameof(Serializer.GetMemberInfos))]
	public static class LoadAction
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyLib.Transpilers.MethodReplacer(instructions,
				AccessTools.Method(typeof(TypeUtils), nameof(TypeUtils.GetMembers), new Type[] { typeof(Type) }),
				AccessTools.Method(typeof(SaveAction), nameof(SaveAction.GetMembersForActionsType)));
	}

	[HarmonyPatch(typeof(Serializer), nameof(Serializer.DeserializeIntoClassOrStruct))]
	public static class LoadActionPrivate
	{
		//patch the call to GetMemberType to fucking allow private fields for fucks goddamn sake. Then loading _endTime should work.
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyLib.Transpilers.MethodReplacer(instructions,
				AccessTools.Method(typeof(TypeUtils), nameof(TypeUtils.GetMemberType)),
				AccessTools.Method(typeof(LoadActionPrivate), nameof(LoadActionPrivate.GetMemberTypeForAction)));

		//public static Type GetMemberType(MemberInfo i)
		public static Type GetMemberTypeForAction(MemberInfo i)
		{
			if (typeof(SomaSim.AI.Action).IsAssignableFrom(i.DeclaringType) && i is FieldInfo fi)
				return fi.FieldType;

			return TypeUtils.GetMemberType(i);
		}

		//internal void DeserializeIntoClassOrStruct(object value, object target)
		public static bool Prefix(object value, object target)
		{
			//Automatic deserialzer can't handle ActionFollowPath's Queue. Because it uses Reflection to find "Add" method and Queue doesn't have it. Jfffff.
			if (target is ActionFollowPath actionFollowPath)
			{
				Hashtable hash = value as Hashtable;

				SaveLoadUtils.DeserializeSingleKey(hash, "speedwobble", delegate (float x) { actionFollowPath.speedwobble = x; });
				SaveLoadUtils.DeserializeSingleKey(hash, "_type", delegate (PathModType x) { actionFollowPath._type = x; });
				SaveLoadUtils.DeserializeSingleKey(hash, "_delivery", delegate (bool x) { actionFollowPath._delivery = x; });
				SaveLoadUtils.DeserializeSingleKey(hash, "_updatedOnce", delegate (bool x) { actionFollowPath._updatedOnce = x; });

				if (hash.ContainsKey("_path"))
				{
					List<PathElement> paths = Game.Game.serv.serializer.Deserialize(hash["_path"], typeof(List<PathElement>)) as List<PathElement>;
					paths.Reverse();//for enqueing
					foreach (PathElement p in paths)
					{
						actionFollowPath._path.Enqueue(p);
					}
				}

				return false;
			}
			return true;
		}

	}


	//Can't do CreateInstance because ActionFollowPath has no default constructor geez.
	[HarmonyPatch(typeof(Serializer), nameof(Serializer.CreateInstance))]
	public static class LoadActionCreateActionFollowPath
	{
		//private object CreateInstance(Type type, int length)
		public static bool Prefix(ref object __result, Type type)
		{
			if (type == typeof(ActionFollowPath))
			{
				__result = new ActionFollowPath(new Queue<PathElement>(), 0, false);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PeepTracker), nameof(PeepTracker.OnAfterLoad))]
	public static class AfterLoadDontScrewWithPos
	{
		//public void OnAfterLoad()
		public static bool Prefix(PeepTracker __instance)
		{

			Grid grid = Game.Game.ctx.board.grid;
			foreach (Entity allEntity in Game.Game.ctx.entityman.GetAllEntities())
			{
				if (allEntity.components.peep != null)
				{
					GridPos gridpos = allEntity.data.placement.gridpos;
					__instance.MovePeep(allEntity, null, (GridPosF)gridpos);

					//And the game code here would now round the worldpos y down.
					//I imagine that's because people might have been interupted walking up stairs and would be floating, BECAUSE THE GAME DIDN'T SAVE THEIR ACTION STATE TO BEGIN WITH
					//Now that I have save/loading walking up stairs working, it doesn't need to do that anymore.
				}
			}
			return false;
		}
	}


	//Following up from above:
	//if (value is ActionWaitSeated || value is ActionWaitInLine || value is ActionWaitForElevator)
	//These need to be restarted on load, so use their loaded values:

	/*
	 * 
	 * TODO, though, these actions don't track their own actual time but their timeout time.
	 * The restaurant is the one who tracks the time for ActionWaitSeated when it does AddToSitting.
	 * Of course it'd be near impossible to resolve which sitting time refers to which person. So that timer will be restarted.
	 * Also, to play the correct animation, we'd need to save ReservationsComponent in the restaurant so they have an achor point to sit at. (created by ActionChangePatronReservation)
	 * instead they'll play the fallbackanims peepeat. Not so bad. Also applies to ActionWaitVending.
	[HarmonyPatch(typeof(ActionWaitSeated), nameof(ActionWaitSeated.OnStarted))]
	public static class ActionWaitSeatedRememberValues
	{
		public static void Prefix(ActionWaitSeated __instance, out float __state)
		{
			__state = __instance._endTime;
		}
		public static void Postfix(ActionWaitSeated __instance, float __state)
		{
			if (__state > 0)
				__instance._endTime = __state;
		}
	}
	*/

	//Waiting in line is easy. When space is available, we get callback and end the action.
	//After timeout, it fails.
	//Of course that means since people sitting have their time reset, the wait time here should also be reset. Shit
	//Okay. Nevermind about using saved timeout values here since they're unfair with longer expected wait times
	/*
	[HarmonyPatch(typeof(ActionWaitInLine), nameof(ActionWaitInLine.OnStarted))]
	public static class ActionWaitInLineRememberValues
	{
		public static void Prefix(ActionWaitInLine __instance, out float __state)
		{
			__state = __instance._endTime;
		}
		public static void Postfix(ActionWaitInLine __instance, float __state)
		{
			if (__state > 0)
				__instance._endTime = __state;
		}
	}
	*/

	//Honestly I don't think elevators really queue or take time anyway. This game doesn't have a detailed elevator system, just static wait times.
	[HarmonyPatch(typeof(ActionWaitForElevator), nameof(ActionWaitForElevator.OnStarted))]
	public static class ActionWaitForElevatorRememberValues
	{
		public static void Prefix(ActionWaitForElevator __instance, out float __state)
		{
			__state = __instance._timeoutTime;
		}
		public static void Postfix(ActionWaitForElevator __instance, float __state)
		{
			if (__state > 0)
				__instance._timeoutTime = __state;
		}
	}
}
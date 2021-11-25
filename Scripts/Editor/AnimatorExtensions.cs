// Some Harmony based Unity animator window patches to help workflow
// Copyright (c) 2021 Dj Lukis.LT
// MIT license (see LICENSE in https://github.com/lukis101/VRCUnityStuffs)

// Known issues:
// Unsupported.PasteToStateMachineFromPasteboard copies some parameters, but does not copy their default values
//	 It also does not have proper undo handling causing dangling sub-assets left in the controller
//	 TODO: add undo callback handler to delete sub-state machines properly
// State node motion label overlaps progress bar in "Live Link" mode

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;

namespace DJL
{
	[InitializeOnLoad]
	class AnimatorExtensions
	{
		public static int Version_major = 0;
		public static int Version_minor = 1;

		// Preferences
		private static bool ShowStateMotionLabel = true;
		private static bool ShowStateExtrasLabel = true;

		// Cache
		private static readonly Type AnimatorWindowType = AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool");
		private static readonly MethodInfo AnimatorControllerGetter = AccessTools.PropertyGetter(AnimatorWindowType, "animatorController");
		private static readonly Type LayerControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.LayerControllerView");
		private static readonly Type ParameterControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.ParameterControllerView");
		private static readonly Type RenameOverlayType = AccessTools.TypeByName("UnityEditor.RenameOverlay");
		private static readonly MethodInfo BeginRenameMethod = AccessTools.Method(RenameOverlayType, "BeginRename");
		private static readonly MethodInfo GetElementHeightMethod = AccessTools.Method(typeof(ReorderableList), "GetElementHeight", new Type[]{typeof(int)});
		private static readonly MethodInfo GetElementYOffsetMethod = AccessTools.Method(typeof(ReorderableList), "GetElementYOffset", new Type[]{typeof(int)});
		private static readonly FieldInfo LayerScrollField = AccessTools.Field(LayerControllerViewType, "m_LayerScroll");
		private static readonly FieldInfo LayerListField = AccessTools.Field(LayerControllerViewType, "m_LayerList");
		private static GUIStyle StateMotionStyle = null;
		private static GUIStyle StateExtrasStyle = null;

		private static bool _refocusSelectedLayer = false;

		static AnimatorExtensions()
		{
			var harmonyInstance = new Harmony("djl.animatorextensions");

			// --- Layer and parameter lists --- //
			// Workaround for layer list scroll reset
			MethodInfo resetui_target = AccessTools.Method(LayerControllerViewType, "ResetUI");
			MethodInfo resetui_prefix = AccessTools.Method(typeof(AnimatorExtensions), "ResetUI_Prefix");
			MethodInfo resetui_postfix = AccessTools.Method(typeof(AnimatorExtensions), "ResetUI_Postfix");
			harmonyInstance.Patch(resetui_target, prefix:new HarmonyMethod(resetui_prefix), postfix:new HarmonyMethod(resetui_postfix));

			// Scroll to parameter list bottom when adding a new one to see the rename field
			MethodInfo addparametermenu_target = AccessTools.Method(ParameterControllerViewType, "AddParameterMenu");
			MethodInfo addparametermenu_postfix = AccessTools.Method(typeof(AnimatorExtensions), "AddParameterMenu_Postfix");
			harmonyInstance.Patch(addparametermenu_target, postfix:new HarmonyMethod(addparametermenu_postfix));

			// Add extra options for layer list context menu
			MethodInfo ondrawlayer_target = AccessTools.Method(LayerControllerViewType, "OnDrawLayer");
			MethodInfo ondrawlayer_prefix = AccessTools.Method(typeof(AnimatorExtensions), "OnDrawLayer_Prefix");
			harmonyInstance.Patch(ondrawlayer_target, prefix: new HarmonyMethod(ondrawlayer_prefix));
			// And same via keyboard hooks
			MethodInfo layercontrollerongui_target = AccessTools.Method(LayerControllerViewType, "OnGUI");
			MethodInfo layercontrollerongui_prefix = AccessTools.Method(typeof(AnimatorExtensions), "LayerController_OnGUI_Prefix");
			harmonyInstance.Patch(layercontrollerongui_target, prefix:new HarmonyMethod(layercontrollerongui_prefix));


			// --- Main graph --- //
			// Click bottom bar to ping the actual controller asset
			MethodInfo dographbottombar_target = AccessTools.Method(AnimatorWindowType, "DoGraphBottomBar");
			MethodInfo dographbottombar_postfix = AccessTools.Method(typeof(AnimatorExtensions), "DoGraphBottomBar_postfix");
			harmonyInstance.Patch(dographbottombar_target, postfix:new HarmonyMethod(dographbottombar_postfix));

			// State info labels and rename trigger
			MethodInfo statenode_nodeui_target = AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateNode"), "NodeUI");
			MethodInfo nodeui_postfix = AccessTools.Method(typeof(AnimatorExtensions), "NodeUI_postfix");
			harmonyInstance.Patch(statenode_nodeui_target, postfix:new HarmonyMethod(nodeui_postfix));
			MethodInfo statemachinenode_nodeui_target = AccessTools.Method(AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.StateMachineNode"), "NodeUI");
			harmonyInstance.Patch(statemachinenode_nodeui_target, postfix:new HarmonyMethod(nodeui_postfix));


			// --- Extras --- //
			// Purposely break 'undo' of sub-state machine pasting as that leaves dead sub-assets inside the controller
			MethodInfo pastetostatemachine_target = typeof(Unsupported).GetMethod("PasteToStateMachineFromPasteboard", BindingFlags.Static | BindingFlags.Public);//AccessTools.Method(typeof(Unsupported), "PasteToStateMachineFromPasteboard");
			MethodInfo pastetostatemachine_postfix = AccessTools.Method(typeof(AnimatorExtensions), "PasteToStateMachineFromPasteboard_Postfix");
			harmonyInstance.Patch(pastetostatemachine_target, postfix:new HarmonyMethod(pastetostatemachine_postfix));

			// Prevent transition condition parameter change from altering the condition function
			Type AnimatorTransitionInspectorBaseType = AccessTools.TypeByName("UnityEditor.Graphs.AnimationStateMachine.AnimatorTransitionInspectorBase");
			MethodInfo drawconditionselement_target = AccessTools.Method(AnimatorTransitionInspectorBaseType, "DrawConditionsElement");
			MethodInfo drawconditionselement_prefix = AccessTools.Method(typeof(AnimatorExtensions), "DrawConditionsElement_Prefix");
			MethodInfo drawconditionselement_postfix = AccessTools.Method(typeof(AnimatorExtensions), "DrawConditionsElement_Postfix");
			harmonyInstance.Patch(drawconditionselement_target, prefix:new HarmonyMethod(drawconditionselement_prefix), postfix:new HarmonyMethod(drawconditionselement_postfix));

			// Preferences through window menu
			MethodInfo additemstomenu_target = AccessTools.Method(AnimatorWindowType, "AddItemsToMenu");
			MethodInfo additemstomenu_postfix = AccessTools.Method(typeof(AnimatorExtensions), "AddItemsToMenu_Postfix");
			harmonyInstance.Patch(additemstomenu_target, postfix:new HarmonyMethod(additemstomenu_postfix));

			// Load preferences
			ShowStateExtrasLabel = EditorPrefs.GetBool("AnimatorExtensions.ShowStateExtrasLabel", true);
			ShowStateMotionLabel = EditorPrefs.GetBool("AnimatorExtensions.ShowStateMotionLabel", true);
		}

		#region ExtraStuff
		// Toggle features through the window menu
		public static void AddItemsToMenu_Postfix(object __instance, GenericMenu menu)
		{
			menu.AddDisabledItem(new GUIContent("AExtenstions/Version: "+Version_major+"."+Version_minor));
			menu.AddSeparator("AExtenstions/");
			menu.AddItem(new GUIContent("AExtenstions/Extra Indicators"), ShowStateExtrasLabel, WindowMenuCallback, "ShowStateExtrasLabel");
			menu.AddItem(new GUIContent("AExtenstions/Motion name"), ShowStateMotionLabel, WindowMenuCallback, "ShowStateMotionLabel");
		}
		private static void WindowMenuCallback(object obj)
		{
			String varname = obj as string;
			if (varname.Equals("ShowStateExtrasLabel"))
			{
				ShowStateExtrasLabel = !ShowStateExtrasLabel;
				EditorPrefs.SetBool("AnimatorExtensions.ShowStateExtrasLabel", ShowStateMotionLabel);
			}
			else if (varname.Equals("ShowStateMotionLabel"))
			{
				ShowStateMotionLabel = !ShowStateMotionLabel;
				EditorPrefs.SetBool("AnimatorExtensions.ShowStateMotionLabel", ShowStateMotionLabel);
			}
		}

		// Prevent scroll position reset when rearranging or editing layers
		private static Vector2 _layerScrollCache;
		public static void ResetUI_Prefix(object __instance)
		{
			_layerScrollCache = (Vector2)LayerScrollField.GetValue(__instance);
		}
		public static void ResetUI_Postfix(object __instance)
		{
			Vector2 scrollpos = (Vector2)LayerScrollField.GetValue(__instance);
			if (scrollpos.y == 0)
				LayerScrollField.SetValue(__instance, _layerScrollCache);
			_refocusSelectedLayer = true; // Defer focusing to OnGUI to get latest list size and window rect
		}

		// Set scroll to far down when adding new parameter, gets clamped so good enough
		public static void AddParameterMenu_Postfix(object __instance, object value)
		{
			Traverse.Create(__instance).Field("m_ScrollPosition").SetValue(new Vector2(0, 9001));
		}

		// Break 'undo' of sub-state machine pasting
		public static void PasteToStateMachineFromPasteboard_Postfix(
			AnimatorStateMachine sm,
			AnimatorController controller,
			int layerIndex,
			Vector3 position)
		{
			//Debug.Log("Yeeet: "+Undo.GetCurrentGroupName());
			Undo.ClearUndo(sm);
		}

		// Prevent transition condition parameter change from altering the condition function
		private static int ConditionIndex;
		private static int ConditionMode_pre;
		private static string ConditionParam_pre;
		private static AnimatorControllerParameterType ConditionParamType_pre;
		public static void DrawConditionsElement_Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
		{
			ConditionIndex = index;
			SerializedProperty conditions = (SerializedProperty)Traverse.Create(__instance).Field("m_Conditions").GetValue();
			SerializedProperty arrayElementAtIndex = conditions.GetArrayElementAtIndex(index);
			ConditionMode_pre = arrayElementAtIndex.FindPropertyRelative("m_ConditionMode").intValue;
			ConditionParam_pre = arrayElementAtIndex.FindPropertyRelative("m_ConditionEvent").stringValue;

			AnimatorController ctrl = Traverse.Create(__instance).Field("m_Controller").GetValue() as AnimatorController;
			if (ctrl)
			{
				// Unity, why make IndexOfParameter(name) internal -_-
				foreach (var param in ctrl.parameters)
				{
					if (param.name.Equals(ConditionParam_pre))
					{
						ConditionParamType_pre = param.type;
						break;
					}
				}
			}
		}
		public static void DrawConditionsElement_Postfix(object __instance, Rect rect, int index, bool selected, bool focused)
		{
			if (ConditionIndex == index)
			{
				SerializedProperty conditions = (SerializedProperty)Traverse.Create(__instance).Field("m_Conditions").GetValue();
				SerializedProperty arrayElementAtIndex = conditions.GetArrayElementAtIndex(index);
				SerializedProperty m_ConditionMode = arrayElementAtIndex.FindPropertyRelative("m_ConditionMode");
				string conditionparam_post = arrayElementAtIndex.FindPropertyRelative("m_ConditionEvent").stringValue;

				if (!conditionparam_post.Equals(ConditionParam_pre) && (m_ConditionMode.intValue != ConditionMode_pre))
				{
					// Parameter and condition changed, restore condition if parameter type is same
					AnimatorController ctrl = Traverse.Create(__instance).Field("m_Controller").GetValue() as AnimatorController;
					if (ctrl)
					{
						// Unity, why make IndexOfParameter(name) internal -_-
						foreach (var param in ctrl.parameters)
						{
							if (param.name.Equals(conditionparam_post))
							{
								// same type or float->int, fully compatible
								if ((param.type == ConditionParamType_pre)
								    || ((ConditionParamType_pre == AnimatorControllerParameterType.Float) && (param.type == AnimatorControllerParameterType.Int)))
								{
									m_ConditionMode.intValue = ConditionMode_pre;
									Debug.Log("Restored transition condition mode");
								}
								// int->float has restrictions
								else if ((ConditionParamType_pre == AnimatorControllerParameterType.Int) && (param.type == AnimatorControllerParameterType.Float))
								{
									AnimatorConditionMode premode = (AnimatorConditionMode)ConditionMode_pre;
									if ((premode != AnimatorConditionMode.Equals) && (premode != AnimatorConditionMode.NotEqual))
									{
										m_ConditionMode.intValue = ConditionMode_pre;
										Debug.Log("Restored transition condition mode 2");
									}
								}
								break;
							}
						}
					}
				}
			}
		}
		#endregion ExtraStuff

		#region GraphStuff
		// Controller asset pinging/selection via bottom bar
		public static void DoGraphBottomBar_postfix(object __instance, Rect nameRect)
		{
			UnityEngine.Object ctrl = (UnityEngine.Object)AnimatorControllerGetter.Invoke(__instance, null);
			if (ctrl != (UnityEngine.Object)null)
			{
				EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link); // "I'm clickable!"

				Event current = Event.current;
				if (((current.type == EventType.MouseDown) && (current.button == 0)) && nameRect.Contains(current.mousePosition))
				{
					EditorGUIUtility.PingObject(ctrl);
					if (current.clickCount == 2) // Adhere to the 'select only on double click' convention
						Selection.activeObject = ctrl;
					current.Use();
				}
			}
		}

		// Show motion name and extra details on state graph nodes
		public static void NodeUI_postfix(object __instance, UnityEditor.Graphs.GraphGUI host)
		{
			// Figure out which node type
			AnimatorState astate = Traverse.Create(__instance).Field("state").GetValue<AnimatorState>();
			bool hasmotion = astate != null;
			AnimatorStateMachine asm = Traverse.Create(__instance).Field("stateMachine").GetValue<AnimatorStateMachine>();
			bool hassm = asm != null;

			// Lazy-init styles because built-in ones not available during static init
			if (StateMotionStyle == null)
			{
				StateExtrasStyle = new GUIStyle(EditorStyles.label);
				StateExtrasStyle.alignment = TextAnchor.UpperRight;
				StateExtrasStyle.fontStyle = FontStyle.Bold;
				StateMotionStyle = new GUIStyle(EditorStyles.label);
				StateMotionStyle.alignment = TextAnchor.LowerCenter;
			}
			Rect rect = GUILayoutUtility.GetLastRect();

			// Tags in corner, similar to what layer editor does
			if ((hasmotion || hassm) && ShowStateExtrasLabel)
			{
				string extralabel = "";
				if (hasmotion)
				{
					if (astate.behaviours != null)
						if (astate.behaviours.Length > 0)
							extralabel += "  B";
					if (astate.writeDefaultValues)
						extralabel += "  WD";
				}
				else
				{
					if (asm.behaviours != null)
						if (asm.behaviours.Length > 0)
							extralabel += "  B";
				}
				Rect extralabelrect = new Rect(rect.x, rect.y - 30, rect.width, 20);
				EditorGUI.LabelField(extralabelrect, extralabel, StateExtrasStyle);
			}

			// Name of Motion (btree or animation clip) at bottom
			// TODO? overlaps progress bar in play mode
			if (hasmotion && ShowStateMotionLabel)
			{
				string motionname = "[None]";
				if (astate.motion)
					motionname = "[" + astate.motion.name + "]";
				Rect motionlabelrect = new Rect(rect.x, rect.y - 10, rect.width, 20);
				EditorGUI.LabelField(motionlabelrect, motionname, StateMotionStyle);
			}

			Event current = Event.current;
			if (current.type == EventType.KeyDown && current.keyCode == KeyCode.F2)
			{
				Debug.Log("TODO: Rename");
			}
		}
		#endregion Graphstuff

		#region LayerStuff
		// Layer copy-pasting
		private static AnimatorControllerLayer _layerClipboard = null;
		private static AnimatorController _controllerClipboard = null;
		[HarmonyPriority(Priority.Low)] // Low to not consume event for more extensive tools
		public static void OnDrawLayer_Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
		{
			Event current = Event.current;
			if (((current.type == EventType.MouseUp) && (current.button == 1)) && rect.Contains(current.mousePosition))
			{
				current.Use();
				GenericMenu menu = new GenericMenu();
				menu.AddItem(EditorGUIUtility.TrTextContent("Copy layer", null, (Texture) null), false,
					new GenericMenu.MenuFunction2(AnimatorExtensions.CopyLayer), __instance);
				if (_layerClipboard != null)
				{
					menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer", null, (Texture) null), false,
						new GenericMenu.MenuFunction2(AnimatorExtensions.PasteLayer), __instance);
					menu.AddItem(EditorGUIUtility.TrTextContent("Paste layer settings", null, (Texture) null), false,
						new GenericMenu.MenuFunction2(AnimatorExtensions.PasteLayerSettings), __instance);
				}
				else
				{
					menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste layer", null, (Texture) null));
					menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste layer settings", null, (Texture) null));
				}
				menu.AddItem(EditorGUIUtility.TrTextContent("Delete layer", null, (Texture) null), false,
					new GenericMenu.MenuFunction(() => Traverse.Create(__instance).Method("DeleteLayer").GetValue(null)));
				menu.ShowAsContext();
			}
		}

		private static void CopyLayer(object layerControllerView)
		{
			var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
			var ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();
			_layerClipboard = rlist.list[rlist.index] as AnimatorControllerLayer;
			_controllerClipboard = ctrl;
			Unsupported.CopyStateMachineDataToPasteboard(_layerClipboard.stateMachine, ctrl, rlist.index);
		}

		public static void PasteLayer(object layerControllerView)
		{
			if (_layerClipboard == null)
				return;
			var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
			var ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();

			// Will paste layer right below selected one
			int targetindex = rlist.index + 1;
			string newname = ctrl.MakeUniqueLayerName(_layerClipboard.name);
			Undo.FlushUndoRecordObjects();

			// Use unity built-in function to clone state machine
			ctrl.AddLayer(newname);
			var layers = ctrl.layers;
			int pastedlayerindex = layers.Length - 1;
			var pastedlayer = layers[pastedlayerindex];
			Unsupported.PasteToStateMachineFromPasteboard(pastedlayer.stateMachine, ctrl, pastedlayerindex, Vector3.zero);

			// Promote from child to main
			var pastedsm = pastedlayer.stateMachine.stateMachines[0].stateMachine;
			pastedsm.name = newname;
			pastedlayer.stateMachine.stateMachines = new ChildAnimatorStateMachine[0];
			UnityEngine.Object.DestroyImmediate(pastedlayer.stateMachine, true);
			pastedlayer.stateMachine = pastedsm;
			PasteLayerProperties(pastedlayer, _layerClipboard);

			// Move up to desired spot
			for (int i = layers.Length-1; i > targetindex; i--)
				layers[i] = layers[i - 1];
			layers[targetindex] = pastedlayer;
			ctrl.layers = layers;

			 // Make layer unaffected by undo, forces user to delete manually but prevents dangling sub-assets
			Undo.ClearUndo(ctrl);

			// Pasting to different controller, sync parameters
			if (ctrl != _controllerClipboard)
			{
				Undo.IncrementCurrentGroup();
				int curgroup = Undo.GetCurrentGroup();
				Undo.RecordObject(ctrl, "Sync pasted layer parameters");

				// cache names
				// TODO: do this before pasting to workaround default values not being copied
				var destparams = new Dictionary<string, AnimatorControllerParameter>(ctrl.parameters.Length);
				foreach (var param in ctrl.parameters)
					destparams[param.name] = param;

				var srcparams = new Dictionary<string, AnimatorControllerParameter>(_controllerClipboard.parameters.Length);
				foreach (var param in _controllerClipboard.parameters)
					srcparams[param.name] = param;

				var queuedparams = new Dictionary<string, AnimatorControllerParameter>(_controllerClipboard.parameters.Length);

				// Recursively loop over all nested state machines
				GatherSmParams(pastedsm, ref srcparams, ref queuedparams);

				// Sync up whats missing
				foreach (var param in queuedparams.Values)
				{
					string pname = param.name;
					if (!destparams.ContainsKey(pname))
					{
						Debug.Log("Transferring parameter "+pname); // TODO: count or concatenate names?
						ctrl.AddParameter(param);
						// note: queuedparams should not have duplicates so don't need to append to destparams
					}
				}
				Undo.CollapseUndoOperations(curgroup);
			}

			EditorUtility.SetDirty(ctrl);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// Update list selection
			Traverse.Create(layerControllerView).Property("selectedLayerIndex").SetValue(targetindex);
		}

		public static void PasteLayerSettings(object layerControllerView)
		{
			var rlist = (ReorderableList)LayerListField.GetValue(layerControllerView);
			AnimatorController ctrl = Traverse.Create(layerControllerView).Field("m_Host").Property("animatorController").GetValue<AnimatorController>();

			var layers = ctrl.layers;
			var targetlayer = layers[rlist.index];
			PasteLayerProperties(targetlayer, _layerClipboard);
			ctrl.layers = layers; // needed for edits to apply
		}

		public static void PasteLayerProperties(AnimatorControllerLayer dest, AnimatorControllerLayer src)
		{
			dest.avatarMask = src.avatarMask;
			dest.blendingMode = src.blendingMode;
			dest.defaultWeight = src.defaultWeight;
			dest.iKPass = src.iKPass;
			dest.syncedLayerAffectsTiming = src.syncedLayerAffectsTiming;
			dest.syncedLayerIndex = src.syncedLayerIndex;
		}

		// Keyboard hooks for layer editing
		public static void LayerController_OnGUI_Prefix(object __instance, Rect rect)
		{
			var rlist = (ReorderableList)LayerListField.GetValue(__instance);
			if (rlist.HasKeyboardControl())
			{
				Event current = Event.current;
				switch (current.type)
				{
					case EventType.ExecuteCommand:
						if (current.commandName == "Copy")
						{
							current.Use();
							CopyLayer(__instance);
						}
						else if (current.commandName == "Paste")
						{
							current.Use();
							PasteLayer(__instance);
						}
						else if (current.commandName == "Duplicate")
						{
							current.Use();
							CopyLayer(__instance);
							PasteLayer(__instance);
							// todo: dupe without polluting clipboard
						}
						break;

					case EventType.KeyDown:
					{
						KeyCode keyCode = Event.current.keyCode;
						if (keyCode == KeyCode.F2) // Rename
						{
							current.Use();
							_refocusSelectedLayer = true;
							AnimatorControllerLayer layer = rlist.list[rlist.index] as AnimatorControllerLayer;
							var rovl = Traverse.Create(__instance).Property("renameOverlay").GetValue();
							BeginRenameMethod.Invoke(rovl, new object[] {layer.name, rlist.index, 0.1f});
							break;
						}
						break;
					}
				}
			}

			// Adjust scroll to get selected layer visible
			if (_refocusSelectedLayer)
			{
				_refocusSelectedLayer = false;
				Vector2 curscroll = (Vector2)LayerScrollField.GetValue(__instance);
				float height = (float)GetElementHeightMethod.Invoke(rlist, new object[] {rlist.index}) + 20;
				float offs = (float)GetElementYOffsetMethod.Invoke(rlist, new object[] {rlist.index});
				if (offs < curscroll.y)
					LayerScrollField.SetValue(__instance, new Vector2(curscroll.x,offs));
				else if (offs+height > curscroll.y+rect.height)
					LayerScrollField.SetValue(__instance, new Vector2(curscroll.x,offs+height-rect.height));
			}
		}

		// Recursive helper functions to gather deeply-nested parameter references
		private static void GatherBtParams(BlendTree bt,
			ref Dictionary<string, AnimatorControllerParameter> srcparams,
			ref Dictionary<string, AnimatorControllerParameter> queuedparams)
		{
			if (srcparams.ContainsKey(bt.blendParameter))
				queuedparams[bt.blendParameter] = srcparams[bt.blendParameter];
			if (srcparams.ContainsKey(bt.blendParameterY))
				queuedparams[bt.blendParameterY] = srcparams[bt.blendParameterY];

			foreach (var cmotion in bt.children)
			{
				if (srcparams.ContainsKey(cmotion.directBlendParameter))
					queuedparams[cmotion.directBlendParameter] = srcparams[cmotion.directBlendParameter];

				// Go deeper to nested BlendTrees
				var cbt = cmotion.motion as BlendTree;
				if (!(cbt is null))
					GatherBtParams(cbt, ref srcparams, ref queuedparams);
			}
		}
		private static void GatherSmParams(AnimatorStateMachine sm,
			ref Dictionary<string, AnimatorControllerParameter> srcparams,
			ref Dictionary<string, AnimatorControllerParameter> queuedparams)
		{
			// Go over states to check controlling or BlendTree params
			foreach (var cstate in sm.states)
			{
				var s = cstate.state;
				if (s.mirrorParameterActive && srcparams.ContainsKey(s.mirrorParameter))
					queuedparams[s.mirrorParameter] = srcparams[s.mirrorParameter];
				if (s.speedParameterActive && srcparams.ContainsKey(s.speedParameter))
					queuedparams[s.speedParameter] = srcparams[s.speedParameter];
				if (s.timeParameterActive && srcparams.ContainsKey(s.timeParameter))
					queuedparams[s.timeParameter] = srcparams[s.timeParameter];
				if (s.cycleOffsetParameterActive && srcparams.ContainsKey(s.cycleOffsetParameter))
					queuedparams[s.cycleOffsetParameter] = srcparams[s.cycleOffsetParameter];

				var bt = s.motion as BlendTree;
				if (!(bt is null))
					GatherBtParams(bt, ref srcparams, ref queuedparams);
			}

			// Go over all transitions
			var transitions = new List<AnimatorStateTransition>(sm.anyStateTransitions.Length);
			transitions.AddRange(sm.anyStateTransitions);
			foreach (var cstate in sm.states)
				transitions.AddRange(cstate.state.transitions);
			foreach (var transition in transitions)
			foreach (var cond in transition.conditions)
				if (srcparams.ContainsKey(cond.parameter))
					queuedparams[cond.parameter] = srcparams[cond.parameter];

			// Go deeper to child sate machines
			foreach (var csm in sm.stateMachines)
				GatherSmParams(csm.stateMachine, ref srcparams, ref queuedparams);
		}
		#endregion Layerstuff
	}
}
#endif

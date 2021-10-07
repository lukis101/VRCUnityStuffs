// Some Harmony based Unity animator window patches to help workflow
// Copyright (c) 2021 Dj Lukis.LT
// MIT license (see LICENSE in https://github.com/lukis101/VRCUnityStuffs)

// Known issue: Unsupported.PasteToStateMachineFromPasteboard copies some parameters, but does not copy their default values

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
		//private static readonly Type AnimatorWindowType = AccessTools.TypeByName("UnityEditor.Graphs.AnimatorControllerTool");
		private static readonly Type LayerControllerViewType = AccessTools.TypeByName("UnityEditor.Graphs.LayerControllerView");
		private static readonly Type RenameOverlayType = AccessTools.TypeByName("UnityEditor.RenameOverlay");
		private static readonly MethodInfo BeginRenameMethod = AccessTools.Method(RenameOverlayType, "BeginRename");
		private static readonly MethodInfo GetElementHeightMethod = AccessTools.Method(typeof(ReorderableList), "GetElementHeight", new Type[]{typeof(int)});
		private static readonly MethodInfo GetElementYOffsetMethod = AccessTools.Method(typeof(ReorderableList), "GetElementYOffset", new Type[]{typeof(int)});
		private static readonly FieldInfo LayerScrollField = AccessTools.Field(LayerControllerViewType, "m_LayerScroll");
		private static readonly FieldInfo LayerListField = AccessTools.Field(LayerControllerViewType, "m_LayerList");
		private static bool _refocusSelectedLayer = false;

		static AnimatorExtensions()
		{
			var harmonyInstance = new Harmony("djl.animatorextensions");
		
			// Workaround for layer list scroll reset
			MethodInfo resetui_target = AccessTools.Method(LayerControllerViewType, "ResetUI");
			MethodInfo resetui_prefix = AccessTools.Method(typeof(AnimatorExtensions), "ResetUI_Prefix");
			MethodInfo resetui_postfix = AccessTools.Method(typeof(AnimatorExtensions), "ResetUI_Postfix");
			harmonyInstance.Patch(resetui_target, prefix:new HarmonyMethod(resetui_prefix), postfix:new HarmonyMethod(resetui_postfix));
			
			// Add extra options for layer list context menu
			MethodInfo ondrawlayer_target = AccessTools.Method(LayerControllerViewType, "OnDrawLayer");
			MethodInfo ondrawlayer_prefix = AccessTools.Method(typeof(AnimatorExtensions), "OnDrawLayer_Prefix");
			harmonyInstance.Patch(ondrawlayer_target, prefix: new HarmonyMethod(ondrawlayer_prefix));
			// And same via keyboard hooks
			MethodInfo layercontrollerongui_target = AccessTools.Method(LayerControllerViewType, "OnGUI");
			MethodInfo layercontrollerongui_prefix = AccessTools.Method(typeof(AnimatorExtensions), "LayerController_OnGUI_Prefix");
			harmonyInstance.Patch(layercontrollerongui_target, prefix:new HarmonyMethod(layercontrollerongui_prefix));
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

		// Layer copy-pasting
		private static AnimatorControllerLayer _layerClipboard = null;
		private static AnimatorController _controllerClipboard = null;
		[HarmonyPriority(Priority.Low)] // Low to not consume event for more extensive tools
		public static void OnDrawLayer_Prefix(object __instance, Rect rect, int index, bool selected, bool focused)
		{
			Event current = Event.current;
			if (((current.type == EventType.MouseUp) && (current.button == 1)) && rect.Contains(current.mousePosition))
			{
				Event.current.Use();
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

			// Pasting to different controller, sync parameters
			if (ctrl != _controllerClipboard)
			{
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

			Debug.LogWarning("Copy! "+ctrl.name);
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
	}
}
#endif

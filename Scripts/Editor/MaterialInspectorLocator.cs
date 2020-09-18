using UnityEditor;
using UnityEngine;

namespace DJL
{
	public class MaterialInspectorLocator
	{
		[MenuItem("CONTEXT/Material/Select Material")]
		private static void SelectAssetObject(MenuCommand command)
		{
			Material obj = command.context as Material;
			 
			// Select the object in the project folder
			Selection.activeObject = obj;
			 
			// Also flash the folder yellow to highlight it
			EditorGUIUtility.PingObject(obj);
		}
	}
}

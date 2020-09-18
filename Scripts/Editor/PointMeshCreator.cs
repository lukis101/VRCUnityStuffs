using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DJL
{
	public class PointMeshCreator : EditorWindow
	{
		public Vector3 meshSize = new Vector3(1, 1, 1);
		public Vector3 originPos = new Vector3(0, 0, 0);
		public Vector3Int meshDensity = new Vector3Int(10, 10, 10);


		[MenuItem("Tools/DJL/Create Point Mesh")]
		public static void ShowWindow()
		{
			GetWindow<PointMeshCreator>(true, "Create Point Mesh", true);
		}

		void OnGUI()
		{
			meshSize = EditorGUILayout.Vector3Field("Mesh size", meshSize, GUILayout.Width(300));
			meshDensity = EditorGUILayout.Vector3IntField("Point amount", meshDensity, GUILayout.Width(300));
			originPos = EditorGUILayout.Vector3Field("Origin (unit)", originPos, GUILayout.Width(300));

			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Create Mesh", GUILayout.Width(100), GUILayout.Height(30)))
			{
				string path = EditorUtility.SaveFilePanelInProject("Save mesh to", "Pmesh.asset", "asset", "message");
				if (path.Length == 0)
					return;

				// TODO: check if asset exists and update it instead

				int numpoints = meshDensity.x * meshDensity.y * meshDensity.z;
				Mesh mesh = new Mesh();
				mesh.name = "Pointmesh";
				mesh.indexFormat = numpoints > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

				List<Vector3> vertices = new List<Vector3>();
				Vector3 step = new Vector3(meshSize.x/(meshDensity.x-1),meshSize.y/(meshDensity.y-1), meshSize.z/(meshDensity.z-1));
				Vector3 offs = Vector3.Scale(meshSize, originPos);
				for (int z = 0; z < meshDensity.z; z++)
					for (int y = 0; y < meshDensity.y; y++)
						for (int x = 0; x < meshDensity.x; x++)
							vertices.Add(new Vector3(step.x*x - offs.x, step.y*y - offs.y, step.z*z - offs.z));
				mesh.vertices = vertices.ToArray();
				int[] indices = new int[numpoints];
				for (int i = 0; i < indices.Length; i++)
					indices[i] = i;
				mesh.SetIndices(indices, MeshTopology.Points, 0);

				//mesh.Optimize();
				mesh.RecalculateBounds();

				AssetDatabase.CreateAsset(mesh, path);
				AssetDatabase.SaveAssets();
			}
		}
	}
}

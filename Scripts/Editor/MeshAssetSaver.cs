using UnityEditor;
using UnityEngine;

namespace DJL
{
	public static class MeshAssetSaver
	{
		[MenuItem("CONTEXT/MeshFilter/Export as asset")]
		public static void SaveMeshInPlace(MenuCommand menuCommand) {
			MeshFilter mf = menuCommand.context as MeshFilter;
			Mesh m = mf.sharedMesh;
			SaveMesh(m, m.name, true, false);
		}

		[MenuItem("CONTEXT/MeshFilter/Optimise and export as asset")]
		public static void SaveMeshNewInstanceItem(MenuCommand menuCommand) {
			MeshFilter mf = menuCommand.context as MeshFilter;
			Mesh m = mf.sharedMesh;
			SaveMesh(m, m.name, true, true);
		}

		[MenuItem("CONTEXT/SkinnedMeshRenderer/Export as asset/Regular")]
		public static void SaveSkinnedMeshInPlace(MenuCommand menuCommand) {
			SkinnedMeshRenderer smr = menuCommand.context as SkinnedMeshRenderer;
			Mesh m = smr.sharedMesh;
			SaveMesh(m, m.name, true, false);
		}

		[MenuItem("CONTEXT/SkinnedMeshRenderer/Export as asset/Optimized")]
		public static void SaveSkinnedMeshNewInstanceItem(MenuCommand menuCommand) {
			SkinnedMeshRenderer smr = menuCommand.context as SkinnedMeshRenderer;
			Mesh m = smr.sharedMesh;
			SaveMesh(m, m.name, true, true);
		}

		[MenuItem("CONTEXT/SkinnedMeshRenderer/Export as asset/With custom bounds")]
		public static void SaveSkinnedMeshInPlaceBounds(MenuCommand menuCommand) {
			SkinnedMeshRenderer smr = menuCommand.context as SkinnedMeshRenderer;
			Mesh m = smr.sharedMesh;
			//Bounds bounds = m.bounds
			m.bounds = smr.localBounds;
			SaveMesh(m, m.name, true, false);
		}

		// Bounds copy-pasting
		static bool boundscopied = false;
		static Bounds copiedbounds = new Bounds();
		[MenuItem("CONTEXT/SkinnedMeshRenderer/Copy Bounds")]
		public static void CopySkinnedMeshBounds(MenuCommand menuCommand)
		{
			SkinnedMeshRenderer smr = menuCommand.context as SkinnedMeshRenderer;
			copiedbounds = smr.localBounds;
			boundscopied = true;
		}
		[MenuItem("CONTEXT/MeshRenderer/Copy Bounds")]
		public static void CopyMeshRendererBounds(MenuCommand menuCommand)
		{
			MeshRenderer mr = menuCommand.context as MeshRenderer;
			copiedbounds = mr.bounds;
			boundscopied = true;
		}
		[MenuItem("CONTEXT/Mesh/Copy Bounds")]
		public static void CopyMeshBounds(MenuCommand menuCommand)
		{
			Mesh smr = menuCommand.context as Mesh;
			copiedbounds = smr.bounds;
			boundscopied = true;
		}

		[MenuItem("CONTEXT/SkinnedMeshRenderer/Paste Bounds", true)]
		public static bool CanPasteSkinnedMeshBounds(MenuCommand menuCommand)
		{
			return boundscopied;
		}
		[MenuItem("CONTEXT/SkinnedMeshRenderer/Paste Bounds", false)]
		public static void PasteSkinnedMeshBounds(MenuCommand menuCommand)
		{
			SkinnedMeshRenderer smr = menuCommand.context as SkinnedMeshRenderer;
			Undo.RecordObject(smr, "Paste Bounds");
			smr.localBounds = copiedbounds;
		}

		public static void SaveMesh (Mesh mesh, string name, bool makeNewInstance, bool optimizeMesh) {
			string path = EditorUtility.SaveFilePanel("Save Separate Mesh Asset", "Assets/", name, "asset");
			if (string.IsNullOrEmpty(path)) return;
			
			path = FileUtil.GetProjectRelativePath(path);

			Mesh meshToSave = (makeNewInstance) ? UnityEngine.Object.Instantiate(mesh) as Mesh : mesh;

			if (optimizeMesh)
				 MeshUtility.Optimize(meshToSave);
			
			AssetDatabase.CreateAsset(meshToSave, path);
			AssetDatabase.SaveAssets();
		}
	}
}

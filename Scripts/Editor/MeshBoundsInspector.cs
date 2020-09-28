
// Editor script to draw regular mesh bounds like it's done for skinned meshes
// Based on Unity C# reference source
// https://github.com/Unity-Technologies/UnityCsReference/blob/2018.4/Editor/Mono/Inspector/SkinnedMeshRendererEditor.cs

// TODO: add a way of hiding, as minimizing component does not

using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

[CustomEditor(typeof(MeshFilter))]
[CanEditMultipleObjects]
public class MeshBoundsInspector : Editor
{
    private BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();
    public void OnEnable()
    {
        // https://github.com/Unity-Technologies/UnityCsReference/blob/046023e393f90a7952d53a7386bfcfd231bf4870/Editor/Mono/Handles.cs#L55
        Color s_BoundingBoxHandleColor = new Color(255, 255, 255, 150) / 255;
        m_BoundsHandle.SetColor(s_BoundingBoxHandleColor);
    }

    public void OnSceneGUI()
    {
        if (!target)
            return;
        MeshFilter mf = (MeshFilter)target;
        var mesh = mf.sharedMesh;
        if (!mesh)
            return;

        using (new Handles.DrawingScope(mf.transform.localToWorldMatrix))
        {
            Bounds bounds = mesh.bounds;
            m_BoundsHandle.center = bounds.center;
            m_BoundsHandle.size = bounds.size;

            m_BoundsHandle.DrawHandle();
        }
    }
}

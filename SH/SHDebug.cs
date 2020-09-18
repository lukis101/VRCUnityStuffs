using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class SHDebug : MonoBehaviour
{
	public Transform anchor;
    public Vector3[] SH;

    public Vector3 L0;

    public Vector4 unity_SHAr;
    public Vector4 unity_SHAg;
    public Vector4 unity_SHAb;
    public Vector4 unity_SHBr;
    public Vector4 unity_SHBg;
    public Vector4 unity_SHBb;
    public Vector4 unity_SHC;

    // Based on https://github.com/keijiro/LightProbeUtility
    // SHB.yz were fixed according to findings at https://forum.unity.com/threads/sampling-lightprobes-from-a-script.418945/
    // Native way would be to use MaterialPropertyBlock.CopySHCoefficientArraysFrom
    void pupulateSHvars(SphericalHarmonicsL2 sh, int i, ref Vector4 SHA, ref Vector4 SHB, ref Vector4 SHC)
    {
        // Linear and Constant
        SHA = new Vector4(
            sh[i, 3], sh[i, 1], sh[i, 2], sh[i, 0] - sh[i, 6]
        );

        // Quadratic polynomials
        SHB = new Vector4(
            sh[i, 4], sh[i, 5], sh[i, 6] * 3, sh[i, 7]
        );

        // Final quadratic polynomial
        SHC[i] = sh[i, 8];
        SHC.w = 1;
    }

    void Update()
    {
        SphericalHarmonicsL2 sh;
		Vector3 pos = (anchor) ? anchor.position : transform.position;
        LightProbes.GetInterpolatedProbe(pos, null, out sh);
        if (SH.Length != 9)
            SH = new Vector3[9];
        for (int i=0; i<9; i++)
        {
            SH[i] = new Vector3(sh[0, i], sh[1, i], sh[2, i]);
        }

        pupulateSHvars(sh, 0, ref unity_SHAr, ref unity_SHBr, ref unity_SHC);
        pupulateSHvars(sh, 1, ref unity_SHAg, ref unity_SHBg, ref unity_SHC);
        pupulateSHvars(sh, 2, ref unity_SHAb, ref unity_SHBb, ref unity_SHC);

        L0 = new Vector3(sh[0, 0] - sh[0, 6], sh[1, 0] - sh[1, 6], sh[2, 0] - sh[2, 6]);
    }
}
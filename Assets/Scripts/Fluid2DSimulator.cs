using System;
using UnityEngine;

public class Fluid2DSimulator : MonoBehaviour
{
    #region UI Variable

    public float Gravity      = -9.8f; // 重力
    public int   NumIteration = 10;    // 迭代次数

    public RenderTexture RenderTexture;

    #endregion

    #region System Event

    private void Awake()
    {
        Init(0.5f,
             RenderTexture.width,
             RenderTexture.height,
             1.0f);
    }

    #endregion

    private void Update()
    {
        var dt = Time.deltaTime / NumIteration;
        for (int i = 0; i < NumIteration; i++)
        {
            Integrate(dt, Gravity);
        }
    }

    public float   density;
    public int     numX;
    public int     numY;
    public int     numCells;
    public float   h;
    public float[] u;
    public float[] v;
    public float[] newU;
    public float[] newV;
    public float[] p;
    public float[] s;
    public float[] m;
    public float[] newM;

    public void Init(float densityVar, int numXVar, int numYVar, float hVar)
    {
        density = densityVar;
        numX = numXVar;
        numY = numYVar;
        h = hVar;
        numCells = numX * numY;
        u = new float[numCells];
        v = new float[numCells];
        newU = new float[numCells];
        newV = new float[numCells];
        p = new float[numCells];
        s = new float[numCells];
        m = new float[numCells];
        newM = new float[numCells];
        Array.Fill(m, 1f);
    }

    public void Integrate(float deltaTime, float gravity)
    {
        for (int i = 1; i < numX; i++)
        for (int j = 1; j < numY - 1; j++)
        {
            if (s[i * numY + j] != 0 && s[i * numY + j - 1] != 0)
            {
                v[i * numY + j] += gravity * deltaTime;
            }
        }
    }
}
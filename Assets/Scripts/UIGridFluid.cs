using System;
using UnityEngine;

public class UIGridFluid : MonoBehaviour
{
    [HideInInspector] public SpriteRenderer _Sprite;
    [HideInInspector] public Texture2D      _Texture;

    public int Length;
    /// <summary>
    ///     密度
    /// </summary>
    public float Density;
    /// <summary>
    ///     单个单元格所占 uv 大小
    /// </summary>
    [HideInInspector] public float Dh;
    /// <summary>
    ///     横向速度场
    /// </summary>
    [HideInInspector] public float[] UVelocity;
    [HideInInspector] public float[] NewUVelocity;
    /// <summary>
    ///     垂直速度场
    /// </summary>
    [HideInInspector] public float[] VVelocity;
    [HideInInspector] public float[] NewVVelocity;
    /// <summary>
    ///     可运行块
    /// </summary>
    [HideInInspector] public bool[] Blocks;
    /// <summary>
    ///     烟雾
    /// </summary>
    [HideInInspector] public float[] SmokeBlocks;
    [HideInInspector] public float[] NewSmokeBlocks;
    /// <summary>
    ///     压力
    /// </summary>
    [HideInInspector] public float[] Pressure;

    [Range(1f, 2f)]
    public float OverRelaxation = 1.9f;
    [Range(0f, 1000f)]
    public float FirstVelocity = 10f;

    [Range(1f, 20f)]
    public int Iterations = 10;
    private int CellNum;

    private int NumX;
    private int NumY;
    
    public void ChangeVelocity(float value)
    {
        FirstVelocity = Mathf.Clamp01(value) * 1000f;
    }

    private void Awake()
    {
        Init();
    }

    private void Update()
    {
        // 初始化速度场
        int halfY = NumY / 2;
        UVelocity[(halfY - 1) * (NumX + 1) + NumX * 3 / 4] = FirstVelocity;
        UVelocity[halfY * (NumX + 1) + NumX * 3 / 4] = FirstVelocity;
        UVelocity[(halfY + 1) * (NumX + 1) + NumX * 3 / 4] = FirstVelocity;

        VelocitySimulation(Iterations, Time.deltaTime);
        AdvectVelocity(Time.deltaTime);
        AdvectSmoke(Time.deltaTime);
        RenderToTexture2D();
    }

    public void WritePressure()
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (float f in Pressure)
        {
            min = Mathf.Min(min, f);
            max = Mathf.Max(max, f);
        }

        for (int j = 0; j < NumY; j++)
        for (int i = 0; i < NumX; i++)
        {
            _Texture.SetPixel(i, j, getSciColor(Pressure[i + j * NumX], min, max));
        }
    }

    public void WriteSmoke()
    {
        for (int j = 0; j < NumY; j++)
        for (int i = 0; i < NumX; i++)
        {
            float smoke = Mathf.Clamp01(SmokeBlocks[i + j * NumX]);
            Color32 color32 = new(Convert.ToByte(255f * smoke), Convert.ToByte(255f * smoke), Convert.ToByte(255f * smoke), 255);
            _Texture.SetPixel(i, j, color32);
        }
    }

    public void RenderToTexture2D()
    {
        WriteSmoke();
        _Texture.Apply();
    }

    private void Init()
    {
        Dh = 1f / Length;
        NumX = Length;
        NumY = Length;
        CellNum = NumX * NumY;

        UVelocity = new float[(NumX + 1) * NumY];
        VVelocity = new float[NumX * (NumY + 1)];
        NewUVelocity = new float[(NumX + 1) * NumY];
        NewVVelocity = new float[NumX * (NumY + 1)];
        Blocks = new bool[CellNum];
        SetActiveBlocks();
        Pressure = new float[CellNum];
        SmokeBlocks = new float[CellNum];
        NewSmokeBlocks = new float[CellNum];

        for (int j = 0; j < NumY; j++)
        for (int i = 0; i < NumX; i++)
        {
            if ((new Vector2(i, j) * Dh - Vector2.one * 0.5f).magnitude <= 0.01f)
            {
                SmokeBlocks[j * NumX + i] = 0f;
            }
            else
            {
                SmokeBlocks[j * NumX + i] = 1f;
            }
        }

        {
            _Texture = new Texture2D(NumX, NumY, TextureFormat.RGBA32, false);
            _Texture.filterMode = FilterMode.Point;
            _Texture.wrapMode = TextureWrapMode.Clamp;
            for (int j = 0; j < NumY; j++)
            for (int i = 0; i < NumX; i++)
            {
                _Texture.SetPixel(i, j, Color.black);
            }

            _Texture.Apply();
        }
        {
            _Sprite = GetComponent<SpriteRenderer>();
            _Sprite.sprite = Sprite.Create(_Texture, new Rect(0, 0, NumX, NumY), Vector2.one * 0.5f);
        }
    }

    private void SetActiveBlocks()
    {
        for (int y = 1; y < NumY - 1; y++)
        for (int x = 1; x < NumX - 1; x++)
        {
            Blocks[x + y * NumX] = true;
        }
    }

    /// <summary>
    ///     速度场更新
    /// </summary>
    private void VelocitySimulation(int iterations, float deltaTime)
    {
        // 清空压力
        Array.Clear(Pressure, 0, Pressure.Length);

        float cp = Density * Dh / deltaTime;
        for (int iterationCount = 0; iterationCount < iterations; iterationCount++)
        {
            for (int j = 0; j < NumY; j++)
            for (int i = 0; i < NumX; i++)
            {
                if (!Blocks[j * NumX + i])
                {
                    continue;
                }

                float sLeft = GetBlock(i - 1, j) ? 1f : 0f;
                float sRight = GetBlock(i + 1, j) ? 1f : 0f;
                float sBottom = GetBlock(i, j - 1) ? 1f : 0f;
                float sTop = GetBlock(i, j + 1) ? 1f : 0f;

                float s = sLeft + sRight + sBottom + sTop;
                if (s <= 0f)
                {
                    continue;
                }

                float uLeft = UVelocity[j * (NumX + 1) + i];
                float uRight = UVelocity[j * (NumX + 1) + i + 1];
                float vBottom = VVelocity[j * NumX + i];
                float vTop = VVelocity[(j + 1) * NumX + i];

                float div = OverRelaxation * 0.5f * (uRight - uLeft + vTop - vBottom);

                UVelocity[j * (NumX + 1) + i] = uLeft + div * sLeft / s;
                UVelocity[j * (NumX + 1) + i + 1] = uRight - div * sRight / s;
                VVelocity[j * NumX + i] = vBottom + div * sBottom / s;
                VVelocity[(j + 1) * NumX + i] = vTop - div * sTop / s;

                Pressure[j * NumX + i] += div / s * cp;
            }
        }
    }

    private void Swap(ref float[] o1, ref float[] o2)
    {
        float[] temp = o1;
        o1 = o2;
        o2 = temp;
    }

    /// <summary>
    ///     移流速度场
    /// </summary>
    private void AdvectVelocity(float deltaTime)
    {
        // U Update

        for (int j = 0; j < NumY; j++)
        for (int i = 0; i < NumX + 1; i++)
        {
            float u = UVelocity[j * (NumX + 1) + i];
            float v = AvgV(i, j);
            Vector2 pos = (new Vector2(i, j) + new Vector2(0, 0.5f)) * Dh - new Vector2(u, v) * deltaTime;
            float sample = SampleUVelocity(pos.x, pos.y);
            NewUVelocity[j * (NumX + 1) + i] = sample;
        }

        // V Update
        for (int j = 0; j < NumY + 1; j++)
        for (int i = 0; i < NumX; i++)
        {
            float u = AvgU(i, j);
            float v = VVelocity[j * NumX + i];
            Vector2 pos = (new Vector2(i, j) + new Vector2(0.5f, 0)) * Dh - new Vector2(u, v) * deltaTime;
            float sample = SampleVVelocity(pos.x, pos.y);
            NewVVelocity[j * NumX + i] = sample;
        }

        Swap(ref NewUVelocity, ref UVelocity);
        Swap(ref NewVVelocity, ref VVelocity);
    }

    private void AdvectSmoke(float deltaTime)
    {
        for (int j = 0; j < NumY; j++)
        for (int i = 0; i < NumX; i++)
        {
            if (SmokeBlocks[j * NumX + i] > 0f)
            {
                float u = 0.5f * (UVelocity[j * (NumX + 1) + i] + UVelocity[j * (NumX + 1) + i + 1]);
                float v = 0.5f * (VVelocity[j * NumX + i] + VVelocity[(j + 1) * NumX + i]);
                Vector2 dir = new(u, v);
                Vector2 pos = (new Vector2(i, j) + Vector2.one * 0.5f) * Dh - dir * deltaTime;
                float sample = SampleSmoke(pos.x, pos.y);
                NewSmokeBlocks[j * NumX + i] = sample;
            }
        }

        Swap(ref NewSmokeBlocks, ref SmokeBlocks);
    }

    private bool GetBlock(int x, int y)
    {
        if (x < 0 || y < 0 || NumX <= x || NumY <= y)
        {
            return false;
        }
        else
        {
            return Blocks[y * NumX + x];
        }
    }

    private float SampleUVelocity(float vu, float vv)
    {
        vu = Mathf.Clamp01(vu);
        vv = Mathf.Clamp01(vv);

        vv -= Dh * 0.5f;

        float dx = vu / Dh;
        float dy = vv / Dh;

        int ix = Mathf.Clamp(Mathf.FloorToInt(dx), 0, NumX - 1);
        int iy = Mathf.Clamp(Mathf.FloorToInt(dy), 0, NumY - 2);

        float w01 = dx - ix;
        float w11 = dy - iy;
        float w00 = 1f - w01;
        float w10 = 1f - w11;

        return w01 * w11 * UVelocity[(iy + 1) * (NumX + 1) + ix + 1]
             + w00 * w11 * UVelocity[(iy + 1) * (NumX + 1) + ix]
             + w01 * w10 * UVelocity[iy * (NumX + 1) + ix + 1]
             + w00 * w10 * UVelocity[iy * (NumX + 1) + ix];
    }

    private float SampleVVelocity(float vu, float vv)
    {
        vu = Mathf.Clamp01(vu);
        vv = Mathf.Clamp01(vv);

        vu -= Dh * 0.5f;

        float dx = vu / Dh;
        float dy = vv / Dh;

        int ix = Mathf.Clamp(Mathf.FloorToInt(dx), 0, NumX - 2);
        int iy = Mathf.Clamp(Mathf.FloorToInt(dy), 0, NumY - 1);

        float w01 = dx - ix;
        float w11 = dy - iy;
        float w00 = 1f - w01;
        float w10 = 1f - w11;

        return w01 * w11 * VVelocity[(iy + 1) * NumX + ix + 1]
             + w00 * w11 * VVelocity[(iy + 1) * NumX + ix]
             + w01 * w10 * VVelocity[iy * NumX + ix + 1]
             + w00 * w10 * VVelocity[iy * NumX + ix];
    }

    private float SampleSmoke(float vu, float vv)
    {
        vu = Mathf.Clamp01(vu);
        vv = Mathf.Clamp01(vv);

        vu -= Dh * 0.5f;
        vv -= Dh * 0.5f;

        float dx = vu / Dh;
        float dy = vv / Dh;

        int ix = Mathf.Clamp(Mathf.FloorToInt(dx), 0, NumX - 2);
        int iy = Mathf.Clamp(Mathf.FloorToInt(dy), 0, NumY - 2);

        float w01 = dx - ix;
        float w11 = dy - iy;
        float w00 = 1f - w01;
        float w10 = 1f - w11;

        return w01 * w11 * SmokeBlocks[(iy + 1) * NumX + ix + 1]
             + w00 * w11 * SmokeBlocks[(iy + 1) * NumX + ix]
             + w01 * w10 * SmokeBlocks[iy * NumX + ix + 1]
             + w00 * w10 * SmokeBlocks[iy * NumX + ix];
    }

    private float AvgU(int vx, int vy)
    {
        if (vy == 0)
        {
            return 0.25f * (UVelocity[vx]
                          + UVelocity[vx + 1]);
        }
        else if (vy == NumY)
        {
            return 0.25f * (UVelocity[(vy - 1) * (NumX + 1) + vx]
                          + UVelocity[(vy - 1) * (NumX + 1) + vx + 1]);
        }
        else
        {
            return 0.25f * (UVelocity[vy * (NumX + 1) + vx]
                          + UVelocity[vy * (NumX + 1) + vx + 1]
                          + UVelocity[(vy - 1) * (NumX + 1) + vx]
                          + UVelocity[(vy - 1) * (NumX + 1) + vx + 1]);
        }
    }

    private float AvgV(int ux, int uy)
    {
        if (ux == 0)
        {
            return 0.25f * (VVelocity[uy * NumX]
                          + VVelocity[(uy + 1) * NumX]);
        }
        else if (ux == NumX)
        {
            return 0.25f * (VVelocity[uy * NumX + ux - 1]
                          + VVelocity[(uy + 1) * NumX + ux - 1]);
        }
        else
        {
            return 0.25f * (VVelocity[uy * NumX + ux]
                          + VVelocity[(uy + 1) * NumX + ux]
                          + VVelocity[uy * NumX + ux - 1]
                          + VVelocity[(uy + 1) * NumX + ux - 1]);
        }
    }

    public Color32 getSciColor(float val, float minVal, float maxVal)
    {
        val = Mathf.Clamp(val, minVal, maxVal);
        float d = maxVal - minVal;
        val = d == 0.0f ? 0.5f : (val - minVal) / d;
        float m = 0.25f;
        float num = Mathf.Floor(val / m);
        float s = Mathf.Clamp01((val - num * m) / m);
        float r, g, b;

        switch (num)
        {
            case 0:
                r = 0.0f;
                g = s;
                b = 1.0f;
                break;
            case 1:
                r = 0.0f;
                g = 1.0f;
                b = 1.0f - s;
                break;
            case 2:
                r = s;
                g = 1.0f;
                b = 0.0f;
                break;
            default:
                r = 1.0f;
                g = Mathf.Clamp01(1.0f - s);
                b = 0.0f;
                break;
        }

        return new Color32(Convert.ToByte(255f * r), Convert.ToByte(255f * g), Convert.ToByte(255f * b), 255);
    }
}
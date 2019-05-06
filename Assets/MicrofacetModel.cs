using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 微表面反射模型
/// </summary>
class MicrofacetModel
{
    float s = 0.8f;
    float d = 0.2f;
    public float m = 0.5f;
    public float FresnelScale = 0.2f;

    /// <summary>
    /// 高光 0~1
    /// </summary>
    public float S { get { return s; } set { s = value; d = 1 - s; } }

    /// <summary>
    /// 反射 0~1
    /// </summary>
    public float D { get { return d; } set { d = value; s = 1 - d; } }


    public enum Effect
    {
        Wall//墙
            , Carbon//碳
            , Rubber//橡胶
            , Obsidian//黑曜石
            , Lunar_Dust//月尘
            , Olive_Drab//橄榄褐色
            , Rust//生锈
    }


    Effect effective;
    public Effect Effective
    {
        get { return effective; }
        set
        {
            effective = value;
            switch (value)
            {
                case Effect.Wall: S = 0.0f; m = 0.0f; break;
                case Effect.Carbon: S = 0.3f; m = 0.4f; break;
                case Effect.Rubber: S = 0.4f; m = 0.3f; break;
                case Effect.Obsidian: S = 0.8f; m = 0.15f; break;
                case Effect.Lunar_Dust: S = 0.0f; m = 0.0f; break;
                case Effect.Olive_Drab: S = 0.3f; m = 0.5f; break;
                case Effect.Rust: S = 0.2f; m = 0.35f; break;
                default:
                    break;
            }
        }
    }

    public MicrofacetModel(Effect effect = Effect.Lunar_Dust)
    {
        Effective = effect;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="s">高光反射率</param>
    /// <param name="m">菲涅尔</param>
    public MicrofacetModel(float s, float m)
    {
        this.s = s;
        this.d = 1 - s;
        this.m = m;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="s">高光反射率</param>
    /// <param name="d">漫反射率</param>
    /// <param name="m">菲涅尔</param>
    public MicrofacetModel(float s, float d, float m)
    {
        this.s = s;
        this.d = d;
        this.m = m;
    }

    /// <summary>
    /// 理想漫反射强度
    /// </summary>
    /// <returns></returns>
    const float manfanshe = (float)(1 / Math.PI);
    public float getDIntensity() { return d * manfanshe; }

    /// <summary>
    /// 高光反射强度
    /// https://blog.csdn.net/uwa4d/article/details/72763733
    /// </summary>
    /// <param name="N">面法向</param>
    /// <param name="L">点到光源的向量</param>
    /// <param name="V">点到视点的向量</param>
    /// <returns></returns>
    public float getSIntensity(Vector3 N, Vector3 L, Vector3 V)
    {
        var H = (L + V).normalized;

        //菲涅尔项
        double F = Fresnel_Reflection(N,V);
        
        //朝向分布项
        double D = D_Beckmann(N,L,V);

        //阴影遮挡项
        float G = Math.Min(Vector3.Dot(N, V), Vector3.Dot(N, L)) * Vector3.Dot(N, H) * 2 / Vector3.Dot(H, V);
        G = Math.Min(1, G);

        float Rs = (float)(F * D * G) / (float)(Math.PI * Vector3.Dot(N, L) * Vector3.Dot(N, V));

        if (float.IsNaN(Rs))
            Rs = 0;

        Rs = Math.Min(Rs, 1);

        return s * Rs;
    }

    public double Fresnel_Reflection(Vector3 N, Vector3 V)
    {
        return FresnelScale + (1 - FresnelScale) * Math.Pow(1 - Vector3.Dot(V, N), 5);
    }

    /// <summary>
    /// 朝向分布项
    /// </summary>
    /// <param name="N"></param>
    /// <param name="L"></param>
    /// <param name="V"></param>
    /// <returns></returns>
    double D_Beckmann(Vector3 N, Vector3 L, Vector3 V)
    {
        double D = 0;
        if (m > 0.001)
        {
            //Beckmann分布
            var H = (L + V).normalized;
            var cosa = Vector3.Dot(N, H);
            cosa = Math.Min(cosa, 1);
            var a = Math.Acos(cosa);
            var powE = Math.Pow(Math.Tan(a) / m, 2);
            D = 1 / (m * m * Math.Pow(cosa, 4)) * Math.Pow(Math.E, -powE);

            if (double.IsNaN(D))
                D = 0;
        }

       

        return D;
    }

    double D_Blinn(Vector3 N, Vector3 L, Vector3 V)
    {
        double D = 0;
        if (m > 0.001)
        {
            //Beckmann分布
            var H = (L + V).normalized;
            var cosa = Vector3.Dot(N, H);
            cosa = Math.Min(cosa, 1);
            var a = Math.Acos(cosa);
            var powE = a/m;
            const double C = Math.PI*2;
            D = C * Math.Pow(Math.E, -powE);
        }
        return D;
    }

    static List<Vector3> reflectBall = new List<Vector3>();

    public List<Vector3> ReflectDirections(Vector3 N, Vector3 V, int count)
    {
        if (d == 1)
            return ReflectDirectionsBall(N,V,count);
        else
            return ReflectDirectionsD(N, V, count);
    }
    public List<Vector3> ReflectDirectionsBall(Vector3 N, Vector3 V, int count)
    {
        List<Vector3> reflectBall = new List<Vector3>();
        while (reflectBall.Count<count)
        {
            Vector3 L = new Vector3(
                Random.Range(-1.0f, 1.0f)
                , Random.Range(-1.0f, 1.0f)
                , Random.Range(-1.0f, 1.0f)
                );
            L.Normalize();
            reflectBall.Add(L);
        }

        List<Vector3> ret = new List<Vector3>(reflectBall.ToArray());
        for (int i = 0; i < ret.Count; i++)
        {
            var item = ret[i];
            item += N*0.5f;
            item.Normalize();
            ret[i] = item;
        }     
        return ret;
    }

    public List<Vector3> ReflectDirectionsD(Vector3 N,Vector3 V,int count)
    {
        List<Vector3> ret = new List<Vector3>();
        ret.Add(Vector3.Reflect(-V, N));

        //int errorCount = 0;
        //count--;
        //if (ret.Count == 0)
        //    ret.Add(Vector3.Reflect(-V, N));

        //while (ret.Count < count)
        //{
        //    Vector3 L = new Vector3(
        //        Random.Range(-1.0f, 1.0f)
        //        , Random.Range(-1.0f, 1.0f)
        //        , Random.Range(0.0f, 2.0f)
        //        );
        //    L.Normalize();
        //    if (D_Func(N, V, L) > 0.6)
        //    {
        //        ret.Add(L);
        //        errorCount = 0;
        //    }
        //    else if (++errorCount > 20)
        //        break;
        //}
        return ret;
    }

}


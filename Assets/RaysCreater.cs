using System;
using System.Collections.Generic;
using UnityEngine;

class RayInfo
{
    public int x, y;
    public Ray ray;
    public Color color;
}

class RaysCreater
{
    Vector2Int size = new Vector2Int(0, 0);
    public Vector2Int Size { get { return size; } set { size = value; } }

    Vector2 scale = new Vector2(1, 1);

    public int Samplify
    {
        get { return samplify; }
        set { samplify = value; samplifyInv = 1.0f / samplify; }
    }
    int samplify = 1;

    public float SamplifyInv2 { get { return samplifyInv * samplifyInv; } }
    public float SamplifyInv { get { return samplifyInv; } }
    float samplifyInv = 1;

    int x = 0, y = 0;

    public RaysCreater(int width, int height)
    {
        size.x = width;
        size.y = height;
        scale = new Vector2(1, 1);
    }

    public RaysCreater(int width, int height, int screenWidth, int screenHeight)
    {
        size.x = width;
        size.y = height;
        scale.x = screenWidth * 1.0f / width;
        scale.y = screenHeight * 1.0f / height;
    }

    public bool isFinish()
    {
        return y >= size.y;
    }

    public int sum { get { return size.x * size.y*samplify*samplify; } }
    public int finishCount { get { return (x + 1) * samplify +(size.x * (y + 1) * samplify * samplify); } }

    public void reset() { x = y = 0; }

    public List<RayInfo> getRays(Camera cam, int count)
    {
        List<RayInfo> ret = new List<RayInfo>();

        for (; y < size.y;)
        {
            for (; x < size.x;)
            {
                //图片坐标
                Vector3 p = new Vector3(x + 0.5f, y + 0.5f, 0);

                //采样偏移
                float beginOffset = -0.5f*samplifyInv;

                for (int i = 0; i < samplify; i++)
                {
                    for (int j = 0; j < samplify; j++)
                    {
                        Vector3 sp = new Vector3(beginOffset, beginOffset, 0);
                        sp.x += j * samplifyInv;
                        sp.y += i * samplifyInv;

                        //实际屏幕坐标
                        var screenPoint = p + sp;
                        screenPoint.x *= scale.x;
                        screenPoint.y *= scale.y;

                        RayInfo info = new RayInfo();
                        info.x = x;
                        info.y = y;
                        info.ray = cam.ScreenPointToRay(screenPoint);
                        ret.Add(info);
                    }
                }

                x++;
                if (ret.Count >= count)
                    return ret;
            }

            if (x >= size.x)
            {
                x = 0;
                y++;
            }

        }
        return ret;
    }
}


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class buttonRendering : MonoBehaviour {
    
    // Use this for initialization
    void Start () {
        btnRender = GetComponent<UnityEngine.UI.Button>();
        btnRender.onClick.AddListener(clickButton);
    }

    

    UnityEngine.UI.Button btnRender;

    public int sceneMuilt = 1;
    public int samplify = 1;//采样

    public bool isReflect = false;
    public int maxDepth = 2;//递归深度
    public int reflectCount = 4;//递归深度
    public float refractiveIndices = 1.3f;

    Light[] lights;


    RaysCreater rayCreater;
    DateTime startTime;
    Texture2D image;

    // Update is called once per frame
    void Update () {

        if (rayCreater == null || rayCreater.isFinish()) return;

        List<RayInfo> rays;
        rays = rayCreater.getRays(Camera.main, 1000);

        {
            var process = rayCreater.finishCount * 1.0f / rayCreater.sum ;
            TimeSpan tpass = new TimeSpan((DateTime.Now - startTime).Ticks);
            TimeSpan tremain = new TimeSpan((long)(tpass.Ticks / process * (1 - process)));

            Debug.Log(string.Format("正在渲染 {0:00.00}%,{1}/{2}"
            , process * 100
            , rayCreater.finishCount
            , rayCreater.sum
            ) + string.Format(" 用时{0}:{1:00}:{2:00}", tpass.Hours, tpass.Minutes, tpass.Seconds)
             + string.Format(" 剩余约{0}:{1:00}:{2:00}", tremain.Hours, tremain.Minutes, tremain.Seconds)
            );
        }
        

        foreach (var r in rays)
        {
            RaycastHit info;
            RayTracing(r.ray, out r.color, out info);
        }

        foreach (var r in rays)
        {
            var curColor = image.GetPixel(r.x, r.y);
            image.SetPixel(r.x, r.y, r.color * rayCreater.SamplifyInv2 + curColor);
        }

        if (rayCreater.isFinish())
        {
            TimeSpan tpass = new TimeSpan((DateTime.Now - startTime).Ticks);
            Debug.Log("渲染完毕"
                + string.Format("，用时:{0}:{1}:{2}", tpass.Hours, tpass.Minutes, tpass.Seconds)
                );
            string file = "renderingTemp.png";
            File.WriteAllBytes(file, image.EncodeToPNG());
            Application.OpenURL(file);
            btnRender.enabled = true;
        }
    }

   

    public void clickButton()
    {
        btnRender.enabled = false;
        Debug.Log("开始渲染");
        rayCreater = new RaysCreater(Screen.width, Screen.height);
        rayCreater.Samplify = samplify;
        startTime = DateTime.Now;
        
        {
            List<Light> temp=new List<Light>();
            foreach (var obj in gameObject.scene.GetRootGameObjects())
            {
                temp.AddRange(obj.GetComponentsInChildren<Light>());
            }
            this.lights = temp.ToArray();
        }
        Physics.queriesHitBackfaces = true;
        image = new Texture2D(rayCreater.Size.x, rayCreater.Size.y,TextureFormat.RGBAFloat,false);
        for (int y = 0; y < image.height; y++)
            for (int x = 0; x < image.width; x++)
                image.SetPixel(x, y, Color.black);
    }

    static bool FixNLV(ref Vector3 N,Vector3 L,Vector3 V)
    {
        var dotNL = Vector3.Dot(N, L);
        var dotNV = Vector3.Dot(N, V);

        //如果不再同一平面，则认为被遮挡
        if (dotNL * dotNV <= 0)
            return false;
        if (dotNL < 0)
            N = -N;

        return true;
    }

    bool Refract(Vector3 V,Vector3 N,float ni_over_nt,out Vector3 refractV)
    {
        float dot = Vector3.Dot(V, N);
        float discrimiant = 1.0f - ni_over_nt * ni_over_nt * (1 - dot*dot);
        if (discrimiant > 0)
        {
            refractV = ni_over_nt * (V - N * dot) - N * (float)Math.Sqrt(discrimiant);
            return true;
        }
        else
        {
            refractV = new Vector3();
            return false;
        }
    }



    private void ReflectFunc(Ray ray, RaycastHit hit, MicrofacetModel BRDF, int depth, float multi, ref Color slightColor, ref Color dlightColor)
    {
        //反射
        var V = -ray.direction;
        var N = hit.normal;
        var dirs = BRDF.ReflectDirections(N, V, reflectCount);
        float countInv = 1.0f / dirs.Count;
        foreach (var reflectDir in dirs)
        {
            var L = reflectDir;
            Ray refRay = new Ray(hit.point, reflectDir);
            if (FixNLV(ref N, L, V) == false) continue;

            var s = BRDF.getSIntensity(N, L, V);
            var d = BRDF.getDIntensity();

            Color reflectColor = Color.black;
            RaycastHit reflecthit;
            if (RayTracing(refRay, out reflectColor, out reflecthit, depth + 1, multi * Math.Max(s, d)))
            {
                float intensity = countInv;

                reflectColor *= intensity;

                //高光计算
                slightColor += reflectColor * s;

                //漫反射计算
                dlightColor += reflectColor * d;
            }
        }
    }

    bool RayTracing(Ray ray,out Color desColor,out RaycastHit hit, int depth = 0,float multi = 1)
    {
        desColor = Color.black;
        hit = new RaycastHit();
        if (depth >= maxDepth) return false;
        if (multi < 0.001) return false;

        if (Physics.Raycast(ray,out hit))
        {
            //双向反射分布模型
            var BRDF = new MicrofacetModel();

            //表面颜色
            Color textureColor = Color.white;
            var meshRenderer = hit.transform.GetComponent<MeshRenderer>();

            int mode = 0;
            if (meshRenderer && meshRenderer.material)
            {
                var material = meshRenderer.material;
                textureColor = material.color;
                var texture = material.mainTexture as Texture2D;
                if (texture)
                {
                    textureColor *= texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                }

                //0不透明，3透明
                mode = material.GetInt("_Mode");

                float Metallic = material.GetFloat("_Metallic");//金属
                float Smoothness = material.GetFloat("_Glossiness");//高光分布

                BRDF.S = Metallic;
                BRDF.m = 1 - Smoothness;
            }

            //次表面颜色
            Color subfaceColor = Color.black;

            Color slightColor = Color.black;
            Color dlightColor = Color.black;

            Lighting(ray, hit, BRDF, ref slightColor, ref dlightColor);

            var faceColor = textureColor + subfaceColor;
            desColor = faceColor * dlightColor + slightColor;//漫反射

            //递归追踪,看似光源的一种
            if (isReflect)
            {
                var alpha = textureColor.a;
                ReflectFunc(ray, hit, BRDF, depth, multi*textureColor.a, ref slightColor, ref dlightColor);

                if (mode == 3 && refractiveIndices > 0.01f)
                {
                    var a = textureColor.a;
                    Vector3 refractV;

                    float ni_over_nt = refractiveIndices;
                    var N = hit.normal;
                    var V = -ray.direction;
                    if (Vector3.Dot(V, N) <= 0)
                    {
                        N = -N;
                        ni_over_nt = 1 / ni_over_nt;
                    }


                    if (Refract(V, N, ni_over_nt, out refractV))
                    {
                        Color refractIColor;
                        RaycastHit refractInfo;
                        if(RayTracing(new Ray(hit.point+N*0.001f,refractV),out refractIColor,out refractInfo,depth+1,multi*(1- alpha)))
                        {
                            desColor = a*desColor + (1 - a) * refractIColor;
                        }
                    }
                }
            }
            
            //按照距离衰减
            if(depth != 0)
            {
                float intensity = 1;
                float len = (hit.point - ray.origin).magnitude;
                if (len >= 30) intensity = 0;
                intensity *= (float)(Math.Pow(1 - len / 30, 2));//光根据距离衰减
                intensity *= Math.Max(0, Vector3.Dot(hit.normal,-ray.direction));//光投影到面的衰减
                desColor *= intensity;
            }
            desColor.a = 1;
            return true;
        }

        return false;
    }

    private void Lighting(Ray ray, RaycastHit hit, MicrofacetModel BRDF, ref Color slightColor, ref Color dlightColor)
    {
        foreach (var light in lights)
        {
            switch (light.type)
            {
                case LightType.Spot:
                    {

                    }
                    break;
                case LightType.Directional:
                    break;
                case LightType.Point:
                    {
                        var L = light.transform.position - hit.point;
                        var len = L.magnitude;
                        L.Normalize();

                        //点到光源之间被遮挡，阴影特效
                        if (len > light.range || Physics.Raycast(hit.point + hit.normal * 0.001f, L, len))
                            break;
                        var N = hit.normal;
                        var V = -ray.direction;

                        //在碰撞点上面的光强
                        var intensity = light.intensity;
                        intensity *= (float)(Math.Pow(1 - len / light.range, 2));//光根据距离衰减
                        intensity *= Math.Max(0, Vector3.Dot(N, L));//光投影到面的衰减

                        if (FixNLV(ref N, L, V) == false) continue;

                        //高光计算
                        slightColor += light.color * intensity * BRDF.getSIntensity(N, L, V);

                        //漫反射计算
                        dlightColor += light.color * intensity * BRDF.getDIntensity();//在半球上进行均匀漫反射 d*1/(2*pi)
                    }
                    break;
                case LightType.Area:
                    break;
                default:
                    break;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class buttonRendering : MonoBehaviour {
    public void Click()
    {
        Debug.Log("Button Clicked. TestClick.");
    }

    // Use this for initialization
    void Start () {
        var btn = GetComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(clickButton);

    }
	
	// Update is called once per frame
	void Update () {
		
	}

    public int sceneMuilt = 1;
    public int samplify = 1;//采样

    public int maxDepth = 2;//递归深度
    public int reflectCount = 4;//递归深度

    const float piInv = (float)(1/Math.PI);
    const float manfanshe = (float)(1-1 / Math.PI/2);
    float samplifyRate;//采样比例
    Light[] lights;

    class RayInfo
    {
        public float x,y;
        public Ray ray;
        public Color color;
    }


    
    public void clickButton()
    {
        var startTime = DateTime.Now;

        samplifyRate = 1.0f / (samplify * samplify);

        List<RayInfo> rays = new List<RayInfo>();

        float posScaleInv = 1 / sceneMuilt;
        int height = (int)(Screen.height * sceneMuilt);
        int width = (int)(Screen.width * sceneMuilt);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                rays.AddRange(createRays(x* posScaleInv,y* posScaleInv));
            }
        }


        {
            List<Light> temp=new List<Light>();
            foreach (var obj in gameObject.scene.GetRootGameObjects())
            {
                temp.AddRange(obj.GetComponentsInChildren<Light>());
            }
            this.lights = temp.ToArray();
        }
        Physics.queriesHitBackfaces = true;

        foreach (var r in rays)
        {
            Vector3 point;
            RayTracing(r.ray, out r.color, out point);
        }

        Texture2D image = new Texture2D(width, height);
        foreach (var r in rays)
        {
            image.SetPixel((int)(r.x+0.5), (int)(r.y + 0.5), r.color);
        }

        var endTime = DateTime.Now;
        TimeSpan times = new TimeSpan((endTime - startTime).Ticks);

        Debug.Log("渲染完毕"+"，用时:"+ times.ToString());

        string file = "renderingTemp.png";
        File.WriteAllBytes(file, image.EncodeToPNG());
        Application.OpenURL(file);
    }

    List<RayInfo> createRays(float x,float y)
    {
        List<RayInfo> ret = new List<RayInfo>();
        RayInfo info = new RayInfo();
        info.x = x;
        info.y = y;
        info.ray = Camera.main.ScreenPointToRay(new Vector3(x, y, 0));
        ret.Add(info);

        return ret;
    }
    class RaysCreater
    {
        Vector2Int size = new Vector2Int(0,0);
        Vector2 scale = new Vector2(1,1);

        public int Samplify { get { return samplify; }
            set { samplify = value; samplifyInv = 1.0f / samplify; }
        }
        int samplify = 1;

        public float SamplifyInv2 { get { return samplifyInv* samplifyInv; } }
        public float SamplifyInv { get { return samplifyInv; } }
        float samplifyInv = 1;

        int x = 0, y=0;

        public RaysCreater(int width,int height) {
            size.x = width;
            size.y = height;
        }

        public RaysCreater(int width, int height, int screenWidth, int screenHeight)
        {
            size.x = width;
            size.y = height;
            scale.x = screenWidth*1.0f / width;
            scale.y = screenHeight * 1.0f / height;
        }

        public void reset() { x = y = 0; }

        public List<Ray> getRays(Camera cam,int count=1)
        {
            List<Ray> ret = new List<Ray>();

            for (; y < size.y; y++)
            {
                for (; x < size.x; x++)
                {
                    Vector3 p = new Vector3(x + 0.5f, y + 0.5f, 0);
                    Vector3 sp = new Vector3(-(samplify-1)/2.0f, -(samplify - 1) / 2.0f, 0);

                    for (int i = 0; i < samplify; i++)
                    {
                        for (int j = 0; j < samplify; j++)
                        {
                            sp.x += j * samplifyInv;
                            sp.y += i * samplifyInv;
                            var screenPoint = p + sp;
                            screenPoint.x *= scale.x;
                            screenPoint.y *= scale.y;

                            var ray = cam.ScreenPointToRay(screenPoint);
                            ret.Add(ray);
                        }
                    }
                    
                    if (ret.Count >= count) break;
                }
            }
            return ret;
        }
    }



    bool RayTracing(Ray ray,out Color desColor,out Vector3 point,int depth = 0,float multi = 1)
    {
        desColor = Color.black;
        point = Vector3.zero;
        if (depth >= maxDepth) return false;
        if (multi < 0.001) return false;

        RaycastHit hit;
        if (Physics.Raycast(ray,out hit))
        {
            point = hit.point;

            //双向反射分布模型
            var BRDF = new MicrofacetModel();

            //表面颜色
            Color textureColor = Color.white;
            var meshRenderer = hit.transform.GetComponent<MeshRenderer>();
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
                int mode = material.GetInt("_Mode");

                float Metallic = material.GetFloat("_Metallic");//金属
                float Smoothness = material.GetFloat("_Glossiness");//高光分布

                BRDF.S = Metallic;
                BRDF.m = 1-Smoothness;
            }

            //次表面颜色
            Color subfaceColor = Color.black;


            Color slightColor = Color.black;
            Color dlightColor = Color.black;

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
                            if (len > light.range || Physics.Raycast(hit.point+hit.normal*0.001f, L, len))
                                break;
                            var N = hit.normal;
                            var V = -ray.direction;

                            //在碰撞点上面的光强
                            var intensity = light.intensity;
                            intensity *= (float)(Math.Pow(1 - len / light.range, 2));//光根据距离衰减
                            intensity *= Math.Max(0, Vector3.Dot(N, L));//光投影到面的衰减

                            //高光计算
                            slightColor += light.color* intensity * BRDF.getSIntensity(N,L,V);                       

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

            var faceColor = textureColor + subfaceColor;

            //递归追踪,看似光源的一种

            if(true)
            {
                var V = -ray.direction;
                var N = hit.normal;
                var dirs = BRDF.ReflectDirections(N,V, reflectCount);
                float countInv = 1.0f / dirs.Count;
                foreach (var reflectDir in dirs)
                {
                    Ray refRay = new Ray(hit.point, reflectDir);

                    var L = reflectDir;
                    var s = BRDF.getSIntensity(N, L, V);
                    var d = BRDF.getDIntensity();

                    Color reflectColor = Color.black;
                    Vector3 hitPoint;
                    if (RayTracing(refRay, out reflectColor, out hitPoint, depth + 1, multi * Math.Max(s, d)))
                    {
                        float intensity = countInv;
                        float len = (hitPoint - refRay.origin).magnitude;
                        intensity *= (float)(Math.Pow(1 - len / 30, 2));//光根据距离衰减
                        intensity *= Math.Max(0, Vector3.Dot(N, L));//光投影到面的衰减
                        reflectColor *= intensity;

                        //高光计算
                        slightColor += reflectColor * s;

                        //漫反射计算
                        dlightColor += reflectColor * d;
                    }
                }
            }

            desColor = faceColor * dlightColor+ slightColor;//漫反射
            //for (int i = 0; i < 3; i++)
            //{
            //    desColor[i] = Math.Max(0, desColor[i]);
            //    desColor[i] = Math.Min(1, desColor[i]);
            //}
            desColor.a = 1;
            return true;
        }

        return false;
    }
}

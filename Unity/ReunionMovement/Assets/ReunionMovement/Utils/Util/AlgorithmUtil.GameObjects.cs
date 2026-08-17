using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReunionMovement.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 算法工具类
    /// </summary>
    /// <summary>
    /// AlgorithmUtil 拆分部分（partial class，与其余 *.cs 同属一个静态类，调用方式不变）
    /// </summary>
    public static partial class AlgorithmUtil
    {
        #region GameObject
        /// <summary>
        /// 设置宽
        /// </summary>
        /// <param name="rectTrans"></param>
        /// <param name="width"></param>
        public static void SetWidth(this RectTransform rectTrans, float width)
        {
            rectTrans.sizeDelta = new Vector2(width, rectTrans.sizeDelta.y);
        }

        /// <summary>
        /// 设置高
        /// </summary>
        /// <param name="rectTrans"></param>
        /// <param name="height"></param>
        public static void SetHeight(this RectTransform rectTrans, float height)
        {
            rectTrans.sizeDelta = new Vector2(rectTrans.sizeDelta.x, height);
        }
        /// <summary>
        /// 获取位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newX"></param>
        public static void SetPositionX(this Transform t, float newX)
        {
            t.position = new Vector3(newX, t.position.y, t.position.z);
        }
        /// <summary>
        /// 获取位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newY"></param>
        public static void SetPositionY(this Transform t, float newY)
        {
            t.position = new Vector3(t.position.x, newY, t.position.z);
        }
        /// <summary>
        /// 获取位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newZ"></param>
        public static void SetPositionZ(this Transform t, float newZ)
        {
            t.position = new Vector3(t.position.x, t.position.y, newZ);
        }
        /// <summary>
        /// 获取本地位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newX"></param>
        public static void SetLocalPositionX(this Transform t, float newX)
        {
            t.localPosition = new Vector3(newX, t.localPosition.y, t.localPosition.z);
        }
        /// <summary>
        /// 获取本地位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newY"></param>
        public static void SetLocalPositionY(this Transform t, float newY)
        {
            t.localPosition = new Vector3(t.localPosition.x, newY, t.localPosition.z);
        }
        /// <summary>
        /// 获取本地位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newZ"></param>
        public static void SetLocalPositionZ(this Transform t, float newZ)
        {
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, newZ);
        }
        /// <summary>
        /// 设置缩放为0
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newScale"></param>
        public static void SetLocalScale(this Transform t, Vector3 newScale)
        {
            t.localScale = newScale;
        }
        /// <summary>
        /// 设置本地缩放为0
        /// </summary>
        /// <param name="t"></param>
        public static void SetLocalScaleZero(this Transform t)
        {
            t.localScale = Vector3.zero;
        }
        /// <summary>
        /// 获取位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetPositionX(this Transform t)
        {
            return t.position.x;
        }
        /// <summary>
        /// 获取位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetPositionY(this Transform t)
        {
            return t.position.y;
        }
        /// <summary>
        /// 获取位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetPositionZ(this Transform t)
        {
            return t.position.z;
        }
        /// <summary>
        /// 获取本地位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetLocalPositionX(this Transform t)
        {
            return t.localPosition.x;
        }
        /// <summary>
        /// 获取本地位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetLocalPositionY(this Transform t)
        {
            return t.localPosition.y;
        }
        /// <summary>
        /// 获取本地位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetLocalPositionZ(this Transform t)
        {
            return t.localPosition.z;
        }

        /// <summary>
        /// 变换转矩阵变换
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static RectTransform AsRectTransform(this Transform t)
        {
            return t?.gameObject.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 设置动画速度
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="newSpeed"></param>
        public static void SetSpeed(this Animation anim, float newSpeed)
        {
            anim[anim.clip.name].speed = newSpeed;
        }
        /// <summary>
        /// v3转v2
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static Vector2 ToVector2(this Vector3 vec)
        {
            return new Vector2(vec.x, vec.y);
        }
        /// <summary>
        /// 设置活动状态
        /// </summary>
        /// <param name="com"></param>
        /// <param name="visible"></param>
        public static void SetActive(this Component com, bool visible)
        {
            if (com && com.gameObject && com.gameObject.activeSelf != visible) com.gameObject.SetActive(visible);
        }
        /// <summary>
        /// 设置活动状态（反向）
        /// </summary>
        /// <param name="go"></param>
        /// <param name="visible"></param>
        public static void SetActiveReverse(this GameObject go, bool visible)
        {
            if (go && go.activeSelf != visible) go.SetActive(visible);
        }
        /// <summary>
        /// 设置名字
        /// </summary>
        /// <param name="go"></param>
        /// <param name="name"></param>
        public static void SetName(this GameObject go, string name)
        {
            if (go && go.name != name) go.name = name;
        }

        /// <summary>
        /// 获取附加到给定游戏对象的组件
        /// 如果找不到，则附加一个新的并返回
        /// </summary>
        /// <param name="gameObject">Game object.</param>
        /// <returns>Previously or newly attached component.</returns>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 检查游戏对象是否附加了类型为T的组件
        /// </summary>
        /// <param name="gameObject">Game object.</param>
        /// <returns>True when component is attached.</returns>
        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() != null;
        }

        /// <summary>
        /// 搜索子物体组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static T Get<T>(this GameObject go, string subnode) where T : Component
        {
            if (go != null)
            {
                Transform sub = go.transform.Find(subnode);
                if (sub != null) return sub.GetComponent<T>();
            }
            return null;
        }

        /// <summary>
        /// 递归设置游戏对象的层
        /// </summary>
        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }

        /// <summary> 
        /// 在指定物体上添加指定图片 
        /// </summary>
        public static Image AddImage(this GameObject target, Sprite sprite)
        {
            target.SetActive(false);
            Image image = target.GetComponent<Image>();
            if (!image)
            {
                image = target.AddComponent<Image>();
            }
            image.sprite = sprite;
            image.SetNativeSize();
            target.SetActive(true);
            return image;
        }

        /// <summary>
        /// 查找子对象
        /// </summary>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名称</param>
        /// <returns></returns>
        public static GameObject Child(this GameObject go, string objName)
        {
            return Child(go.transform, objName);
        }

        /// <summary>
        /// 查找子对象
        /// </summary>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名称</param>
        /// <returns></returns>
        public static GameObject Child(Transform go, string objName)
        {
            Transform tran = go.Find(objName);
            return tran?.gameObject;
        }

        /// <summary>
        /// 查找子对象
        /// </summary>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名</param>
        /// <param name="check_visible">检查可见</param>
        /// <param name="error">错误</param>
        /// <returns></returns>
        public static GameObject Child(this GameObject go, string objName, bool check_visible = false, bool error = true)
        {
            if (!go)
            {
                if (error)
                {
                    Log.Error("查找失败，GameObject是空的！");
                }
                return null;
            }

            var t = Child(go.transform, objName, check_visible, error);
            return t?.gameObject;
        }

        /// <summary>
        /// 查找子对象组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名</param>
        /// <param name="check_visible">检查可见</param>
        /// <param name="error">错误</param>
        /// <returns></returns>
        public static T Child<T>(this GameObject go, string objName, bool check_visible = false, bool error = true) where T : Component
        {
            var t = Child(go, objName, check_visible, error);
            return t?.GetComponent<T>();
        }

        /// <summary>
        /// 取平级对象
        /// </summary>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static GameObject Peer(this GameObject go, string subnode)
        {
            return Peer(go.transform, subnode);
        }

        /// <summary>
        /// 取平级对象
        /// </summary>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static GameObject Peer(Transform go, string subnode)
        {
            Transform tran = go.parent.Find(subnode);
            return tran?.gameObject;
        }

        /// <summary>
        /// 清除单个实例，默认延迟为0，仅在场景中删除对应对象
        /// </summary>
        public static void DestroyObject(this UnityEngine.Object obj, float delay = 0)
        {
            GameObject.Destroy(obj, delay);
        }

        /// <summary>
        /// 立刻清理实例对象，会在内存中清理实例，Editor适用
        /// </summary>
        public static void DestroyObjectImmediate(this UnityEngine.Object obj)
        {
            GameObject.DestroyImmediate(obj);
        }

        /// <summary>
        /// 清除一组实例
        /// </summary>
        /// <typeparam name="T">实例类型</typeparam>
        /// <param name="objs">对象实例集合</param>
        public static void DestroyObjects<T>(IEnumerable<T> objs) where T : UnityEngine.Object
        {
            foreach (var obj in objs)
            {
                GameObject.Destroy(obj);
            }
        }

        /// <summary>
        /// 清除所有子节点
        /// </summary>
        /// <param name="go"></param>
        public static void ClearChild(this GameObject go)
        {
            var tran = go.transform;

            while (tran.childCount > 0)
            {
                var child = tran.GetChild(0);

                if (Application.isEditor && !Application.isPlaying)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
                else
                {
                    GameObject.Destroy(child.gameObject);
                }
                child.parent = null;
            }
        }
        #endregion


        #region Object
        /// <summary>
        /// 从一个 object[] 数组中，安全地获取并转换指定下标的元素为目标类型 T。
        /// object[] args = { 123, "hello", 3.14f };
        /// int a = args.Get<int>(0);// 123
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="openArgs"></param>
        /// <param name="index">下标</param>
        /// <param name="isLog">开启log</param>
        /// <returns></returns>
        public static T Get<T>(this object[] openArgs, int index, bool isLog = true)
        {
            if (openArgs == null)
            {
                if (isLog)
                {
                    Log.Error("[获取错误<object[]>], openArgs为null");
                }
                return default;
            }

            if (index < 0 || index >= openArgs.Length)
            {
                if (isLog)
                {
                    Log.Error("[获取错误<object[]>], 越界: {0}  {1}", index, openArgs.Length);
                }
                return default;
            }

            var arrElement = openArgs[index];
            if (arrElement == null || arrElement is DBNull)
            {
                return default;
            }

            try
            {
                // 直接类型匹配
                if (arrElement is T t)
                {
                    return t;
                }

                // 可空类型支持
                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                // 针对常用类型做特殊处理
                if (underlyingType == typeof(int))
                {
                    return (T)(object)arrElement.ObjToInt32();
                }
                if (underlyingType == typeof(long))
                {
                    return (T)(object)arrElement.ObjToInt64();
                }
                if (underlyingType == typeof(float))
                {
                    return (T)(object)arrElement.ObjToFloat();
                }
                if (underlyingType == typeof(string))
                {
                    return (T)(object)arrElement.ObjToString();
                }

                // 其它类型尝试通用转换
                return (T)Convert.ChangeType(arrElement, underlyingType);
            }
            catch (Exception ex)
            {
                if (isLog)
                    Log.Error("[获取错误<object[]>], '{0}' 无法转换为类型<{1}>: {2}", arrElement, typeof(T), ex);
                return default;
            }
        }
        /// <summary>
        /// object转int32
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static int ObjToInt32(this object obj)
        {
            if (obj is int i)
            {
                return i;
            }

            try
            {
                return Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                Log.Error("ToInt32 : " + ex);
                return 0;
            }
        }

        /// <summary>
        /// object转int64 | long
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static long ObjToInt64(this object obj)
        {
            if (obj is long l)
            {
                return l;
            }

            try
            {
                return Convert.ToInt64(obj);
            }
            catch (Exception ex)
            {
                Log.Error("ToInt64 : " + ex);
                return 0;
            }
        }

        /// <summary>
        /// object转float
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static float ObjToFloat(this object obj)
        {
            if (obj is float f)
            {
                return f;
            }

            try
            {
                return (float)Math.Round(Convert.ToSingle(obj), 2);
            }
            catch (Exception ex)
            {
                Log.Error("object转float失败 : " + ex);
                return 0;
            }
        }

        /// <summary>
        /// object转string
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ObjToString(this object obj)
        {
            if (obj is string s)
            {
                return s;
            }

            try
            {
                return Convert.ToString(obj);
            }
            catch (Exception ex)
            {
                Log.Error("object转string失败 : " + ex);
                return default;
            }
        }
        #endregion


        #region Texture
        /// <summary>
        /// texture 转换成 texture2d
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D TextureToTexture2D(Texture texture)
        {
            Texture2D texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 32, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, renderTexture);

            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();

            RenderTexture.active = currentRT;
            RenderTexture.ReleaseTemporary(renderTexture);

            return texture2D;
        }

        /// <summary>
        /// 解除texture锁
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Texture2D DuplicateTexture(this Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            return readableText;
        }

        /// <summary>
        /// 裁剪正方形
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D CutForSquare(this Texture2D texture)
        {
            Texture2D tex;
            int TextureWidth = texture.width;//图片的宽
            int TextureHeight = texture.height;//图片的高

            int TextureSide = Mathf.Min(TextureWidth, TextureHeight);
            tex = new Texture2D(TextureSide, TextureSide);
            UnityEngine.Color[] col = texture.GetPixels((TextureWidth - TextureSide) / 2, (TextureHeight - TextureSide) / 2, TextureSide, TextureSide);
            tex.SetPixels(0, 0, TextureSide, TextureSide, col);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 正方型裁剪
        /// 以图片中心为轴心，截取正方型，然后等比缩放
        /// 用于头像处理
        /// </summary>
        /// <param name="texture">要处理的图片</param>
        /// <param name="side_x">指定的边长</param>
        /// <param name="side_y">指定的边宽</param>
        /// <returns></returns>
        public static Texture2D CutForSquare(this Texture2D texture, int side_x, int side_y)
        {
            Texture2D tex;
            int TextureWidth = texture.width;//图片的宽
            int TextureHeight = texture.height;//图片的高

            //如果图片的高和宽都比side大
            if (TextureWidth >= side_x && TextureHeight >= side_y)
            {
                tex = new Texture2D(side_x, side_y);
                UnityEngine.Color[] col = texture.GetPixels((TextureWidth - side_x) / 2, (TextureHeight - side_y) / 2, side_x, side_y);
                tex.SetPixels(0, 0, side_x, side_y, col);
                tex.Apply();
                return tex;
            }
            //如果图片的宽或高小于side
            if (TextureWidth < side_x || TextureHeight < side_y)
            {
                int TextureSide = Mathf.Min(TextureWidth, TextureHeight);
                tex = new Texture2D(TextureSide, TextureSide);
                UnityEngine.Color[] col = texture.GetPixels((TextureWidth - TextureSide) / 2, (TextureHeight - TextureSide) / 2, TextureSide, TextureSide);
                tex.SetPixels(0, 0, TextureSide, TextureSide, col);
                tex.Apply();
                return tex;
            }
            return null;
        }

        /// <summary>
        /// byte[]转换为Texture2D
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Texture2D BytesToTexture2D(this byte[] bytes, int width, int height)
        {
            Texture2D texture2D = new Texture2D(width, height);
            texture2D.LoadImage(bytes);
            return texture2D;
        }

        /// <summary>
        /// 双线性插值法缩放图片，等比缩放 
        /// </summary>
        public static Texture2D ScaleTextureBilinear(this Texture2D originalTexture, float scaleFactor)
        {
            Texture2D newTexture = new Texture2D(Mathf.CeilToInt(originalTexture.width * scaleFactor),
                Mathf.CeilToInt(originalTexture.height * scaleFactor));
            float scale = 1.0f / scaleFactor;
            int maxX = originalTexture.width - 1;
            int maxY = originalTexture.height - 1;
            for (int y = 0; y < newTexture.height; y++)
            {
                for (int x = 0; x < newTexture.width; x++)
                {
                    float targetX = x * scale;
                    float targetY = y * scale;
                    int x1 = Mathf.Min(maxX, Mathf.FloorToInt(targetX));
                    int y1 = Mathf.Min(maxY, Mathf.FloorToInt(targetY));
                    int x2 = Mathf.Min(maxX, x1 + 1);
                    int y2 = Mathf.Min(maxY, y1 + 1);

                    float u = targetX - x1;
                    float v = targetY - y1;
                    float w1 = (1 - u) * (1 - v);
                    float w2 = u * (1 - v);
                    float w3 = (1 - u) * v;
                    float w4 = u * v;
                    Color color1 = originalTexture.GetPixel(x1, y1);
                    Color color2 = originalTexture.GetPixel(x2, y1);
                    Color color3 = originalTexture.GetPixel(x1, y2);
                    Color color4 = originalTexture.GetPixel(x2, y2);
                    Color color = new Color(
                        Mathf.Clamp01(color1.r * w1 + color2.r * w2 + color3.r * w3 + color4.r * w4),
                        Mathf.Clamp01(color1.g * w1 + color2.g * w2 + color3.g * w3 + color4.g * w4),
                        Mathf.Clamp01(color1.b * w1 + color2.b * w2 + color3.b * w3 + color4.b * w4),
                        Mathf.Clamp01(color1.a * w1 + color2.a * w2 + color3.a * w3 + color4.a * w4)
                    );
                    newTexture.SetPixel(x, y, color);
                }
            }

            newTexture.Apply();
            return newTexture;
        }

        /// <summary> 
        /// 双线性插值法缩放图片为指定尺寸 
        /// </summary>
        public static Texture2D SizeTextureBilinear(this Texture2D originalTexture, Vector2 size)
        {
            Texture2D newTexture = new Texture2D(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.y));
            float scaleX = originalTexture.width / size.x;
            float scaleY = originalTexture.height / size.y;
            int maxX = originalTexture.width - 1;
            int maxY = originalTexture.height - 1;
            for (int y = 0; y < newTexture.height; y++)
            {
                for (int x = 0; x < newTexture.width; x++)
                {
                    float targetX = x * scaleX;
                    float targetY = y * scaleY;
                    int x1 = Mathf.Min(maxX, Mathf.FloorToInt(targetX));
                    int y1 = Mathf.Min(maxY, Mathf.FloorToInt(targetY));
                    int x2 = Mathf.Min(maxX, x1 + 1);
                    int y2 = Mathf.Min(maxY, y1 + 1);

                    float u = targetX - x1;
                    float v = targetY - y1;
                    float w1 = (1 - u) * (1 - v);
                    float w2 = u * (1 - v);
                    float w3 = (1 - u) * v;
                    float w4 = u * v;
                    Color color1 = originalTexture.GetPixel(x1, y1);
                    Color color2 = originalTexture.GetPixel(x2, y1);
                    Color color3 = originalTexture.GetPixel(x1, y2);
                    Color color4 = originalTexture.GetPixel(x2, y2);
                    Color color = new Color(
                        Mathf.Clamp01(color1.r * w1 + color2.r * w2 + color3.r * w3 + color4.r * w4),
                        Mathf.Clamp01(color1.g * w1 + color2.g * w2 + color3.g * w3 + color4.g * w4),
                        Mathf.Clamp01(color1.b * w1 + color2.b * w2 + color3.b * w3 + color4.b * w4),
                        Mathf.Clamp01(color1.a * w1 + color2.a * w2 + color3.a * w3 + color4.a * w4)
                    );
                    newTexture.SetPixel(x, y, color);
                }
            }

            newTexture.Apply();
            return newTexture;
        }

        /// <summary> 
        /// Texture旋转
        /// </summary>
        public static Texture2D RotateTexture(this Texture2D texture, float eulerAngles)
        {
            int x;
            int y;
            int i;
            int j;
            float phi = eulerAngles / (180 / Mathf.PI);
            float sn = Mathf.Sin(phi);
            float cs = Mathf.Cos(phi);
            Color32[] arr = texture.GetPixels32();
            Color32[] arr2 = new Color32[arr.Length];
            int W = texture.width;
            int H = texture.height;
            int xc = W / 2;
            int yc = H / 2;

            for (j = 0; j < H; j++)
            {
                for (i = 0; i < W; i++)
                {
                    arr2[j * W + i] = new Color32(0, 0, 0, 0);

                    x = (int)(cs * (i - xc) + sn * (j - yc) + xc);
                    y = (int)(-sn * (i - xc) + cs * (j - yc) + yc);

                    if ((x > -1) && (x < W) && (y > -1) && (y < H))
                    {
                        arr2[j * W + i] = arr[y * W + x];
                    }
                }
            }

            Texture2D newImg = new Texture2D(W, H);
            newImg.SetPixels32(arr2);
            newImg.Apply();

            return newImg;
        }
        #endregion
    }
}

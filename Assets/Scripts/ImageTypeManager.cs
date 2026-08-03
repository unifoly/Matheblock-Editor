using System;
using System.IO;
using UnityEngine;

public class ImageTypeManager
{
    public string SetImageToString(string imgPath)
    {
        using (var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read))
        {
            // 用 MemoryStream 完整读取（FileStream.Read 不保证一次读满）
            using (var ms = new MemoryStream((int)fs.Length))
            {
                fs.CopyTo(ms);
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    public Texture2D GetTextureByString(string textureStr)
    {
        // 空数据直接返回，避免 FromBase64String 抛 ArgumentNullException
        if (string.IsNullOrEmpty(textureStr))
        {
            return null;
        }

        try
        {
            // 先解码再创建纹理：FormatException 分支不会泄漏已创建的纹理
            var arr = Convert.FromBase64String(textureStr);
            var tex = new Texture2D(1, 1);
            if (tex.LoadImage(arr))
            {
                tex.Apply();
                return tex;
            }

            DestroyTex(tex);
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static void DestroyTex(Texture2D tex)
    {
#if UNITY_EDITOR
        UnityEngine.Object.DestroyImmediate(tex);
#else
        UnityEngine.Object.Destroy(tex);
#endif
    }
}
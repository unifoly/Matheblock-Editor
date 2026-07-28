using System;
using System.IO;
using UnityEngine;

public class ImageTypeManager
{
    public string SetImageToString(string imgPath)
    {
        using (var fs = new FileStream(imgPath, FileMode.Open))
        {
            var imgByte = new byte[fs.Length];
            fs.Read(imgByte, 0, imgByte.Length);
            return Convert.ToBase64String(imgByte);
        }
    }

    public Texture2D GetTextureByString(string textureStr)
    {
        var tex = new Texture2D(1, 1);
        var arr = Convert.FromBase64String(textureStr);
        tex.LoadImage(arr);
        tex.Apply();
        return tex;
    }
}
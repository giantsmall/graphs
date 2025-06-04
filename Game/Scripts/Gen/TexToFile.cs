using Assets.Game.Scripts.Gen.Models;
using Assets.Game.Scripts.Utility;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Game.Scripts.Gen.WorldGen
{
    public static class TexToFile
    {
        public enum SaveFormat
        {
            EXR, JPG, PNG, TGA
        };

        static public void SaveTexture2DToFile(Texture2D tex, string filePath, SaveFormat fileFormat = SaveFormat.PNG, int jpgQuality = 95)
        {
            switch (fileFormat)
            {
                case SaveFormat.EXR:
                    System.IO.File.WriteAllBytes(filePath + ".exr", tex.EncodeToEXR());
                    break;
                case SaveFormat.JPG:
                    System.IO.File.WriteAllBytes(filePath + ".jpg", tex.EncodeToJPG(jpgQuality));
                    break;
                case SaveFormat.PNG:
                    System.IO.File.WriteAllBytes(filePath + ".png", tex.EncodeToPNG());
                    break;
                case SaveFormat.TGA:
                    System.IO.File.WriteAllBytes(filePath + ".tga", tex.EncodeToTGA());
                    break;
            }
        }


        /// <summary>
        /// Saves a RenderTexture to disk with the specified filename and image format
        /// </summary>
        /// <param name="renderTexture"></param>
        /// <param name="filePath"></param>
        /// <param name="fileFormat"></param>
        /// <param name="jpgQuality"></param>
        static public void SaveRenderTextureToFile(RenderTexture renderTexture, string filePath, SaveFormat fileFormat = SaveFormat.PNG, int jpgQuality = 95)
        {
            Texture2D tex;
            if (fileFormat != SaveFormat.EXR)
                tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false, false);
            else
                tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false, true);
            var oldRt = RenderTexture.active;
            RenderTexture.active = renderTexture;
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();
            RenderTexture.active = oldRt;
            SaveTexture2DToFile(tex, filePath, fileFormat, jpgQuality);
            if (Application.isPlaying)
                Object.Destroy(tex);
            else
                Object.DestroyImmediate(tex);

        }

    }
}
using UnityEngine;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class TextureExporter
    {
        public static string SaveTemp(Texture2D tex)
        {
            string path = Path.Combine(Application.temporaryCachePath, tex.name + "_temp.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }
    }
}

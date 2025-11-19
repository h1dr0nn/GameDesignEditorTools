#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class ImageIOUtility
    {
        public static bool SaveTexture(Texture2D tex, string path, string format = "png", int quality = 85)
        {
            try
            {
                byte[] bytes;
                format = format.ToLowerInvariant();

                switch (format)
                {
                    case "jpg":
                    case "jpeg":
                        bytes = tex.EncodeToJPG(quality);
                        break;

                    case "png":
                        bytes = tex.EncodeToPNG();
                        break;

                    default:
                        Debug.LogWarning($"[ImageIO] ❌ Unsupported format '{format}', fallback to PNG.");
                        bytes = tex.EncodeToPNG();
                        break;
                }

                File.WriteAllBytes(path, bytes);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ImageIO] ⚠️ Error saving '{path}': {ex.Message}");
                return false;
            }
        }

        public static Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ImageIO] File not found: {path}");
                return null;
            }

            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            return tex;
        }

        public static void RefreshAssetDatabase()
        {
            AssetDatabase.Refresh();
        }
    }
}
#endif

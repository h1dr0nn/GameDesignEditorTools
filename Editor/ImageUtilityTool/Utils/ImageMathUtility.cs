#if UNITY_EDITOR
using UnityEngine;

namespace h1dr0n.EditorTools
{
    public static class ImageMathUtility
    {
        public static Vector2Int GetScaledSize(Vector2Int original, int maxWidth, int maxHeight, bool keepAspect = true)
        {
            if (!keepAspect)
                return new Vector2Int(maxWidth, maxHeight);

            float aspect = (float)original.x / original.y;
            int newW = maxWidth;
            int newH = Mathf.RoundToInt(maxWidth / aspect);

            if (newH > maxHeight)
            {
                newH = maxHeight;
                newW = Mathf.RoundToInt(maxHeight * aspect);
            }

            return new Vector2Int(newW, newH);
        }

        public static Texture2D Resize(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        public static float GetCompressionRatio(long originalSize, long newSize)
        {
            if (originalSize <= 0) return 0;
            return 1f - (float)newSize / originalSize;
        }
    }
}
#endif

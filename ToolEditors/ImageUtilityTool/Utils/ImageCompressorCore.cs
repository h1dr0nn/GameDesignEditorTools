#if UNITY_EDITOR
using UnityEngine;
using System.IO;

public static class ImageCompressorCore
{
    public static (float finalKB, int bestQuality) CompressToTargetSize(Texture2D texture, string path, int minKB, int maxKB, string format = "jpg")
    {
        int low = 5, high = 95, bestQ = 75;
        byte[] bestData = null;
        string tempPath = Path.ChangeExtension(path, $".tmp.{format}");

        while (low <= high)
        {
            int mid = (low + high) / 2;
            byte[] bytes = EncodeTexture(texture, format, mid);
            long size = bytes.Length;

            if (size < minKB * 1024)
                low = mid + 1;
            else if (size > maxKB * 1024)
                high = mid - 1;
            else
            {
                bestQ = mid;
                bestData = bytes;
                break;
            }

            bestQ = mid;
            bestData = bytes;
        }

        if (bestData != null)
            File.WriteAllBytes(tempPath, bestData);

        long finalSize = new FileInfo(tempPath).Length;
        float finalKB = finalSize / 1024f;

        // Replace source
        File.Delete(path);
        File.Move(tempPath, path);

        return (finalKB, bestQ);
    }

    private static byte[] EncodeTexture(Texture2D tex, string format, int quality)
    {
        switch (format.ToLowerInvariant())
        {
            case "png": return tex.EncodeToPNG();
            case "jpg":
            case "jpeg": return tex.EncodeToJPG(quality);
            default: return tex.EncodeToJPG(quality);
        }
    }
}
#endif

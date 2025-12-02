using UnityEngine;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class RealESRGANPath
    {
        public static string GetBundleFolder()
        {
            string scriptPath = GetScriptFolder();
            return Path.Combine(scriptPath, "Bundle");
        }

        private static string GetScriptFolder()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("RealESRGANPath");
            if (guids.Length == 0) return null;

            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return Path.GetDirectoryName(assetPath);
        }

        public static string GetBinaryPath()
        {
            string root = GetBundleFolder();
            if (string.IsNullOrEmpty(root)) return null;

#if UNITY_EDITOR_WIN
            return Path.Combine(root, "window-realesrgan-ncnn-vulkan.exe");
#elif UNITY_EDITOR_OSX
            return Path.Combine(root, "macos-realesrgan-ncnn-vulkan");
#elif UNITY_EDITOR_LINUX
            return Path.Combine(root, "ubuntu-realesrgan-ncnn-vulkan");
#else
            return null;
#endif
        }

        public static bool Exists()
        {
            var p = GetBinaryPath();
            return !string.IsNullOrEmpty(p) && File.Exists(p);
        }
    }
}

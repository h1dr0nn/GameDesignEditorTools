using UnityEngine;
using UnityEditor;
using System.IO;

namespace h1dr0n.EditorTools
{
    public static class RealESRGANPath
    {
        private const string PACKAGE_NAME = "com.h1dr0n.editortools";

        public static string GetBundleFolder()
        {
            string packagePath = GetPackageRootPath();
            if (string.IsNullOrEmpty(packagePath))
                return null;

            return Path.Combine(packagePath, "Editor", "ImageUtilityTool", "Utils", "Bundle");
        }

        private static string GetPackageRootPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + PACKAGE_NAME);
            if (packageInfo != null)
            {
                return packageInfo.resolvedPath;
            }

            string[] guids = AssetDatabase.FindAssets("t:Script RealESRGANPath");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string scriptDir = Path.GetDirectoryName(assetPath);
                
                if (!string.IsNullOrEmpty(scriptDir))
                {
                    string utilsDir = scriptDir;
                    string imageToolDir = Path.GetDirectoryName(utilsDir);
                    string editorDir = Path.GetDirectoryName(imageToolDir);
                    return Path.GetDirectoryName(editorDir);
                }
            }

            Debug.LogError("[RealESRGAN] Could not find package root path.");
            return null;
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

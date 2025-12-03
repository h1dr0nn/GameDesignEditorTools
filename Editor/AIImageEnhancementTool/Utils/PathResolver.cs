using System.IO;
using UnityEngine;

namespace h1dr0n.EditorTools
{
    public static class PathResolver
    {
        private const string BUNDLE_FOLDER = "Bundle";
        private const string REALESRGAN_FOLDER = "Real-ESRGAN-ncnn-vulkan";
        private const string WAIFU2X_FOLDER = "waifu2x-ncnn-vulkan";

        public static string GetRealESRGANPath()
        {
            string exeName;
            if (Application.platform == RuntimePlatform.WindowsEditor)
                exeName = "window-realesrgan-ncnn-vulkan.exe";
            else if (Application.platform == RuntimePlatform.OSXEditor)
                exeName = "macos-realesrgan-ncnn-vulkan";
            else // Linux
                exeName = "ubuntu-realesrgan-ncnn-vulkan";

            // Try multiple search locations
            string[] searchPaths = GetSearchPaths();
            foreach (string basePath in searchPaths)
            {
                string fullPath = Path.Combine(basePath, BUNDLE_FOLDER, REALESRGAN_FOLDER, exeName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        public static string GetWaifu2xPath()
        {
            string exeName;
            if (Application.platform == RuntimePlatform.WindowsEditor)
                exeName = "window-waifu2x-ncnn-vulkan.exe";
            else if (Application.platform == RuntimePlatform.OSXEditor)
                exeName = "macos-waifu2x-ncnn-vulkan";
            else // Linux
                exeName = "linux-waifu2x-ncnn-vulkan";

            // Try multiple search locations
            string[] searchPaths = GetSearchPaths();
            foreach (string basePath in searchPaths)
            {
                string fullPath = Path.Combine(basePath, BUNDLE_FOLDER, WAIFU2X_FOLDER, exeName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        public static bool RealESRGANExists()
        {
            return !string.IsNullOrEmpty(GetRealESRGANPath());
        }

        public static bool Waifu2xExists()
        {
            return !string.IsNullOrEmpty(GetWaifu2xPath());
        }

        public static bool ValidateRealESRGANModel(string modelName)
        {
            string[] searchPaths = GetSearchPaths();
            foreach (string basePath in searchPaths)
            {
                string modelsPath = Path.Combine(basePath, BUNDLE_FOLDER, REALESRGAN_FOLDER, "models");
                
                if (!Directory.Exists(modelsPath))
                    continue;

                // Check for .param and .bin files
                string paramFile = Path.Combine(modelsPath, modelName + ".param");
                string binFile = Path.Combine(modelsPath, modelName + ".bin");
                
                if (File.Exists(paramFile) && File.Exists(binFile))
                    return true;
            }
            
            return false;
        }

        public static bool ValidateWaifu2xModel(string modelName)
        {
            string[] searchPaths = GetSearchPaths();
            foreach (string basePath in searchPaths)
            {
                string modelsPath = Path.Combine(basePath, BUNDLE_FOLDER, WAIFU2X_FOLDER, "models-" + modelName);
                
                if (Directory.Exists(modelsPath))
                    return true;
            }
            
            return false;
        }

        private static string[] GetSearchPaths()
        {
            var paths = new System.Collections.Generic.List<string>();
            
            // 1. Current script directory in Assets
            string scriptPath = GetScriptDirectory();
            if (!string.IsNullOrEmpty(scriptPath))
                paths.Add(scriptPath);
            
            // 2. PackageCache directory
            string packageCachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                // Search for com.h1dr0n.editortools package in cache
                string[] packageDirs = Directory.GetDirectories(packageCachePath, "com.h1dr0n.editortools@*");
                foreach (string packageDir in packageDirs)
                {
                    string editorPath = Path.Combine(packageDir, "Editor", "ImageUtilityTool", "Utils");
                    if (Directory.Exists(editorPath))
                        paths.Add(editorPath);
                }
            }
            
            // 3. Packages directory (for local packages)
            string packagesPath = Path.Combine(Application.dataPath, "..", "Packages", "com.h1dr0n.editortools");
            if (Directory.Exists(packagesPath))
            {
                string editorPath = Path.Combine(packagesPath, "Editor", "ImageUtilityTool", "Utils");
                if (Directory.Exists(editorPath))
                    paths.Add(editorPath);
            }
            
            return paths.ToArray();
        }

        private static string GetScriptDirectory()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Script PathResolver");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return Path.GetDirectoryName(Path.GetDirectoryName(path)); // Go up to AIImageEnhancementTool folder
            }
            return Application.dataPath;
        }
    }
}

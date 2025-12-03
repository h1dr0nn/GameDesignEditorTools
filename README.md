# Game Design Editor Tools

A collection of Unity Editor utilities that streamline game production: bulk asset renaming, preview icon creation, texture and audio import optimization, prefab helpers, scene presets, and a drag-and-drop shortcut board. The package follows the Unity Package structure, so it can be installed directly from Git.

## Key Features
- **Asset Utility Tool**: asset filters plus batch renaming and preview icon generation.
- **Image Utility Tool**: fast texture/sprite workflows (import settings, batch processing).
- **AI Image Enhancement Tool**: AI-powered image enhancement (upscale, denoise, face restoration) using Real-ESRGAN and Waifu2x.
- **Audio Utility Tool**: tweak and standardize audio clip import settings.
- **Prefab Utility Tool**: prefab helpers, including checks and bulk actions.
- **Scene Presets**: save and apply common scene setups (lighting, skybox, camera, etc.).
- **Quick Shortcuts Window**: a drag-and-drop board for assets/prefabs to ping, select, or drop into the Scene/Hierarchy.

## Requirements
- Unity **2021.3** or newer.
- Git repository access to install via the Package Manager.

## Installing via Git URL

1. Open **Window > Package Manager** in Unity.
2. Select **+** and choose **Add package from git URL...**.
3. Paste the repository URL:
   ```
   https://github.com/h1dr0nn/GameDesignEditorTools.git#main
   ```
4. Unity will download the package and expose the tools under **Tools/Game Design/**.

## Quick Usage
- Open **Tools/Game Design/** to launch each utility window.
- Drag assets from Project/Hierarchy into the window to run batch actions.
- Save presets or shortcut lists to reuse across sessions.

## Changelog

### Version 1.1.2 (2025-12-03)
- **NEW**: AI Image Enhancement Tool with three modules, powered by `xinntao/Real-ESRGAN` and `nihui/waifu2x-ncnn-vulkan`:
  - **AI Upscale**: Dual-engine support with model selection
  - **AI Denoise**: Noise reduction using Waifu2x or Real-ESRGAN denoise variant
  - **AI Face Enhancement**: Face restoration using Real-ESRGAN with GFPGAN
- Refactored AI upscaling from Image Utility Tool into dedicated enhancement tool
- Added PathResolver for centralized binary management
- Added engine abstraction layer (RealESRGANEngine, Waifu2xEngine)
- Improved overall architecture and separation of concerns

### Version 1.1.0 (2025-12-02)
- Added a new feature: ImageAIUpscaleModule (Upscale Image), powered by `xinntao/Real-ESRGAN`.
- Added supporting features: TextureExporter updates, RealESRGAN utility improvements, and Image Utility Tool enhancements.
- Enhanced code structure and improved overall consistency across the package.

### Version 1.0.0
- Initial release.

## Contributing
Please open pull requests or issues for contributions and bug reports. Keep the Unity Package structure intact and follow the project's coding standards.

# Game Design Editor Tools

This repository now follows the Unity package layout so it can be consumed straight from Git using the Package Manager.

## Structure

```
GameDesignEditorTools/
├── Editor/                  # All editor-only scripts
└── package.json             # Unity package manifest
```

All existing tools remain unchanged, but they now live under the `Editor/` folder so Unity automatically limits them to the Editor environment when the package is imported.

## Installing via Git URL

1. Open **Window > Package Manager** in Unity.
2. Click the **+** button and choose **Add package from git URL...**
3. Enter the repository URL, for example:
   ```
   https://github.com/your-org/GameDesignEditorTools.git
   ```
4. Unity will download the package and make the tools available in your project.

If you need to reference a specific revision or tag, append it to the URL (for example `#v1.0.0`).

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class ScenePresetItem
{
    public string name = "New Preset";
    public List<SceneAsset> scenes = new List<SceneAsset>();
}

public class ScenePresetDataSO : ScriptableObject
{
    public List<ScenePresetItem> presets = new List<ScenePresetItem>();
}
#endif

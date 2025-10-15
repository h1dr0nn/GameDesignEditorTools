#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class QuickShortcutsWindow : EditorWindow
{
    [System.Serializable]
    private class Cache
    {
        public List<string> guids = new List<string>();
        public float iconSize = 80f;
        public bool showLabels = true;
    }

    private const string BaseKey = "QuickShortcuts_Cache_";
    private Cache _cache = new Cache();
    private Vector2 _scroll;
    private int _dragIndex = -1;
    private Vector2 _dragStart;
    private string _search = "";

    [MenuItem("Tools/Game Design/Quick Shortcuts")]
    public static void Open() => GetWindow<QuickShortcutsWindow>("Quick Shortcuts");

    private string PrefsKey => BaseKey + Application.dataPath;

    private void OnEnable() => Load();
    private void OnDisable() => Save();

    private void OnGUI()
    {
        TopBar();
        AcceptExternalDrop();
        using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = sv.scrollPosition;
            DrawGrid();
        }
    }

    private void TopBar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Quick Shortcuts", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _cache.iconSize = EditorGUILayout.Slider("Icon", _cache.iconSize, 48, 128, GUILayout.Width(220));
                _cache.showLabels = EditorGUILayout.ToggleLeft("Labels", _cache.showLabels, GUILayout.Width(80));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Search", GUILayout.Width(48));
                _search = EditorGUILayout.TextField(_search);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add From Selection", GUILayout.Height(22))) AddFromSelection();
                if (GUILayout.Button("Remove Nulls", GUILayout.Height(22))) RemoveNulls();
                if (GUILayout.Button("Clear", GUILayout.Height(22))) { _cache.guids.Clear(); Save(); Repaint(); }
            }
        }
    }

    private void DrawGrid()
    {
        var objs = ResolveObjects();
        bool filtered = !string.IsNullOrWhiteSpace(_search);
        if (filtered)
            objs = objs.Where(o => o != null && o.name.ToLowerInvariant().Contains(_search.ToLowerInvariant())).ToList();

        float cell = _cache.showLabels ? _cache.iconSize + 24f : _cache.iconSize + 8f;
        float w = position.width - 24f;
        int cols = Mathf.Max(1, Mathf.FloorToInt(w / cell));
        int idx = 0;

        if (objs.Count == 0)
        {
            EditorGUILayout.HelpBox("Drag assets here to add shortcuts. You can drag from this window into Hierarchy/Project.", MessageType.Info);
            return;
        }

        for (int r = 0; idx < objs.Count; r++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int c = 0; c < cols && idx < objs.Count; c++, idx++)
                {
                    var obj = objs[idx];
                    int realIndex = filtered ? FindGuidIndex(obj) : idx;
                    var rect = GUILayoutUtility.GetRect(cell, cell, GUILayout.Width(cell), GUILayout.Height(cell));
                    DrawItem(rect, obj, realIndex);
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

    private void DrawItem(Rect rect, Object obj, int index)
    {
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        if (obj != null)
        {
            var previewRect = new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - (_cache.showLabels ? 28 : 12));
            var labelRect = new Rect(rect.x + 6, rect.yMax - 22, rect.width - 12, 18);
            var tex = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
            if (tex != null) GUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);
            if (_cache.showLabels) EditorGUI.LabelField(labelRect, new GUIContent(obj.name), EditorStyles.miniBoldLabel);
        }
        else
        {
            var labelRect = new Rect(rect.x + 6, rect.center.y - 9, rect.width - 12, 18);
            EditorGUI.LabelField(labelRect, "Missing", EditorStyles.centeredGreyMiniLabel);
        }

        var e = Event.current;

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            if (index >= 0)
            {
                _dragIndex = index;
                _dragStart = e.mousePosition;
            }
            if (e.clickCount == 2 && obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
            e.Use();
        }

        if (e.type == EventType.MouseDrag && _dragIndex == index && obj != null)
        {
            if ((e.mousePosition - _dragStart).sqrMagnitude > 16f)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { obj };
                DragAndDrop.StartDrag(obj.name);
                _dragIndex = -1;
                e.Use();
            }
        }

        if (e.type == EventType.MouseUp && rect.Contains(e.mousePosition) && e.button == 1)
        {
            var m = new GenericMenu();
            if (obj != null)
            {
                m.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(obj));
                m.AddItem(new GUIContent("Select"), false, () => Selection.activeObject = obj);
                if (IsPrefabAsset(obj)) m.AddItem(new GUIContent("Instantiate In Scene"), false, () => InstantiateInScene(obj));
            }
            m.AddSeparator("");
            if (index >= 0)
            {
                m.AddItem(new GUIContent("Move Up"), false, () => Move(index, -1));
                m.AddItem(new GUIContent("Move Down"), false, () => Move(index, +1));
                m.AddSeparator("");
                m.AddItem(new GUIContent("Remove"), false, () => { _cache.guids.RemoveAt(index); Save(); Repaint(); });
            }
            else
            {
                m.AddDisabledItem(new GUIContent("Move Up"));
                m.AddDisabledItem(new GUIContent("Move Down"));
                m.AddSeparator("");
                m.AddDisabledItem(new GUIContent("Remove"));
            }
            m.ShowAsContext();
            e.Use();
        }
    }

    private int FindGuidIndex(Object obj)
    {
        if (obj == null) return -1;
        var path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return -1;
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid)) return -1;
        return _cache.guids.IndexOf(guid);
    }

    private void Move(int index, int delta)
    {
        int ni = Mathf.Clamp(index + delta, 0, _cache.guids.Count - 1);
        if (ni == index) return;
        var tmp = _cache.guids[index];
        _cache.guids.RemoveAt(index);
        _cache.guids.Insert(ni, tmp);
        Save();
        Repaint();
    }

    private void AcceptExternalDrop()
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var o in DragAndDrop.objectReferences)
            {
                var guid = ToGuid(o);
                if (string.IsNullOrEmpty(guid)) continue;
                if (!_cache.guids.Contains(guid)) _cache.guids.Add(guid);
            }
            Save();
            Repaint();
        }
        e.Use();
    }

    private void AddFromSelection()
    {
        foreach (var o in Selection.objects)
        {
            var guid = ToGuid(o);
            if (string.IsNullOrEmpty(guid)) continue;
            if (!_cache.guids.Contains(guid)) _cache.guids.Add(guid);
        }
        Save();
        Repaint();
    }

    private void RemoveNulls()
    {
        _cache.guids = _cache.guids.Where(g => !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))).ToList();
        Save();
        Repaint();
    }

    private List<Object> ResolveObjects()
    {
        var list = new List<Object>(_cache.guids.Count);
        foreach (var g in _cache.guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(path)) { list.Add(null); continue; }
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            list.Add(obj);
        }
        return list;
    }

    private static string ToGuid(Object o)
    {
        if (o == null) return null;
        var path = AssetDatabase.GetAssetPath(o);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.AssetPathToGUID(path);
    }

    private static bool IsPrefabAsset(Object o)
    {
        if (o == null) return false;
        var t = PrefabUtility.GetPrefabAssetType(o);
        return t != PrefabAssetType.NotAPrefab && t != PrefabAssetType.MissingAsset;
    }

    private static void InstantiateInScene(Object o)
    {
        if (!IsPrefabAsset(o)) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(o);
        if (go != null)
        {
            Undo.RegisterCreatedObjectUndo(go, "Instantiate Prefab");
            Selection.activeObject = go;
        }
    }

    private void Load()
    {
        var json = EditorPrefs.GetString(PrefsKey, "");
        _cache = string.IsNullOrEmpty(json) ? new Cache() : JsonUtility.FromJson<Cache>(json) ?? new Cache();
    }

    private void Save()
    {
        var json = JsonUtility.ToJson(_cache);
        EditorPrefs.SetString(PrefsKey, json);
    }
}
#endif

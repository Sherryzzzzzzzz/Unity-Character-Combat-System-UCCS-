using System.Collections.Generic;
using UnityEngine;

/// <summary>黑板值类型</summary>
public enum BlackboardType
{
    Float, Int, Bool, Vector3, GameObject, Transform
}

/// <summary>黑板条目定义（存盘用）</summary>
[System.Serializable]
public class BlackboardEntry
{
    public string key;
    public BlackboardType type;
}

/// <summary>运行时黑板 — 键值对存储</summary>
public class BTBlackboard
{
    private readonly Dictionary<string, float>    _floats   = new();
    private readonly Dictionary<string, int>      _ints     = new();
    private readonly Dictionary<string, bool>     _bools    = new();
    private readonly Dictionary<string, Vector3>  _vectors  = new();
    private readonly Dictionary<string, GameObject> _gos   = new();
    private readonly Dictionary<string, Transform>  _transforms = new();

    /// <summary>根据资产定义初始化黑板键</summary>
    public void Initialize(List<BlackboardEntry> entries)
    {
        Clear();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.key)) continue;
            switch (entry.type)
            {
                case BlackboardType.Float:      Set(entry.key, 0f);       break;
                case BlackboardType.Int:        Set(entry.key, 0);        break;
                case BlackboardType.Bool:       Set(entry.key, false);    break;
                case BlackboardType.Vector3:    Set(entry.key, Vector3.zero); break;
                case BlackboardType.GameObject: Set(entry.key, (GameObject)null); break;
                case BlackboardType.Transform:  Set(entry.key, (Transform)null);  break;
            }
        }
    }

    public void Clear()
    {
        _floats.Clear(); _ints.Clear(); _bools.Clear();
        _vectors.Clear(); _gos.Clear(); _transforms.Clear();
    }

    // ── Set ──────────────────────────────────────
    public void Set(string key, float v)    { _floats[key] = v; }
    public void Set(string key, int v)      { _ints[key] = v; }
    public void Set(string key, bool v)     { _bools[key] = v; }
    public void Set(string key, Vector3 v)  { _vectors[key] = v; }
    public void Set(string key, GameObject v) { _gos[key] = v; }
    public void Set(string key, Transform v)  { _transforms[key] = v; }

    // ── Get ──────────────────────────────────────
    public T Get<T>(string key)
    {
        var t = typeof(T);
        if (t == typeof(float)          && _floats.TryGetValue(key,      out var fv)) return (T)(object)fv;
        if (t == typeof(int)            && _ints.TryGetValue(key,        out var iv)) return (T)(object)iv;
        if (t == typeof(bool)           && _bools.TryGetValue(key,       out var bv)) return (T)(object)bv;
        if (t == typeof(Vector3)        && _vectors.TryGetValue(key,     out var vv)) return (T)(object)vv;
        if ((t == typeof(GameObject) || t == typeof(Object))
                                        && _gos.TryGetValue(key,         out var go)) return (T)(object)go;
        if (t == typeof(Transform)      && _transforms.TryGetValue(key,  out var tr)) return (T)(object)tr;
        return default;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = Get<T>(key);
        return !EqualityComparer<T>.Default.Equals(value, default);
    }

    public bool GetBool(string key) => _bools.TryGetValue(key, out var v) && v;
}

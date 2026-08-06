using Sirenix.OdinInspector;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public partial class QuestObjData
{
    public string nameKey;
    public string descKey;
    public QuestObjType type;
    public Dictionary<string, object> data;// 搜索System bool

    T Get<T>(string key)
    {
        if (data.TryGetValue(key, out var value) && value is T casted)
            return casted;

        Debug.LogError($"QuestObjData {nameKey} 缺少数据 {key}");
        return default;
    }
    public string GetString(string key) => Get<string>(key);
    public int GetInt(string key) => Get<int>(key);
    public bool GetBool(string key) => Get<bool>(key);
    public float GetFloat(string key) => Get<float>(key);

    public QuestObjData()
    {
        this.nameKey = "Unset";
        this.type = QuestObjType.None;
        this.data = new();
    }
    public override string ToString()
    {
        return string.Join(", ", data.Select(kv => $"{kv.Key}:{kv.Value}"));
    }
}

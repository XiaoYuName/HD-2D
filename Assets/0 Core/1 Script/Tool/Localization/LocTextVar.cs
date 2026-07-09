using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

// 带变量多语言文本：Key 含占位符（如"价格：{Price}"），外部灌值即自动展示并随语言刷新。
// 用法：goldText.SetVar(LocalizeVarSet.WitchPotion.GameCoin, InventoryManager.Instance.GameCoin);
// 多占位符：前几个传 refresh:false，最后一个默认刷新，避免占位符未全赋值时 SmartFormat 抛异常。
//         例："{Opened}/{Total}" → SetVar(Opened, x, false); SetVar(Total, y);
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocTextVar : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] LocKeyRef data;

    LocRef locRef;

    void Awake()
    {
        Init();
        // locRef = LocRef.Create(text);
        // locRef.SetReference(data.Table, data.Value);
    }

    void OnDestroy() => locRef?.Unbind();

    void Init()
    {
        if(locRef == null)
        {
            locRef = LocRef.Create(text);
            locRef.SetReference(data.Table, data.Value);
        }
    }
    public void SetVar(string name, int value, bool refresh = true)
    {
        if(locRef == null)
            Init();

        locRef.SetVar(name, value);
        if(refresh)
            locRef.Refresh();
    }
    public void SetVar(string name, float value, bool refresh = true) 
    {
        locRef.SetVar(name, value); if(refresh) locRef.Refresh();
    }
    public void SetVar(string name, string value, bool refresh = true)
    {
        locRef.SetVar(name, value); if(refresh) locRef.Refresh();
    }
    public void SetVar(string name, bool value, bool refresh = true)
    {
        locRef.SetVar(name, value); if(refresh) locRef.Refresh();
    }
    public void Clear() => locRef?.Clear();
    [Button]
    void SetRef() => text = GetComponent<TextMeshProUGUI>();
    void Reset() => SetRef();
}

using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

// 可变多语言文本：Key 由外部运行时决定（如"成功"/"失败"/"平局"），随语言自动刷新。
// 用法：resultText.SetText(LocalizeTableSet.CasinoGame, win ? "Win" : "Lose");
// 默认文本可空：填了就在启动时先展示，不填则等待首次 SetText。
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocTextSwitch : MonoBehaviour
{
    [SerializeField] TMP_Text text;

    [Title("默认文本（可空，不填则等待 SetText）")]
    [SerializeField] LocalSelectedData defaultData;

    LocRef locRef;

    void Awake()
    {
        locRef = LocRef.Create(text);
        if(defaultData.IsValid())
            SetText(defaultData.Table, defaultData.Value);
    }

    void OnDestroy() => locRef.Unbind();

    public void SetText(string table, string key)
    {
        locRef.SetReference(table, key);
        locRef.Refresh();
    }

    [Button]
    void SetRef() => text = GetComponent<TextMeshProUGUI>();

    void Reset() => SetRef();
}

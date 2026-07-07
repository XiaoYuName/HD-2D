using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

// 固定多语言文本：Inspector 选好表 + Key（LocKeyRef 带下拉与预览），自动展示并随语言刷新。
// 适用："退出""开始游戏"等不变文案，无占位符、无运行时改 Key。
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocText : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] LocKeyRef data;

    LocRef locRef;

    void Awake()
    {
        locRef = LocRef.Create(text);
        locRef.SetReference(data.Table, data.Value);
        locRef.Refresh();
    }

    void OnDestroy() => locRef.Unbind();
    [Button]
    void SetRef() => text = GetComponent<TextMeshProUGUI>();

    void Reset() => SetRef();
}

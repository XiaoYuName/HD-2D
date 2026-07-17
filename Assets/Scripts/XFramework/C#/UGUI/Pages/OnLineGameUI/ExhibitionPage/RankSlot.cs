using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using XFramework;

public class RankSlot : UIBase
{
    private LocalizeStringEvent NameStringEvent;
    private TextMeshProUGUI ValueStringEvent;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        NameStringEvent = Get<LocalizeStringEvent>("NameStringEvent");
        ValueStringEvent = Get<TextMeshProUGUI>("ValueStringEvent");
    }

    public void SetLabel(string nameTable, string nameValue, string value)
    {
        NameStringEvent.SetText(nameTable, nameValue);
        ValueStringEvent.text = value;
    }
}

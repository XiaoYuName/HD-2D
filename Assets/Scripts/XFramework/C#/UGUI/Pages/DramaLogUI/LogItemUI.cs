using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class LogItemUI : UIBase
{
    private LocalizeStringEvent nameStringEvent;
    private LocalizeStringEvent contentStingEvent;
    private HorizontalLayoutGroup nameLayoutGroup;
    private ContentSizeFitter contentSizeFitter;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        nameStringEvent = Get<LocalizeStringEvent>("NameLabel/Label");
        contentStingEvent = Get<LocalizeStringEvent>("ContentLabel");
        nameLayoutGroup = Get<HorizontalLayoutGroup>("NameLabel");
        contentSizeFitter = Get<ContentSizeFitter>("NameLabel");
    }

    public void SetData(LocalSelectedData name,LocalSelectedData content)
    {
        StartCoroutine(UpdateLabel(name,content));
    }

    private IEnumerator UpdateLabel(LocalSelectedData name,LocalSelectedData content)
    {
        nameStringEvent.StringReference.SetReference(name.Table,name.Value);
        nameStringEvent.StringReference.RefreshString();
        contentStingEvent.StringReference.SetReference(content.Table,content.Value);
        contentStingEvent.StringReference.RefreshString();
        yield return new WaitForEndOfFrame();
        contentSizeFitter.SetLayoutHorizontal();
        nameLayoutGroup.CalculateLayoutInputHorizontal();
    }
}

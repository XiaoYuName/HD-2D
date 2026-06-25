using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class LogItemUI : UIBase
{
    private GameObject NameLabel;
    private LocalizeStringEvent nameStringEvent;
    private LocalizeStringEvent contentStingEvent;
    private HorizontalLayoutGroup nameLayoutGroup;
    private ContentSizeFitter contentSizeFitter;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        NameLabel = Get("NameLabel");
        nameStringEvent = Get<LocalizeStringEvent>("NameLabel/Label");
        contentStingEvent = Get<LocalizeStringEvent>("ContentLabel");
        nameLayoutGroup = Get<HorizontalLayoutGroup>("NameLabel");
        contentSizeFitter = Get<ContentSizeFitter>("NameLabel");
    }

    public void SetData(bool isShowName,LocalSelectedData name,LocalSelectedData content)
    {
        StartCoroutine(UpdateLabel(isShowName,name,content));
    }

    public void SetData(bool isShowName, TbLocalzationKeyData name, TbLocalzationKeyData content)
    {
        StartCoroutine(UpdateLabel(isShowName,name,content));
    }

    private IEnumerator UpdateLabel(bool isShowName,LocalSelectedData name,LocalSelectedData content)
    {
        if (isShowName)
        {
            NameLabel.SetActive(true);
        }
        else
        {
            NameLabel.SetActive(false);
        }
        nameStringEvent.StringReference.SetReference(name.Table,name.Value);
        nameStringEvent.StringReference.RefreshString();
        contentStingEvent.StringReference.SetReference(content.Table,content.Value);
        contentStingEvent.StringReference.RefreshString();
        yield return new WaitForEndOfFrame();
        contentSizeFitter.SetLayoutHorizontal();
        nameLayoutGroup.CalculateLayoutInputHorizontal();
    }
    
    private IEnumerator UpdateLabel(bool isShowName,TbLocalzationKeyData name,TbLocalzationKeyData content)
    {
        if (isShowName)
        {
            NameLabel.SetActive(true);
            nameStringEvent.SetText(name.Table,name.Value);
            nameStringEvent.StringReference.RefreshString();
        }
        else
        {
            NameLabel.SetActive(false);
        }
        contentStingEvent.SetText(content.Table,content.Value);
        contentStingEvent.StringReference.RefreshString();
        yield return new WaitForEndOfFrame();
        contentSizeFitter.SetLayoutHorizontal();
        nameLayoutGroup.CalculateLayoutInputHorizontal();
    }
}

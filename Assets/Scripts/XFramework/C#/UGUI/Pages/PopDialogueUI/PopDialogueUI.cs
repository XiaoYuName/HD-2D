using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class PopDialogueUI : UIBase
{
    private LocalizeStringEvent titleStringEvent;
    private TextMeshProUGUI titleTmpTex;
    private LocalizeStringEvent contentStringEvent;
    private TextMeshProUGUI contentTmpTex;
    
    private CustomButton CancelButton;
    private TextMeshProUGUI cancelTmpTex;
    private CustomButton ActionButton;
    private TextMeshProUGUI actionTmpTex;
    private CustomButton CloseButton;
   
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        titleStringEvent = Get<LocalizeStringEvent>("UIMask/Background/TitleFarme/Label");
        titleTmpTex = Get<TextMeshProUGUI>("UIMask/Background/TitleFarme/Label");
        
        contentStringEvent = Get<LocalizeStringEvent>("UIMask/Background/ContentLabel");
        contentTmpTex = Get<TextMeshProUGUI>("UIMask/Background/ContentLabel");
        
        CancelButton = Get<CustomButton>("UIMask/Background/DownButtons/CancelButton");
        cancelTmpTex  = Get<TextMeshProUGUI>("UIMask/Background/DownButtons/CancelButton");
        
        ActionButton = Get<CustomButton>("UIMask/Background/DownButtons/ActionButton");
        actionTmpTex = Get<TextMeshProUGUI>("UIMask/Background/DownButtons/ActionButton");
        
        CloseButton = Get<CustomButton>("UIMask/Background/CloseButton");
        Bind(CloseButton,Close,"");
    }
    
    
    /// <summary>
    /// 显示一个对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="content">内容文本</param>
    /// <param name="cancelData">取消按钮文本</param>
    /// <param name="actionData">Action 按钮文本</param>
    /// <param name="cancel">点击回调</param>
    /// <param name="action">点击回调</param>
    public void ShowDialogue(LocalSelectedData title,LocalSelectedData content,LocalSelectedData cancelData,LocalSelectedData actionData,
        Action cancel = null,Action action = null)
    {
        titleStringEvent.StringReference.SetReference(title.Table,title.Value);
        titleStringEvent.StringReference.RefreshString();
        
        contentStringEvent.StringReference.SetReference(content.Table,content.Value);
        contentStringEvent.StringReference.RefreshString();
        
        ActionButton.gameObject.SetActive(true);
        CancelButton.SetLabel(cancelData);
        ActionButton.SetLabel(actionData);
        Bind(CancelButton, () =>
        {
            cancel?.Invoke();
            Close();
        },"");
        Bind(ActionButton, () =>
        {
            action?.Invoke();
            Close();
        },"");
    }

    /// <summary>
    /// 显示一个提示框
    /// </summary>
    /// <param name="title">标题文本</param>
    /// <param name="content">内容文本</param>
    /// <param name="cancelData">取消按钮文本</param>
    /// <param name="cancel">点击回调</param>
    public void ShowPopWindow(LocalSelectedData title, LocalSelectedData content, LocalSelectedData cancelData,
        Action cancel = null)
    {
        titleStringEvent.StringReference.SetReference(title.Table,title.Value);
        titleStringEvent.StringReference.RefreshString();
        contentStringEvent.StringReference.SetReference(content.Table,content.Value);
        contentStringEvent.StringReference.RefreshString();
        CancelButton.SetLabel(cancelData);
        ActionButton.gameObject.SetActive(false);
        Bind(CancelButton, () =>
        {
            cancel?.Invoke();
            Close();
        },"");
    }

    /// <summary>
    /// 显示一个提示框
    /// </summary>
    /// <param name="title">标题文本</param>
    /// <param name="content">内容文本</param>
    /// <param name="cancelTex">取消文本</param>
    /// <param name="cancel">点击回调</param>
    public void ShowPopWindow(string content,string title = "Tips",string cancelTex = "Confirm",
        Action cancel = null)
    {
        titleTmpTex.text = LanguageManager.Instance.GetLocalizedString("UIText", title);
        contentTmpTex.text = LanguageManager.Instance.GetLocalizedString("PopDialogue", content);
        CancelButton.SetLabel(LanguageManager.Instance.GetLocalizedString("UIText", cancelTex));
        ActionButton.gameObject.SetActive(false);
        Bind(CancelButton, () =>
        {
            cancel?.Invoke();
            Close();
        },"");
    }
}

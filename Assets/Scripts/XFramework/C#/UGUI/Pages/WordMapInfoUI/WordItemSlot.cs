using System;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class WordItemSlot : UIBase
{
    private LocalizeStringEvent _stringEvent;
    private Button _button;
    private Image _image;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _button = Get<Button>("");
        _image = Get<Image>("");
        _stringEvent = Get<LocalizeStringEvent>("Label");
        
    }

    public void SetData(Color baseColor,MinSceneData minSceneData,Action<MinSceneData> callback)
    {
        _image.color = baseColor;
        _stringEvent.StringReference.SetReference(minSceneData.sceneName.Table,minSceneData.sceneName.Value);
        _stringEvent.StringReference.RefreshString();
        Bind(_button, () =>
        {
            callback?.Invoke(minSceneData);
        },"");
    }
}

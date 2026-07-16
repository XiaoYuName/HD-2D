using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XFramework;

public partial class PopRewardPropertyUI : UIBase
{
    private List<TextMeshProUGUI> mLabels = new List<TextMeshProUGUI>();
    private Action OnClose;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(rewardBtn,Close,"");
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var label in mLabels)
        {
            AssetsManager.Instance.FreeGameObject(label.gameObject);
        }
        mLabels.Clear();
        OnClose?.Invoke();
        OnClose = null;
    }

    public void ShowingLabels(List<string> values,Action onClose = null)
    {
        this.OnClose = onClose;
        foreach (var label in mLabels)
        {
            AssetsManager.Instance.FreeGameObject(label.gameObject);
        }
        mLabels.Clear();
        foreach (var text in values)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.LabelTexPath);
            obj.transform.SetParent(content);
            obj.transform.localScale = Vector3.one;

            var tmpText = obj.GetComponent<TextMeshProUGUI>();
            tmpText.text = text;
            mLabels.Add(tmpText);
        }
    }
}

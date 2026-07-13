using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class OnLineGameUI : UIBase
{
    private Dictionary<OnLinePageType,SelectedButton> mLinePageButtons = new Dictionary<OnLinePageType, SelectedButton>();
    private Dictionary<OnLinePageType,UIBase>  mOnLinePages = new Dictionary<OnLinePageType, UIBase>();
    private OnLinePageType selectedType = OnLinePageType.None;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        mLinePageButtons =  new Dictionary<OnLinePageType, SelectedButton>();
        mOnLinePages = new Dictionary<OnLinePageType, UIBase>();
        
        Bind(closeBtn,Close,"");
        Bind(fanInfoButton, () =>
        {
            OptionPage(OnLinePageType.Fan);
        },"");
        mLinePageButtons.Add(OnLinePageType.Fan, fanInfoButton);
        mOnLinePages.Add(OnLinePageType.Fan,fanPage);

        foreach (var data in GameDataManager.Instance.onLineGameData.OnLineTypeMenuData)
        {
            if (!mLinePageButtons.ContainsKey(data.onLinePageType))
            {
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.OnlineMenuButtonPath);
                obj.transform.SetParent(menuButtonScrollRect.content);
                obj.transform.localScale = Vector3.one;
                var btn = obj.GetComponent<SelectedButton>();
                btn.SetLabel(data.labelNameString);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(()=>OptionPage(data.onLinePageType));
                mLinePageButtons.Add(data.onLinePageType, btn);
            }

            if (!mOnLinePages.ContainsKey(data.onLinePageType))
            {
                var pageObj = AssetsManager.Instance.Instantiate(data.onLinePagePath);
                pageObj.transform.SetParent(pageContent);
                pageObj.transform.localScale = Vector3.one;
                pageObj.transform.localPosition = Vector3.zero;
                pageObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);
                var ui =  pageObj.GetComponent<UIBase>();
                mOnLinePages.Add(data.onLinePageType, ui);
            }
        }
        
        foreach (var key in mOnLinePages.Keys)
        {
            mOnLinePages[key].Init();
        }
        
        OptionPage(OnLinePageType.Fan);
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(UpdatePlayerData);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(UpdatePlayerData);
        
    }

    private void UpdatePlayerData(PlayerData playerData)
    {
        fenCount.text = $"{playerData.GetProperty(PropertyType.FenCount)}";
    }


    private void OptionPage(OnLinePageType type)
    {
        if (selectedType == type) return;
        selectedType = type;
        foreach (var onlinePageType in mLinePageButtons.Keys)
        {
            if (onlinePageType == type)
            {
                mLinePageButtons[onlinePageType].SetSelected(true);
            }
            else
            {
                mLinePageButtons[onlinePageType].SetSelected(false);
            }
        }
        foreach (var onlinePageType in mOnLinePages.Keys)
        {
            if (onlinePageType == type)
            {
                mOnLinePages[onlinePageType].Open();
            }
            else
            {
                mOnLinePages[onlinePageType].Close();
            }
        }
    }

}

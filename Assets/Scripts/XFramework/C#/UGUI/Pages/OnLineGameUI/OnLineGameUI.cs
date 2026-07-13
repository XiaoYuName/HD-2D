using System.Collections.Generic;
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


        foreach (var key in mOnLinePages.Keys)
        {
            mOnLinePages[key].Init();
        }
        
        OptionPage(OnLinePageType.Fan);
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

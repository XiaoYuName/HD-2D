using System;
using UnityEngine;
using UnityEngine.Localization.Components;
using XFramework;

public class CustomDropdownUI : UIBase
{
    private LocalizeStringEvent SelectedStringEvent;
    private CustomButton[] OptionButtons;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        
    }

    public void SetItemSortType(ItemSortType itemSortType,Action<ItemSortType> OnSelected)
    {
    }
}



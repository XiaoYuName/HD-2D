using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using XFramework;

public class CustomDropdownUI : UIBase,IPointerClickHandler
{
    private LocalizeStringEvent SelectedStringEvent;
    private RectTransform dropdownRect;
    private Action<ItemSortType> SelectedSortType;
    private Dictionary<ItemSortType, SelectedButton> LocalSelectedBtnDic;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        SelectedStringEvent = Get<LocalizeStringEvent>("Label");
        dropdownRect = Get<RectTransform>("SortDropodwn");
        LocalSelectedBtnDic = new Dictionary<ItemSortType, SelectedButton>();
    }

    public void SetItemSortType(Action<ItemSortType> OnSelected)
    {
        SelectedSortType = OnSelected;
        foreach (ItemSortType sortType in Enum.GetValues(typeof(ItemSortType)))
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.SortButtonPath);
            obj.transform.SetParent(dropdownRect.transform);
            obj.transform.localScale = Vector3.one;
            
            LocalSelectedData localSelectedData = new LocalSelectedData();
            localSelectedData.Table = "EnumsText";
            localSelectedData.Value = sortType.ToString();
            
            var btn = obj.GetComponent<SelectedButton>();
            btn.SetLabel(localSelectedData);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                OnSelectedSortType(sortType);
            });
            LocalSelectedBtnDic.Add(sortType, btn);
        }
        OnSelectedSortType(ItemSortType.CreatTime);
    }

    private void OnSelectedSortType(ItemSortType sortType)
    {
        foreach (var Type in LocalSelectedBtnDic.Keys)
        {
            LocalSelectedBtnDic[Type].SetSelected(sortType== Type);
        }
        
        LocalSelectedData localSelectedData = new LocalSelectedData();
        localSelectedData.Table = "EnumsText";
        localSelectedData.Value = sortType.ToString();
        SelectedStringEvent.SetText(localSelectedData);
        SelectedSortType?.Invoke(sortType);
        dropdownRect.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        dropdownRect.gameObject.SetActive(!dropdownRect.gameObject.activeSelf);
    }
}



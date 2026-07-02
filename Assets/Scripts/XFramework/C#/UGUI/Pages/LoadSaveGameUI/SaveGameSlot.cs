using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using XFramework;

public class SaveGameSlot : UIBase
{
    private LocalizeStringEvent dayTextString;
    private TextMeshProUGUI userTimeText;
    private TextMeshProUGUI userNameText;
    private TextMeshProUGUI userGoldText;
    private Image userImage;
    private TextMeshProUGUI IndexText;
    private GameObject isAutoObj;
    private GameObject dataObj;
    private GameObject emptyObj;
    private CommonButton btn;
    
    public UserSaveSummary UserSaveSummaryData { get; private set; }
    
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        dayTextString = Get<LocalizeStringEvent>("Data/DayFarme/Text");
        userTimeText = Get<TextMeshProUGUI>("Data/TimerFarme/Text");
        userNameText = Get<TextMeshProUGUI>("Data/UserNameFarme/Text");
        userGoldText = Get<TextMeshProUGUI>("Data/UserGoldFarme/Text");
        userImage = Get<Image>("Data/UserImageFarme/Image");
        IndexText = Get<TextMeshProUGUI>("Data/IndexTex");
        isAutoObj = Get("Data/AutoFarme");
        dataObj = Get("Data");
        emptyObj = Get("Mask");
        btn = Get<CommonButton>("");
        UserSaveSummaryData = null;
    }
    
    public void RegisterClick(Action<SaveGameSlot> action)
    {
        BindAGVClick(btn, () =>
        {
            action?.Invoke(this);
        },"");
    }

    public void SetData(UserSaveSummary userSaveSummaryData)
    {
        if (userSaveSummaryData == null)
        {
           
            SetEmpty();
            return;
        }
        dataObj.SetActive(true);
        emptyObj.SetActive(false);
        UserSaveSummaryData = userSaveSummaryData;
        if (dayTextString.StringReference.TryGetValue("DayValue", out IVariable variable))
        {
            if (variable is StringVariable stringVariable)
            {
                stringVariable.Value = userSaveSummaryData.PreviewDay.ToString();
            }
            dayTextString.RefreshString();
        }
        userTimeText.text = userSaveSummaryData.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
        userNameText.text = userSaveSummaryData.UserName;
        userGoldText.text = userSaveSummaryData.PreviewGoldNumber.ToString();
    }

    public void SetEmpty()
    {
        
        UserSaveSummaryData = null;
        dataObj.SetActive(false);
        emptyObj.SetActive(true);
    }
}

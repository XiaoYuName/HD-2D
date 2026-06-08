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
        isAutoObj = Get("Data/AutoFarme/Obj");
    }

    public void SetData(User userData)
    {
        if (dayTextString.StringReference.TryGetValue("DayValue", out IVariable variable))
        {
            if (variable is StringVariable stringVariable)
            {
                stringVariable.Value = userData.Day.ToString();
            }
            dayTextString.RefreshString();
        }
        userTimeText.text = userData.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
        userNameText.text = userData.UserName;
        userGoldText.text = "10000";
        //userImage.sprite = userData.Avatar;
        //IndexText.text = userData.Index.ToString();
        //isAutoObj.SetActive(userData.IsAuto);
    }
}

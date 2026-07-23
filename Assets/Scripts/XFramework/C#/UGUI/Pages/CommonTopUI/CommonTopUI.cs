using System;
using UnityEngine;
using XFramework;

public partial class CommonTopUI : UIBase
{
    [SerializeField] TimeSlotConfig envModeConfig;
    
    public override void Init()
    {
        InitAutoBind();

        
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        envModeConfig = AssetsManager.Instance.LoadAssets<TimeSlotConfig>(AssetKeys.TimeSlotConfigPath);
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        AssetsManager.Instance.FreeAsset(AssetKeys.TimeSlotConfigPath);
        
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
    }

    public void SetTitle(string table,string key)
    {
        tileLabTex.SetText(table,key);
    }

    public void SetClose(Action callback)
    {
        Bind(closeButton,callback,"");
    }


    private void PlayerDataChange(PlayerData playerData)
    {
        timeSlotVal.SetText(LocTableSet.MainUI,envModeConfig.GetNameKey(playerData.TimeSlot));
        timeSlotIcon.SetIcon(envModeConfig.GetIconPath(playerData.TimeSlot));
        actionPointsVal.text = GameDataManager.Instance.GetPropertyText(PropertyType.ActionPointsValue);
        strengthValue.text = GameDataManager.Instance.GetPropertyText(PropertyType.Strength);
    }
}

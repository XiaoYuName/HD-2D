using XFramework;

public partial class MedicinalSolutionUI : UIBase
{
    private MedicinalSolutionSettingData Setting;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Setting = LoadAsset<MedicinalSolutionSettingData>(AssetKeys.MedicinalSolutionSettingDataPath);
    }
}

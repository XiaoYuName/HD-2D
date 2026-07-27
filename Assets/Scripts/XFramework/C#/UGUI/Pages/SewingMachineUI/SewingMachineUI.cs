using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class SewingMachineUI : UIBase
{
    private SewingMachineGameData Setting;
    private Sprite CursorTexture;
    
    public override void Init()
    {
        InitAutoBind();

        Setting = LoadAsset<SewingMachineGameData>(AssetKeys.SewingMachineGameDataPath);
        CursorTexture = LoadAsset<Sprite>(AssetKeys.ShouPath);
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Cursor.SetCursor(CursorTexture.texture, Vector2.zero, CursorMode.Auto);
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }


    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        
    }
    
}

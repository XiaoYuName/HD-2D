using System;
using XFramework;

public partial class PopCompleteWindow : UIBase
{
    private Action OnClose;
    
    public override void Init()
    {
        InitAutoBind();
        Bind(btnQueren,Close,"");

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void ShowCompleteWindow(Action Close)
    {
        OnClose?.Invoke();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        OnClose?.Invoke();
        OnClose = null;
    }
}

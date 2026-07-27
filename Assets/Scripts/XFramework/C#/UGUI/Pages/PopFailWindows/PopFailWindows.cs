using System;
using XFramework;

public partial class PopFailWindows : UIBase
{
    private Action OnFail;
    private Action OnClose;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnReset,FailClick,"");
        Bind(btnQueren,CloseClick,"");
    }

    public void InitializeUI(bool isReset, Action onFail, Action onClose)
    {
        this.OnFail = onFail;
        this.OnClose = onClose;
        btnReset.interactable = isReset;
    }

    private void FailClick()
    {
        OnFail?.Invoke();
        Close();
    }

    private void CloseClick()
    {
        OnClose?.Invoke();
        Close();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        OnFail = null;
        OnClose = null;
    }
}

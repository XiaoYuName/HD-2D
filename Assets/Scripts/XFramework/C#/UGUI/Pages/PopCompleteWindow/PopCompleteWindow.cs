using System;
using UnityEngine;
using XFramework;

public partial class PopCompleteWindow : UIBase
{
    private Action OnClose;
    private Action OnNext;

    /// <summary>btn_next 隐藏时要把确认按钮居中，这里记住 prefab 里的原始位置</summary>
    private Vector2 confirmOriginPos;

    public override void Init()
    {
        InitAutoBind();
        Bind(btnQueren,Close,"");
        Bind(btnNext,OnNextClick,"");

        if (btnQueren != null)
        {
            confirmOriginPos = ((RectTransform)btnQueren.transform).anchoredPosition;
        }

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    /// <summary>
    /// 结算弹窗。
    /// onNext 不为空表示还有下一个小游戏，显示"继续"按钮；
    /// 为空时只留确认按钮并把它居中。
    /// </summary>
    public void ShowCompleteWindow(Action Close, Action Next = null)
    {
        OnClose = Close;
        OnNext = Next;

        bool hasNext = Next != null;
        if (btnNext != null)
        {
            btnNext.gameObject.SetActive(hasNext);
        }

        if (btnQueren != null)
        {
            ((RectTransform)btnQueren.transform).anchoredPosition =
                hasNext ? confirmOriginPos : new Vector2(0f, confirmOriginPos.y);
        }
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        Action onClose = OnClose;
        OnClose = null;
        OnNext = null;
        onClose?.Invoke();
    }

    private void OnNextClick()
    {
        Action onNext = OnNext;
        // 先清掉 OnClose，否则下面的 Close() 会把"返回界面"那套逻辑也跑一遍
        OnClose = null;
        OnNext = null;
        Close();
        onNext?.Invoke();
    }
}

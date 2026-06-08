using DG.Tweening;
using UnityEngine;
using XFramework;

public class SetUserNameUI : UIBase
{
    private CanvasGroup _canvasGroup;
    private Tweener canvasTweener;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _canvasGroup = Get<CanvasGroup>("UIMask/Panel");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        if (_canvasGroup == null)
        {
            Debug.Log("组件为空");
            return;
        }

        _canvasGroup.alpha = 0;
        canvasTweener = _canvasGroup.DOFade(1, 0.3f).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        
        canvasTweener?.Kill();
        canvasTweener = _canvasGroup.DOFade(0, 0.3f).SetEase(Ease.InQuad);
        canvasTweener.OnComplete(() =>
        {
            base.Close();
        });
    }
}

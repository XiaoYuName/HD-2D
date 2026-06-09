using DG.Tweening;
using TMPro;
using UnityEngine;
using XFramework;

public class SetUserNameUI : UIBase
{
    private CanvasGroup _canvasGroup;
    private Tweener canvasTweener;

    private TMP_InputField _inputField;
    private CommonButton _confirmButton;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _canvasGroup = Get<CanvasGroup>("UIMask/Panel");
        _inputField = Get<TMP_InputField>("UIMask/Panel/InputField (TMP)");
        _confirmButton = Get<CommonButton>("UIMask/Panel/ConfirmButton");
        
        BindAGVClick(_confirmButton,OnConfirmButtonClick,"");
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
    
    public void OnConfirmButtonClick()
    {
        if (string.IsNullOrEmpty(_inputField.text))
        {
            //TODO: 展示提示框
            return;
        }
        GameDataManager.Instance.SetUserName(_inputField.text);
        Close();
    }
}

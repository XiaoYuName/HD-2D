using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 商店帮忙小游戏的单个货架格子：空格只显示底框，摆入货物时显示货物图标并播放一次缩放弹跳动效。
/// 内容与命中判定由 <see cref="ShopHelpPanel"/> 在拖拽过程中驱动，本类不含游戏逻辑。
/// </summary>
public class ShopHelpItemCellUI : MonoBehaviour
{
    [LabelText("底框")][SerializeField] Image bg;
    [LabelText("货物图标")][SerializeField] Image icon;

    // 摆放弹跳时长（比之前 0.5s 更利落）
    public const float AnimDur = 0.28f;

    RectTransform rt;
    int index = -1;
    Sequence anim;

    public int Index => index;
    public RectTransform Rt => rt != null ? rt : rt = (RectTransform)transform;

    public void Init(int index)
    {
        this.index = index;
        rt = (RectTransform)transform;
        SetEmpty();
    }

    /// <summary>清空为无货物状态。</summary>
    public void SetEmpty()
    {
        anim.Stop();
        if(icon != null)
        {
            icon.enabled = false;
            icon.rectTransform.localScale = Vector3.one;
        }
    }

    /// <summary>摆入 / 替换为某货物图标（AA Key），播放一次缩放弹跳动效（0.5s）。</summary>
    public void Show(string iconPath)
    {
        if(icon == null)
            return;

        icon.enabled = true;
        if(!string.IsNullOrEmpty(iconPath))
            icon.SetIcon(iconPath);

        anim.Stop();
        icon.rectTransform.localScale = Vector3.zero;
        anim = Sequence.Create(Tween.Scale(icon.rectTransform, Vector3.one, AnimDur, Ease.OutBack));
    }

    /// <summary>屏幕点是否落在本格内（拖拽命中判定用）。</summary>
    public bool ContainsScreenPoint(Vector2 screenPoint, Camera cam)
        => RectTransformUtility.RectangleContainsScreenPoint(Rt, screenPoint, cam);
}

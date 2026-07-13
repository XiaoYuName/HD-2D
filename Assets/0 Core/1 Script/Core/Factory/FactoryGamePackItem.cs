using System;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 传送带上的一件加工产品视图：按音符类型显示左品(红)/右品(青)/残次品，
/// 打包成功换盒装外观、失败换失败包装并闪红，机器卡住时换锁定外观，残次品被丢弃时弹飞渐隐。
/// 实例由 <see cref="FactoryProcessGamePanel"/> 对象池管理。
/// </summary>
public class FactoryGamePackItem : MonoBehaviour
{
    [SerializeField] RectTransform rt;
    [SerializeField] Image icon;
    [LabelText("正常外观 [0]=左 [1]=右 [2]=残次品")][SerializeField] Sprite[] normalSprites;
    [LabelText("打包成功 [0]=左 [1]=右")][SerializeField] Sprite[] packedSprites;
    [LabelText("打包失败 [0]=左 [1]=右")][SerializeField] Sprite[] failedSprites;
    [LabelText("锁定(机器卡住) [0]=左 [1]=右 [2]=残次品")][SerializeField] Sprite[] lockedSprites;

    static readonly Color FailFlashColor = new (1f, 0.4f, 0.4f);

    Sequence seq;

    public RectTransform Rt => rt;

    // 精灵下标：左=0 右=1 残次品(Up)=2
    static int Idx(FactoryNoteType type) => type == FactoryNoteType.Left ? 0 : type == FactoryNoteType.Right ? 1 : 2;

    /// <summary>出场（含池内复用）：按类型设外观并重置动画残留状态。</summary>
    public void Set(FactoryNoteType type)
    {
        seq.Stop();
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        icon.color = Color.white;
        icon.sprite = normalSprites[Idx(type)];
    }

    /// <summary>打包结果：成功换盒装并弹一下（闪白高光），失败换失败包装并闪红；产品随传送带继续滚出。</summary>
    public void SetPacked(FactoryNoteType type, bool success)
    {
        icon.sprite = (success ? packedSprites : failedSprites)[Idx(type)];
        if(success)
            PunchScale();
        else
            FlashFail();
    }

    /// <summary>机器卡住期间产品的锁定外观切换（正常品、残次品均适用）。</summary>
    public void SetLocked(FactoryNoteType type, bool locked) =>
        icon.sprite = (locked ? lockedSprites : normalSprites)[Idx(type)];

    /// <summary>按错键的闪红反馈。</summary>
    public void FlashFail()
    {
        seq.Stop();
        seq = Sequence.Create(Tween.Color(icon, FailFlashColor, 0.09f, Ease.Default, 2, CycleMode.Yoyo));
    }

    /// <summary>残次品被正确丢弃：向上弹飞 + 旋转 + 渐隐，动画完回调 <paramref name="onDone"/>（供回收进池）。</summary>
    public void FlickAway(Action onDone)
    {
        seq.Stop();
        seq = Sequence.Create()
            .Group(Tween.UIAnchoredPosition(rt, rt.anchoredPosition + new Vector2(140f, 340f), 0.45f, Ease.OutCubic))
            .Group(Tween.LocalRotation(rt, Quaternion.Euler(0f, 0f, -80f), 0.45f, Ease.OutCubic))
            .Group(Tween.Alpha(icon, 0f, 0.45f, Ease.InQuad))
            .ChainCallback(onDone);
    }

    void PunchScale()
    {
        seq.Stop();
        seq = Sequence.Create(Tween.PunchScale(rt, Vector3.one * 0.3f, 0.25f));
    }
}

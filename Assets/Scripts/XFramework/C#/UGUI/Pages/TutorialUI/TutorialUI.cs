using Coffee.UIExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 新手引导遮罩界面：只负责把 <see cref="TutorialStepContext"/> 画出来 ——
/// 挖洞(Unmask)、气泡提示、手指/箭头。判断"该教哪一步、什么时候算过"都在
/// <see cref="TutorialManager"/> 里，这里不碰配置表也不认识业务界面。
///
/// 预制体层级（Tip / Hand 必须在 ShotMask <b>外面</b>：它们在 Mask 里面的话，
/// Unmask 挖的洞会把气泡和手指一起挖穿）：
/// <code>
/// TutorialUI
/// └ UIMask                    Image(RaycastTarget=false)
///   ├ ShotMask                Image + Mask + UnmaskRaycastFilter  ← 洞外点击挡在这层
///   │ ├ Unmask                Image + Unmask                      ← 洞本身
///   │ └ Screen                Image(半透黑)                       ← 被挖洞的暗底
///   ├ Hand                    Finger / Arrow 两个图标，按配置选一个
///   └ Tip                     Image + TipText(TMP)
/// </code>
/// </summary>
public class TutorialUI : UIBase, IPointerClickHandler
{
    /// <summary>气泡离洞的间距。</summary>
    private const float TipGap = 40f;

    /// <summary>气泡贴边的最小留白，防止气泡被挤出屏幕。</summary>
    private const float ScreenPadding = 20f;

    private RectTransform selfRect;
    private RectTransform unmaskRect;
    private Unmask unmask;
    private Image unmaskImage;
    private UnmaskRaycastFilter raycastFilter;

    private RectTransform handRoot;
    private RectTransform fingerIcon;
    private RectTransform arrowIcon;

    private RectTransform tipRoot;
    private TextMeshProUGUI tipText;

    /// <summary>预制体上原本那张九宫格图：配置没给 MaskSpriteName 时用它，也就是默认的方形洞。</summary>
    private Sprite defaultMaskSprite;

    /// <summary>当前正在展示的步骤，null 表示没在引导。</summary>
    private TutorialStepContext context;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        selfRect = transform as RectTransform;

        unmask = Get<Unmask>("UIMask/ShotMask/Unmask");
        unmaskRect = Get<RectTransform>("UIMask/ShotMask/Unmask");
        unmaskImage = Get<Image>("UIMask/ShotMask/Unmask");
        raycastFilter = Get<UnmaskRaycastFilter>("UIMask/ShotMask");

        handRoot = Get<RectTransform>("UIMask/Hand");
        fingerIcon = Get<RectTransform>("UIMask/Hand/Finger");
        arrowIcon = Get<RectTransform>("UIMask/Hand/Arrow");

        tipRoot = Get<RectTransform>("UIMask/Tip");
        tipText = Get<TextMeshProUGUI>("UIMask/Tip/TipText");

        defaultMaskSprite = unmaskImage.sprite;

        HideAll();
    }

    /// <summary>
    /// 展示一步引导。<see cref="TutorialManager"/> 调，参数里已经带好了解析完的目标节点和洞形状图。
    /// </summary>
    public void ShowStep(TutorialStepContext stepContext)
    {
        context = stepContext;

        if (context?.Data == null)
        {
            HideAll();
            return;
        }

        TutorialStepConfig data = context.Data;
        bool hasHole = data.HasHole;

        unmask.gameObject.SetActive(hasHole);
        if (hasHole)
        {
            // 自己算洞的位置,不用 Unmask 的 fitTarget:它是严格贴合目标 rect 的,
            // 塞不进 MaskPadding / MaskOffset / 自定义尺寸这些配置。
            // 注意:fitTarget 的 setter 会无条件 FitTo(value),赋 null 会在包里抛空引用,
            // 所以只在它确实有值时才清掉
            if (unmask.fitTarget != null)
            {
                unmask.fitTarget = null;
            }

            unmask.fitOnLateUpdate = false;

            unmaskImage.sprite = context.MaskSprite != null ? context.MaskSprite : defaultMaskSprite;
            // 异形图(人物剪影这类)按九宫格拉会把边角拉花,只有默认那张方形框才走 Sliced
            unmaskImage.type = context.MaskSprite != null ? Image.Type.Simple : Image.Type.Sliced;

            FitHole();
        }

        // 洞外点击挡住、洞内放行,靠这个 filter。整屏都不让点(纯展示步骤)时把它关掉,
        // 关掉后 IsRaycastLocationValid 恒为 true,遮罩就吃下所有点击
        raycastFilter.enabled = hasHole && data.ClickThrough;

        SetupTip(data);
        SetupHand(data);
        LayoutTipAndHand();
    }

    /// <summary>收摊：把洞、气泡、手指都收掉。引导完成或中止时调。</summary>
    public void Clear()
    {
        context = null;
        HideAll();
    }

    public override void Close()
    {
        Clear();
        base.Close();
    }

    /// <summary>
    /// 遮罩被点了。<see cref="TutorialFinishType.AnyClick"/> 的步骤靠它过场 ——
    /// 点在洞里的话射线根本打不到遮罩,不会走到这里,所以不用额外判断点的是不是洞。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (context == null || !TutorialManager.IsInitialized)
        {
            return;
        }

        TutorialManager.Instance.NotifyMaskClick();
    }

    /// <summary>
    /// 目标可能一直在动（列表滚动、界面进场动画），所以跟着节点走的洞每帧都重贴一次。
    /// 屏幕固定位置的洞<b>不在这里刷</b> —— 每帧写回配置值的话，PlayMode 里就拖不动它了，
    /// 而「拖到位再把坐标抄回配置」正是那种洞的配法。
    /// </summary>
    private void LateUpdate()
    {
        if (context?.Data == null)
        {
            return;
        }

        if (context.Target != null)
        {
            FitHole();
        }

        LayoutTipAndHand();
    }

    #region 挖洞

    /// <summary>
    /// 把洞贴到目标上。做的事情和 <see cref="Unmask.FitTo"/> 一样，
    /// 区别是以目标 rect 的中心为基准，这样 MaskPadding 能往四边对称地扩，
    /// 而且支持 Custom 尺寸（人物立绘那种洞，大小跟按钮 rect 没关系）。
    /// </summary>
    private void FitHole()
    {
        RectTransform target = context.Target;
        TutorialStepConfig data = context.Data;

        // 没有目标节点的洞（屏幕固定位置）：位置和大小直接照配置摆，摆完就不管了
        if (target == null)
        {
            unmaskRect.pivot = new Vector2(0.5f, 0.5f);
            unmaskRect.anchorMin = unmaskRect.anchorMax = new Vector2(0.5f, 0.5f);
            unmaskRect.localRotation = Quaternion.identity;
            unmaskRect.localScale = Vector3.one;
            unmaskRect.sizeDelta = data.MaskSize;
            unmaskRect.anchoredPosition = data.HolePosition;
            return;
        }

        unmaskRect.pivot = new Vector2(0.5f, 0.5f);
        unmaskRect.anchorMin = unmaskRect.anchorMax = new Vector2(0.5f, 0.5f);
        unmaskRect.rotation = target.rotation;

        // 目标和洞可能挂在缩放不同的父级下（比如目标在一个放大过的面板里），换算成本地缩放
        Vector3 targetScale = target.lossyScale;
        Vector3 parentScale = unmaskRect.parent.lossyScale;
        unmaskRect.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : targetScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : targetScale.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? 1f : targetScale.z / parentScale.z);

        Vector2 size;
        Vector2 centerOffset;

        if (data.MaskFitType == TutorialMaskFitType.Custom)
        {
            size = data.MaskSize;
            centerOffset = Vector2.zero;
        }
        else
        {
            // 宽高各加两边的扩边量，中心再往留白多的那侧挪一半
            Rect targetRect = target.rect;
            TutorialMaskPadding padding = data.MaskPadding;
            size = new Vector2(
                targetRect.width + padding.Left + padding.Right,
                targetRect.height + padding.Top + padding.Bottom);
            centerOffset = new Vector2(
                (padding.Right - padding.Left) * 0.5f,
                (padding.Top - padding.Bottom) * 0.5f);
        }

        unmaskRect.sizeDelta = size;

        // 以目标 rect 的中心为锚：目标 pivot 不在中心时(比如 pivot y=0)也不会把洞挖偏
        unmaskRect.position = target.TransformPoint(target.rect.center);
        unmaskRect.anchoredPosition += centerOffset + data.MaskOffset;
    }

    /// <summary>
    /// 这个屏幕坐标在不在洞里。<see cref="TutorialFinishType.ClickHole"/> 的判定用，
    /// <see cref="TutorialManager"/> 收到全局点击后问它。
    /// </summary>
    public bool IsPointInsideHole(Vector2 screenPoint)
    {
        if (context?.Data == null || !context.Data.HasHole || !unmask.gameObject.activeSelf)
        {
            return false;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(unmaskRect, screenPoint, eventCamera);
    }

    #endregion

    #region 气泡 / 手指

    /// <summary>
    /// 提示文案走 <see cref="UnityEngine.Localization.LocalizedString"/>：表和 Key 都在 Inspector 里选，
    /// 不用手打也拼不错。留空就不显示气泡。
    /// </summary>
    private void SetupTip(TutorialStepConfig data)
    {
        bool hasTip = data.TipText != null && !data.TipText.IsEmpty;
        tipRoot.gameObject.SetActive(hasTip);

        if (hasTip)
        {
            tipText.text = data.TipText.GetLocalizedString();
        }
    }

    /// <summary>
    /// 摆指引图标。位置是<b>绝对坐标</b>（屏幕中心为原点），不是相对洞的偏移，
    /// 而且<b>只在这里摆一次、不每帧刷</b> —— 这样 PlayMode 里可以直接拖着图标调位置、
    /// 把 Inspector 上的 Pos X/Y 抄回配置表，代码不会每帧把它顶回去。
    /// </summary>
    private void SetupHand(TutorialStepConfig data)
    {
        bool hasHand = data.HandType != TutorialHandType.None;
        handRoot.gameObject.SetActive(hasHand);

        if (!hasHand)
        {
            return;
        }

        // 容器只当个壳,永远待在屏幕中心不转:这样图标自己的 anchoredPosition 就是屏幕坐标,
        // 编辑器里读到多少、表里就填多少
        handRoot.anchoredPosition = Vector2.zero;
        handRoot.localRotation = Quaternion.identity;

        fingerIcon.gameObject.SetActive(data.HandType == TutorialHandType.Finger);
        arrowIcon.gameObject.SetActive(data.HandType == TutorialHandType.Arrow);

        RectTransform icon = data.HandType == TutorialHandType.Finger ? fingerIcon : arrowIcon;
        icon.anchoredPosition = data.HandPosition;
        icon.localRotation = Quaternion.Euler(0f, 0f, data.HandRotation);
    }

    /// <summary>
    /// 气泡和手指都摆到洞的边上。手指压在洞的中心（图标自己在预制体里偏出去多少由美术定），
    /// 气泡按配置挑一侧，Auto 的话洞在屏幕上半就往下摆、下半就往上摆。
    /// </summary>
    private void LayoutTipAndHand()
    {
        if (context?.Data == null)
        {
            return;
        }

        TutorialStepConfig data = context.Data;

        // 没有洞的步骤(纯提示)：气泡摆屏幕中间，手指没有意义
        if (context.Target == null)
        {
            if (tipRoot.gameObject.activeSelf)
            {
                tipRoot.anchoredPosition = Vector2.zero;
            }

            return;
        }

        Vector2 holeCenter = unmaskRect.anchoredPosition;
        Vector2 holeSize = unmaskRect.rect.size * new Vector2(unmaskRect.localScale.x, unmaskRect.localScale.y);

        // 指引图标不在这里摆：它是绝对坐标、由 SetupHand 一次性摆好的,每帧刷会让编辑器里拖不动

        if (!tipRoot.gameObject.activeSelf)
        {
            return;
        }

        Vector2 tipSize = tipRoot.rect.size;
        TutorialTipPos side = data.TipPosType;

        if (side == TutorialTipPos.Auto)
        {
            // 洞在屏幕上半 → 气泡放下面，反之放上面：往屏幕中间放，最不容易出界
            side = holeCenter.y > 0f ? TutorialTipPos.Down : TutorialTipPos.Up;
        }

        Vector2 tipPosition = side switch
        {
            TutorialTipPos.Up => holeCenter + new Vector2(0f, (holeSize.y + tipSize.y) * 0.5f + TipGap),
            TutorialTipPos.Down => holeCenter - new Vector2(0f, (holeSize.y + tipSize.y) * 0.5f + TipGap),
            TutorialTipPos.Left => holeCenter - new Vector2((holeSize.x + tipSize.x) * 0.5f + TipGap, 0f),
            TutorialTipPos.Right => holeCenter + new Vector2((holeSize.x + tipSize.x) * 0.5f + TipGap, 0f),
            _ => holeCenter,
        };

        tipRoot.anchoredPosition = ClampInsideScreen(tipPosition, tipSize);
    }

    /// <summary>把气泡按回屏幕里：目标贴边时算出来的位置会有一半在屏幕外。</summary>
    private Vector2 ClampInsideScreen(Vector2 position, Vector2 size)
    {
        Vector2 limit = (selfRect.rect.size - size) * 0.5f - new Vector2(ScreenPadding, ScreenPadding);
        limit.x = Mathf.Max(limit.x, 0f);
        limit.y = Mathf.Max(limit.y, 0f);

        return new Vector2(
            Mathf.Clamp(position.x, -limit.x, limit.x),
            Mathf.Clamp(position.y, -limit.y, limit.y));
    }

    #endregion

    private void HideAll()
    {
        if (unmask != null)
        {
            unmask.gameObject.SetActive(false);
        }

        if (raycastFilter != null)
        {
            // 收摊时别让 filter 继续生效：界面还没关完的这一帧里，它会挡住所有点击
            raycastFilter.enabled = false;
        }

        if (handRoot != null)
        {
            handRoot.gameObject.SetActive(false);
        }

        if (tipRoot != null)
        {
            tipRoot.gameObject.SetActive(false);
        }
    }
}

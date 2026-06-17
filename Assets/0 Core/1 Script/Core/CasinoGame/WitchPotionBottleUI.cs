using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 女巫毒药小游戏的单个药瓶格子：未开启时外观完全一致，开启后显示安全/毒药。
/// </summary>
public class WitchPotionBottleUI : MonoBehaviour
{
    [LabelText("按钮")][SerializeField] Button button;
    [LabelText("背景图")][SerializeField] Image bg;
    [LabelText("内容图标（安全/毒药）")][SerializeField] Image icon;

    [Title("外观")]
    [LabelText("未开启颜色")][SerializeField] Color closedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [LabelText("安全颜色")][SerializeField] Color safeColor = new Color(0.6f, 0.45f, 0.65f, 1f);
    [LabelText("毒药颜色")][SerializeField] Color poisonColor = new Color(0.93f, 0.2f, 0.45f, 1f);
    [LabelText("安全图标")][SerializeField] Sprite safeSprite;
    [LabelText("毒药图标")][SerializeField] Sprite poisonSprite;

    int index;

    /// <summary>点击事件，参数为格子索引。</summary>
    public event Action<int> OnClick;

    void Awake()
    {
        button.onClick.AddListener(() => OnClick?.Invoke(index));
    }

    /// <summary>初始化为未开启状态。</summary>
    public void Init(int index)
    {
        this.index = index;
        SetClosed();
    }

    public void SetClosed()
    {
        button.interactable = true;
        bg.color = closedColor;
        icon.enabled = false;
    }

    /// <summary>翻开为安全瓶或毒药瓶。</summary>
    public void Reveal(bool isPoison)
    {
        button.interactable = false;
        bg.color = isPoison ? poisonColor : safeColor;
        icon.sprite = isPoison ? poisonSprite : safeSprite;
        icon.enabled = icon.sprite != null;
    }

    /// <summary>开瓶后禁用其余未开格子的点击（结束时调用）。</summary>
    public void SetInteractable(bool value)
    {
        button.interactable = value;
    }
}

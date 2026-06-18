using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 传送带上单件产品的轻量视图：本体卡片 + 品质标记（合格 ★ / 次品 ✕）。
/// 由 <see cref="FactoryProcessPanel"/> 从模板实例化并逐帧驱动位置，引用在生成界面时直接赋值。
/// </summary>
public class FactoryItemView : MonoBehaviour
{
    public RectTransform rtf;
    public Image bodyImage;
    public TMP_Text markerText;

    static readonly Color QualifiedColor = new (0.95f, 0.6f, 0.2f);
    static readonly Color DefectiveColor = new (0.85f, 0.2f, 0.2f);

    /// <summary>按品质设置标记，并复位本体颜色（实例复用时清除已判定的灰化）。</summary>
    public void SetData(bool qualified)
    {
        markerText.text = qualified ? "★" : "✕";
        markerText.color = qualified ? QualifiedColor : DefectiveColor;
        bodyImage.color = Color.white;
    }

    /// <summary>已下压 / 已判定：本体灰化淡出。</summary>
    public void SetResolved() => bodyImage.color = new Color(0.72f, 0.72f, 0.72f, 0.6f);
}

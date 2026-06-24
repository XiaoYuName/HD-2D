using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 传送带上单件产品的轻量视图：原始产品本体 + 品质标记；压制后本体替换为打包盒预制（正品 / 次品）。
/// 由 <see cref="FactoryProcessPanel"/> 从模板实例化、取池复用并逐帧驱动位置，引用在生成界面时直接赋值。
/// </summary>
public class FactoryItemView : MonoBehaviour
{
    public RectTransform rtf;
    public Image bodyImage;
    public TMP_Text markerText;

    static readonly Color QualifiedColor = new (0.95f, 0.6f, 0.2f);
    static readonly Color DefectiveColor = new (0.85f, 0.2f, 0.2f);

    GameObject spawnedBox;   // 压制后实例化的打包盒，取池复用时销毁
    bool resolved;

    /// <summary>按品质设置标记并复位外观（取池复用时调用：销毁上件残留的打包盒、恢复本体）。</summary>
    public void SetData(bool qualified)
    {
        resolved = false;
        if(spawnedBox != null)
        {
            Destroy(spawnedBox);
            spawnedBox = null;
        }
        bodyImage.enabled = true;
        bodyImage.color = Color.white;
        markerText.gameObject.SetActive(true);
        markerText.text = qualified ? "★" : "✕";
        markerText.color = qualified ? QualifiedColor : DefectiveColor;
    }

    /// <summary>
    /// 已下压判定：产品压制后变为打包盒（合格→正品盒 / 不合格→次品盒）。逐帧调用安全（幂等，只生成一次）。
    /// </summary>
    public void SetResolved(GameObject boxPrefab)
    {
        if(resolved)
            return;
        resolved = true;

        bodyImage.enabled = false;
        markerText.gameObject.SetActive(false);

        spawnedBox = Instantiate(boxPrefab, transform);
        RectTransform brt = (RectTransform)spawnedBox.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        brt.localScale = Vector3.one;
    }
}

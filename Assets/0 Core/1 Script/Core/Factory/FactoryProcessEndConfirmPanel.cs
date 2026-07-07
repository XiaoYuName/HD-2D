using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using TMPro;
using UnityEditor;
#endif

/// <summary>
/// 「提前结束本局」二次确认弹窗：作为 <see cref="FactoryProcessPanel"/> 预制下的<b>隐藏子物体</b>存在（非独立 UI 页面），
/// 由父面板在点「结束本局」时 <see cref="Show"/> 显示。提示提前结束将什么也不会获得，「确认结束」回调关闭小游戏返回主界面，
/// 「取消」仅隐藏本物体继续本局。默认应在预制里设为未激活（隐藏）。
/// </summary>
public class FactoryProcessEndConfirmPanel : MonoBehaviour
{
    [Title("Button")]
    [LabelText("确认结束")][SerializeField] Button confirmButton;
    [LabelText("取消")][SerializeField] Button cancelButton;

    Action onConfirm;
    Action onCancel;
    bool bound;

    void Awake() => Bind();

    void Bind()
    {
        if(bound)
            return;
        bound = true;
        confirmButton.onClick.AddListener(OnConfirmButton);
        cancelButton.onClick.AddListener(OnCancelButton);
    }

    /// <summary>显示确认弹窗：<paramref name="onConfirm"/> 确认结束，<paramref name="onCancel"/> 取消（均在弹窗隐藏前回调一次）。</summary>
    public void Show(Action onConfirm, Action onCancel)
    {
        Bind();   // 子物体默认隐藏，Awake 可能尚未执行，这里兜底绑定一次
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;
        gameObject.SetActive(true);
    }

    void Hide() => gameObject.SetActive(false);

    void OnConfirmButton()
    {
        Action cb = onConfirm;
        onConfirm = onCancel = null;
        Hide();
        cb?.Invoke();
    }

    void OnCancelButton()
    {
        Action cb = onCancel;
        onConfirm = onCancel = null;
        Hide();
        cb?.Invoke();
    }

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("在本物体根节点下生成：半透明遮罩 + 居中圆角窗口（标题 / 正文 / 确认结束 / 取消），并自动赋值各按钮引用。\n" +
             "本物体应作为 FactoryProcessPanel 预制下的隐藏子物体，生成后把它拖给父面板的「提前结束确认弹窗」字段，并把本物体设为未激活。\n" +
             "重复点击会先清掉上次生成的窗口与遮罩。", InfoMessageType.Info)]
    void BuildUI()
    {
        Color titleColor = new (0.26f, 0.26f, 0.26f);
        Color contentColor = new (0.36f, 0.36f, 0.36f);
        Color panelColor = new (0.96f, 0.96f, 0.96f, 1f);
        Color confirmBg = new (0.93f, 0.74f, 0.5f);
        Color cancelBg = new (0.82f, 0.82f, 0.85f);

        // 根：充满父级 + 透明 Image 拦截点击（挡住背后的小游戏输入）
        RectTransform rootRt = (RectTransform)transform;
        FactoryUIGen.Stretch(rootRt);
        if(GetComponent<Image>() == null)
            gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        // 清除上次生成
        foreach(string n in new[] { "Mask", "Window" })
        {
            Transform old = transform.Find(n);
            if(old != null)
                DestroyImmediate(old.gameObject);
        }

        // 遮罩
        Image mask = FactoryUIGen.Img("Mask", transform, new Color(0f, 0f, 0f, 0.45f));
        FactoryUIGen.Stretch(mask.rectTransform);

        // 窗口
        Image window = FactoryUIGen.Img("Window", transform, panelColor);
        FactoryUIGen.Center(window.rectTransform, 720f, 380f, 0f, 0f);
        Transform win = window.transform;

        // 标题
        FactoryUIGen.Center(FactoryUIGen.Loc("TitleText", win, FactoryLocKeySet.Process.EndConfirmTitle, 40, titleColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 600f, 60f, 0f, 110f);

        // 正文
        TMP_Text content = FactoryUIGen.Loc("ContentText", win, FactoryLocKeySet.Process.EndConfirmContent, 30, contentColor, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
        content.textWrappingMode = TextWrappingModes.Normal;
        FactoryUIGen.Center(content.rectTransform, 620f, 120f, 0f, 10f);

        // 确认结束（左） / 取消（右）
        confirmButton = FactoryUIGen.Btn("ConfirmButton", win, FactoryLocKeySet.Process.EndConfirmOk, confirmBg, Color.white);
        FactoryUIGen.Center((RectTransform)confirmButton.transform, 220f, 72f, -140f, -110f);

        cancelButton = FactoryUIGen.Btn("CancelButton", win, FactoryLocKeySet.Process.EndConfirmCancel, cancelBg, new Color(0.3f, 0.3f, 0.35f));
        FactoryUIGen.Center((RectTransform)cancelButton.transform, 220f, 72f, 140f, -110f);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryProcessEndConfirmPanel] 确认界面已生成。请把本物体拖给 FactoryProcessPanel 的「提前结束确认弹窗」字段，并将本物体设为未激活。", this);
    }
    #endregion
#endif
}

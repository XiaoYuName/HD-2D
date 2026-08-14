using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using UnityEngine;
using XFramework;

/// <summary>
/// 剧情选项面板。<see cref="Drama.Runtime.Services.IChoiceView"/> 的落地实现。
///
/// 平时整个遮罩是收起来的，只有走到选项指令时才开 —— 显示是选项指令的副作用，
/// 和对话框那套是一个路子。
///
/// <b>遮罩不只是为了压暗背景</b>：DramaRuntimeUI 上有个盖满全屏的 OnClikc 按钮负责翻页，
/// 选项期间必须挡住它，否则玩家点选项的同时把台词也翻过去了。
/// 靠 UIMask 的 <c>Blocks Raycasts</c> 拦下来，它在层级里排在 OnClikc 后面（渲染在上层）。
/// </summary>
public partial class OptionController : UIBase
{
    /// <summary>本轮生成的按钮，选完 / 被打断时要还回对象池。</summary>
    private readonly List<GameObject> spawned = new List<GameObject>();

    /// <summary><see cref="PickAsync"/> 在等的信号，按钮点击时填下标。</summary>
    private UniTaskCompletionSource<int> picking;

    public override void Init()
    {
        InitAutoBind();

        // 预制体里遮罩多半是开着的（不然编辑器里没法摆），进来先收干净
        SetMaskVisible(false);
    }

    /// <summary>
    /// 弹出选项并等玩家选，返回选中的下标。取消（退出剧情）时抛 <c>OperationCanceledException</c>。
    /// </summary>
    public async UniTask<int> PickAsync(LocalizedRef[] options, CancellationToken ct)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[Drama] 选项指令没有任何选项，直接返回 -1");
            return -1;
        }

        // 上一轮要是被异常打断留下了残留，先清干净再摆新的
        ClearImmediate();

        UniTaskCompletionSource<int> tcs = new UniTaskCompletionSource<int>();
        picking = tcs;

        for (int i = 0; i < options.Length; i++)
        {
            SpawnButton(i, options[i], tcs);
        }

        SetMaskVisible(true);

        try
        {
            return await tcs.Task.AttachExternalCancellation(ct);
        }
        finally
        {
            // ★ 必须清掉。留着的话下一次选项会被这次残留的 tcs 立刻满足，选项面板一闪而过
            if (picking == tcs)
            {
                picking = null;
            }

            ClearImmediate();
        }
    }

    /// <summary>立刻收掉面板并回收按钮。剧情结束 / 被打断时兜底。</summary>
    public void ClearImmediate()
    {
        // 还有人在等就别把它挂死，让 await 那边以取消收场
        picking?.TrySetCanceled();
        picking = null;

        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null)
            {
                continue;
            }

            // 池子里的对象会被复用，监听器不摘干净下次会连着上一轮的下标一起触发
            CustomButton button = spawned[i].GetComponent<CustomButton>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }

            AssetsManager.Instance.FreeGameObject(spawned[i]);
        }

        spawned.Clear();
        SetMaskVisible(false);
    }

    private void SpawnButton(int index, LocalizedRef label, UniTaskCompletionSource<int> tcs)
    {
        GameObject obj = AssetsManager.Instance.Instantiate(AssetKeys.OptionCustomButtonPath);
        if (obj == null)
        {
            Debug.LogError($"[Drama] 选项按钮预制体加载不到：{AssetKeys.OptionCustomButtonPath}");
            return;
        }

        obj.transform.SetParent(content, worldPositionStays: false);
        obj.SetActive(true);
        spawned.Add(obj);

        CustomButton button = obj.GetComponent<CustomButton>();
        if (button == null)
        {
            Debug.LogError("[Drama] 选项按钮预制体上没有 CustomButton");
            return;
        }

        // 池子取出来的对象带着上一轮的监听器，先摘干净
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => tcs.TrySetResult(index));

        // 走 LocalizeStringEvent.SetText(表, 键) 而不是查好表再 SetLabel(string)：
        // 面板会一直挂着等玩家，这期间玩家去设置里切语言，选项要跟着变。
        // 和台词那边（TalkContentController）是同一套做法
        button.SetLabel(label.Table, label.Key);
    }

    private void SetMaskVisible(bool visible)
    {
        if (uIMask == null)
        {
            return;
        }

        uIMask.gameObject.SetActive(visible);

        // interactable 必须打开：按钮是 UIMask 的子节点，CanvasGroup 关着它的话
        // 整棵子树都收不到点击 —— 表现是"选项显示出来了但点不动"。
        // 在代码里兜一道，免得预制体上被误勾
        uIMask.interactable = visible;
        uIMask.blocksRaycasts = visible;
    }
}

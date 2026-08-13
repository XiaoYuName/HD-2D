using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 对话记录（Log）。把玩家看过的台词按发生顺序列出来，最新的在最下面。
///
/// <b>取数是异步的</b>：历史里只存了 (剧本ID, 指令下标)，正文要回剧本里现取，
/// 而在主菜单打开时剧本根本不在内存里（见 <see cref="DramaHistoryResolver"/>）。
/// 所以填充分两步走 —— 先把界面开出来，数据回来了再摆条目。
///
/// 剧情里点 LOG 之前会先关掉自动 / 跳过（在 <c>TalkActionController.OnLogClick</c> 里），
/// 和原工程一致。
/// </summary>
public class DramaLogUI : UIBase
{
    private ScrollRect scrollRect;
    private CustomButton closeButton;

    /// <summary>本次摆出来的条目，关界面时要还回对象池。</summary>
    private readonly List<GameObject> spawned = new List<GameObject>();

    /// <summary>
    /// 填充批次号。取数是异步的，期间玩家完全可能把界面关掉又打开一次 ——
    /// 批次号对不上就说明这批数据已经过期，摆上去会和新的一批叠在一起。
    /// </summary>
    private int fillSession;

    public override void Init()
    {
        scrollRect = Get<ScrollRect>("UIMask/RawImage/Scroll View");
        closeButton = Get<CustomButton>("UIMask/RawImage/Close");
        Bind(closeButton, Close, "");
    }

    public override void Close()
    {
        base.Close();

        // 关了就作废还在路上的那次填充，否则它回来时会往一个关着的界面里摆条目
        fillSession++;
        ClearItems();
    }

    /// <summary>
    /// 取历史并填充。<b>即发即忘</b> —— 界面已经开出来了，数据晚几帧到没关系。
    /// </summary>
    public void ShowHistory()
    {
        ShowHistoryAsync().Forget();
    }

    private async UniTaskVoid ShowHistoryAsync()
    {
        int session = ++fillSession;

        // 先清干净：重复打开时不能让上一次的条目留在里面
        ClearItems();

        List<DramaHistoryLine> lines = await DramaManager.Instance.ResolveHistoryAsync();

        // 等数据这段时间里界面被关了 / 又开了一次，这批就不要了
        if (session != fillSession || !isOpen)
        {
            return;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            SpawnItem(lines[i]);
        }

        ScrollToBottom();
    }

    private void SpawnItem(in DramaHistoryLine entry)
    {
        GameObject obj = AssetsManager.Instance.Instantiate(AssetKeys.LogItemUIPath);

        if (obj == null)
        {
            Debug.LogError($"[Drama] 对话记录条目预制体加载不到：{AssetKeys.LogItemUIPath}");
            return;
        }

        obj.transform.SetParent(scrollRect.content, worldPositionStays: false);
        obj.SetActive(true);
        spawned.Add(obj);

        LogItemUI item = obj.GetComponent<LogItemUI>();

        if (item == null)
        {
            Debug.LogError("[Drama] 对话记录条目预制体上没有 LogItemUI");
            return;
        }

        item.Init();
        item.SetData(entry.Line);
    }

    /// <summary>
    /// 滚到底 —— 最新的一条在最下面，打开时玩家最想看的是刚刚那几句。
    ///
    /// <b>必须先 <c>ForceUpdateCanvases</c></b>：条目是这一帧刚生成的，
    /// 布局还没算，这时候设归一化位置会被随后的布局重算冲掉。
    /// </summary>
    private void ScrollToBottom()
    {
        if (scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void ClearItems()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                AssetsManager.Instance.FreeGameObject(spawned[i]);
            }
        }

        spawned.Clear();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 主线剧情总管理器
/// </summary>
public class DramaManager : MonoSingleton<DramaManager>,ISaveable
{
    #region 游戏设置

    public bool isAutoDrama = false;
    

    #endregion
    
    #region ISaveable
    
    public string GUID => "DramaManager";

    public void Start()
    {
        ((ISaveable)this).RegisterSaveable();
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public void SaveData(GameSaveData data)
    {
        data.DramaHistoryList = History.Snapshot();
        data.FinishedDramaIds = new List<long>(_finishedDramas);
        data.DramaProgress = CaptureRestorePoint();

        // 已读是跨存档的、走自己的文件，但落盘时机蹭存档这一下正好：
        // 玩家存了档就说明他觉得这里是个安全点
        ReadMarks.SaveIfDirty();
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="data"></param>
    public void LoadData(GameSaveData data)
    {
        // 对话历史是每个存档槽独立的，整个换成这一档的。
        // 新开档 / 老存档没这个字段时是 null，Restore 会当成清空处理
        History.Restore(data?.DramaHistoryList);

        // 剧情完成记录同理：换档必须整个换掉，不能留上一档的 ——
        // 留着的话新开档的任务会凭空满足
        _finishedDramas.Clear();

        if (data?.FinishedDramaIds != null)
        {
            for (int i = 0; i < data.FinishedDramaIds.Count; i++)
            {
                _finishedDramas.Add(data.FinishedDramaIds[i]);
            }
        }

        // 已读【不】在这里读：它跨存档共享，换档不该跟着变，
        // 而且它自己会在第一次查询时把文件读进来

        // 只接下来，不在这儿开播 —— 读档时场景还没切完，UI 也还没就位。
        // 由场景流程在合适的时机调 ResumeSavedDramaAsync
        _savedProgress = data?.DramaProgress;
    }
    

    #endregion

    #region 对话历史 / 已读

    // 历史（Log 回看）和已读（跳过已读）是两本账，作用域也不同：
    //   历史 —— 每个存档槽独立，只存 (剧本ID, 指令下标)，文字显示时回剧本现取
    //   已读 —— 跨存档共享，单独一个文件，见 DramaReadMarks

    /// <summary>
    /// 台词历史（Log）。<b>每个存档槽独立</b>，跟着 <c>GameSaveData</c> 存读，
    /// 滚动保留最近 <see cref="DramaHistory.DefaultCapacity"/> 条。
    ///
    /// 里面只有 (剧本ID, 指令下标)，要显示得先过 <see cref="ResolveHistoryAsync"/>。
    /// </summary>
    public DramaHistory History { get; } = new DramaHistory();

    /// <summary>
    /// 把历史还原成能显示的台词，供 Log 界面用。<b>剧情里和主菜单都能调</b> ——
    /// 用到的剧本不在内存里会临时加载再还回去。
    ///
    /// 结果是最老的在前，UI 一般要倒着排。
    /// </summary>
    public UniTask<List<DramaHistoryLine>> ResolveHistoryAsync()
    {
        return DramaHistoryResolver.ResolveAsync(History.Entries);
    }

    /// <summary>
    /// 重播一条历史台词的语音。对话记录里的小喇叭用。
    ///
    /// 走的是剧情自己那条人声轨（<c>IDramaAudio.PlayVoice</c>），所以会自动掐掉上一条 ——
    /// 玩家连点两条不会叠在一起念。
    ///
    /// <b>在剧情外（主菜单）也能用</b>，只是那时候语音的 Asset Table 没被预热过，
    /// 头一次点会同步加载一下、可能顿一帧。
    /// </summary>
    public void PlayHistoryVoice(LocalizedRef voice)
    {
        if (voice.IsEmpty)
        {
            return;
        }

        Director.Context.Audio?.PlayVoice(voice);
    }

    /// <summary>
    /// 打开对话记录界面。<b>剧情里的 LOG 按钮和剧情外的入口（主菜单）走的都是这一个口子。</b>
    ///
    /// 界面自己去 <see cref="ResolveHistoryAsync"/> 取数并填充 —— 那一步是异步的
    /// （历史里只有下标，剧本不在内存时要临时加载），不能让调用方等。
    ///
    /// 剧情里点 LOG 还要先关掉自动 / 跳过，那一步在
    /// <c>TalkActionController.OnLogClick</c> 里做：按钮的选中态归它管，这里够不着。
    /// </summary>
    public void ShowDramaLogUI()
    {
        DramaLogUI logUI = UISystem.Instance.OpenUI<DramaLogUI>(UIKeys.DramaLogUI);

        if (logUI == null)
        {
            Debug.LogError($"[Drama] 打不开对话记录界面：UI 系统里没有「{UIKeys.DramaLogUI}」");
            return;
        }

        logUI.ShowHistory();
    }

    /// <summary>
    /// 已读标记。<b>跨存档共享</b>，存在存档目录下自己的文件里，二周目 / 换档都还认。
    /// </summary>
    public DramaReadMarks ReadMarks { get; } = new DramaReadMarks();

    /// <summary>
    /// 完整播完过的剧情。<b>每个存档槽独立</b> —— 这是玩家在这一周目的进度，
    /// 和跨存档的「已读」（<see cref="ReadMarks"/>）不是一回事：
    /// 二周目该重新做的任务，不能因为一周目看过就直接算完成。
    /// </summary>
    private readonly HashSet<long> _finishedDramas = new HashSet<long>();

    /// <summary>
    /// 这一段剧情<b>完整播完过</b>没有。任务 / 条件系统的"做过某段剧情"就问它。
    ///
    /// "完整播完" = 剧本正常走到头（或者走到头之后 Goto 去了下一本）。
    /// <b>中途退出不算</b>，玩家没看到后半段。
    /// </summary>
    public bool HasDrama(long dramaID)
    {
        return dramaID > 0 && _finishedDramas.Contains(dramaID);
    }

    /// <summary>已经播完过的剧情，只读。存档 / 调试用。</summary>
    public IReadOnlyCollection<long> FinishedDramas => _finishedDramas;

    /// <summary>
    /// 一本剧本播完了。<see cref="DramaDirector.DramaFinished"/> 转过来的。
    /// </summary>
    private void OnDramaFinished(long dramaID)
    {
        if (dramaID <= 0)
        {
            return;
        }

        _finishedDramas.Add(dramaID);

        // ★ 重播也要报（不只是第一次）：任务是玩家中途才接的，
        //   接之前那次上报它没听见，只能靠这次重播或者被动重扫补上。
        //   下游只是拿它去重扫一遍任务，报重了不会错，漏报才会
        QuestEventBus.ReportDialogueFinished(dramaID);
    }

    private const string SkipScopePrefKey = "Drama.SkipScope";

    /// <summary>
    /// 「跳过」的作用范围：只跳已读 / 全部跳过。<b>默认只跳已读。</b>
    ///
    /// 存 PlayerPrefs 而不是进存档 —— 这是玩家的<b>全局偏好</b>（和音量一个性质），
    /// 换存档槽不该变。设置界面读它来画开关的当前状态，改用 <see cref="SetSkipScope"/>。
    /// </summary>
    public EDramaSkipScope SkipScope
    {
        get => (EDramaSkipScope)PlayerPrefs.GetInt(SkipScopePrefKey, (int)EDramaSkipScope.OnlyRead);
        private set => PlayerPrefs.SetInt(SkipScopePrefKey, (int)value);
    }

    /// <summary>
    /// 设置界面改「跳过范围」时调这一个方法。
    ///
    /// 和直接写属性的区别是它会 <c>PlayerPrefs.Save()</c> 立刻落盘 ——
    /// 设置项是玩家专门去点的，不该因为之后崩一次就丢掉。
    ///
    /// <b>当场生效</b>：跳过与否是每条台词现读的（<see cref="OnTalkStarting"/>），
    /// 玩家正在跳过的过程中改这个开关，下一句就按新的算。
    /// </summary>
    public void SetSkipScope(EDramaSkipScope scope)
    {
        if (SkipScope == scope)
        {
            return;
        }

        SkipScope = scope;
        PlayerPrefs.Save();
    }

    /// <summary>「跳过」要不要在没读过的台词上停下。内部判断用，设置界面看 <see cref="SkipScope"/>。</summary>
    private bool SkipOnlyReadLines => SkipScope == EDramaSkipScope.OnlyRead;

    /// <summary>
    /// 已读记录的条数。设置界面显示"已读 N 句"、或者判断「重置已读」按钮要不要可点。
    /// </summary>
    public int ReadLineCount => ReadMarks.Count;

    /// <summary>
    /// 清空已读记录并<b>立刻落盘</b>。设置里的「重置已读记录」按钮用。
    ///
    /// 清完之后「只跳已读」就跳不动了（所有台词都算没读过），这是预期行为。
    /// 对话历史不受影响 —— 那是每个存档自己的流水账，和已读是两本账。
    /// </summary>
    public void ClearReadMarks()
    {
        ReadMarks.ClearAll();
        ReadMarks.Save();
    }

    /// <summary>
    /// 一条台词即将播出。<see cref="DramaDirector.TalkStarting"/> 转过来的，
    /// 读档的静默重放期间不会走到这儿。
    /// </summary>
    private void OnTalkStarting(long dramaId, TalkAction talk)
    {
        if (talk == null)
        {
            return;
        }

        // ★ 顺序要紧：先问已读，再记已读。
        //   反过来的话这一句刚被自己标成"读过"，「跳过已读」就永远停不下来了
        bool read = ReadMarks.IsRead(dramaId, talk.Text);

        if (!read && SkipOnlyReadLines && PlaybackMode == EDramaPlaybackMode.Skip)
        {
            StopSkipOnUnreadLine();
        }

        History.Add(DramaHistoryEntry.From(dramaId, talk));
        ReadMarks.Mark(dramaId, talk.Text);
    }

    /// <summary>
    /// 跳过时撞到没读过的台词：退出跳过，让这一句正常播。
    ///
    /// <b>要走 <c>TalkActionController.StopAutoAndSkip</c> 而不是自己
    /// <see cref="SetPlaybackMode"/></b>：AUTO / SKIP 的按钮选中态在那个控制器手里，
    /// 只改模式的话按钮还亮着，和实际状态对不上（选项面板那边也是同一个理由）。
    ///
    /// 本方法在指令<b>执行之前</b>被调，所以模式改完紧接着执行的就是这一条 ——
    /// 语音、打字机都还没开始，玩家看到的就是一句正常播出的新台词。
    /// </summary>
    private void StopSkipOnUnreadLine()
    {
        TalkActionController talk = _runtimeUI != null ? _runtimeUI.TalkActionController : null;

        if (talk != null)
        {
            talk.StopAutoAndSkip();
            return;
        }

        // UI 还没就位（理论上走不到，兜个底）。模式先改对，按钮态等 UI 开出来自己刷
        SetPlaybackMode(EDramaPlaybackMode.Normal);
    }

    #endregion

    #region 获取数据

    public NpcData GetNpcData(long npcID)
    {
        return LubanManager.Instance.TbNpcData[npcID];
    }

    #endregion

    #region DramaRuntime

    private DramaDirector _director;
    private CancellationTokenSource _dramaTokenSource;
    private DramaRuntimeUI _runtimeUI;

    /// <summary>进剧情前被顶掉的那批UI,收尾时原样还回去。</summary>
    private List<string> _suspendedUIPages = new();

    /// <summary>
    /// 播放批次号。收尾是异步的(要等 PlayAsync 真正走完 finally),
    /// 期间可能已经有下一段剧情开播了——批次号对不上就说明自己已经被顶掉,
    /// 这时候再收尾会把人家刚开的 UI 关掉。
    /// </summary>
    private int _dramaSession;

    /// <summary>
    /// 调度器。Handler 注册表和上下文都挂在它下面，
    /// 表现层打开剧情 UI 后要往 <c>Director.Context</c> 里塞 Dialogue / Choice / Actors。
    /// </summary>
    public DramaDirector Director
    {
        get
        {
            if (_director == null)
            {
                _director = new DramaDirector();

                // 已读 / 对话历史 / 剧情完成挂在这两个事件上。订阅只能在这儿做 ——
                // Director 是懒创建的，别处订阅要么还没建、要么建了两次
                _director.TalkStarting += OnTalkStarting;
                _director.DramaFinished += OnDramaFinished;
            }

            return _director;
        }
    }

    /// <summary>
    /// 当前播放模式（正常 / 自动 / 跳过）。
    ///
    /// <b>播放中途可以改</b>，Handler 每次用到都是现读 <c>ctx.Mode</c>，
    /// 所以玩家点 AUTO / SKIP 立刻对后续指令生效，不用等下一段剧本。
    ///
    /// <b>跨剧本保持</b>：上一段是自动 / 跳过结束的，下一段进来还是那个模式，
    /// 不在 <see cref="StartDramaRuntime"/> 里重置。注意这意味着玩家上次开着跳过退出的话，
    /// 下次进剧情会直接往前冲 —— 这是刻意的，跟 AUTO / SKIP 按钮的显示是一致的。
    /// </summary>
    public EDramaPlaybackMode PlaybackMode { get; private set; } = EDramaPlaybackMode.Normal;

    /// <summary>切播放模式。三种模式互斥，关掉自动 / 跳过就是切回 <see cref="EDramaPlaybackMode.Normal"/>。</summary>
    public void SetPlaybackMode(EDramaPlaybackMode mode)
    {
        PlaybackMode = mode;

        // Director 还没建就只记状态，等 StartDramaRuntime 里装配时一起刷进去
        if (_director != null)
        {
            _director.Context.Mode = mode;
        }

        if (mode == EDramaPlaybackMode.Skip)
        {
            CompleteRunningAnimations();
        }
    }

    /// <summary>
    /// 把此刻还在跑的表现层动画一次性推到终点。
    ///
    /// <b>只改模式是不够的</b>：Tween 的时长是发起那一刻按当时的模式算好的，
    /// 之后再改模式叫不醒它。玩家在一条 3 秒的立绘位移中途点跳过，
    /// 看到的会是"点了没反应，还得等它慢慢走完"。
    /// 推到终点而不是取消 —— 跳过的语义是"结果照旧，过程不看"。
    /// </summary>
    private void CompleteRunningAnimations()
    {
        if (_runtimeUI == null)
        {
            return;
        }

        _runtimeUI.ActorController?.CompleteAllTweens();
        _runtimeUI.BackgroundController?.CompleteAllTweens();
        _runtimeUI.ScreenActionController?.CompleteRunning();
        _director?.CGStage.CompleteAllTweens();
    }

    // ==================================================== 存档 / 读档

    /// <summary>读档时接下来的剧情进度，等场景就位后由 <see cref="ResumeSavedDrama"/> 消费。</summary>
    private DramaRestorePoint _savedProgress;

    /// <summary>入口剧本的资产 key。剧本一律由本类加载，收尾时要还引用。</summary>
    private string _ownedEntryScriptKey;

    /// <summary>存档里有没有一段没播完的剧情。</summary>
    public bool HasSavedDrama => _savedProgress is { IsValid: true };

    /// <summary>
    /// 取当前的存档点。没在播、或者还没播到任何台词，就返回 null。
    ///
    /// <b>只在台词处可存</b>：台词等玩家点击是整段剧情唯一的空闲时刻。
    /// 别的指令要么瞬间完成，要么正在跑动画 —— 存在那里读档会落在半路。
    /// </summary>
    public DramaRestorePoint CaptureRestorePoint()
    {
        if (_director == null || _director.CurrentDramaId <= 0 || _director.CurrentTalkIndex < 0)
        {
            return null;
        }

        return DramaRestorePoint.Capture(
            _director.CurrentDramaId, _director.CurrentTalkIndex, _director.ChoicePath);
    }

    /// <summary>
    /// 把存档里那段没播完的剧情接着播下去。场景和 UI 就位之后调。
    ///
    /// 恢复的做法是从剧本开头<b>静默重放</b>到存档点（等待归零、台词不等输入、
    /// 选项走存档里的记录），不是直接跳下标 —— 原因见 <see cref="DramaRestorePoint"/>。
    /// </summary>
    /// <returns>true = 已经开播；false = 存档里没有未播完的剧情。</returns>
    public bool ResumeSavedDrama()
    {
        DramaRestorePoint point = _savedProgress;

        // 只用一次。否则每次进场景都会再恢复一遍
        _savedProgress = null;

        if (point is not { IsValid: true })
        {
            return false;
        }

        // 剧本加载归 StartDramaRuntime 那条统一路径，这里只负责把恢复点交过去
        StartDramaRuntime(point.DramaId, point);
        return true;
    }

    /// <summary>
    /// 播一段剧情。重复调用会先掐掉上一段。
    /// 
    /// 剧本资产的位置由配置表 <c>DramaData</c> 给出（ID → DramaScriptsPath），
    /// 加载在 <see cref="PlayAndTeardownAsync"/> 里做 —— 放那儿而不是这儿，
    /// 是为了让剧情 UI 先开出来（玩家立刻看到进了剧情，而不是干等几帧），
    /// 而且加载失败也能走同一条收尾路径把 UI 还回去。
    /// </summary>
    /// <param name="dramaID">DramaData配置表ID</param>
    /// <param name="restore">读档恢复点，null = 从头正常播。</param>
    public void StartDramaRuntime(long dramaID, DramaRestorePoint restore = null)
    {
        // 先占批次号再停:上一段的收尾有可能在 Cancel() 里同步回调过来,
        // 那时候批次号必须已经变了,否则它会把下面刚开的 UI 收掉
        int session = ++_dramaSession;

        StopDramaRuntime();
        _suspendedUIPages = UISystem.Instance.CloseAllUIAndSnapshot(new List<string>());
        _dramaTokenSource = new CancellationTokenSource();
        _runtimeUI = UISystem.Instance.OpenUI<DramaRuntimeUI>(UIKeys.DramaRuntimeUI);
        Director.Context.Dialogue = _runtimeUI;
        Director.Context.Background = _runtimeUI.BackgroundController;
        Director.Context.Screen = _runtimeUI.ScreenActionController;
        Director.Context.Choice = _runtimeUI;
        Director.Context.Actors = _runtimeUI.ActorController;
        // 立绘走 Director 那个 Provider 实例：Director 开播前已经按它预载过了，
        // 舞台再自己去 AssetsManager 加载会把引用计数记两次
        _runtimeUI.ActorController.Assets = Director.AssetProvider;

        // CG 层的三个场景引用：
        //   模型挂在 DramaManager 下（世界空间，Cubism 进不了 Canvas）
        //   立绘层就是立绘舞台本身 —— 进 CG 时整层藏掉
        //   替身挂在剧情面板根节点下，★ 不能挂在立绘舞台下 ★：
        //     那一层进 CG 时会被 SetActive(false)，替身跟着失活，Canvas 就不再给它算布局了，
        //     模型的位置会僵在藏起来那一刻的值上
        Director.CGStage.ProxyParent = (RectTransform)_runtimeUI.transform;
        Director.CGStage.ModelParent = transform;
        Director.CGStage.ActorLayer = _runtimeUI.ActorController.gameObject;

        // 这里【不】重置播放模式：上一段结束时是自动 / 跳过，下一段就接着自动 / 跳过。
        // 玩家开了自动就是不想再点了，进下一段又要重新点一次是倒退。
        // Director 是复用的，Context.Mode 本来就还留着，重新塞一遍是为了
        // Director 头一次被创建的那种情况（属性 getter 里 new 出来，Mode 是默认值）
        SetPlaybackMode(PlaybackMode);

        PlayAndTeardownAsync(dramaID, restore, session, _dramaTokenSource.Token).Forget();
    }

    /// <summary>
    /// 播完(或被取消 / 报错)之后把剧情 UI 收掉,再把进剧情前那批 UI 还回去。
    ///
    /// 收尾必须挂在 PlayAsync 后面而不是 <see cref="StopDramaRuntime"/> 里:
    /// 剧本自然播完是没人调 Stop 的,那时候对话框会一直挂在屏幕上;
    /// 而且这样收尾一定排在 Director 自己的 finally(还资源、清舞台)之后,
    /// 不会出现"UI 都销毁了 Tween 还在动它"。
    /// </summary>
    private async UniTaskVoid PlayAndTeardownAsync(
        long dramaID, DramaRestorePoint restore, int session, CancellationToken ct)
    {
        try
        {
            DramaScript script = await LoadScriptAsync(dramaID);
            if (script == null)
            {
                return;   // finally 里会把 UI 还回去
            }

            // ★ 把配置表 ID 一起交过去：剧本资产里写的那个 ID 不参与加载，错了也照样能播，
            //   但存档点 / 已读 / 对话历史都要靠它回头查表。见 DramaDirector.ResolveDramaId
            await Director.PlayAsync(script, ct, restore, dramaID);
        }
        finally
        {
            if (session == _dramaSession)
            {
                TeardownDramaRuntime();
            }
        }
    }

    /// <summary>
    /// 按剧情ID 从配置表拿路径并加载剧本。加载成功时把 key 记下来，收尾时还引用。
    /// </summary>
    private async UniTask<DramaScript> LoadScriptAsync(long dramaID)
    {
        string key = DramaDirector.ScriptKeyOf(dramaID);
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError($"[Drama] 开播失败：剧情表里没有 {dramaID}，或者它没填 DramaScriptsPath");
            return null;
        }

        DramaScript script = await AssetsManager.Instance.LoadAssetsUniTask<DramaScript>(key);
        if (script == null)
        {
            Debug.LogError($"[Drama] 开播失败，加载不到剧本 {dramaID}：{key}");

            // 加载失败也要还引用，否则这个 key 的计数会一直挂着
            AssetsManager.Instance.FreeAsset(key);
            return null;
        }

        _ownedEntryScriptKey = key;
        return script;
    }

    /// <summary>关剧情 UI + 恢复原来的界面。重复调用无害。</summary>
    private void TeardownDramaRuntime()
    {
        // 一段剧情读下来攒的已读落个盘。不等玩家存档 —— 中途关游戏的话
        // 这一段白跳了，下次进来又得再跳一遍
        ReadMarks.SaveIfDirty();

        if (_dramaTokenSource != null)
        {
            _dramaTokenSource.Dispose();
            _dramaTokenSource = null;
        }

        // 入口剧本是本类加载的，Director 只释放它自己 Goto 加载的那些
        if (!string.IsNullOrEmpty(_ownedEntryScriptKey))
        {
            AssetsManager.Instance.FreeAsset(_ownedEntryScriptKey);
            _ownedEntryScriptKey = null;
        }

        if (!UISystem.IsInitialized)
        {
            _runtimeUI = null;
            return;
        }

        if (_runtimeUI != null)
        {
            UISystem.Instance.CloseUI(_runtimeUI);
            _runtimeUI = null;
        }

        UISystem.Instance.RestoreUI(_suspendedUIPages);
        _suspendedUIPages = new List<string>();

        OpenEndUIIfRequested();
    }

    /// <summary>
    /// 剧本里「UI结束」节点点名要开的界面。
    ///
    /// <b>时机必须在收尾之后</b>：关剧情面板、把进剧情前那批界面还回去，
    /// 都做完了才轮到它 —— 顺序反过来的话，它要么被 CloseUI 顺手关掉，
    /// 要么被 RestoreUI 恢复上来的界面压到底层。
    /// 剧情被中途打断（玩家退出）时指令根本没执行到，自然也不会开。
    /// </summary>
    private void OpenEndUIIfRequested()
    {
        string page = _director?.GameBridge.ConsumePendingEndUI();
        if (string.IsNullOrEmpty(page))
        {
            return;
        }

        if (UISystem.Instance.OpenUI<UIBase>(page) == null)
        {
            Debug.LogError($"[Drama] 「UI结束」要打开界面「{page}」，但 UI 系统里没有这个界面");
        }
    }

    /// <summary>
    /// 中断当前剧情。这里只负责喊停,收尾在 <see cref="PlayAndTeardownAsync"/> 的 finally 里——
    /// Director 要先把资源还干净(它自己的 finally),才轮得到关 UI。
    /// </summary>
    public void StopDramaRuntime()
    {
        _dramaTokenSource?.Cancel();
    }

    /// <summary>
    /// 切后台时把已读落个盘。移动端上进程可能直接被系统回收，
    /// <see cref="OnDestroy"/> 未必跑得到，这是最后一个稳定时机。
    /// </summary>
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            ReadMarks.SaveIfDirty();
        }
    }

    protected override void OnDestroy()
    {
        StopDramaRuntime();
        // 对象都在销毁了,异步收尾未必还跑得到,这里补一刀
        _dramaSession++;
        TeardownDramaRuntime();
        base.OnDestroy();
    }

    #endregion
    
}

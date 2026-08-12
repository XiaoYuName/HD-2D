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
        data.DialogueDataList = new List<DialogueData>(_dataList);
        data.DramaProgress = CaptureRestorePoint();
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="data"></param>
    public void LoadData(GameSaveData data)
    {
        if (data is { DialogueDataList: not null })
        {
            _dataList = data.DialogueDataList;
        }
        else
        {
            _dataList = new List<DialogueData>();
        }

        // 只接下来，不在这儿开播 —— 读档时场景还没切完，UI 也还没就位。
        // 由场景流程在合适的时机调 ResumeSavedDramaAsync
        _savedProgress = data?.DramaProgress;
    }
    

    #endregion
    
    #region Log系统
    private List<DialogueData> _dataList = new();

    public void AddData(DialogueData data)
    {
        _dataList.Add(data);
        QuestEventBus.ReportDialogueFinished(data.Id); // 任务系统：对话播过上报（和 HasDialogue 同一时机）
    }

    /// <summary>
    /// 判断对话是否对话过
    /// </summary>
    /// <param name="dialogueID"></param>
    /// <returns></returns>
    public bool HasDialogue(long dialogueID)
    {
        return _dataList.Any(temp => temp.Id == dialogueID);
    }

    /// <summary>
    /// 显示对话日志UI
    /// </summary>
    public void ShowDramaLogUI()
    {
         var logUI = UISystem.Instance.OpenUI<DramaLogUI>("DramaLogUI");
         if (logUI != null)
         {
             logUI.SetDates(_dataList);
         }
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
    public DramaDirector Director => _director ??= new DramaDirector();

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
    }

    // ==================================================== 存档 / 读档

    /// <summary>读档时接下来的剧情进度，等场景就位后由 <see cref="ResumeSavedDramaAsync"/> 消费。</summary>
    private DramaRestorePoint _savedProgress;

    /// <summary>为恢复而自己加载的剧本，收尾时要还掉（正常开播那条路上剧本归调用方）。</summary>
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
    /// <returns>true = 已经开播；false = 没有存档点，或者剧本加载失败。</returns>
    public async UniTask<bool> ResumeSavedDramaAsync()
    {
        DramaRestorePoint point = _savedProgress;

        // 只用一次。失败也要清掉，否则每次进场景都会再试一遍
        _savedProgress = null;

        if (point is not { IsValid: true })
        {
            return false;
        }

        string key = DramaDirector.ScriptKeyOf(point.DramaId);
        DramaScript script = await AssetsManager.Instance.LoadAssetsUniTask<DramaScript>(key);
        if (script == null)
        {
            Debug.LogError($"[Drama] 恢复剧情失败，加载不到剧本 {point.DramaId}：{key}");
            AssetsManager.Instance.FreeAsset(key);
            return false;
        }

        StartDramaRuntime(script, point);
        _ownedEntryScriptKey = key;
        return true;
    }

    /// <summary>
    /// 播一段剧情。重复调用会先掐掉上一段。
    /// </summary>
    /// <param name="restore">读档恢复点，null = 从头正常播。</param>
    public void StartDramaRuntime(DramaScript script, DramaRestorePoint restore = null)
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

        // 这里【不】重置播放模式：上一段结束时是自动 / 跳过，下一段就接着自动 / 跳过。
        // 玩家开了自动就是不想再点了，进下一段又要重新点一次是倒退。
        // Director 是复用的，Context.Mode 本来就还留着，重新塞一遍是为了
        // Director 头一次被创建的那种情况（属性 getter 里 new 出来，Mode 是默认值）
        SetPlaybackMode(PlaybackMode);

        PlayAndTeardownAsync(script, restore, session, _dramaTokenSource.Token).Forget();
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
        DramaScript script, DramaRestorePoint restore, int session, CancellationToken ct)
    {
        try
        {
            await Director.PlayAsync(script, ct, restore);
        }
        finally
        {
            if (session == _dramaSession)
            {
                TeardownDramaRuntime();
            }
        }
    }

    /// <summary>关剧情 UI + 恢复原来的界面。重复调用无害。</summary>
    private void TeardownDramaRuntime()
    {
        if (_dramaTokenSource != null)
        {
            _dramaTokenSource.Dispose();
            _dramaTokenSource = null;
        }

        // 恢复那条路上的入口剧本是我们自己加载的，Director 只释放它自己 Goto 加载的那些
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

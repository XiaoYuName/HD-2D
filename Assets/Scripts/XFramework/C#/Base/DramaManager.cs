using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
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
    }
    

    #endregion
    
    #region Log系统
    private List<DialogueData> _dataList = new();

    public void AddData(DialogueData data)
    {
        _dataList.Add(data);
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
    /// 播一段剧情。重复调用会先掐掉上一段。
    /// </summary>
    public void StartDramaRuntime(DramaScript script)
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

        PlayAndTeardownAsync(script, session, _dramaTokenSource.Token).Forget();
    }

    /// <summary>
    /// 播完(或被取消 / 报错)之后把剧情 UI 收掉,再把进剧情前那批 UI 还回去。
    ///
    /// 收尾必须挂在 PlayAsync 后面而不是 <see cref="StopDramaRuntime"/> 里:
    /// 剧本自然播完是没人调 Stop 的,那时候对话框会一直挂在屏幕上;
    /// 而且这样收尾一定排在 Director 自己的 finally(还资源、清舞台)之后,
    /// 不会出现"UI 都销毁了 Tween 还在动它"。
    /// </summary>
    private async UniTaskVoid PlayAndTeardownAsync(DramaScript script, int session, CancellationToken ct)
    {
        try
        {
            await Director.PlayAsync(script, ct);
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

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
        StopDramaRuntime();
        UISystem.Instance.CloseAllUIAndSnapshot(new List<string>());
        _dramaTokenSource = new CancellationTokenSource();
        _runtimeUI = UISystem.Instance.OpenUI<DramaRuntimeUI>(UIKeys.DramaRuntimeUI);
        Director.Context.Dialogue = _runtimeUI;
        Director.Context.Background = _runtimeUI.BackgroundController;
        Director.Context.Screen = _runtimeUI.ScreenActionController;
        Director.Context.Choice = _runtimeUI;
        Director.Context.Actors = _runtimeUI;
        
        Director.PlayAsync(script, _dramaTokenSource.Token).Forget();
    }

    /// <summary>中断当前剧情。Director 的 finally 会把资源还干净。</summary>
    public void StopDramaRuntime()
    {
        if (_dramaTokenSource == null)
        {
            return;
        }

        _dramaTokenSource.Cancel();
        _dramaTokenSource.Dispose();
        _dramaTokenSource = null;
        if(UISystem.IsInitialized)
            UISystem.Instance.RestoreUI(new List<string>());
    }

    protected override void OnDestroy()
    {
        StopDramaRuntime();
        base.OnDestroy();
    }

    #endregion
    
}

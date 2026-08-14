using Cysharp.Threading.Tasks;
using UnityEngine;
using XFramework;

/// <summary>
/// 展会管理器 - 框架存根版本
/// 如果项目不需要展会系统，可以删除此文件及相关引用
/// </summary>
public class ExhibitionManager : MonoSingleton<ExhibitionManager>
{
    /// <summary>
    /// 开始准备展会 - 框架存根版本
    /// </summary>
    public void StartPrepareExhibition()
    {
        Debug.LogWarning("ExhibitionManager.StartPrepareExhibition stub called - implement if needed");
    }

    public async UniTask Initialized()
    {
        await UniTask.CompletedTask;
    }

    public async UniTask Release()
    {
        await UniTask.CompletedTask;
    }
}

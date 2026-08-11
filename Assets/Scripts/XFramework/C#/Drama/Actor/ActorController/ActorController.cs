using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Services;
using XFramework;

public partial class ActorController : UIBase,IActorStage
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    /// <summary>拿到（必要时加载并入场）指定角色的立绘。</summary>
    public async UniTask<IActorView> AcquireAsync(int actorId, CancellationToken ct)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>找已经在台上的立绘；不在台上返回 null。</summary>
    public IActorView Find(int actorId)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 显隐。<paramref name="duration"/> 为 0 就是瞬间切换。
    ///
    /// 实现里产生的 Tween <b>必须登记到舞台自己名下</b>，
    /// 这样 <see cref="CompleteAllTweens"/> 才收得住 —— 见
    /// <see cref="ActorShowAction.WaitForCompletion"/> 为 false 的情况。
    /// </summary>
    public async UniTask SetVisibleAsync(IActorView actor, bool visible, float duration, Ease ease, CancellationToken ct)
    {
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 把所有还在跑的立绘动画立刻推到终点。
    /// 剧本结束 / 跳转 / 切到 Skip 时调，防止游离动画漏到下一段剧情里。
    /// </summary>
    public void CompleteAllTweens()
    {
        throw new System.NotImplementedException();
    }

    /// <summary>清空舞台并释放立绘资源。</summary>
    public void ReleaseAll()
    {
        throw new System.NotImplementedException();
    }
}

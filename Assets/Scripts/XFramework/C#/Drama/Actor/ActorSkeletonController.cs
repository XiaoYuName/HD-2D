using Spine.Unity;
using UnityEngine;
using XFramework;

public class ActorSkeletonController : UIBase
{
    private SkeletonGraphic skeletonGraphic;
    private SkeletonAnimation skeletonAnimation;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
    }
}

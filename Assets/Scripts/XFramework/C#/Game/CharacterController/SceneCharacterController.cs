using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

/// <summary>
/// 场景里站着的一个 NPC。
///
/// 表现从 <see cref="SpriteRenderer"/> 换成了 Spine：预制体是<b>所有 NPC 共用一个模板</b>，
/// 靠 <see cref="Init"/> 把角色表 <c>ScreenSpienPath</c> 指向的 <see cref="SkeletonDataAsset"/>
/// 换进来变成具体角色 —— 和剧情立绘 <c>ActorSkeletonController.Bind</c> 是同一套路子，
/// 区别只是这里在世界空间（SkeletonRenderer + MeshRenderer），剧情那边在 Canvas 里（SkeletonGraphic）。
///
/// <b>预制体约定</b>：本组件挂根节点（带 CapsuleCollider2D 收点击），
/// SkeletonRenderer / SkeletonAnimation 挂子节点 Skeleton 上。
/// </summary>
public class SceneCharacterController : GameBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    private SkeletonRenderer skeletonRenderer;
    private SkeletonAnimation skeletonAnimation;

    /// <summary>
    /// 当前这个实例持有的 Spine 资源 Key。
    ///
    /// 记下来而不是 Release 时再去读 <c>npcData.ScreenSpienPath</c>：
    /// 实例是走对象池复用的，只有"真的加载成功了"才该还一次，
    /// 否则路径没配 / 加载失败的那些也会白还一次引用，把别人的计数扣穿。
    /// </summary>
    private string loadedSkeletonKey;

    public NpcData npcData { get; private set; }

    public void Init(NpcData npcData)
    {
        // 池子里捞出来的旧实例可能还攥着上一个角色的资源，先还掉再换新的
        FreeSkeletonAsset();

        skeletonRenderer = GetComponentInChildren<SkeletonRenderer>(true);
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);

        this.npcData = npcData;

        ApplySkeleton(npcData.ScreenSpienPath);

        transform.localPosition = new Vector3( npcData.ScenePosition.X, npcData.ScenePosition.Y,0);
    }

    /// <summary>
    /// 把这个角色的 Spine 数据换到共用预制体上。
    ///
    /// 路径没配 / 加载不出来时保留预制体自带的那套骨骼，只报警告不抛 ——
    /// 一个 NPC 的资源缺了不该让整个场景的 NPC 都生成不出来。
    /// </summary>
    private void ApplySkeleton(string skeletonPath)
    {
        if (skeletonRenderer == null)
        {
            Debug.LogError($"[SceneCharacter] 预制体上找不到 SkeletonRenderer，NPC {npcData.Id}（{npcData.Remark}）显示不出来", this);
            return;
        }

        if (string.IsNullOrEmpty(skeletonPath))
        {
            Debug.LogWarning($"[SceneCharacter] NPC {npcData.Id}（{npcData.Remark}）没配场景 Spine（ScreenSpienPath 为空）", this);
            return;
        }

        SkeletonDataAsset skeletonData = AssetsManager.Instance.LoadAssets<SkeletonDataAsset>(skeletonPath);
        if (skeletonData == null)
        {
            Debug.LogError($"[SceneCharacter] NPC {npcData.Id}（{npcData.Remark}）的场景 Spine 加载失败：{skeletonPath}", this);
            return;
        }

        // 加载成功才登记，Release 时按这个 Key 一一对应地还
        loadedSkeletonKey = skeletonPath;

        skeletonRenderer.skeletonDataAsset = skeletonData;

        // 预制体上的初始皮肤是照着模板那套骨骼配的，换成别的骨骼可能就没这个皮肤了 ——
        // SkeletonRenderer.AssignInitialSkin 里的 skeleton.SetSkin(名字) 找不到会直接抛，
        // 所以这里先按新数据核一遍，对不上就退回默认皮肤（初始动画那边找不到会自己跳过，不用管）
        EnsureInitialSkinValid(skeletonData);

        // overwrite: true 强制按新数据重建 skeleton 和 mesh；
        // 这一句会连带把动画组件也初始化好（见 SkeletonAnimationBase.Initialize），
        // 预制体上配的初始动画会重新播起来
        if (skeletonAnimation != null)
        {
            skeletonAnimation.Initialize(true);
        }
        else
        {
            skeletonRenderer.Initialize(true);
        }
    }

    /// <summary>新骨骼里没有预制体配的那个初始皮肤时，把它清成默认，避免初始化时抛异常。</summary>
    private void EnsureInitialSkinValid(SkeletonDataAsset skeletonData)
    {
        string skinName = skeletonRenderer.initialSkinName;
        if (string.IsNullOrEmpty(skinName) || skinName == "default")
        {
            return;
        }

        Spine.SkeletonData data = skeletonData.GetSkeletonData(false);
        if (data != null && data.FindSkin(skinName) == null)
        {
            // 改的是实例上的字段，不动预制体
            skeletonRenderer.initialSkinName = string.Empty;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {

    }

    public void OnPointerExit(PointerEventData eventData)
    {

    }

    public void OnPointerClick(PointerEventData eventData)
    {
      if (eventData.button != PointerEventData.InputButton.Left) return;

      // 任务系统：点击NPC上报。用角色表 ID（npcData.Id 是场景摆放行，任务配表按角色配）
      QuestEventBus.ReportNpcClicked(npcData.CharacterData);

      //没有对话内容,但是有功能
      if (npcData.PointerDialogue.Count <= 0 && npcData.FunctionType != FunctionGroup.Node)
      {
          if(npcData.FunctionType== FunctionGroup.Node)return;
          var ui = UISystem.Instance.OpenUI<CharacterFunctionUI>("CharacterFunctionUI");
          ui.SetData(npcData);
          return;
      }

      if (npcData.PointerDialogue.Count > 0)
      {
          QuestEventBus.ReportNpcTalked(npcData.CharacterData); // 任务系统：与NPC对话上报（整段对话结束时一次）
      }

    }

    public void Release()
    {
        FreeSkeletonAsset();
        npcData = null;
    }

    private void FreeSkeletonAsset()
    {
        if (string.IsNullOrEmpty(loadedSkeletonKey))
        {
            return;
        }

        AssetsManager.Instance.FreeAsset(loadedSkeletonKey);
        loadedSkeletonKey = null;
    }
}

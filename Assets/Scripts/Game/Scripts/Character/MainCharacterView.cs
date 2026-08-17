using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 主角 Spine 外观与动画驱动，对应原作的 CharacterModel_Default。
///
/// 这个角色是套装式骨架（108 个皮肤，hair / clothes / decoration 分离），
/// 默认皮肤是空的——不在运行时组合皮肤的话进 PlayMode 会整个人不见。
///
/// 八方向只用 5 个动画 + X 翻转覆盖，和原作一致：
///   move_0 背朝镜头 / move_45 / move_90 侧向 / move_135 / move_180 面朝镜头
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SkeletonAnimation))]
public class MainCharacterView : MonoBehaviour
{
    [Header("皮肤组合（按顺序叠加）")]
    [SerializeField] private string[] skinNames = { "skin_base", "full_skins/0_original" };

    [Header("动画名")]
    [SerializeField] private string idleNorth = "idle/idle_0";
    [SerializeField] private string idleSouth = "idle/idle_135";

    private SkeletonAnimation skelAni;

    /// <summary>上一次朝向是否偏向背离镜头，决定待机用哪个 idle。原作同名字段。</summary>
    private bool toNorth;

    /// <summary>当前动画状态编号，语义同原作：0 待机，1-5 走，6-10 跑。</summary>
    private int currAniState = -1;

    public SkeletonAnimation SkeletonAnimation => skelAni;

    // 用 OnEnable 而非 Awake：加了 [ExecuteAlways]，编辑态下也要装配一次，
    // 否则皮肤和材质只是运行时内存状态，退出 PlayMode 后场景里就是空的
    private void OnEnable()
    {
        Setup();
    }

    private void Setup()
    {
        skelAni = GetComponent<SkeletonAnimation>();
        if (skelAni.skeletonDataAsset == null) return;

        skelAni.Initialize(false);
        if (skelAni.Skeleton == null) return;

        currAniState = -1;
        ApplySkin();
        PlayIdle();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Setup(); };
    }
#endif

    /// <summary>把配置里的皮肤叠成一套并应用。</summary>
    public void ApplySkin()
    {
        SkeletonData data = skelAni.Skeleton.Data;
        var combined = new Skin("combined");
        var missing = new List<string>();

        foreach (string n in skinNames)
        {
            Skin s = data.FindSkin(n);
            if (s != null) combined.AddSkin(s);
            else missing.Add(n);
        }

        if (missing.Count > 0)
            Debug.LogWarning($"[MainCharacterView] 找不到皮肤: {string.Join(", ", missing)}", this);

        skelAni.Skeleton.SetSkin(combined);
        skelAni.Skeleton.SetupPoseSlots();
    }

    public void SetFlipScale(float val)
    {
        skelAni.Skeleton.ScaleX = val;
    }

    public void PlayIdle()
    {
        if (currAniState == 0) return;
        currAniState = 0;
        skelAni.AnimationState.SetAnimation(0, toNorth ? idleNorth : idleSouth, true);
    }

    /// <summary>state 1-5，对应原作 CharacterModel_Default.PlayWalk。</summary>
    public void PlayWalk(int state)
    {
        if (currAniState == state) return;
        currAniState = state;

        switch (state)
        {
            case 1: toNorth = false; SetAnim("move/move_90");  break;
            case 2: toNorth = true;  SetAnim("move/move_0");   break;
            case 3: toNorth = true;  SetAnim("move/move_45");  break;
            case 4: toNorth = false; SetAnim("move/move_180"); break;
            case 5: toNorth = false; SetAnim("move/move_135"); break;
        }
    }

    /// <summary>state 6-10，对应原作 CharacterModel_Default.PlayRun。</summary>
    public void PlayRun(int state)
    {
        if (currAniState == state) return;
        currAniState = state;

        switch (state)
        {
            case 6:  toNorth = false; SetAnim("run/run_90");  break;
            case 7:  toNorth = true;  SetAnim("run/run_0");   break;
            case 8:  toNorth = true;  SetAnim("run/run_45");  break;
            case 9:  toNorth = false; SetAnim("run/run_180"); break;
            case 10: toNorth = false; SetAnim("run/run_135"); break;
        }
    }

    private void SetAnim(string name)
    {
        if (skelAni.Skeleton.Data.FindAnimation(name) == null)
        {
            Debug.LogWarning($"[MainCharacterView] 找不到动画 {name}", this);
            return;
        }
        skelAni.AnimationState.SetAnimation(0, name, true);
    }
}

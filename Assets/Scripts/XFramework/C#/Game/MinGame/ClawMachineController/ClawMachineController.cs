using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;
using Random = UnityEngine.Random;

public class ClawMachineController : GameBase
{
    [Title("移动参数")]
    [LabelText("水平移动速度")] 
    public float moveSpeed = 3f;
    [LabelText("下落速度")] 
    public float dropSpeed = 8f;
    [LabelText("上升速度")] 
    public float riseSpeed = 6f;

    [Title("边界限制")]
    [LabelText("X活动范围")]
    public Vector2 BorderXRadius = new Vector2(-2.75f, 2.8f);
    [LabelText("动态X范围")]
    public Vector2 runtimeRadius = new Vector2(1.02f,6f);
    
    [LabelText("Y活动范围")]
    public Vector2 BorderYRadius = new Vector2(-0.68f, 0.55f);
    [LabelText("娃娃生成X范围")]
    public Vector2 BabyBorderXRadius = new Vector2(-2, 2);

    [Title("初始设置")]
    [LabelText("起始位置")]
    public Vector2 StartPoint;
    [Title("抓取检测")]
    [LabelText("检测半径")]
    public float catchRadius = 0.5f;
    [LabelText("娃娃层级")]
    public LayerMask dollLayer = ~0;

    [Title("震动")]
    [LabelText("震动范围:X")]
    public Vector2 ShockForceX;
    [LabelText("震动范围:Y")]
    public Vector2 ShockForceY;

    [Title("Spine动画")] 
    [SpineAnimation,LabelText("待机动画")]
    public string IdleAnimName;
    [SpineAnimation,LabelText("抓钩动画")]
    public string hockAnimName;
    [SpineAnimation,LabelText("锁定动画")]
    public string lockAnimName;
    
    
    private Rigidbody2D hock;
    private Transform hockCheckTransform;
    private Rigidbody2D hockRb;
    private Transform babyContent;
    /// <summary>
    /// 爪子的权威局部坐标。开启插值后 Transform 拿到的是渲染插值位姿，
    /// 每帧回读再累加会导致移动变慢并抖动，所以自己维护目标位置。
    /// </summary>
    private Vector2 hockLocalPos;
    /// <summary>入场 Tween 期间由 Tween 驱动 Transform，这段时间不做 MovePosition</summary>
    private bool isEnterTweening;
    /// <summary>被吊住期间要排除的箱壁层</summary>
    private LayerMask wallExcludeMask;
    private SkeletonAnimation hockAnim;
    private Collider2D[] hockColliders;
    
    private Canvas baseCanvas;
    private ContinuousButton LeftMoveButton;
    private ContinuousButton RightMoveButton;
    private TextMeshProUGUI downTimeTex;
    private TextMeshProUGUI gameNumberTex;
    private Button OnHockButton;
    private CustomButton AddGameNumberBtn;

    private enum ClawState { None,Idle, Dropping, Rising,Hock,AI,Wait}
    private ClawState state = ClawState.None;
    private float autoHockTime;

    private float inputX;
    private bool isSubCoin = false;
    
    private List<Rigidbody2D> babyList = new List<Rigidbody2D>();

    private BoxCollider2D runtimeWall;

    public void Initialized()
    {
        //Complete
        hock = Get<Rigidbody2D>("../HockController/RopePoint");
        hockCheckTransform = Get<Transform>("../HockController/Hock/CheckController");
        // 娃娃必须焊到 Hock 这个真正被模拟的动态刚体上。
        // CheckController 是 Hock 的子物体且自带 Kinematic 刚体，属于嵌套刚体：
        // 它的位姿每步被父级 Transform 瞬移同步进物理，而 velocity 恒为 0，
        // 关节求解器每步都要靠位置修正硬拽娃娃，这是抽搐的结构性来源。
        hockRb = Get<Rigidbody2D>("../HockController/Hock");
        babyContent = Get<Transform>("../BabyContent");
        hockAnim = Get<SkeletonAnimation>("../HockController/Hock/SpineRoot/Sprite");
        hockColliders = Get("../HockController/Hock/SpineRoot").transform.GetComponentsInChildren<Collider2D>();
        hockAnim.AnimationState.SetAnimation(0, IdleAnimName, true);
        
        baseCanvas = Get<Canvas>("MenuFarme/BaseCanvas");
        LeftMoveButton = Get<ContinuousButton>("MenuFarme/BaseCanvas/LeftMoveButton");
        RightMoveButton = Get<ContinuousButton>("MenuFarme/BaseCanvas/RightMoveButton");
        OnHockButton = Get<Button>("MenuFarme/BaseCanvas/OnHockButton");
        runtimeWall = Get<BoxCollider2D>("runtimeWall");
        downTimeTex = Get<TextMeshProUGUI>("MenuFarme/BaseCanvas/DownTimer/ValueTex");
        gameNumberTex = Get<TextMeshProUGUI>("MenuFarme/BaseCanvas/GameInfoUI/GameInfoPage/GameNumberTex");
        AddGameNumberBtn = Get<CustomButton>("MenuFarme/BaseCanvas/GameInfoUI/AddGameNumberBtn");
        
        
        baseCanvas.worldCamera =  Camera.main;
        LeftMoveButton.ContinuousButtonPressed.RemoveAllListeners();
        LeftMoveButton.ContinuousButtonPressed.AddListener(OnMovementLeft);
        LeftMoveButton.ContinuousButtonReleased.RemoveAllListeners();
        LeftMoveButton.ContinuousButtonReleased.AddListener(OnStopMovement);
        
        RightMoveButton.ContinuousButtonPressed.RemoveAllListeners();
        RightMoveButton.ContinuousButtonPressed.AddListener(OnMovementRight);
        RightMoveButton.ContinuousButtonReleased.RemoveAllListeners();
        RightMoveButton.ContinuousButtonReleased.AddListener(OnStopMovement);
        
        
        OnHockButton.onClick.RemoveAllListeners();
        OnHockButton.onClick.AddListener(OnHock);
        
        AddGameNumberBtn.onClick.RemoveAllListeners();
        AddGameNumberBtn.onClick.AddListener(InsertCoin);
        
        hock.bodyType = RigidbodyType2D.Kinematic;
        hock.useFullKinematicContacts = true;
        // 物理 50Hz、屏幕 60/120Hz，不插值即使物理算对了画面也会有顿挫
        hock.interpolation = RigidbodyInterpolation2D.None;
        hockRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        wallExcludeMask = LayerMask.GetMask("Ground");
        isSubCoin = true;

        isEnterTweening = true;
        hockLocalPos = hock.transform.localPosition;
        hock.transform.DOLocalMove(StartPoint, 0.15f).OnComplete(() =>
        {
            hockLocalPos = StartPoint;
            isEnterTweening = false;
            // Tween 直接写 Transform，插值要等它结束后再开，避免两者打架
            hock.interpolation = RigidbodyInterpolation2D.Interpolate;
        });


        state = ClawState.None;
        autoHockTime = GuideManager.Instance.ClawMachineSettingData.minGameTimer;
        
        foreach (var wall in hockColliders)
        {
            Physics2D.IgnoreCollision(wall,runtimeWall,true);
            Debug.Log("ingoreCollision : " + wall.gameObject.name + "target : " + runtimeWall.gameObject.name);
        }
        
        GuideManager.Instance.RegisterClawMachineDollResetChange(CreatDollController);
        GameDataManager.Instance.RegisterPlayerDataChange(UpdatePlayerData);
    }

    public void Release()
    {
        autoHockTime = GuideManager.Instance.ClawMachineSettingData.minGameTimer;
        if (hock != null) hock.transform.DOKill();
        isEnterTweening = false;
        hockColliders = hockAnim.transform.GetComponentsInChildren<Collider2D>();
        // 娃娃回池前先解开焊接，否则关节和排除层会被带进下一局
        PineAllDoll();
        GuideManager.Instance.UnregisterClawMachineDollResetChange(CreatDollController);
        GameDataManager.Instance.UnregisterPlayerDataChange(UpdatePlayerData);
        if (babyList != null && babyList.Count > 0)
        {
            foreach (var rb in babyList)
            {
                AssetsManager.Instance.FreeGameObject(rb.gameObject);
            }
            babyList.Clear();
        }
    }


    #region 按钮控制位移

    public void OnMovementLeft()
    {
        if (!isSubCoin) return; //没投币
        if (state == ClawState.Idle || state == ClawState.None)
        {
            inputX = -1f;
        }
    }

    public void OnMovementRight()
    {
        if (!isSubCoin) return; //没投币
        if (state == ClawState.Idle || state == ClawState.None)
        {
            inputX = 1f;
        }
    }

    public void OnStopMovement()
    {
        inputX = 0f;
    }

    public void OnHock()
    {
        if (!isSubCoin) return; //没投币
        if (state == ClawState.Idle)
        {
            autoHockTime = GuideManager.Instance.ClawMachineSettingData.minGameTimer;
            state = ClawState.Dropping;
        }
    }

    public void InsertCoin()
    {
        if (isSubCoin) return;
        if (GameDataManager.Instance.GetProperty(PropertyType.ClawMachineValue).Value >= 1)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.ClawMachineValue,1);
            isSubCoin = true;
        }
    }


    #endregion

    [Button("震动全部娃娃")]
    public void Shock()
    {
        foreach (var baby in babyList)
        {
            baby.AddForce(new Vector2(Random.Range(ShockForceX.x, ShockForceX.y), Random.Range(ShockForceY.x,
                ShockForceY.y)), ForceMode2D.Impulse);
        }
    }
    
    private void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null) return;

        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private float hockTime;
    private float resetTime;
    private void Update()
    {
        LeftMoveButton.interactable = isSubCoin;
        RightMoveButton.interactable = isSubCoin;
        OnHockButton.interactable = isSubCoin;
        AddGameNumberBtn.interactable = !isSubCoin;
        switch (state)
        {
            case ClawState.None:
                if (hockLocalPos.x >= runtimeRadius.x)
                {
                    state = ClawState.Idle;
                    foreach (var wall in hockColliders)
                    {
                        Physics2D.IgnoreCollision(wall,runtimeWall,false);
                        Debug.Log("IgnoreCollision : " + wall.gameObject.name + "target : " + runtimeWall.gameObject.name);
                    }
                }
                break;
            case ClawState.Idle:
                autoHockTime -= Time.deltaTime;
                if (autoHockTime <= 0)
                {
                    autoHockTime = GuideManager.Instance.ClawMachineSettingData.minGameTimer;
                    state = ClawState.Dropping;
                }
                break;
            case ClawState.Dropping:
                FallAction();
                if (hockLocalPos.y <= BorderYRadius.x)
                {
                    hockTime = 1.5f;
                    hockAnim.AnimationState.SetAnimation(0, hockAnimName, false);
                    state = ClawState.Hock;
                }
                break;
            case ClawState.Hock:
                hockTime -= Time.deltaTime;
                if (hockTime <= 0)
                {
                    RisingAction();
                    state = ClawState.Rising; 
                }
                break;
            case ClawState.Rising:
                if (hockLocalPos.y >= BorderYRadius.y)
                {
                    state = ClawState.AI;
                    // 进入回程，爪子放行 runtimeWall。只需切一次，不用每帧刷
                    foreach (var wall in hockColliders)
                    {
                        Physics2D.IgnoreCollision(wall,runtimeWall,true);
                    }
                }
                break;
            case ClawState.AI:
                break;
            case ClawState.Wait:
                resetTime -= Time.deltaTime;
                if (resetTime <= 0)
                {
                    isSubCoin = false;
                    state = ClawState.None;
                    // 娃娃与箱壁的碰撞在 PineAllDoll 里随 excludeLayers 一起恢复
                    PineAllDoll();
                }
                break;
        }
        downTimeTex.text = autoHockTime.ToString("N0");
    }

    private void FixedUpdate()
    {
        if (hock == null) return;
        if(!isSubCoin) return;
        if (isEnterTweening)
        {
            // 入场 Tween 在写 Transform，这期间以 Transform 为准，不要再 MovePosition
            hockLocalPos = hock.transform.localPosition;
            return;
        }

        Vector2 localPos = hockLocalPos;
        switch (state)
        {
            case ClawState.None:
                localPos.x += inputX * moveSpeed * Time.fixedDeltaTime;
                localPos.x = Mathf.Clamp(localPos.x, BorderXRadius.x, BorderXRadius.y);
                break;
            case ClawState.Idle:
                localPos.x += inputX * moveSpeed * Time.fixedDeltaTime;
                localPos.x = Mathf.Clamp(localPos.x, runtimeRadius.x, runtimeRadius.y);
                break;
            case ClawState.Dropping:
                localPos.y -= dropSpeed * Time.fixedDeltaTime;
                break;
            case ClawState.Rising:
                localPos.y += riseSpeed * Time.fixedDeltaTime;
                break;
            case ClawState.AI:
                MoveHockToStartPointByPhysics();
                return;

        }
        hockLocalPos = localPos;
        Vector2 worldPos = hock.transform.parent.TransformPoint(localPos);
        hock.MovePosition(worldPos);
    }

    private void MoveHockToStartPointByPhysics()
    {
        Vector2 targetLocalPos = StartPoint;

        Vector2 nextLocalPos = Vector2.MoveTowards(
            hockLocalPos,
            targetLocalPos,
            moveSpeed * Time.fixedDeltaTime
        );

        hockLocalPos = nextLocalPos;

        Vector2 nextWorldPos = hock.transform.parent != null
            ? hock.transform.parent.TransformPoint(nextLocalPos)
            : nextLocalPos;

        hock.MovePosition(nextWorldPos);

        if (Vector2.Distance(nextLocalPos, targetLocalPos) <= 0.02f)
        {
            //PineAllDoll();
            hockAnim.AnimationState.SetAnimation(0,lockAnimName,true);
            hockAnim.AnimationState.AddAnimation(0, IdleAnimName, true, 0);
            resetTime = 0.4f;
            state = ClawState.Wait;
        }
    }
    
    //爪子在下落过程中，检测到娃娃，将娃娃设置的为只和爪子/地面有碰撞关系
    //收起爪子的时候，将娃娃的固定点修改为0,0
    private readonly List<Rigidbody2D> dollColliders = new List<Rigidbody2D>();
    /// <summary>
    /// 下落的行为代码
    /// </summary>
    private void FallAction()
    {
        // Collider2D[] hits = Physics2D.OverlapCircleAll(hockCheckTransform.position, catchRadius, dollLayer);
        // if (hits == null || hits.Length == 0)
        // {
        //     return;
        // }
        //
        //
        // foreach (var baby in hits)
        // {
        //     if (baby == null) continue;
        //     if (!dollColliders.Contains(baby))
        //     {
        //         // 切换到下落层
        //         SetLayerRecursively(baby.gameObject, LayerMask.NameToLayer("FallDoll"));
        //         dollColliders.Add(baby);
        //     }
        // }
    }

    private void RisingAction()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(hockCheckTransform.position, catchRadius, dollLayer);
        if (hits == null || hits.Length == 0)
        {
            return;
        }
        
        
        foreach (var baby in hits)
        {
            if (baby == null) continue;

            // 碰撞体挂在子物体上，刚体在父级，用 attachedRigidbody 拿才可靠
            Rigidbody2D babyRb = baby.attachedRigidbody;
            if (babyRb == null) continue;

            // 防止重复添加
            if (babyRb.GetComponent<FixedJoint2D>() != null)
                continue;

            FixedJoint2D joint2D = babyRb.gameObject.AddComponent<FixedJoint2D>();

            joint2D.connectedBody = hockRb;
            joint2D.enableCollision = false;
            joint2D.autoConfigureConnectedAnchor = false;

            // 娃娃自身的连接点，这里先用娃娃中心
            joint2D.anchor = Vector2.zero;

            // anchor 是相对娃娃刚体的，换算世界坐标必须用刚体的 Transform，
            // 用碰撞体子物体换算的话，碰撞体一旦有偏移，创建 Joint 的瞬间娃娃就会被弹一下
            Vector3 babyAnchorWorldPos = babyRb.transform.TransformPoint(joint2D.anchor);

            // 把娃娃当前 anchor 世界坐标，转换成钩子的局部坐标
            // 这样创建 Joint 的瞬间，娃娃不会被拉走
            joint2D.connectedAnchor = hockRb.transform.InverseTransformPoint(babyAnchorWorldPos);

            // FixedJoint2D 的默认参数是个 1Hz 量级的软弹簧，娃娃会像挂在橡皮筋上一样晃，
            // frequency = 0 表示完全刚性。想保留一点吊挂手感就改成 25~40 + dampingRatio 1
            joint2D.dampingRatio = 1f;
            joint2D.frequency = 0f;
            joint2D.breakForce = Mathf.Infinity;
            joint2D.breakTorque = Mathf.Infinity;

            // 切换到被抓层
            SetLayerRecursively(babyRb.gameObject, LayerMask.NameToLayer("CaughtDoll"));

            // 被吊住期间不再和箱壁做穿透修正，否则会和焊接约束互相顶
            babyRb.excludeLayers = wallExcludeMask;

            var dollController = babyRb.GetComponent<DollController>();
            if (dollController != null) dollController.IsGrabbed = true;

            dollColliders.Add(babyRb);
        }
    }

    private void PineAllDoll()
    {
        foreach (var babyRb in dollColliders)
        {
            if (babyRb == null) continue;

            SetLayerRecursively(babyRb.gameObject, LayerMask.NameToLayer("CaughtDoll"));
            babyRb.excludeLayers = default;

            var dollController = babyRb.GetComponent<DollController>();
            if (dollController != null) dollController.IsGrabbed = false;

            var joint = babyRb.GetComponent<FixedJoint2D>();
            if (joint != null) Destroy(joint);
        }
        dollColliders.Clear();
    }

    private void OnDrawGizmos()
    {
        if (hockCheckTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hockCheckTransform.position, catchRadius);
        }
    }

    #region UI更新

    public void UpdatePlayerData(PlayerData playerData)
    {
        gameNumberTex.text = playerData.GetProperty(PropertyType.ClawMachineValue).ToString();
    }


    
    /// <summary>
    /// 生成娃娃
    /// </summary>
    /// <param name="playerData"></param>
    private void CreatDollController(ClawMachineGameData playerData)
    {
        // 重置娃娃前先解开还挂在爪子上的，避免关节引用到已回池的对象
        PineAllDoll();

        if (babyList != null && babyList.Count > 0)
        {
            foreach (var rb in babyList)
            {
                AssetsManager.Instance.FreeGameObject(rb.gameObject);
            }
            babyList.Clear();
        }

        babyList = new List<Rigidbody2D>();
        
        var allDolls = GuideManager.Instance.GetDollCatalogData();
        List<DollCatalogData> dollIds = RandomWeightUtility.GetRandomListByWeightNoRepeat(
            allDolls,
            playerData.DollNumber,
            data => data.Weight,
            data => !InventoryManager.Instance.HasItemUnlock(data.ItemID) || data.IsUnlockRandom);
        
        
        for (int i = 0; i < dollIds.Count; i++)
        {
           
            var obj = AssetsManager.Instance.Instantiate(dollIds[i].PrefabPath);
            obj.gameObject.name = "Doll_" + i.ToString();
            obj.transform.SetParent(babyContent);
            obj.transform.localPosition = new Vector3(Random.Range(BabyBorderXRadius.x, BabyBorderXRadius.y), 0);
            obj.transform.localScale =new Vector3(dollIds[i].Scale.X, dollIds[i].Scale.Y, dollIds[i].Scale.Z);
            obj.gameObject.layer =  LayerMask.NameToLayer("Doll");
            var rb = obj.GetComponent<Rigidbody2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
            // 对象池复用，必须清掉上一局被抓时设的排除层
            rb.excludeLayers = default;
            rb.simulated = true;
            rb.WakeUp();
            var dollController = obj.GetComponent<DollController>();
            ItemData guideBag = InventoryManager.Instance.GetItemData(dollIds[i].ItemID);
            dollController.SetData(dollIds[i], guideBag);
            babyList.Add(rb);
        }
    }

    #endregion
}

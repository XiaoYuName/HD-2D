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
        hockRb = hockCheckTransform.GetComponent<Rigidbody2D>();
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
        isSubCoin = true;
        hock.transform.DOLocalMove(StartPoint, 0.15f);
       

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
        hockColliders = hockAnim.transform.GetComponentsInChildren<Collider2D>();
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
                if (hock.transform.localPosition.x >= runtimeRadius.x)
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
                if (hock.transform.localPosition.y <= BorderYRadius.x)
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
                if (hock.transform.localPosition.y >= BorderYRadius.y)
                {
                    state = ClawState.AI; 
                }
                break;
            case ClawState.AI:
                foreach (var wall in hockColliders)
                {
                    Physics2D.IgnoreCollision(wall,runtimeWall,true);
                }

                foreach (var dollCollider in dollColliders)
                {
                    Physics2D.IgnoreCollision(dollCollider,runtimeWall,true);
                }
                break;
            case ClawState.Wait:
                resetTime -= Time.deltaTime;
                if (resetTime <= 0)
                {
                    isSubCoin = false;
                    state = ClawState.None;
                    PineAllDoll();
                    foreach (var dollCollider in dollColliders)
                    {
                        Physics2D.IgnoreCollision(dollCollider,runtimeWall,false);
                    }
                }
                break;
        }
        downTimeTex.text = autoHockTime.ToString("N0");
    }

    private void FixedUpdate()
    {
        if (hock == null) return;
        if(!isSubCoin) return;
        Vector2 localPos = hock.transform.localPosition;
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
        Vector2 worldPos = hock.transform.parent.TransformPoint(localPos);
        hock.MovePosition(worldPos);
    }
    
    private void MoveHockToStartPointByPhysics()
    {
        Vector2 currentLocalPos = hock.transform.localPosition;
        Vector2 targetLocalPos = StartPoint;

        Vector2 nextLocalPos = Vector2.MoveTowards(
            currentLocalPos,
            targetLocalPos,
            moveSpeed * Time.fixedDeltaTime
        );

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
    private List<Collider2D> dollColliders = new List<Collider2D>();
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

            // 防止重复添加
            if (baby.transform.parent.gameObject.GetComponent<FixedJoint2D>() != null)
                continue;

            FixedJoint2D joint2D = baby.transform.parent.gameObject.AddComponent<FixedJoint2D>();

            joint2D.connectedBody = hockRb;
            joint2D.enableCollision = false;
            joint2D.autoConfigureConnectedAnchor = false;

            // 娃娃自身的连接点，这里先用娃娃中心
            joint2D.anchor = Vector2.zero;

            // 娃娃当前 anchor 的世界坐标
            Vector3 babyAnchorWorldPos = baby.transform.TransformPoint(joint2D.anchor);

            // 把娃娃当前 anchor 世界坐标，转换成钩子的局部坐标
            // 这样创建 Joint 的瞬间，娃娃不会被拉走
            joint2D.connectedAnchor = hockRb.transform.InverseTransformPoint(babyAnchorWorldPos);

            //joint2D.connectedAnchor = Vector2.zero;
            // 切换到被抓层
            SetLayerRecursively(baby.transform.parent.gameObject, LayerMask.NameToLayer("CaughtDoll"));
            dollColliders.Add(baby);
        }
    }

    private void PineAllDoll()
    {
        foreach (var joint in dollColliders)
        {
            SetLayerRecursively(joint.transform.parent.gameObject, LayerMask.NameToLayer("CaughtDoll"));
            Destroy(joint.transform.parent.GetComponent<FixedJoint2D>());
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
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
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

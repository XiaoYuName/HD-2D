using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;
using Random = UnityEngine.Random;

public class ClawMachineController : GameBase
{
    [Title("移动参数")]
    [LabelText("水平移动速度")] public float moveSpeed = 3f;
    [LabelText("下落速度")] public float dropSpeed = 8f;
    [LabelText("上升速度")] public float riseSpeed = 6f;

    [Title("边界限制")]
    [LabelText("X活动范围")]
    public Vector2 BorderXRadius = new Vector2(-2.75f, 2.8f);
    [LabelText("Y活动范围")]
    public Vector2 BorderYRadius = new Vector2(-0.68f, 0.55f);
    [LabelText("娃娃生成X范围")]
    public Vector2 BabyBorderXRadius = new Vector2(-2, 2);

    [Title("初始设置")]
    [LabelText("起始位置")]
    public Vector2 StartPoint;
    [LabelText("生成娃娃数量")]
    public int babyNumber = 5;

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

    [Title("预制体")]
    public Rigidbody2D hock;
    public Transform hockCheckTransform;
    public Rigidbody2D hockRb;
    public Transform babyContent;
    public GameObject babyPrefab;
    public Animator hockAnim;

    /// <summary>当前是否正在出抓（下落或上升中）</summary>
    public bool IsBusy => state != ClawState.Idle;

    private enum ClawState { Idle, Dropping, Rising,Hock,AI,Wait}
    private ClawState state = ClawState.Idle;

    private float inputX;
    private List<Rigidbody2D> babyList = new List<Rigidbody2D>();
    private List<FixedJoint2D> jointList = new List<FixedJoint2D>();

    private void Start()
    {
        Initialized();
    }

    public void Initialized()
    {
        // Kinematic: 钩子不被娃娃阻挡，但能物理挤压推开娃娃
        hock.bodyType = RigidbodyType2D.Kinematic;
        hock.useFullKinematicContacts = true;
        hockRb = hockCheckTransform.GetComponent<Rigidbody2D>();
        hock.transform.DOLocalMove(StartPoint, 0.15f);
        
        for (int i = 0; i < babyNumber; i++)
        {
            var obj = Instantiate(babyPrefab, babyContent);
            obj.transform.localPosition = new Vector3(Random.Range(BabyBorderXRadius.x, BabyBorderXRadius.y), 0);
            obj.gameObject.layer =  LayerMask.NameToLayer("Doll");
            var rb = obj.GetComponent<Rigidbody2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            babyList.Add(rb);
        }
    }

    #region 按钮控制位移

    public void OnMovementLeft()
    {
        if (state == ClawState.Idle)
        {
            inputX = 1f;
        }
    }

    public void OnMovementRight()
    {
        if (state == ClawState.Idle)
        {
            inputX = -1f;
        }
    }

    public void OnStopMovement()
    {
        inputX = 0f;
    }

    public void OnHock()
    {
        if (state == ClawState.Idle)
        {
            state = ClawState.Dropping;
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

    [Button("模拟全部抓到效果")]
    public void FixedJointAll()
    {
        if (hockRb == null)
        {
            Debug.LogError("hockCheckTransform 上没有 Rigidbody2D");
            return;
        }

        foreach (var baby in babyList)
        {
            if (baby == null) continue;

            // 防止重复添加
            if (baby.GetComponent<FixedJoint2D>() != null)
                continue;

            FixedJoint2D joint2D = baby.gameObject.AddComponent<FixedJoint2D>();

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

            // 切换到被抓层
            SetLayerRecursively(baby.gameObject, LayerMask.NameToLayer("CaughtDoll"));

            jointList.Add(joint2D);

            // 慢慢把 connectedAnchor 拉回到钩子中心 Vector2.zero
            DOTween.To(
                () => joint2D.connectedAnchor,
                value =>
                {
                    if (joint2D != null)
                        joint2D.connectedAnchor = value;
                },
                Vector2.zero,
                0.35f
            ).SetEase(Ease.OutQuad);
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
        switch (state)
        {
            case ClawState.Idle:
                break;
            case ClawState.Dropping:
                TryCatch_2();
                if (hock.transform.localPosition.y <= BorderYRadius.x)
                {
                    hockTime = 1.5f;
                    hockAnim.SetTrigger("hock");
                    state = ClawState.Hock;
                }
                break;
            case ClawState.Hock:
                hockTime -= Time.deltaTime;
                if (hockTime <= 0)
                {
                    RisingStart();
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
                break;
            case ClawState.Wait:
                resetTime -= Time.deltaTime;
                if (resetTime <= 0)
                {
                    state = ClawState.Idle;
                }
                break;
        }

        if (Input.GetKeyUp(KeyCode.Q))
        {
            PineAllDoll();
        }
    }

    private void FixedUpdate()
    {
        if (hock == null) return;
        Vector2 localPos = hock.transform.localPosition;
        switch (state)
        {
            case ClawState.Idle:
                localPos.x += inputX * moveSpeed * Time.fixedDeltaTime;
                localPos.x = Mathf.Clamp(localPos.x, BorderXRadius.x, BorderXRadius.y);
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
            PineAllDoll();
            hockAnim.SetTrigger("reset");
            resetTime = 2f;
            state = ClawState.Wait;
        }
    }

    /// <summary>
    /// 第一版(下路过程中)
    /// </summary>
    private void TryCatch()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(hockCheckTransform.position, catchRadius, dollLayer);
        if (hits == null || hits.Length == 0)
        {
            Debug.LogError("没有抓取到物体");
            return;
        }
        
        foreach (var baby in hits)
        {
            if (baby == null) continue;

            // 防止重复添加
            if (baby.GetComponent<FixedJoint2D>() != null)
                continue;

            FixedJoint2D joint2D = baby.gameObject.AddComponent<FixedJoint2D>();

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

            // 切换到被抓层
            SetLayerRecursively(baby.gameObject, LayerMask.NameToLayer("CaughtDoll"));

            jointList.Add(joint2D);

            // 慢慢把 connectedAnchor 拉回到钩子中心 Vector2.zero
            DOTween.To(
                () => joint2D.connectedAnchor,
                value =>
                {
                    if (joint2D != null)
                        joint2D.connectedAnchor = value;
                },
                Vector2.zero,
                0.35f
            ).SetEase(Ease.OutQuad);
        }
        
    }

    private void TryCatch_2()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(hockCheckTransform.position, catchRadius, dollLayer);
        if (hits == null || hits.Length == 0)
        {
            Debug.LogError("没有抓取到物体");
            return;
        }
        
        foreach (var baby in hits)
        {
            if (baby == null) continue;

            // 防止重复添加
            if (baby.GetComponent<FixedJoint2D>() != null)
                continue;

            FixedJoint2D joint2D = baby.gameObject.AddComponent<FixedJoint2D>();

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

            // 切换到被抓层
            SetLayerRecursively(baby.gameObject, LayerMask.NameToLayer("CaughtDoll"));

            jointList.Add(joint2D);

            // 慢慢把 connectedAnchor 拉回到钩子中心 Vector2.zero
            // DOTween.To(
            //     () => joint2D.connectedAnchor,
            //     value =>
            //     {
            //         if (joint2D != null)
            //             joint2D.connectedAnchor = value;
            //     },
            //     Vector2.zero,
            //     0.35f
            // ).SetEase(Ease.OutQuad);
        }
    }

    private void RisingStart()
    {
        // foreach (var fixedJoint2D in jointList)
        // {
        //     fixedJoint2D.connectedAnchor = Vector2.zero;
        // }
    }

    private void PineAllDoll()
    {
        foreach (var joint in jointList)
        {
            SetLayerRecursively(joint.gameObject, LayerMask.NameToLayer("CaughtDoll"));
            Destroy(joint);
        }
        jointList.Clear();
    }

    private void OnDrawGizmos()
    {
        if (hockCheckTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hockCheckTransform.position, catchRadius);
        }
    }
}

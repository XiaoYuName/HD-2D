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
    public Transform babyContent;
    public GameObject babyPrefab;
    public List<Collider2D> hockColliderList = new List<Collider2D>();

    /// <summary>当前是否正在出抓（下落或上升中）</summary>
    public bool IsBusy => state != ClawState.Idle;

    private enum ClawState { Idle, Dropping, Rising}
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

        hock.transform.DOLocalMove(StartPoint, 0.15f);
        foreach (var co in hockColliderList)
        {
            co.enabled = false;
        }
        
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
        Rigidbody2D hockRb = hockCheckTransform.GetComponent<Rigidbody2D>();

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

    private void Update()
    {
        switch (state)
        {
            case ClawState.Idle:
                inputX = Input.GetAxisRaw("Horizontal");
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    foreach (var co in hockColliderList)
                    {
                        co.enabled = true;
                    }
                    state = ClawState.Dropping;
                }
                break;
            case ClawState.Dropping:
                if (hock.transform.localPosition.y <= BorderYRadius.x)
                {
                    foreach (var co in hockColliderList)
                    {
                        co.enabled = false;
                    }
                    TryCatch();
                    state = ClawState.Rising;
                }
                break;
            case ClawState.Rising:
                if (hock.transform.localPosition.y >= BorderYRadius.y)
                {
                    state = ClawState.Idle; 
                    
                }
                break;
        }
        if (Input.GetKeyUp(KeyCode.Q))
        {
            foreach (var joint in jointList)
            {
                joint.gameObject.layer = LayerMask.NameToLayer("Doll");
                Destroy(joint);
            }
            jointList.Clear();
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
        }
        Vector2 worldPos = hock.transform.parent.TransformPoint(localPos);
        hock.MovePosition(worldPos);
    }

    private void TryCatch()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(hockCheckTransform.position, catchRadius, dollLayer);
        if (hits == null || hits.Length == 0)
        {
            Debug.LogError("没有抓取到物体");
            return;
        }
        foreach (var hit in hits)
        {
            if (hit != null && hit.attachedRigidbody != null && hit.attachedRigidbody != hock)
            {
                // 给娃娃挂 FixedJoint2D，连接到钩子，模拟真实抓持
               var joint = hit.gameObject.AddComponent<FixedJoint2D>();
               
               joint.gameObject.layer = LayerMask.NameToLayer("CaughtDoll");
               joint.connectedBody = hockCheckTransform.GetComponent<Rigidbody2D>();
               
               joint.enableCollision = false;
               
               joint.autoConfigureConnectedAnchor = false;
               
               joint.anchor = Vector2.zero;
               
               joint.connectedAnchor = Vector2.zero;
               
                jointList.Add(joint);
            }
        }
        
        
        
        
       
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

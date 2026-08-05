using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class HockWallController : MonoBehaviour
{
    [LabelText("推力")]
    public Vector2 force;

    [LabelText("同一娃娃的推力冷却")]
    public float pushCooldown = 0.2f;

    // 爪臂是 BoneFollower 驱动的运动学碰撞体，按渲染帧率瞬移，
    // 合爪过程中接触会反复 enter/exit，不加冷却就会变成连发冲量。
    private readonly Dictionary<Rigidbody2D, float> lastPushTime = new();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.gameObject.CompareTag("Doll")) return;
        // 碰撞体挂在子物体 ulockSprite 上，刚体在父级，必须用 attachedRigidbody
        TryPush(other.attachedRigidbody);
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (!other.gameObject.CompareTag("Doll")) return;
        TryPush(other.rigidbody);
    }

    private void TryPush(Rigidbody2D rb)
    {
        if (rb == null) return;

        // 已经被爪子焊住的娃娃不再施加冲量，否则会和焊接约束互相打架
        if (rb.GetComponent<FixedJoint2D>() != null) return;

        if (lastPushTime.TryGetValue(rb, out float last) && Time.time - last < pushCooldown) return;
        lastPushTime[rb] = Time.time;

        rb.AddForce(force, ForceMode2D.Impulse);
    }

    private void OnDisable()
    {
        lastPushTime.Clear();
    }
}

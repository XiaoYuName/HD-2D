using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 主角八方向移动，对应原作的 CharacterCtrl.DealMove / DealMoveAni。
///
/// 地图是 XZ 平面（Grid.CellSwizzle = XZY），所以输入的 (x, y) 映射到世界的 (x, z)。
/// 扇区划分和动画编号完全照搬原作：
///   sector = floor((SignedAngle(dir, up) + 202.5) % 360 / 45)
///   0 → 4   1|7 → 5   2|6 → 1   3|5 → 3   4 → 2      （跑步 +5）
/// 输入走 PlayerInputActions 的 Game/Move（Vector2，WASD + 方向键）和 Game/Sprint。
/// </summary>
[RequireComponent(typeof(MainCharacterView))]
public class MainCharacterController : MonoBehaviour
{
    [Header("速度（单位/秒，一格是 1.28）")]
    [SerializeField] private float walkSpeed = 3.2f;
    [SerializeField] private float runSpeed = 6.4f;

    [Header("碰撞")]
    [Tooltip("为 0 时不做射线阻挡，纯自由移动")]
    [SerializeField] private float radius = 0.35f;
    [SerializeField] private LayerMask blockMask = 0;

    private MainCharacterView view;
    private PlayerInputActions input;
    private InputAction moveAction;
    private InputAction sprintAction;

    private void Awake()
    {
        view = GetComponent<MainCharacterView>();
        // 自己持有一份，不依赖 PlayerInputManager 是否已经初始化
        input = new PlayerInputActions();
        moveAction = input.Game.Move;
        sprintAction = input.Game.Sprint;
    }

    private void OnEnable() => input.Game.Enable();
    private void OnDisable() => input.Game.Disable();
    private void OnDestroy() => input?.Dispose();

    private void Update()
    {
        Vector2 dir = moveAction.ReadValue<Vector2>();
        bool sprint = sprintAction.IsPressed();

        DealMoveAni(dir, sprint);
        DealMove(new Vector3(dir.x, 0f, dir.y), sprint);
    }

    private void DealMoveAni(Vector2 dir, bool sprint)
    {
        if (Mathf.Approximately(dir.x, 0f) && Mathf.Approximately(dir.y, 0f))
        {
            view.PlayIdle();
            return;
        }

        int sector = Mathf.FloorToInt((Vector2.SignedAngle(dir.normalized, Vector2.up) + 202.5f) % 360f / 45f);
        int state;
        switch (sector)
        {
            case 0:          state = 4; break;
            case 1: case 7:  state = 5; break;
            case 2: case 6:  state = 1; break;
            case 3: case 5:  state = 3; break;
            case 4:          state = 2; break;
            default:         state = 4; break;
        }

        if (sprint) view.PlayRun(state + 5);
        else view.PlayWalk(state);

        // 左右朝向靠骨架 X 翻转，5 个动画就能覆盖 8 个方向
        if (dir.x > 0f) view.SetFlipScale(1f);
        else if (dir.x < 0f) view.SetFlipScale(-1f);
    }

    private void DealMove(Vector3 input, bool sprint)
    {
        if (input == Vector3.zero) return;

        Vector3 delta = input.normalized * (sprint ? runSpeed : walkSpeed) * Time.deltaTime;

        // 原作是 X / Z 各打两条射线做贴墙滑动，这里保留同样的思路，
        // blockMask 为空时直接跳过（当前场景还没有碰撞体）
        if (blockMask.value != 0)
        {
            delta.x = Clamp(delta.x, Vector3.right);
            delta.z = Clamp(delta.z, Vector3.forward);
        }

        transform.position += delta;
    }

    private float Clamp(float amount, Vector3 axis)
    {
        if (Mathf.Approximately(amount, 0f)) return amount;

        Vector3 dir = amount > 0f ? axis : -axis;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, 10f, blockMask, QueryTriggerInteraction.Ignore)
            && hit.distance - radius < Mathf.Abs(amount))
        {
            return (hit.distance - radius) * Mathf.Sign(amount);
        }
        return amount;
    }
}

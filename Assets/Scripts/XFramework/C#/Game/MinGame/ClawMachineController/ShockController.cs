using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShockController : MonoBehaviour, IPointerClickHandler
{
    [Title("旋转震动")]
    [HorizontalGroup("RotateShock"), LabelText("旋转幅度")]
    public float rotateStrength = 8f;

    [HorizontalGroup("RotateShock"), LabelText("震动时间")]
    public float duration = 0.35f;

    [Title("位置震动")]
    [HorizontalGroup("PositionShock"), LabelText("X轴位移幅度")]
    public float positionXStrength = 0.08f;

    [Title("通用参数")]
    [LabelText("震动次数")]
    public int vibrato = 10;

    [LabelText("随机程度")]
    public float randomness = 45f;

    [LabelText("是否逐渐减弱")]
    public bool fadeOut = true;

    private Sequence sequence;

    private Vector3 originLocalEuler;
    private Vector3 originLocalPosition;
    private ClawMachineController machineController;

    private void Awake()
    {
        machineController = GetComponentInParent<ClawMachineController>();
        originLocalEuler = machineController.transform.localEulerAngles;
        originLocalPosition = machineController.transform.localPosition;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayShock();
        machineController?.Shock();
    }

    [Button("测试震动")]
    public void PlayShock()
    {
        sequence?.Kill();
        sequence = null;

        // 每次播放前恢复初始状态，防止连续点击导致偏移/旋转残留
        machineController.transform.localEulerAngles = originLocalEuler;
        machineController.transform.localPosition = originLocalPosition;

        sequence = DOTween.Sequence();

        // 旋转震动：Z 轴左右摇
        Tween rotateTween = machineController.transform
            .DOShakeRotation(
                duration,
                new Vector3(0f, 0f, rotateStrength),
                vibrato,
                randomness,
                fadeOut: fadeOut
            )
            .SetEase(Ease.OutQuad);

        // 位置震动：只震 X 轴
        Tween positionTween =machineController.transform
            .DOShakePosition(
                duration,
                new Vector3(positionXStrength, 0f, 0f),
                vibrato,
                randomness,
                snapping: false,
                fadeOut: fadeOut
            )
            .SetEase(Ease.OutQuad);

        sequence.Join(rotateTween);
        sequence.Join(positionTween);

        sequence.OnKill(ResetTransform);
        sequence.OnComplete(ResetTransform);
    }

    private void ResetTransform()
    {
        machineController.transform.localEulerAngles = originLocalEuler;
        machineController.transform.localPosition = originLocalPosition;
    }

    private void OnDisable()
    {
        sequence?.Kill();
        sequence = null;

        ResetTransform();
    }
}
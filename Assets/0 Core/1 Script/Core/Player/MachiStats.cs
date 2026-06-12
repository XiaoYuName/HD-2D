using Sirenix.OdinInspector;
using UnityEngine;

public class MachiStats : MonoBehaviour
{
    [LabelText("当前好感度")][SerializeField] float curAff;// Affection
    [LabelText("当前压力值")][SerializeField] float maxStress;
    [LabelText("当前压力值")][SerializeField] float curStress;
    [LabelText("最大灵感值")][SerializeField] float maxIp;
    [LabelText("当前灵感值")][SerializeField] float curIp;
    #region Get
    public float CurIp => curIp;
    public float CurStress => curStress;
    #endregion
    #region Lifecycle
    void Awake()
    {
        
    }
    #endregion
}

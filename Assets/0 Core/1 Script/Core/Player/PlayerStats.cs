using UnityEngine;
using System;
using Sirenix.OdinInspector;

public class PlayerStats : MonoBehaviour
{
    #region Parm
    [SerializeField] float maxSp;
    float curSp => GameDataManager.Instance.GetProperty(PropertyType.Strength).Value;
    [SerializeField] float maxAp;
    float curAp => GameDataManager.Instance.GetProperty(PropertyType.ActionPointsValue).Value;
    // [SerializeField] float maxIp;
    // [SerializeField] float curIp;
    [LabelText("最大好感度（对所有人）")][SerializeField] float maxAff;
    [LabelText("当前好感度（对所有人）")][SerializeField] float curAff;
    #endregion
    #region Get
    public float CurSp => GameDataManager.Instance.GetProperty(PropertyType.Strength).Value;
    public float CurAp => GameDataManager.Instance.GetProperty(PropertyType.ActionPointsValue).Value;
    // public event Action<PlayerStats> OnStatsChange;
    #endregion
    #region Func
    public bool CanConsumeSp(float value)
    {
        return curSp >= value;
    }
    public void SubSp(float value)
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.Strength,(int)value);

        // curSp -= value;
        // curSp = Mathf.Max(curSp, 0);
        // OnStatsChange?.Invoke(this);
    }
    public void AddSp(float value)
    {
        GameDataManager.Instance.AddProperty(PropertyType.Strength,(int)value);
        // curSp += value;
        // curSp = Mathf.Min(curSp, maxSp);
        // OnStatsChange?.Invoke(this);
    }
    public bool CanConsumeAp(float value)
    {
        return curAp >= value;
    }
    public void AddAp(float value)
    {
        GameDataManager.Instance.AddProperty(PropertyType.ActionPointsValue,(int)value);
    }
    public void SubAp(float value)
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue,(int)value);
    }
    // public bool CanConsumeIp(float value)
    // {
    //     return curIp >= value;
    // }
    // public void AddIp(float value)
    // {
    //     curIp += value;
    //     curIp = Mathf.Min(curIp, maxIp);
    //     OnStatsChange?.Invoke(this);
    // }
    // public void SubIp(float value)
    // {
    //     curIp -= value;
    //     curIp = Mathf.Max(curIp, 0);
    //     OnStatsChange?.Invoke(this);
    // }
    #endregion
}

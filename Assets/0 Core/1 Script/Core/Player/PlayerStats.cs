using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    #region Parm
    [SerializeField] float maxSp;
    float curSp => GameDataManager.Instance.CurrentUser.Strength;
    [SerializeField] float maxAp;
    float curAp => GameDataManager.Instance.CurrentUser.ActionPointsValue;
    // [SerializeField] float maxIp;
    // [SerializeField] float curIp;
    #endregion
    #region Get
    public float CurSp => GameDataManager.Instance.CurrentUser.Strength;
    public float CurAp => GameDataManager.Instance.CurrentUser.ActionPointsValue;
    // public event Action<PlayerStats> OnStatsChange;
    #endregion
    #region Func
    public bool CanConsumeSp(float value)
    {
        return curSp >= value;
    }
    public void SubSp(float value)
    {
        GameDataManager.Instance.RemoveStrength((int)value);

        // curSp -= value;
        // curSp = Mathf.Max(curSp, 0);
        // OnStatsChange?.Invoke(this);
    }
    public void AddSp(float value)
    {
        GameDataManager.Instance.AddStrength((int)value);

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
        GameDataManager.Instance.AddActionPointsValue((int)value);

        // curAp += value;
        // curAp = Mathf.Min(curAp, maxAp);
        // OnStatsChange?.Invoke(this);
    }
    public void SubAp(float value)
    {
        GameDataManager.Instance.RemoveActionPointsValue((int)value);

        // curAp -= value;
        // curAp = Mathf.Max(curAp, 0);
        // OnStatsChange?.Invoke(this);
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

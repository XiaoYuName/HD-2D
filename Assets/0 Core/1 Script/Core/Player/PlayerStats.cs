using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    #region Parm
    [SerializeField] float maxSp;
    [SerializeField] float curSp;
    [SerializeField] float maxAp;
    [SerializeField] float curAp;
    [SerializeField] float maxIp;
    [SerializeField] float curIp;
    #endregion
    #region Get
    public event Action<PlayerStats> OnStatsChange;
    public float CurSp => curSp;
    public float CurAp => curAp;
    public float CurIp => curIp;
    #endregion
    #region Func
    public bool CanConsumeSp(float value)
    {
        return curSp >= value;
    }
    public void SubSp(float value)
    {
        curSp -= value;
        curSp = Mathf.Max(curSp, 0);
        OnStatsChange?.Invoke(this);
    }
    public void AddSp(float value)
    {
        curSp += value;
        curSp = Mathf.Min(curSp, maxSp);
        OnStatsChange?.Invoke(this);
    }
    public bool CanConsumeAp(float value)
    {
        return curAp >= value;
    }
    public void AddAp(float value)
    {
        curAp += value;
        curAp = Mathf.Min(curAp, maxAp);
        OnStatsChange?.Invoke(this);
    }
    public void SubAp(float value)
    {
        curAp -= value;
        curAp = Mathf.Max(curAp, 0);
        OnStatsChange?.Invoke(this);
    }
    public bool CanConsumeIp(float value)
    {
        return curIp >= value;
    }
    public void AddIp(float value)
    {
        curIp += value;
        curIp = Mathf.Min(curIp, maxIp);
        OnStatsChange?.Invoke(this);
    }
    public void SubIp(float value)
    {
        curIp -= value;
        curIp = Mathf.Max(curIp, 0);
        OnStatsChange?.Invoke(this);
    }
    #endregion
}

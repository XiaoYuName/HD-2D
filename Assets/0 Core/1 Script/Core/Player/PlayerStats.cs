using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    #region Parm
    [SerializeField] float maxStamina;
    [SerializeField] float curStamina;
    [SerializeField] float maxAp;
    [SerializeField] float curAp;
    [SerializeField] float maxIp;
    [SerializeField] float curIp;
    #endregion
    #region Get
    public float CurStamina => curStamina;
    public float CurAp => curAp;
    public float CurIp => curIp;
    #endregion
    #region Func
    public bool CanConsumeStamina(float value)
    {
        return curStamina >= value;
    }
    public void SubStamina(float value)
    {
        curStamina -= value;
        curStamina = Mathf.Max(curStamina, 0);
    }
    public void AddStamina(float value)
    {
        curStamina += value;
        curStamina = Mathf.Min(curStamina, maxStamina);
    }
    public bool CanConsumeAp(float value)
    {
        return curAp >= value;
    }
    public void AddAp(float value)
    {
        curAp += value;
        curAp = Mathf.Min(curAp, maxAp);
    }
    public void SubAp(float value)
    {
        curAp -= value;
        curAp = Mathf.Max(curAp, 0);
    }
    public bool CanConsumeIp(float value)
    {
        return curIp >= value;
    }
    public void AddIp(float value)
    {
        curIp += value;
        curIp = Mathf.Min(curIp, maxIp);
    }
    public void SubIp(float value)
    {
        curIp -= value;
        curIp = Mathf.Max(curIp, 0);
    }
    #endregion
}

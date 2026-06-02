using UnityEngine;
using XFramework;

public class CommonUI : UIBase
{
    public override void Init()
    {
        UISystem.Instance.AddUI("CommonUI",this);
        
        Debug.Log("CommonUI Init");
    }
}

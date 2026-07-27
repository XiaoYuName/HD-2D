using UnityEngine.UI;
using XFramework;

public partial class SewingMachineSlotParent : UIBase
{
    private Image image;
    public override void Init()
    {
        InitAutoBind();
        image = GetComponent<Image>();
    }
    
}

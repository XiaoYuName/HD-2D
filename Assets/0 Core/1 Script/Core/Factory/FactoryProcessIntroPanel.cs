using UnityEngine;
using TMPro;
public class FactoryProcessIntroPanel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] FactoryComposedItemCellUI itemCell;
    [SerializeField] FactoryGameConfig config;
    
    public void Set(FactoryComposedItemInfo itemInfo)
    {
        itemCell.Set(itemInfo);
    }
}
// 帮我加入多语言： 体力值。已选周边。 消耗体力-30（用smart）。开始加工。
// 音乐开始后，加工产品会在传送带出现，需要在加工产品经过包装区域的时候按下对应按键，否则视为本次失败。残次品需要丢掉,否则会影响机器卡住其他需要加工的产品噢。


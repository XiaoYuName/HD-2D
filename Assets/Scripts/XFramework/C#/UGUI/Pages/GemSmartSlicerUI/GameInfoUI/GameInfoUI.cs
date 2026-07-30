using UnityEngine;
using XFramework;

public partial class GameInfoUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ClothingData clothingData)
    {
        bj2.sprite = LoadAsset<Sprite>(GamePathTools.CombinationClothingImagePath(clothingData.ClothingIconName));
        gemIcon.sprite = LoadAsset<Sprite>(GamePathTools.CombinationClothingGemIconPath(clothingData.GemIconName));
    }
}

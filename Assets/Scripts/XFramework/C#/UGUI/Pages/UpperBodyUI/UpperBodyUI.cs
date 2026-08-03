using System.Collections.Generic;
using XFramework;

public partial class UpperBodyUI : UIBase
{
    private CharacterBag characterBag;
    private ClothingBag clothingBag;
    private ClothingData clothingData;
    
    private List<UpperSlot> upperSlots =new List<UpperSlot>();
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,string.Empty);
    }

    public override void Release()
    {
        foreach (var slot in upperSlots)
        {
            slot.Close();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        upperSlots.Clear();
        base.Release();
    }

    public void SetData(CharacterBag characterBag, ClothingBag clothingBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;
        clothingData = LubanManager.Instance.TbClothingData.GetOrDefault(clothingBag.clothingID);
        if (clothingData != null)
        {
            foreach (var slot in upperSlots)
            {
                slot.Close();
                AssetsManager.Instance.FreeGameObject(slot.gameObject);
            }
            upperSlots.Clear();

            CreateUpperSlot();
        }
    }

    private void CreateUpperSlot()
    {
        foreach (var VARIABLE in clothingData.AccessoriesList)
        {
            
        }
    }

}

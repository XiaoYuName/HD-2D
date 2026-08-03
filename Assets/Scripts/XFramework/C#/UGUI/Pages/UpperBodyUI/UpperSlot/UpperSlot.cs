using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public partial class UpperSlot : UIBase, IPointerClickHandler
{
    public ClothingAccessoriesData AccessoriesData { get; private set; }

    private UpperBodyUI ParentUI;
    private bool isComplete;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ClothingAccessoriesData accessoriesData)
    {
        AccessoriesData = accessoriesData;
        icon.sprite =
            LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(accessoriesData.AccessoriesIconName));
        ParentUI = UISystem.Instance.GetUI<UpperBodyUI>(UIKeys.UpperBodyUI);
    }

    public void SetSelected(bool isSelected)
    {
        selected.gameObject.SetActive(isSelected);
    }

    public void SetIsComplete(bool isComplete)
    {
        this.isComplete = isComplete;
        complete.gameObject.SetActive(isComplete);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isComplete) return;
        if (eventData.button != PointerEventData.InputButton.Left || AccessoriesData == null || ParentUI == null)
        {
            return;
        }

        ParentUI.SelectedUpperSlot(this);
    }
}

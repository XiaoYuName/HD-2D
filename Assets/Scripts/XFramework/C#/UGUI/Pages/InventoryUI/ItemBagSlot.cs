using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public class ItemBagSlot : UIBase,IPointerClickHandler,IPointerDownHandler,IPointerUpHandler
{
    private GameObject itemSelected;
    private Image itemImg;
    private Image maskImage;
    private Mask imageMask;
    
    private Image frameImage;
    private TextMeshProUGUI itemAmount;

    private RectTransform SelectedNumberRect;
    private TextMeshProUGUI SelectedNumberText;
    private Button RemoveSelectedButton;
    
    private Action<ItemBagSlot> OnClick;
    public ItemData itemData { get; private set; }
    public ItemInfo  itemBag { get; private set; }
    
    private bool _pressed;
    private float _pressedTime;
    public float LongPressDuration = 0.2f;
    private float _longPressDuration;
    private bool _hasLongPressed;
    
    public UnityEvent onLongPress;
    public UnityEvent onRemove;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        itemSelected = Get("itemSelected");
        maskImage = Get<Image>("Mask");
        imageMask = Get<Mask>("Mask");
        
        itemImg = Get<Image>("Mask/itemImg");
        frameImage = Get<Image>("FrameImage");
        itemAmount = Get<TextMeshProUGUI>("itemAmount");

        SelectedNumberRect = Get<RectTransform>("SelectedNumberRect");
        SelectedNumberText = Get<TextMeshProUGUI>("SelectedNumberRect/SelectedNumberText");
        RemoveSelectedButton = Get<Button>("SelectedNumberRect/RemoveSelectedButton");
        Bind(RemoveSelectedButton,()=>onRemove?.Invoke(),"");
    }

    public void Release()
    {
        if (itemData != null)
        {
            itemImg.sprite = null;
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationItemIconPath(itemData.IconName));
            itemData = null;
        }
        itemBag = null;
    }

    public void SetData(ItemInfo itemBag,Action<ItemBagSlot> onClick = null)
    {
        Release();
        imageMask.enabled = false;
        frameImage.gameObject.SetActive(false);
        frameImage.enabled = false;
        if (!InventoryManager.Instance.HasItemData(itemBag))
        {
            SetRuntimeData(itemBag as RuntimeItemInfo);
        }
        else
        {
            itemData = InventoryManager.Instance.GetItemData(itemBag.ID);
            if (itemData != null)
            {
                itemImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(itemData.IconName));
            }
            
        }
        this.itemBag = itemBag;
        itemAmount.text = $"X{itemBag.Count}";
        OnClick = onClick;
        ActiveSelectedNumber(false);
    }

    #region 动态数据
    private void SetRuntimeData(RuntimeItemInfo itemInfo)
    {
        frameImage.gameObject.SetActive(true);
        frameImage.enabled = true;
        imageMask.enabled = true;
        if (itemInfo is FactoryComposedItemInfo factoryComposedItemInfo)
        {
            long frameId = factoryComposedItemInfo.FrameItemId;
            long paintingId = factoryComposedItemInfo.PaintingItemId;

            MoldFrameConfig moldFrameConfig = AssetsManager.Instance.LoadAssets<MoldFrameConfig>(AssetKeys.MoldFrameConfigPath);
            PaintingConfig paintingConfig = AssetsManager.Instance.LoadAssets<PaintingConfig>(AssetKeys.PaintingConfigPath);

            frameImage.SetIcon(moldFrameConfig.GetFramePath(frameId));
            maskImage.SetIcon(moldFrameConfig.GetMaskPath(frameId));
            itemImg.SetIcon(paintingConfig.GetComposedItemPath(paintingId, frameId));

            AssetsManager.Instance.FreeAsset(AssetKeys.MoldFrameConfigPath);
            AssetsManager.Instance.FreeAsset(AssetKeys.PaintingConfigPath);
        }
    }
    
    #endregion
  


    public void ActiveSelectedNumber(bool active)
    {
        SelectedNumberRect.gameObject.SetActive(active);
    }

    public void ShowSelectedNumber(int number)
    {
        SelectedNumberRect.gameObject.SetActive(true);
        SelectedNumberText.text = $"{number} / {itemBag.Count}";
    }

    public void SetSelected(bool selected)
    {
        itemSelected.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_hasLongPressed)
        {
            OnClick?.Invoke(this);
        }
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        _pressedTime = Time.realtimeSinceStartup;
        _longPressDuration = LongPressDuration;
        _hasLongPressed = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        _longPressDuration = LongPressDuration;
    }
    
    private void Update()
    {
        if (!_pressed)
        {
            return;
        }

        if (Time.realtimeSinceStartup - _pressedTime >= _longPressDuration)
        {
            _pressedTime = Time.realtimeSinceStartup;
            _longPressDuration  = Mathf.Max(0.06f,_longPressDuration - 0.1f);
            onLongPress.Invoke();
            _hasLongPressed = true;
        }
    }
}

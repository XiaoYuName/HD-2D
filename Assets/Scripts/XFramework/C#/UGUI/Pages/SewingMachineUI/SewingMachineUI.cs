using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public partial class SewingMachineUI : UIBase
{
    private Sprite CursorTexture;
    private SewingMachineGameData Setting;
    private SweingMachinePanel currentPanel;
    private GameObject runtimeIron;
    private RectTransform runtimeIronRect;
    private Canvas rootCanvas;

    public CharacterBag CurrentBag { get; private set; }
    public ClothingBag ClothingBag { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        Setting = LoadAsset<SewingMachineGameData>(AssetKeys.SewingMachineGameDataPath);
        CursorTexture = LoadAsset<Sprite>(AssetKeys.ShouPath);
        rootCanvas = GetComponentInParent<Canvas>();
        BindIronDragEvents();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Cursor.SetCursor(CursorTexture.texture, Vector2.zero, CursorMode.Auto);
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        EndIronDrag();
        ClearCurrentPanel();
        base.Close();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }


    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        CurrentBag = characterBag;
        ClothingBag = clothingBag;
        GenerateRandomPanel();
    }

    private void GenerateRandomPanel()
    {
        ClearCurrentPanel();

        if (Setting == null || Setting.Parents == null || Setting.Parents.Count <= 0)
        {
            Debug.LogWarning("SewingMachineGameData 未配置 Parents，无法生成缝纫机面板。");
            return;
        }

        int index = Random.Range(0, Setting.Parents.Count);
        string panelPath = Setting.Parents[index];
        if (string.IsNullOrEmpty(panelPath))
        {
            Debug.LogWarning("SewingMachineGameData Parents 中存在空路径，无法生成缝纫机面板。");
            return;
        }

        GameObject obj = AssetsManager.Instance.Instantiate(panelPath);
        RectTransform rectTransform = (RectTransform)obj.transform;
        rectTransform.SetParent(panelGroup, false);
        rectTransform.localScale = Vector3.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;

        currentPanel = obj.GetComponent<SweingMachinePanel>();
        if (currentPanel == null)
        {
            Debug.LogWarning($"生成的缝纫机面板缺少 SweingMachinePanel 组件：{panelPath}");
            AssetsManager.Instance.FreeGameObject(obj);
            return;
        }

        currentPanel.Init();
        currentPanel.SetData(Setting);
    }

    private void ClearCurrentPanel()
    {
        if (currentPanel == null)
        {
            return;
        }

        currentPanel.Release();
        AssetsManager.Instance.FreeGameObject(currentPanel.gameObject);
        currentPanel = null;
    }

    private void BindIronDragEvents()
    {
        if (iron == null)
        {
            Debug.LogWarning("SewingMachineUI 缺少 UIMask/Iron 的 EventTrigger，无法自动绑定熨斗拖拽。");
            return;
        }

        iron.triggers.Clear();
        AddIronDragEvent(EventTriggerType.BeginDrag, BeginIronDrag);
        AddIronDragEvent(EventTriggerType.Drag, DragIron);
        AddIronDragEvent(EventTriggerType.EndDrag, EndIronDrag);
        AddIronDragEvent(EventTriggerType.PointerUp, EndIronDrag);
    }

    private void AddIronDragEvent(EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(callback);
        iron.triggers.Add(entry);
    }

    private void BeginIronDrag(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointerEventData)
        {
            BeginIronDrag(pointerEventData);
        }
    }

    private void DragIron(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointerEventData)
        {
            DragIron(pointerEventData);
        }
    }

    private void EndIronDrag(BaseEventData eventData)
    {
        EndIronDrag();
    }

    private void BeginIronDrag(PointerEventData eventData)
    {
        if (currentPanel == null || !currentPanel.IsIronScratchReady)
        {
            return;
        }

        CreateRuntimeIron();
        DragIron(eventData);
    }

    private void DragIron(PointerEventData eventData)
    {
        if (runtimeIronRect == null || currentPanel == null)
        {
            return;
        }

        MoveRuntimeIron(eventData.position, eventData.pressEventCamera);
        currentPanel.UpdateIronScratch(eventData.position, GetEventCamera(eventData));
    }

    public void EndIronDrag()
    {
        currentPanel?.StopIronScratch();
        ClearRuntimeIron();
    }

    private void CreateRuntimeIron()
    {
        if (runtimeIron != null)
        {
            return;
        }

        runtimeIron = AssetsManager.Instance.Instantiate(AssetKeys.IronMonoPath);
        if (runtimeIron == null)
        {
            Debug.LogWarning($"SewingMachineUI 生成熨斗失败：{AssetKeys.IronMonoPath}");
            return;
        }

        runtimeIronRect = runtimeIron.transform as RectTransform;
        RectTransform parent = transform as RectTransform;
        runtimeIronRect.SetParent(parent, false);
        runtimeIronRect.localScale = Vector3.one;
        runtimeIronRect.localRotation = Quaternion.identity;
        runtimeIronRect.SetAsLastSibling();

        Graphic[] graphics = runtimeIron.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }

    private void MoveRuntimeIron(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform parent = runtimeIronRect.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Camera camera = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out Vector2 localPoint))
        {
            runtimeIronRect.anchoredPosition = localPoint;
        }
    }

    private void ClearRuntimeIron()
    {
        if (runtimeIron == null)
        {
            return;
        }

        AssetsManager.Instance.FreeGameObject(runtimeIron);
        runtimeIron = null;
        runtimeIronRect = null;
    }

    private Camera GetEventCamera(PointerEventData eventData)
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return eventData.pressEventCamera != null ? eventData.pressEventCamera : rootCanvas.worldCamera;
    }


    public void Complete()
    {
        UIUtility.PopClothingMinGameComplete(CurrentBag, ClothingBag, ClothingMinGameType.SewingMachine, Close);
       
    }

}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public partial class PuzzleUI : UIBase
{
    private PuzzleSettingData Setting;

    /// <summary>拼图格子的父节点,同时兼作拖拽时的临时层级</summary>
    private RectTransform slotPack;
    /// <summary>按"从上到下、从左到右"排好序的格子(zhi),索引即为正确的拼图顺序</summary>
    private readonly List<RectTransform> cellList = new List<RectTransform>();
    private readonly List<PuzzleSlot> slotList = new List<PuzzleSlot>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    // 复用,避免每次生成拼图都产生垃圾
    private readonly List<int> pieceOrder = new List<int>();
    private readonly List<int> pieceRotations = new List<int>();

    private CharacterBag CurrentBag;
    private ClothingBag ClothingBag;

    private bool isCompleted;
    /// <summary>当前按住的拼图块,按空格转的就是它</summary>
    private PuzzleSlot pressedSlot;

    public RectTransform DragLayer => slotPack;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
        slotPack = Get<RectTransform>("UIMask/SlotPack");
        CollectCells();
    }
    
    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        CurrentBag = characterBag;
        ClothingBag = clothingBag;
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Setting = LoadAsset<PuzzleSettingData>(AssetKeys.PuzzleSettingDataPath);
        PlayerInputManager.Instance.OnSpace += OnSpaceRotate;
        BuildPuzzle();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        PlayerInputManager.Instance.OnSpace -= OnSpaceRotate;
        ClearSlots();
    }

    /// <summary>按住某块拼图时记下它,松手时清掉(由 PuzzleSlot 调上来)</summary>
    public void SetPressedSlot(PuzzleSlot slot)
    {
        pressedSlot = slot;
    }

    public void ClearPressedSlot(PuzzleSlot slot)
    {
        if (pressedSlot == slot)
        {
            pressedSlot = null;
        }
    }

    /// <summary>
    /// 按住拼图块的同时按空格,把这一块转 +90°。
    /// 没按住任何一块时空格不做事,免得误触
    /// </summary>
    private void OnSpaceRotate()
    {
        if (!isOpen || pressedSlot == null)
        {
            return;
        }

        pressedSlot.Rotate();
        CheckCompleted();
    }

    /// <summary>
    /// 收集 SlotPack 下的所有格子并按网格顺序(上->下,左->右)排序,同时算出列数
    /// </summary>
    private void CollectCells()
    {
        cellList.Clear();
        if (slotPack == null)
        {
            Debug.LogError("PuzzleUI 未找到 UIMask/SlotPack 节点");
            return;
        }

        for (int i = 0; i < slotPack.childCount; i++)
        {
            if (slotPack.GetChild(i) is RectTransform cell)
            {
                cellList.Add(cell);
            }
        }

        // 取整比较,保证排序结果稳定(格子坐标本身就是整数)
        cellList.Sort((a, b) =>
        {
            int ay = Mathf.RoundToInt(a.anchoredPosition.y);
            int by = Mathf.RoundToInt(b.anchoredPosition.y);
            if (ay != by)
            {
                return by.CompareTo(ay);
            }
            return Mathf.RoundToInt(a.anchoredPosition.x).CompareTo(Mathf.RoundToInt(b.anchoredPosition.x));
        });
    }

    /// <summary>
    /// 随机挑选一组拼图素材,打乱后生成到每个格子里
    /// </summary>
    private void BuildPuzzle()
    {
        ClearSlots();
        isCompleted = false;

        if (Setting == null || Setting.PuzzleGroups == null || Setting.PuzzleGroups.Count == 0)
        {
            Debug.LogError("拼图配置为空,无法生成拼图");
            return;
        }

        if (cellList.Count == 0)
        {
            Debug.LogError("SlotPack 下没有拼图格子,无法生成拼图");
            return;
        }

        var group = Setting.PuzzleGroups[Random.Range(0, Setting.PuzzleGroups.Count)];
        if (group == null || group.PuzzleSprites == null || group.PuzzleSprites.Count == 0)
        {
            Debug.LogError("随机到的拼图素材组为空,无法生成拼图");
            return;
        }

        int count = Mathf.Min(cellList.Count, group.PuzzleSprites.Count);
        if (group.PuzzleSprites.Count != cellList.Count)
        {
            Debug.LogWarning($"拼图素材数量({group.PuzzleSprites.Count})与格子数量({cellList.Count})不一致,按 {count} 个生成");
        }

        BuildShuffledLayout(count);
        for (int i = 0; i < count; i++)
        {
            var cell = cellList[i];
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.PuzzleSlotPath);
            obj.transform.SetParent(cell, false);

            var slot = obj.GetComponent<PuzzleSlot>();
            slot.Init();
            slot.SetData(this, i, cell);
            slot.SetPiece(pieceOrder[i], group.PuzzleSprites[pieceOrder[i]], pieceRotations[i]);
            slotList.Add(slot);
        }
    }

    private void ClearSlots()
    {
        pressedSlot = null;
        foreach (var slot in slotList)
        {
            if (slot == null)
            {
                continue;
            }
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        slotList.Clear();
    }

    /// <summary>
    /// 打乱摆放顺序和朝向,避免一开局就是正确答案。
    /// pieceOrder[i] = 第 i 格摆哪一块, pieceRotations[i] = 这一块转几个 90°
    /// </summary>
    private void BuildShuffledLayout(int count)
    {
        pieceOrder.Clear();
        pieceRotations.Clear();
        for (int i = 0; i < count; i++)
        {
            pieceOrder.Add(i);
            pieceRotations.Add(0);
        }

        if (count <= 0)
        {
            return;
        }

        const int maxShuffleTimes = 10;
        for (int times = 0; times < maxShuffleTimes; times++)
        {
            // 位置打乱
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pieceOrder[i], pieceOrder[j]) = (pieceOrder[j], pieceOrder[i]);
            }

            // 朝向打乱,每块随机转 0~3 个 90°
            for (int i = 0; i < count; i++)
            {
                pieceRotations[i] = Random.Range(0, PuzzleSlot.RotationStepCount);
            }

            if (!IsLayoutSolved())
            {
                break;
            }
        }
    }

    /// <summary>每一块都在自己格子里且朝向摆正</summary>
    private bool IsLayoutSolved()
    {
        for (int i = 0; i < pieceOrder.Count; i++)
        {
            if (pieceOrder[i] != i || pieceRotations[i] != 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 查找指针下方的拼图块(供 PuzzleSlot 拖拽结束时调用)
    /// </summary>
    public PuzzleSlot FindSlotUnderPointer(PointerEventData eventData, PuzzleSlot ignore)
    {
        if (EventSystem.current == null)
        {
            return null;
        }

        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        foreach (var result in raycastResults)
        {
            var slot = result.gameObject.GetComponentInParent<PuzzleSlot>();
            if (slot == null && result.gameObject.transform is RectTransform hitRect && cellList.Contains(hitRect))
            {
                // 射线打到格子(zhi)本身时,取它下面挂的拼图块
                slot = hitRect.GetComponentInChildren<PuzzleSlot>();
            }

            if (slot != null && slot != ignore && slotList.Contains(slot))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// 尝试交换两个拼图块,任意两格都能换,不要求相邻
    /// </summary>
    public bool TrySwapPiece(PuzzleSlot from, PuzzleSlot to)
    {
        if (from == null || to == null || from == to)
        {
            return false;
        }

        // 朝向跟着拼图块一起换过去,它是块自己的状态而不是格子的
        int fromPieceIndex = from.PieceIndex;
        var fromSprite = from.PieceSprite;
        int fromRotation = from.RotationStep;
        from.SetPiece(to.PieceIndex, to.PieceSprite, to.RotationStep);
        to.SetPiece(fromPieceIndex, fromSprite, fromRotation);

        CheckCompleted();
        return true;
    }

    private void CheckCompleted()
    {
        bool solved = true;
        foreach (var slot in slotList)
        {
            // 位置对了还不够,朝向也得摆正
            if (slot == null || slot.PieceIndex != slot.CellIndex || slot.RotationStep != 0)
            {
                solved = false;
                break;
            }
        }

        if (solved && !isCompleted)
        {
            Debug.Log("拼图完成!");
            UIUtility.PopClothingMinGameComplete(CurrentBag, ClothingBag, ClothingMinGameType.Puzzle, Close);
        }
        isCompleted = solved;
    }
}

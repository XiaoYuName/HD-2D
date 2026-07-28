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
    
    private CharacterBag CurrentBag;
    private ClothingBag ClothingBag;

    private int columnCount;
    private bool isCompleted;

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
        BuildPuzzle();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ClearSlots();
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

        // 第一行的格子数量即为列数
        columnCount = 0;
        if (cellList.Count > 0)
        {
            int firstRowY = Mathf.RoundToInt(cellList[0].anchoredPosition.y);
            foreach (var cell in cellList)
            {
                if (Mathf.RoundToInt(cell.anchoredPosition.y) != firstRowY)
                {
                    break;
                }
                columnCount++;
            }
        }
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

        var order = BuildShuffledOrder(count);
        for (int i = 0; i < count; i++)
        {
            var cell = cellList[i];
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.PuzzleSlotPath);
            obj.transform.SetParent(cell, false);

            var slot = obj.GetComponent<PuzzleSlot>();
            slot.Init();
            slot.SetData(this, i, cell);
            slot.SetPiece(order[i], group.PuzzleSprites[order[i]]);
            slotList.Add(slot);
        }
    }

    private void ClearSlots()
    {
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
    /// 生成打乱后的拼图顺序,避免一开局就是正确答案
    /// </summary>
    private List<int> BuildShuffledOrder(int count)
    {
        var order = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            order.Add(i);
        }

        if (count <= 1)
        {
            return order;
        }

        const int maxShuffleTimes = 10;
        for (int times = 0; times < maxShuffleTimes; times++)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            if (!IsOrderSolved(order))
            {
                break;
            }
        }

        return order;
    }

    private bool IsOrderSolved(List<int> order)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] != i)
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
    /// 尝试交换两个拼图块,只有上下左右相邻才允许交换
    /// </summary>
    public bool TrySwapPiece(PuzzleSlot from, PuzzleSlot to)
    {
        if (from == null || to == null || from == to)
        {
            return false;
        }

        if (!IsNeighbour(from.CellIndex, to.CellIndex))
        {
            return false;
        }

        int fromPieceIndex = from.PieceIndex;
        var fromSprite = from.PieceSprite;
        from.SetPiece(to.PieceIndex, to.PieceSprite);
        to.SetPiece(fromPieceIndex, fromSprite);

        CheckCompleted();
        return true;
    }

    private bool IsNeighbour(int cellIndexA, int cellIndexB)
    {
        if (columnCount <= 0)
        {
            return false;
        }

        int rowA = cellIndexA / columnCount;
        int colA = cellIndexA % columnCount;
        int rowB = cellIndexB / columnCount;
        int colB = cellIndexB % columnCount;
        return Mathf.Abs(rowA - rowB) + Mathf.Abs(colA - colB) == 1;
    }

    private void CheckCompleted()
    {
        bool solved = true;
        foreach (var slot in slotList)
        {
            if (slot == null || slot.PieceIndex != slot.CellIndex)
            {
                solved = false;
                break;
            }
        }

        if (solved && !isCompleted)
        {
            Debug.Log("拼图完成!");
        }
        isCompleted = solved;
    }
}

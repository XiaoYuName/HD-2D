using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 商店帮忙小游戏状态机：开局随机取 2~4 种货物（来自 <see cref="TbSuperMarketShopData"/>），
/// 按「格子总数 / 品类数」平均分配数量（除最后一类外各带 ±随机浮动），玩家在倒计时内把所有货物
/// 从右侧箱子拖到中央货架上；每格只放 1 件，拖到已占用格会替换（原货物退回其箱子）。
/// 倒计时内摆满 = 胜利，超时/主动结束时未摆满 = 失败。只负责数据与规则，UI 由 <see cref="ShopHelpPanel"/> 驱动。
/// </summary>
public class ShopHelpGameManager : MonoBehaviour
{
    public static ShopHelpGameManager St;

    [LabelText("配置")]
    [SerializeField] ShopHelpGameConfig config;

    [LabelText("进入面板消耗配置")]
    [SerializeField] GameEnterPanelConfig enterConfig;
    [LabelText("本游戏在进入配置中的面板Id")]
    [SerializeField] string enterPanelId = "ShopHelpPanel";

    public ShopHelpGameConfig Config => config;

    public enum GameState
    {
        /// <summary>待机：未开局（刚加载或结算后）。</summary>
        Idle,
        /// <summary>进行中：倒计时中，可拖拽摆放。</summary>
        Playing,
        /// <summary>本局结束：已结算。</summary>
        Ended,
    }

    /// <summary>一种货物：绑定一件真实物品，携带图标/总数/已摆放数。</summary>
    public class GoodsType
    {
        public long ItemId;
        public string IconPath;   // AA Key，UI 直接 SetIcon
        public int Total;         // 本局该类型总数
        public int Placed;        // 已摆上货架的数量
        /// <summary>箱子里剩余可拖的数量。</summary>
        public int Remaining => Mathf.Max(0, Total - Placed);
    }

    /// <summary>一次摆放的结果。</summary>
    public struct PlaceResult
    {
        public bool Placed;         // 是否真的发生了摆放
        public int DisplacedType;   // 被替换下来退回箱子的品类下标；-1 表示没有替换
    }

    #region 事件
    /// <summary>状态变化。</summary>
    public event Action<GameState> OnStateChanged;
    /// <summary>倒计时刷新：剩余秒数。</summary>
    public event Action<float> OnTimeChanged;
    /// <summary>开局：货物与货架已重建，UI 需整体重建箱子与货架。</summary>
    public event Action OnSetup;
    /// <summary>本局结束：是否胜利、发放金币、发放好感度。</summary>
    public event Action<bool, int, int> OnGameEnd;
    #endregion

    #region 运行时状态
    GameState state = GameState.Idle;
    readonly List<GoodsType> goods = new List<GoodsType>();
    int[] grid;             // 每格 → 货物品类下标；-1 = 空
    float timeLeft;

    public GameState State => state;
    public IReadOnlyList<GoodsType> Goods => goods;
    public int SlotCount => grid?.Length ?? 0;
    public float TimeLeft => timeLeft;

    /// <summary>某格当前货物品类下标，-1 为空。</summary>
    public int SlotType(int slot) => grid != null && slot >= 0 && slot < grid.Length ? grid[slot] : -1;
    /// <summary>是否已摆满（胜利条件）。</summary>
    public bool AllPlaced
    {
        get
        {
            if(grid == null)
                return false;
            for(int i = 0; i < grid.Length; i++)
                if(grid[i] < 0)
                    return false;
            return true;
        }
    }
    #endregion

    void Awake()
    {
        St = this;
    }

    void Update()
    {
        if(state != GameState.Playing)
            return;

        timeLeft -= Time.deltaTime;
        if(timeLeft <= 0f)
        {
            timeLeft = 0f;
            OnTimeChanged?.Invoke(0f);
            EndGame(AllPlaced);   // 超时：摆满算胜，否则失败
            return;
        }
        OnTimeChanged?.Invoke(timeLeft);
    }

    #region 开局
    /// <summary>
    /// 开始一局。consume=true 时先校验并扣除进入消耗（来自 GameEnterPanelConfig 对应面板条目）；不足返回 false。
    /// </summary>
    public bool StartGame(bool consume = true)
    {
        if(config == null)
        {
            Debug.LogError("[ShopHelpGameManager] 配置为空，无法开局。", this);
            return false;
        }

        if(consume)
        {
            if(!HasEnough())
                return false;
            Dictionary<PropertyType, int> c = Consumes();
            if(c != null)
                foreach(KeyValuePair<PropertyType, int> kv in c)
                    GameDataManager.Instance.RemoveProperty(kv.Key, kv.Value);
        }

        if(!BuildGoods())
            return false;

        grid = new int[config.TotalSlots];
        for(int i = 0; i < grid.Length; i++)
            grid[i] = -1;

        timeLeft = config.CountdownSeconds;

        SetState(GameState.Playing);
        OnSetup?.Invoke();
        OnTimeChanged?.Invoke(timeLeft);
        return true;
    }

    // 随机取 2~4 种真实货物并按公式分配数量；数据源不足时返回 false。
    bool BuildGoods()
    {
        goods.Clear();

        var pool = new List<SuperMarketShopData>(LubanManager.Instance.TbSuperMarketShopData.DataList);
        if(pool.Count == 0)
        {
            Debug.LogError("[ShopHelpGameManager] TbSuperMarketShopData 为空，无法生成货物。", this);
            return false;
        }

        int wantMin = Mathf.Min(config.MinGoodsTypes, pool.Count);
        int wantMax = Mathf.Min(config.MaxGoodsTypes, pool.Count);
        int typeCount = UnityEngine.Random.Range(wantMin, wantMax + 1);

        // 洗牌取前 typeCount 个不重复物品
        for(int i = 0; i < typeCount; i++)
        {
            int r = UnityEngine.Random.Range(i, pool.Count);
            (pool[i], pool[r]) = (pool[r], pool[i]);

            SuperMarketShopData src = pool[i];
            goods.Add(new GoodsType
            {
                ItemId = src.ItemID,
                IconPath = ResolveIcon(src.ItemID),
                Total = 0,
                Placed = 0,
            });
        }

        DistributeQuantities(typeCount);
        return true;
    }

    // 图标优先取 ShopHelpGameConfig 按 ItemID 的配置；未配置时回退物品数据库图标以免留白。
    string ResolveIcon(long itemId)
    {
        string icon = config.GetIconPath(itemId);
        if(!string.IsNullOrEmpty(icon))
            return icon;

        ItemData item = InventoryManager.Instance.GetItemData(itemId);
        if(item != null)
            return GamePathTools.CombinationItemIconPath(item.IconName);

        Debug.LogWarning($"[ShopHelpGameManager] 物品 {itemId} 未在 ShopHelpGameItemConfig 配置图标，且物品库无图标。", this);
        return null;
    }

    // 本局进入/再来一局消耗（来自 GameEnterPanelConfig 对应面板条目）；未配置返回 null。
    Dictionary<PropertyType, int> Consumes()
    {
        if(enterConfig != null && enterConfig.DataDict != null
           && enterConfig.DataDict.TryGetValue(enterPanelId, out GameEnterPanelItemData d))
            return d.Consumes;
        return null;
    }

    /// <summary>玩家资源是否够进入/再来一局。</summary>
    public bool HasEnough()
    {
        Dictionary<PropertyType, int> c = Consumes();
        if(c == null)
            return true;
        foreach(KeyValuePair<PropertyType, int> kv in c)
            if(!GameDataManager.Instance.HasProperty(kv.Key, kv.Value))
                return false;
        return true;
    }

    // 数量分配：平均数 = 格子总数 / 品类数；除最后一类外各 = 平均数 + 随机(±range)，并夹取以保证每类 ≥1；
    // 最后一类 = 格子总数 − 前面各类之和，使总和恰好铺满货架。
    void DistributeQuantities(int typeCount)
    {
        int total = config.TotalSlots;
        int avg = total / typeCount;
        int range = config.QuantityRandomRange;
        int remaining = total;

        for(int i = 0; i < typeCount; i++)
        {
            int q;
            if(i == typeCount - 1)
            {
                q = remaining;   // 最后一类兜底，保证总和 = total
            }
            else
            {
                q = avg + UnityEngine.Random.Range(-range, range + 1);
                int typesLeftAfter = typeCount - 1 - i;
                // 至少给自己 1，且给后面每类各留 1
                q = Mathf.Clamp(q, 1, remaining - typesLeftAfter);
            }
            goods[i].Total = q;
            remaining -= q;
        }
    }
    #endregion

    #region 交互
    /// <summary>
    /// 拖拽 <paramref name="draggingType"/> 品类扫过 <paramref name="slot"/> 格时尝试摆放。
    /// 空格直接放；已占用且品类不同则替换（原品类退回箱子）；同品类或剩余为 0 时不动作。
    /// </summary>
    public PlaceResult TryPlaceInto(int slot, int draggingType)
    {
        PlaceResult result = new PlaceResult { Placed = false, DisplacedType = -1 };

        if(state != GameState.Playing || grid == null || slot < 0 || slot >= grid.Length)
            return result;
        if(draggingType < 0 || draggingType >= goods.Count)
            return result;

        int cur = grid[slot];
        if(cur == draggingType)              // 已经是同一品类，无需变化
            return result;
        if(goods[draggingType].Remaining <= 0)   // 箱子里没货可放
            return result;

        if(cur >= 0)                         // 替换：原货物退回它的箱子
        {
            goods[cur].Placed--;
            result.DisplacedType = cur;
        }

        grid[slot] = draggingType;
        goods[draggingType].Placed++;
        result.Placed = true;

        if(AllPlaced)
            EndGame(true);

        return result;
    }
    #endregion

    #region 结算
    /// <summary>主动结束本局（结束本局按钮）。摆满则胜，否则败。</summary>
    public void ManualEnd()
    {
        EndGame(AllPlaced);
    }

    /// <summary>结束本局并结算。重复调用无效。</summary>
    public void EndGame(bool win)
    {
        if(state != GameState.Playing)
            return;

        int coin = win ? config.RewardCoin : 0;
        int favor = win ? config.RewardFavor : 0;
        if(win)
            GrantRewards(coin, favor);

        SetState(GameState.Ended);
        OnGameEnd?.Invoke(win, coin, favor);
    }

    // 发放奖励：金币入账（GameCoin），好感度待接入女主属性系统（暂记录日志）。
    void GrantRewards(int coin, int favor)
    {
        if(coin > 0)
            GameDataManager.Instance.AddProperty(PropertyType.GameCoin, coin);

        if(favor > 0)
            Debug.Log($"[ShopHelpGameManager] 女主好感度 +{favor}。（待接入女主属性系统）");
    }
    #endregion

    void SetState(GameState s)
    {
        state = s;
        OnStateChanged?.Invoke(s);
    }
}

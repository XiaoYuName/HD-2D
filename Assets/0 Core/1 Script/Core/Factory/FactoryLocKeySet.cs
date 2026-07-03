/// <summary>
/// 工厂模块全部多语言 Key 常量集中管理（对应 Factory 字符串表 / Data/Factory/*.csv）。
/// 代码里一律引用本类常量，便于统一修改与检索；新增文案时同步在此登记并补进 CSV。
/// </summary>
public static class FactoryLocKeySet
{
    // 通用（多个面板共用）
    public const string Back = "FactoryProcBack";
    public const string ConfirmSelect = "FactoryConfirmSelect";
    public const string UnitPriceFmt = "FactoryUnitPriceFmt";   // "¥{Price}/个"

    // 下压小游戏（FactoryProcessPanel）
    public static class Process
    {
        public const string MiniGameTitle = "FactoryProcMiniGameTitle";   // 左上角返回键文案「小游戏」
        public const string StatusTitle = "FactoryProcStatusTitle";
        public const string ScoreLabel = "FactoryProcScoreLabel";
        public const string CompletionLabel = "FactoryProcCompletionLabel";
        public const string SuccessLabel = "FactoryProcSuccessLabel";
        public const string FailLabel = "FactoryProcFailLabel";
        public const string Cheer = "FactoryProcCheer";
        public const string AimZone = "FactoryProcAimZone";
        public const string PressHint = "FactoryProcPressHint";
        public const string Rule = "FactoryProcRule";
        public const string EndRound = "FactoryProcEndRound";
        public const string Good = "FactoryProcGood";
        public const string Ok = "FactoryProcOk";
        public const string Bad = "FactoryProcBad";
        public const string NotEnoughStamina = "FactoryProcNotEnoughStamina";
        public const string SettleTitle = "FactoryProcSettleTitle";
        public const string SettleSpeech = "FactoryProcSettleSpeech";
        public const string SettleContent = "FactoryProcSettleContent";   // 含 {Score}{Success}{Fail}{Completion}{Reward}
    }

    // 加工厂主界面（FactoryMainPanel）
    public static class Main
    {
        public const string Title = "FactoryTitle";
        public const string TabProcess = "FactoryTabProcess";
        public const string TabRecycle = "FactoryTabRecycle";   // 已停用，保留备份
        public const string TabUpgrade = "FactoryTabUpgrade";   // 升级设备（替换原回收站 Tab）
        public const string MoldManage = "FactoryMoldManage";   // 模具管理
        public const string SelectMaterial = "FactorySelectMaterial";
        public const string MakeProduct = "FactoryMakeProduct";
        public const string StartProcess = "FactoryStartProcess";
        public const string LevelFmt = "FactoryLevelFmt";                 // "工厂等级 LV{Level}"
        public const string CoopFmt = "FactoryCoopFmt";                   // "合作值 {CoopCur}/{CoopMax}"
        public const string LevelHint = "FactoryLevelHint";
        public const string TotalCostLabel = "FactoryTotalCostLabel";
        public const string CraftCountFmt = "FactoryCraftCountFmt";       // "×{Count}件"
        public const string NpcGreeting = "FactoryNpcGreeting";
        public const string RecycleComingSoon = "FactoryRecycleComingSoon";
        public const string NeedProduct = "FactoryNeedProduct";
    }

    // 选择子面板（添加素材 / 选产品种类）
    public static class Select
    {
        public const string AddMaterialTitle = "FactoryAddMaterialTitle";
        public const string SelectProductTitle = "FactorySelectProductTitle";
    }

    // 物料制作面板（FactoryMoldMgPanel）：选框架 + 选贴纸 → 完成制作产出生产资料
    public static class Mold
    {
        public const string Title = "FactoryMoldTitle";                       // 物料制作模板
        public const string TabFrame = "FactoryMoldTabFrame";                 // 框架
        public const string TabSticker = "FactoryMoldTabSticker";             // 贴纸
        public const string Complete = "FactoryMoldComplete";                 // 完成制作
        public const string Exit = "FactoryMoldExit";                         // 退出
        public const string FramePriceFmt = "FactoryMoldFramePriceFmt";       // 框架 +{Price}
        public const string StickerPriceFmt = "FactoryMoldStickerPriceFmt";   // 贴纸 +{Price}
        public const string SellPriceFmt = "FactoryMoldSellPriceFmt";         // 预估售出价格：¥{Price}/件（框架+贴纸售价之和）
        public const string PriceFmt = "FactoryMoldPriceFmt";                 // 预估制作成本：¥{Price}/件（工厂批量制作成本，暂占位）
        public const string NeedFrame = "FactoryMoldNeedFrame";               // 请先选择框架
        public const string NeedSticker = "FactoryMoldNeedSticker";           // 请先放置贴纸
        public const string NotEnoughSticker = "FactoryMoldNotEnoughSticker"; // 贴纸数量不足
        public const string StickerLimit = "FactoryMoldStickerLimit";         // 贴纸已达上限
        public const string CraftSuccess = "FactoryMoldCraftSuccess";         // 制作成功！
        public const string NoRecipe = "FactoryMoldNoRecipe";                 // 该框架+贴纸组合暂无合成配方（合成表/ItemConfig 未配置）
        public const string Empty = "FactoryMoldEmpty";                       // 暂无可用的框架 / 贴纸

        // 贴纸功能框
        public const string StickerMirror = "FactoryMoldStickerMirror";       // 镜像翻转
        public const string StickerLayerUp = "FactoryMoldStickerLayerUp";     // 图层往上
        public const string StickerLayerDown = "FactoryMoldStickerLayerDown"; // 图层往下
        public const string StickerDelete = "FactoryMoldStickerDelete";       // 删除
        // 框架(物料)工具
        public const string FrameAdjust = "FactoryMoldFrameAdjust";           // 图片调整
        public const string FrameMirror = "FactoryMoldFrameMirror";           // 物料镜像
        public const string FrameTurn = "FactoryMoldFrameTurn";               // 物料转向
    }

    // 升级设备标签内容（FactoryUpgradePanel / FactoryUpgradeCellUI）
    public static class Upgrade
    {
        public const string Title = "FactoryUpgradeTitle";           // 升级设备
        public const string Max = "FactoryUpgradeMax";               // MAX（满级标识文本，如用 TMP）
        public const string Button = "FactoryUpgradeButton";         // 升级
        public const string BonusYield = "FactoryUpgradeBonusYield"; // 良品率
        public const string BonusVolume = "FactoryUpgradeBonusVolume"; // 生产量
        public const string NextEffect = "FactoryUpgradeNextEffect"; // 升级效果→（格子第二行前缀）
        public const string NotEnough = "FactoryUpgradeNotEnough";   // 金币不足
        public const string Maxed = "FactoryUpgradeMaxed";           // 已满级
        public const string Upgraded = "FactoryUpgradeUpgraded";     // 升级成功！
        public const string Empty = "FactoryUpgradeEmpty";           // 暂无可升级设备
    }

    // 回收站标签内容（FactoryRecyclePanel，已停用、保留备份）
    public static class Recycle
    {
        public const string PriceTitle = "FactoryRecyclePriceTitle";             // 回收价格：
        public const string AddHint = "FactoryRecycleAddHint";                   // 长按可加速添加
        public const string IncomeLabel = "FactoryRecycleIncomeLabel";           // 预期收入：
        public const string Sell = "FactoryRecycleSell";                         // 回收出售
        public const string SortDefault = "FactoryRecycleSortDefault";           // 默认排序
        public const string Empty = "FactoryRecycleEmpty";                       // 暂无可回收的周边
        public const string NothingSelected = "FactoryRecycleNothingSelected";   // 请先选择要回收的周边
        public const string Sold = "FactoryRecycleSold";                         // 出售成功！
    }

    // 本局结算面板（FactorySettlePanel）；面板自有文案集中在 Data/Factory/FactorySettlePanel.csv，便于单表审阅 / 一键合并。
    // 产品卡单价沿用通用格式 <see cref="UnitPriceFmt"/>（属共用产品基建，落在 FactoryMainPanel.csv）。
    public static class Settle
    {
        public const string Title = "FactorySettleTitle";                        // 本局结算
        public const string Speech = "FactorySettleSpeech";                      // 台词：挺能干的
        public const string ScoreLabel = "FactorySettleScore";                   // 分数
        public const string SuccessLabel = "FactorySettleSuccess";               // 制作成功
        public const string CompletionLabel = "FactorySettleCompletion";         // 完成率
        public const string SaleMultiplierLabel = "FactorySettleSaleMultiplier"; // 售价倍率
        public const string ItemHint = "FactorySettleItemHint";                  // 道具已自动发放进背包
        public const string Back = "FactorySettleBack";                          // 返回
    }

    // 物料结算面板（FactoryMoldSettlePanel）：FactoryMoldMgPanel 完成制作后展示产出清单。
    // 自有文案（标题/台词）集中在 Data/Factory/FactoryMoldSettlePanel.csv；道具提示 / 返回文案与 <see cref="Settle"/> 完全一致，直接复用。
    public static class MoldSettle
    {
        public const string Title = "FactoryMoldSettleTitle";     // 物料结算
        public const string Speech = "FactoryMoldSettleSpeech";   // 干得漂亮
    }
}

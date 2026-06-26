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
        public const string TabRecycle = "FactoryTabRecycle";
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

    // 回收站标签内容（FactoryRecyclePanel）
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
}

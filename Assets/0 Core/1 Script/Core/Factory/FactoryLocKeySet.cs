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
}

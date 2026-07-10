/// <summary>
/// 工厂模块全部多语言 Key 常量集中管理（对应 Factory 字符串表 / Data/Factory/*.csv）。
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
        public const string SuccessLabel = "FactoryProcSuccessLabel";
        public const string FailLabel = "FactoryProcFailLabel";
        public const string Cheer = "FactoryProcCheer";
        // 战况栏改版（新原型图：本次加工数量/残次品率/失败产品/完成生产，UI 尚未接入项目，键先备好）
        public const string CraftCountLabel = "FactoryProcCraftCountLabel";     // 本次加工数量
        public const string UnitPiece = "FactoryProcUnitPiece";                // 个（独立单位后缀，英文留空）
        public const string DefectRateLabel = "FactoryProcDefectRateLabel";    // 残次品率
        public const string FailProductLabel = "FactoryProcFailProductLabel";  // 失败产品
        public const string DoneLabel = "FactoryProcDoneLabel";                // 完成生产
        public const string PackZone = "FactoryProcPackZone";                  // 包装区域（替代原瞄准区）
        public const string HowToPlayTitle = "FactoryProcHowToPlayTitle";      // 游戏玩法
        public const string HowToPlayDesc = "FactoryProcHowToPlayDesc";        // 玩法说明正文
        public const string InputLeftHint = "FactoryProcInputLeftHint";        // 点击鼠标左键或键盘←
        public const string InputRightHint = "FactoryProcInputRightHint";      // 点击鼠标右键或键盘→
        public const string InputMiddleHint = "FactoryProcInputMiddleHint";    // 点击鼠标中键或键盘↑
        public const string Exit = "FactoryProcExit";                         // 退出
        public const string EndRound = "FactoryProcEndRound";                 // 退出（音游面板右下角按钮，预制体沿用旧 Key）
        public const string Left = "FactoryProcLeft";                         // 左
        public const string Right = "FactoryProcRight";                       // 右
        public const string Middle = "FactoryProcMiddle";                     // 中
        public const string Defective = "FactoryProcDefective";               // 残次品（玩法说明第三个键位标签）
        // 提前结束确认面板（FactoryProcessEndConfirmPanel）
        public const string EndConfirmTitle = "FactoryProcEndConfirmTitle";     // 标题：提示
        public const string EndConfirmContent = "FactoryProcEndConfirmContent"; // 正文：提前结束后，本局将什么也不会获得。
        public const string EndConfirmOk = "FactoryProcEndConfirmOk";           // 确认结束
        public const string EndConfirmCancel = "FactoryProcEndConfirmCancel";   // 取消
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
        public const string ProcessButton = "FactoryProcessButton";
        public const string CoopFmt = "FactoryCoopFmt";                   // 已停用：合作值已按设计图移除，Key 保留备份
        public const string VolumeLabel = "FactoryVolumeLabel";           // 当前产量（左侧状态栏标签）
        public const string VolumeFmt = "FactoryVolumeFmt";               // "{Volume}/次"
        public const string YieldLabel = "FactoryYieldLabel";             // 产出良品率（左侧状态栏标签）
        public const string YieldFmt = "FactoryYieldFmt";                 // "{Yield}%"
        public const string TotalCostLabel = "FactoryTotalCostLabel";
        public const string CraftCountFmt = "FactoryCraftCountFmt";       // "×{Count}件"
        public const string NpcGreeting = "FactoryNpcGreeting";
        public const string RecycleComingSoon = "FactoryRecycleComingSoon";
        public const string NeedProduct = "FactoryNeedProduct";
    }

    // 加工确认弹窗（FactoryProcessIntroPanel，点「加工」后先弹此窗确认再进小游戏）；文案与主界面一并落在 FactoryMainPanel.csv
    public static class Intro
    {
        public const string StaminaLabel = "FactoryStaminaLabel";             // 体力值
        public const string SelectedGoodsLabel = "FactorySelectedGoodsLabel"; // 已选周边
        public const string ConsumeStaminaFmt = "FactoryConsumeStaminaFmt";   // 消耗体力-{Consume}
        public const string StartButton = "FactoryIntroStartButton";          // 开始加工
        public const string RuleDesc = "FactoryIntroRuleDesc";                // 玩法规则说明正文
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
        public const string TabSticker = "FactoryMoldTabSticker";             // 画稿
        public const string Complete = "FactoryMoldComplete";                 // 完成制作
        public const string Exit = "FactoryMoldExit";                         // 退出
        public const string FramePriceFmt = "FactoryMoldFramePriceFmt";       // 框架 +{Price}
        public const string StickerPriceFmt = "FactoryMoldStickerPriceFmt";   // 画稿 +{Price}
        public const string SellPriceFmt = "FactoryMoldSellPriceFmt";         // 预估售出价格：¥{Price}/件（框架+贴纸售价之和）
        public const string PriceFmt = "FactoryMoldPriceFmt";                 // 预估制作成本：¥{Price}/件（工厂批量制作成本，暂占位）
        public const string NeedFrame = "FactoryMoldNeedFrame";               // 请先选择框架
        public const string NeedSticker = "FactoryMoldNeedSticker";           // 请先放置画稿
        public const string NotEnoughSticker = "FactoryMoldNotEnoughSticker"; // 画稿数量不足
        public const string StickerLimit = "FactoryMoldStickerLimit";         // 画稿已达上限
        public const string CraftSuccess = "FactoryMoldCraftSuccess";         // 制作成功！
        public const string NoRecipe = "FactoryMoldNoRecipe";                 // 该框架+画稿组合暂无合成配方（合成表/ItemConfig 未配置）
        public const string Empty = "FactoryMoldEmpty";                       // 暂无可用的框架 / 画稿
        public const string IllustrationEmptyTip = "FactoryMoldIllustrationEmptyTip";   // 暂无画图稿件，快去催马吉画稿吧！（画稿 SV 列表为空时中间提示）

        // 贴纸功能框
        public const string StickerMirror = "FactoryMoldStickerMirror";       // 镜像翻转
        public const string StickerLayerUp = "FactoryMoldStickerLayerUp";     // 图层往上
        public const string StickerLayerDown = "FactoryMoldStickerLayerDown"; // 图层往下
        public const string StickerDelete = "FactoryMoldStickerDelete";       // 删除
        // 框架(物料)工具
        public const string FrameAdjust = "FactoryMoldFrameAdjust";           // 图片调整
        public const string FrameMirror = "FactoryMoldFrameMirror";           // 物料镜像
        public const string FrameTurn = "FactoryMoldFrameTurn";               // 物料转向

        // 框架分类 Tab（FactoryFrameType，按 MoldFrameConfig.csv 的 Type 字段动态生成 FrameTypeButton）
        public static class FrameType
        {
            public const string Badge = "FactoryMoldFrameTypeBadge";           // 徽章
            public const string Album = "FactoryMoldFrameTypeAlbum";           // 画册
            public const string Pillow = "FactoryMoldFrameTypePillow";         // 抱枕
            public const string Blanket = "FactoryMoldFrameTypeBlanket";       // 毯子
            public const string Poster = "FactoryMoldFrameTypePoster";         // 海报
            public const string Postcard = "FactoryMoldFrameTypePostcard";     // 明信片
            public const string Standee = "FactoryMoldFrameTypeStandee";       // 立牌
            public const string Scroll = "FactoryMoldFrameTypeScroll";         // 挂轴
            public const string CanvasBag = "FactoryMoldFrameTypeCanvasBag";   // 帆布包
            public const string Folder = "FactoryMoldFrameTypeFolder";         // 文件夹
            public const string PhoneCase = "FactoryMoldFrameTypePhoneCase";   // 手机壳
            public const string LaserTicket = "FactoryMoldFrameTypeLaserTicket"; // 镭射票
        }

        public const string SelectMaterial = "FactoryMoldSelectMaterial";           // 物料选择
        public const string SelectIllustration = "FactoryMoldSelectIllustration";   // 画稿选择
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
        public const string EffectYield = "FactoryUpgradeEffectYield";   // 良品率增加效果（效果预览条前缀，后跟 "2%→4%"）
        public const string EffectVolume = "FactoryUpgradeEffectVolume"; // 产量增加效果（效果预览条前缀，后跟 "100→150"）
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
        public const string EstPriceFmt = "FactorySettleEstPriceFmt";            // 预计售价：{Price}/个
        public const string ItemHint = "FactorySettleItemHint";                  // 道具已自动发放进背包
        public const string Back = "FactorySettleBack";                          // 返回
        // 新原型图改版新增（UI 尚未接入项目，键先备好；ScoreLabel/SuccessLabel/CompletionLabel 三条对应旧版文案已被删除，
        // 待 FactorySettlePanel.cs 按新图重做后一并清理，清理前旧三条 Key 在 CSV 里已找不到对应行）
        public const string CraftCountLabel = "FactorySettleCraftCountLabel";    // 本次加工数量
        public const string UnitPiece = "FactorySettleUnitPiece";                // 个（独立单位后缀，英文留空）
        public const string DefectRateLabel = "FactorySettleDefectRateLabel";    // 残次品率
        public const string FailProductLabel = "FactorySettleFailProductLabel";  // 失败产品
        public const string DoneLabel = "FactorySettleDoneLabel";                // 完成生产
        public const string ClickToClose = "FactorySettleClickToClose";          // 点击屏幕任意位置关闭界面
    }

    // 物料结算面板（FactoryMoldSettlePanel）：FactoryMoldMgPanel 完成制作后展示产出清单。
    // 自有文案（标题/台词）集中在 Data/Factory/FactoryMoldSettlePanel.csv；道具提示 / 返回文案与 <see cref="Settle"/> 完全一致，直接复用。
    public static class MoldSettle
    {
        public const string Title = "FactoryMoldSettleTitle";     // 物料结算
        public const string Speech = "FactoryMoldSettleSpeech";   // 干得漂亮
        public const string Cost = "FactoryMoldSettleCost";       // 预计制作成本
    }
}

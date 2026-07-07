public static class LocVarSet
{
    public static class MiniGame
    {
        public const string SpConsumeCount = nameof(SpConsumeCount);      // "制作消耗{SpConsumeCount}体力" 中的体力消耗占位符
        public const string ApConsumeCount = nameof(ApConsumeCount);
        public const string CountDownTime = nameof(CountDownTime);
        public const string CoinCosumeCount = nameof(CoinCosumeCount);
        public const string NotEnoughStamina = nameof(NotEnoughStamina);
        public const string NotEnoughMoney = nameof(NotEnoughMoney);
        public const string NotEnoughAp = nameof(NotEnoughAp);
        public const string NotEnoughGameCoin = nameof(NotEnoughGameCoin);
    }

    public static class MiniGame1CookGame
    {
        /// <summary>
        /// 食材大于等于2个才能制作
        /// </summary>
        public const string NeedAtLeastTwoIngredients = nameof(NeedAtLeastTwoIngredients);
        public const string MakeFoodFail = nameof(MakeFoodFail);
        public const string MakeFoodSuccess = nameof(MakeFoodSuccess);
        public const string ItemName = nameof(ItemName);                 // "{ItemName} 制作成功" 中的物品名占位符
    }


    // 女巫毒药小游戏占位符
    public static class WitchPotion
    {
        public const string GameCoin = nameof(GameCoin);            // "现有金币：{Gold}"
        public const string Bet = nameof(Bet);                      // 当前下注金额
        public const string Opened = nameof(Opened);                // 已开瓶数
        public const string Total = nameof(Total);                  // 总瓶数
        public const string Multiplier = nameof(Multiplier);        // "{Multiplier}x"
        public const string Payout = nameof(Payout);                // 可领取/已领取收益
        public const string MinBet = nameof(MinBet);                // "最低金额{MinBet} 最高金额{MaxBet}"
        public const string MaxBet = nameof(MaxBet);

        public const string SettlePlayAgainCost = nameof(SettlePlayAgainCost);
    }

    // 爆点冲刺小游戏占位符
    public static class CrashSprint
    {
        public const string Bet = nameof(Bet);                  // 当前下注金额
        public const string Multiplier = nameof(Multiplier);    // 止盈倍率 "{Multiplier}x"
        public const string Crash = nameof(Crash);              // 本局爆点数值 "{Crash}x"
        public const string Payout = nameof(Payout);            // 获得收益
        public const string MinBet = nameof(MinBet);            // "最低金额{MinBet} 最高金额{MaxBet}"
        public const string MaxBet = nameof(MaxBet);
    }

    // 工厂加工厂主界面占位符
    public static class FactoryMain
    {
        public const string Level = nameof(Level);            // 工厂等级 LV{Level}
        public const string CoopCur = nameof(CoopCur);        // 已停用：合作值已从加工厂界面移除
        public const string CoopMax = nameof(CoopMax);
        public const string Volume = nameof(Volume);          // 当前产量 {Volume}/次
        public const string Yield = nameof(Yield);            // 产出良品率 {Yield}%
        public const string Price = nameof(Price);            // ¥{Price}/个
        public const string Count = nameof(Count);            // ×{Count}件
        public const string Cost = nameof(Cost);              // 总金额消费：{Cost}
        public const string Selected = nameof(Selected);      // 已选素材 {Selected}
    }

    // 工厂物料制作面板占位符
    public static class FactoryMold
    {
        public const string Price = nameof(Price);   // 预估售出/制作价格 ¥{Price}/件
    }

    // 工厂加工（传送带下压）小游戏占位符
    public static class FactoryProcess
    {
        public const string Score = nameof(Score);              // 积分分数
        public const string Success = nameof(Success);          // 制作成功数
        public const string Fail = nameof(Fail);                // 失败数
        public const string Completion = nameof(Completion);    // 完成率（百分比整数）
        public const string Reward = nameof(Reward);            // 结算获得金币
    }

    // 通用「本局结算」面板占位符
    public static class CasinoSettle
    {
        public const string Sp = nameof(Sp);   // "消耗-{Sp}体力" 再来一局体力消耗
    }
}
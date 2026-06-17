public static class LocalizeVarSet
{
    public static class MiniGame
    {
        public const string SpConsumeCount = nameof(SpConsumeCount);      // "制作消耗{SpConsumeCount}体力" 中的体力消耗占位符
        public const string ApConsumeCount = nameof(ApConsumeCount);
        public const string CountDownTime = nameof(CountDownTime);
        public const string CoinCosumeCount = nameof(CoinCosumeCount);
    }

    public static class MiniGame1CookGame
    {
        /// <summary>
        /// 食材大于等于2个才能制作
        /// </summary>
        public const string NeedAtLeastTwoIngredients = nameof(NeedAtLeastTwoIngredients);

        /// <summary>
        /// 体力不足
        /// </summary>
        public const string NotEnoughStamina = nameof(NotEnoughStamina);
    }

    // 女巫毒药小游戏占位符
    public static class WitchPotion
    {
        public const string Gold = nameof(Gold);                    // "现有金币：{Gold}"
        public const string Bet = nameof(Bet);                      // 当前下注金额
        public const string Opened = nameof(Opened);                // 已开瓶数
        public const string Total = nameof(Total);                  // 总瓶数
        public const string Multiplier = nameof(Multiplier);        // "{Multiplier}x"
        public const string Payout = nameof(Payout);                // 可领取/已领取收益
        public const string MinBet = nameof(MinBet);                // "最低金额{MinBet} 最高金额{MaxBet}"
        public const string MaxBet = nameof(MaxBet);

        public const string NotEnoughMoney = nameof(NotEnoughMoney);
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

        public const string NotEnoughMoney = nameof(NotEnoughMoney);
    }

    // 通用「本局结算」面板占位符
    public static class CasinoSettle
    {
        public const string Sp = nameof(Sp);   // "消耗-{Sp}体力" 再来一局体力消耗
    }
}
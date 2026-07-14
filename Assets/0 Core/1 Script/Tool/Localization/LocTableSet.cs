public static class LocTableSet
{
    public const string PhotoStudio = nameof(PhotoStudio);

    public const string Kitchen = nameof(Kitchen);

    public const string PhotoStudioSprite = nameof(PhotoStudioSprite);

    public const string CasinoGame = nameof(CasinoGame);

    public const string Factory = nameof(Factory);
    public const string InventoryItem = nameof(InventoryItem);
    public const string GameEnterPanel = nameof(GameEnterPanel);
    public const string ShopHelpPanel = nameof(ShopHelpPanel);

    /// <summary>跨功能通用文案（如「失败」「返回」「本局结算」），供各小游戏结算面板共用，避免每个功能表各存一份。</summary>
    public const string Common = nameof(Common);
}
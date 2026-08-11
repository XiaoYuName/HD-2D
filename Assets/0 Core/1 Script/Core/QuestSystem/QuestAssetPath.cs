namespace XFramework
{
    /// <summary>
    /// 任务系统自己的资源路径拼接。表里的 IconKey 只填资源名（如 <c>CoinIcon</c>），路径在这里补全。
    /// </summary>
    public static class QuestAssetPath
    {
        public const string IconRoot = "Assets/AddressableAssets/Remote/Texture2D/Quest/";

        public static string Icon(string iconKey) => $"{IconRoot}{iconKey}.png";
    }
}

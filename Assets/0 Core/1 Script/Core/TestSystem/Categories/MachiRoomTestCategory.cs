namespace TestSystem
{
    using XFramework;
    /// <summary>催稿游戏测试分类。</summary>
    public sealed class MachiRoomTestCategory : ITestCategory
    {
        const string CategoryTitle = "催稿游戏";
        const string MachiInStudioForcedLabel = "强制让马吉出现在工作室";
        const string MachiInStudioNormalLabel = "恢复马吉出现规则";
        const string InspirationMaxLabel = "回满灵感";
        const string PressureClearLabel = "清空压力";

        public string Title => CategoryTitle;

        public void SetActions(TestActionList actionList)
        {
            actionList.Add(MachiInStudioForcedLabel, SetMachiInStudioForced);
            actionList.Add(MachiInStudioNormalLabel, SetMachiInStudioNormal);
            actionList.Add(InspirationMaxLabel, SetInspirationMax);
            actionList.Add(PressureClearLabel, ClearPressure);
        }

        void SetMachiInStudioForced()
        {
            MachiRoomGameManager.Instance.IsMachiInStudioForced = true;
        }

        void SetMachiInStudioNormal()
        {
            MachiRoomGameManager.Instance.IsMachiInStudioForced = false;
        }

        void SetInspirationMax()
        {
            GameDataManager.Instance.SetProperty(
                PropertyType.MachiInspire,
                GameDataManager.Instance.GetPropertyData(PropertyType.MachiInspire).NumberLimit);
        }

        void ClearPressure()
        {
            GameDataManager.Instance.SetProperty(PropertyType.MachiPressure, 0);
        }
    }
}

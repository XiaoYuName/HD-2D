namespace TestSystem
{
    using UnityEngine;
    using XFramework;

    /// <summary>服装制作测试分类：喷漆小游戏按配表逐件生成入口。</summary>
    public sealed class DressMakingTestCategory : ITestCategory
    {
        const string CategoryTitle = "服装制作";

        public string Title => CategoryTitle;

        public void SetActions(TestActionList actionList)
        {
            var table = LubanManager.Instance.TbSprayPaintGameData;
            if (table == null || table.DataList.Count == 0)
            {
                Debug.LogWarning("[Test] TbSprayPaintGameData 为空，没有可测的喷漆服装。");
                return;
            }

            for (int i = 0; i < table.DataList.Count; i++)
            {
                long clothingId = table.DataList[i].ClothingID;
                actionList.Add(GetSprayPaintLabel(clothingId), () => OpenSprayPaintGame(clothingId));
            }
        }

        static string GetSprayPaintLabel(long clothingId)
        {
            ClothingData clothingData = LubanManager.Instance.TbClothingData.GetOrDefault(clothingId);
            string remark = clothingData != null ? clothingData.Remark : null;
            return string.IsNullOrWhiteSpace(remark)
                ? $"喷漆小游戏 {clothingId}"
                : $"喷漆小游戏 {clothingId} {remark}";
        }

        static void OpenSprayPaintGame(long clothingId)
        {
            var panel = UISystem.Instance.OpenUI<DressMakingSprayPaintGamePanel>(
                nameof(DressMakingSprayPaintGamePanel));
            if (panel == null)
            {
                return;
            }

            // 配置缺失时面板不会自己关，这里关掉免得留个空面板
            if (!panel.SetClothing(clothingId))
            {
                panel.Close();
            }
        }
    }
}

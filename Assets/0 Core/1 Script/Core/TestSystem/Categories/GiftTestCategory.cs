namespace TestSystem
{
    using UnityEngine;
    using XFramework;

    /// <summary>送礼系统测试：快速向背包添加同时存在于 TbGiftItemData 和 TbItemData 的礼物。</summary>
    public sealed class GiftTestCategory : ITestCategory
    {
        const string CategoryTitle = "礼物测试";
        const int AddCount = 5;

        static readonly long[] TestGiftIds =
        {
            120007, // 食物也可作为礼物：刺身料理
            160000, // 猫咪抱枕
            160001, // 限定漫画杂志
            160002, // 手冲咖啡礼盒
        };

        public string Title => CategoryTitle;

        public void SetActions(TestActionList actionList)
        {
            actionList.Add($"添加全部测试礼物（各 {AddCount} 个）", AddAllTestGifts);

            for (int i = 0; i < TestGiftIds.Length; i++)
            {
                long itemId = TestGiftIds[i];
                GiftItemData giftData = LubanManager.Instance.TbGiftItemData.GetOrDefault(itemId);
                string name = giftData?.Remark ?? itemId.ToString();
                actionList.Add($"添加 {name} x{AddCount}", () => AddGift(itemId));
            }
        }

        static void AddAllTestGifts()
        {
            for (int i = 0; i < TestGiftIds.Length; i++)
            {
                AddGift(TestGiftIds[i]);
            }
        }

        static void AddGift(long itemId)
        {
            if (LubanManager.Instance.TbGiftItemData.GetOrDefault(itemId) == null
                || LubanManager.Instance.TbItemData.GetOrDefault(itemId) == null)
            {
                Debug.LogWarning($"[Test] 礼物 {itemId} 未同时配置在 TbGiftItemData 和 TbItemData，已跳过。");
                return;
            }

            InventoryManager.Instance.AddItem(itemId, AddCount);
        }
    }
}

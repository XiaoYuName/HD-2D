using UnityEngine;
using UnityEngine.AddressableAssets;
using XFramework;
using Cysharp.Threading.Tasks;

public class ItemManager : MonoBehaviour, IGameInitialized
{
    #region Singleton
    private static ItemManager st;
    public static ItemManager St => st != null ? st : st = FindAnyObjectByType<ItemManager>();
    #endregion
    #region Set
    [SerializeField] AssetReference itemConfig;
    [SerializeField] ItemConfig config;
    [SerializeField] PlayerBag playerBag;
    #endregion
    #region Get
    public PlayerBag PlayerBag => playerBag;
    public ItemConfig Config => config;
    #endregion
    #region Singleton
    public async UniTask Initialized()
    {
        st = this;

        config = await itemConfig.LoadAsset<ItemConfig>();
    }
    public async UniTask Release()
    {
        if(st == this)
            st = null;

        await UniTask.CompletedTask;
    }
    void Start()
    {
        if(config == null)
        {
            config = itemConfig.LoadAssets<ItemConfig>();
        }
    }
    #endregion
    #region Get
    public ItemData GetItemData(long id)
    {
        return config.GetItemData(id);
    }
    #endregion
    #region Add Item
    // 物品数据统一走 InventoryManager（PlayerBag 已停用，仅保留备份）
    public void AddItem(long id, int count)
    {
        InventoryManager.Instance.AddItem(id, count);
    }
    public void AddItem(ItemStack itemStack)
    {
        InventoryManager.Instance.AddItem(itemStack.id, itemStack.count);
    }
    #endregion
    #region Recipe
    public bool IsRecipeUnlocked(long recipeItemId) => InventoryManager.Instance.IsRecipeUnlocked(recipeItemId);

    public void UnlockRecipe(long recipeItemId) => InventoryManager.Instance.UnlockRecipe(recipeItemId);
    #endregion
}

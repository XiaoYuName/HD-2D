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
    #endregion
    #region Get
    public ItemData GetItemData(long id)
    {
        return config.GetItemData(id);
    }
    #endregion
    #region Add Item
    public void AddItem(long id, int count)
    {
        playerBag.AddItem(id, count);
    }
    public void AddItem(ItemStack itemStack)
    {
        playerBag.AddItem(itemStack.id, itemStack.count);
    }
    public void AddItem(ItemInfo info)
    {
        playerBag.AddItem(info);
    }
    #endregion
    #region Recipe
    public bool IsRecipeUnlocked(long recipeItemId) => playerBag.IsRecipeUnlocked(recipeItemId);

    public void UnlockRecipe(long recipeItemId) => playerBag.UnlockRecipe(recipeItemId);
    #endregion
}

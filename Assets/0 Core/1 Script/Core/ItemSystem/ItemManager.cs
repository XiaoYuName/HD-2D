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
    // [SerializeField] PlayerBag playerBag;   // PlayerBag 已停用，物品走 InventoryManager
    #endregion
    #region Get
    // public PlayerBag PlayerBag => playerBag;   // PlayerBag 已停用
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
}

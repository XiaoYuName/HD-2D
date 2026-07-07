using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    public static PlayerInfo St => st != null ? st : st = FindAnyObjectByType<PlayerInfo>();
    static PlayerInfo st;
    // [SerializeField] PlayerBag bag;   // PlayerBag 已停用，物品走 InventoryManager
    [SerializeField] PlayerStats stats;
    [SerializeField] MachiStats machiStats;
    // public PlayerBag Bag => bag;      // PlayerBag 已停用
    public PlayerStats Stats => stats;
    public MachiStats MachiachiStats => machiStats;
    void Awake()
    {
        st = this;
    }
    void OnDestroy()
    {
        if(st == null)
            st = null;
    }
}

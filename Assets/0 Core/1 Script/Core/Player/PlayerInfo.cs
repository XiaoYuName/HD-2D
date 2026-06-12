using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    public static PlayerInfo St => st != null ? st : st = FindAnyObjectByType<PlayerInfo>();
    static PlayerInfo st;
    [SerializeField] PlayerBag bag;
    [SerializeField] PlayerStats stats;
    [SerializeField] MachiStats machiStats;
    public PlayerBag Bag => bag;
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

using UnityEngine;

public class GameSmartData : MonoBehaviour
{
    public GemCutTarget GemCutTarget { get; private set; }

    public void Init()
    {
        GemCutTarget = transform.GetComponentInChildren<GemCutTarget>();
    }
}

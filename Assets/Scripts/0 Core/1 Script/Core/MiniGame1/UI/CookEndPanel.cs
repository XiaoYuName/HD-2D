using UnityEngine;
using UnityEngine.InputSystem;

public class CookEndPanel : MonoBehaviour
{
    MiniGameCookResult result;

    public MiniGameCookResult Result => result;

    public void Init(MiniGameCookResult result)
    {
        this.result = result;
        gameObject.SetActive(true);
    }
    void Update()
    {
        if(Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            gameObject.SetActive(false);
    }
}

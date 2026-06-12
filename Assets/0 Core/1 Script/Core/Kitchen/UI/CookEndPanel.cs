using UnityEngine;

public class CookEndPanel : MonoBehaviour
{
    MiniGameCookResult result;

    public MiniGameCookResult Result => result;

    public void Init(MiniGameCookResult result)
    {
        this.result = result;
        gameObject.SetActive(true);
    }

    void OnEnable()
    {
        PlayerInputManager.St.OnSpace += Close;
        PlayerInputManager.St.OnClick += Close;
        PlayerInputManager.St.OnEsc += Close;
    }

    void OnDisable()
    {
        PlayerInputManager.St.OnSpace -= Close;
        PlayerInputManager.St.OnClick -= Close;
        PlayerInputManager.St.OnEsc -= Close;
    }

    void Close()
    {
        gameObject.SetActive(false);
    }
}

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
        PlayerInputManager.Instance.OnSpace += Close;
        PlayerInputManager.Instance.OnClick += Close;
        PlayerInputManager.Instance.OnEsc += Close;
    }

    void OnDisable()
    {
        PlayerInputManager.Instance.OnSpace -= Close;
        PlayerInputManager.Instance.OnClick -= Close;
        PlayerInputManager.Instance.OnEsc -= Close;
    }

    void Close()
    {
        gameObject.SetActive(false);
    }
}

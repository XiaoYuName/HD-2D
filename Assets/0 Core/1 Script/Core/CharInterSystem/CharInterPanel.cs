using UnityEngine;
using UnityEngine.UI;

public class CharInterPanel : MonoBehaviour
{
    [SerializeField] CharClickInter clickInter;
    [SerializeField] Image illImage;

    [SerializeField] RectTransform interButtonContainer;
    [SerializeField] CharInterPanelButton characterInterPanelButtonPrefab;
    
    void Awake()
    {
        clickInter.OnClick += Open;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        PlayerInputManager.Instance.OnEsc += Close;
        PlayerInputManager.Instance.OnRightClick += Close;
    }
    public void Close()
    {
        gameObject.SetActive(false);
        PlayerInputManager.Instance.OnEsc -= Close;
        PlayerInputManager.Instance.OnRightClick -= Close;
    }
}

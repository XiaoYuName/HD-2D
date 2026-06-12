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
        PlayerInputManager.St.OnEsc += Close;
        PlayerInputManager.St.OnRightClick += Close;
    }
    public void Close()
    {
        gameObject.SetActive(false);
        PlayerInputManager.St.OnEsc -= Close;
        PlayerInputManager.St.OnRightClick -= Close;
    }
}

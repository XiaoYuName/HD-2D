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
        PlayerInputManager.Instance.OnEsc += CloseByCancelInput;
        PlayerInputManager.Instance.OnRightClick += CloseByCancelInput;
    }
    public void Close()
    {
        gameObject.SetActive(false);
        PlayerInputManager.Instance.OnEsc -= CloseByCancelInput;
        PlayerInputManager.Instance.OnRightClick -= CloseByCancelInput;
    }

    /// <summary>
    /// 这个面板不走 UISystem，右键/Esc 关闭要自己声明一下已经消费掉了，
    /// 否则同一次输入会接着被“小场景返回大地图”用一遍。
    /// </summary>
    void CloseByCancelInput()
    {
        PlayerInputManager.Instance.ConsumeCancelInput();
        Close();
    }
}

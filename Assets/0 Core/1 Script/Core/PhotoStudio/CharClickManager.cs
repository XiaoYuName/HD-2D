using UnityEngine;

public class CharClickManager : MonoBehaviour
{   // Temp
    [SerializeField] CharClickInter[] clickInter;
    [SerializeField] GameObject panel;

    void Start()
    {
        foreach (var item in clickInter)
            item.OnClick += OnClick;

    }

    void OnClick()
    {
        panel.SetActive(true);
    }
}

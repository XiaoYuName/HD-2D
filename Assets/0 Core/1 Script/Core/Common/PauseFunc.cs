using UnityEngine;
using System;
using UnityEngine.UI;


public class PauseFunc : MonoBehaviour
{
    [SerializeField] Button pauseButton;
    [SerializeField] CanvasGroup pausePanel;
    
    bool isPaused;

    void Awake()
    {
        pauseButton.onClick.AddListener(TogglePause);
    }

    void TogglePause()
    {
        isPaused = !isPaused;
        pausePanel.gameObject.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }
}

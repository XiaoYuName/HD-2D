using UnityEngine;
using UnityEngine.InputSystem;

namespace TestSystem
{
    /// <summary>测试面板入口：F1 开关面板，预制体走 Resources 加载，不接项目 UI 框架。</summary>
    public sealed class TestManager : MonoBehaviour
    {
        TestPanel curPanel;
        const string PanelPrefabPath = "Test/TestPanel";
        
        void Update()
        {
            if (Keyboard.current[Key.F1].wasPressedThisFrame)
                TogglePanel();
        }

        public void TogglePanel()
        {
            if (curPanel == null)
            {
                curPanel = Instantiate(Resources.Load<GameObject>(PanelPrefabPath), transform)
                    .GetComponent<TestPanel>();
                return;
            }

            curPanel.gameObject.SetActive(!curPanel.gameObject.activeSelf);
        }
    }
}

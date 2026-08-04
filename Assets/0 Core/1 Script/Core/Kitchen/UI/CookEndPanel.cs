using UnityEngine;
namespace XFramework
{
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
            PlayerInputManager.Instance.OnEsc += CloseByCancelInput;
        }

        void OnDisable()
        {
            PlayerInputManager.Instance.OnSpace -= Close;
            PlayerInputManager.Instance.OnClick -= Close;
            PlayerInputManager.Instance.OnEsc -= CloseByCancelInput;
        }

        void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 这个面板不走 UISystem，Esc 关闭要自己声明一下已经消费掉了，
        /// 否则同一次 Esc 会接着被“小场景返回大地图”用一遍。
        /// </summary>
        void CloseByCancelInput()
        {
            PlayerInputManager.Instance.ConsumeCancelInput();
            Close();
        }
    }
}

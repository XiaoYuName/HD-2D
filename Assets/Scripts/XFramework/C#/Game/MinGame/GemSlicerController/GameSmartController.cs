using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    public class GameSmartController : MonoSingleton<GameSmartController>
    {
        [LabelText("宝石生成根节点")]
        public Transform SmartRootTran;

        [LabelText("切割刀具")]
        public GemCutController GemCutController;

        /// <summary>本局结算：分数 0~100，切进白色轮廓超阈值时为 0。</summary>
        public event Action<GemCutResult> Completed;

        private GameObject currentGemObj;

        public void SetData(ClothingData clothingData)
        {
            if (clothingData == null || string.IsNullOrEmpty(clothingData.SmartGemPaht))
            {
                Debug.LogError("[GemCut] ClothingData 为空或没配 SmartGemPaht。");
                return;
            }

            // 上一局的宝石已经被切碎了，绝不能留着，也不能进对象池复用
            ClearGem();

            var obj = AssetsManager.Instance.Instantiate(clothingData.SmartGemPaht);
            obj.transform.SetParent(SmartRootTran);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            currentGemObj = obj;

            var gameSmartData = obj.GetComponent<GameSmartData>();
            if (gameSmartData == null)
            {
                Debug.LogError($"[GemCut] 宝石预制体 {clothingData.SmartGemPaht} 根节点上没有 GameSmartData 组件。");
                return;
            }

            gameSmartData.Init();

            GemCutController.Completed -= OnCutCompleted;
            GemCutController.Completed += OnCutCompleted;
            GemCutController.SetData(gameSmartData);
        }

        /// <summary>销毁当前宝石（含切出来的碎块）。小游戏收尾时调。</summary>
        public void ClearGem()
        {
            if (currentGemObj == null)
            {
                return;
            }

            AssetsManager.Instance.ReleaseGameObject(currentGemObj);
            currentGemObj = null;
        }

        private void OnCutCompleted(GemCutResult result)
        {
            Completed?.Invoke(result);
        }

        protected override void OnDestroy()
        {
            if (GemCutController != null)
            {
                GemCutController.Completed -= OnCutCompleted;
            }

            base.OnDestroy();
        }
    }
}

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
        private ClothingData currentClothingData;

        public void SetData(ClothingData clothingData)
        {
            if (clothingData == null || string.IsNullOrEmpty(clothingData.SmartGemPaht))
            {
                Debug.LogError("[GemCut] ClothingData 为空或没配 SmartGemPaht。");
                return;
            }

            currentClothingData = clothingData;

            // 上一局的宝石已经被切碎了，绝不能留着，也不能进对象池复用
            ClearGem();

            var obj = AssetsManager.Instance.Instantiate(clothingData.SmartGemPaht);

            UnpackIfPrefabInstance(obj);

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

        /// <summary>玩家主动交卷，按当前形状立刻结算。给 UI 的「完成」按钮用。</summary>
        public void Finish()
        {
            if (GemCutController != null)
            {
                GemCutController.Finish();
            }
        }

        /// <summary>
        /// 重来一局：整块宝石连带切出来的碎块一起丢掉，重新生成一块完好的。
        /// 比复用旧宝石干净——上一局的碎块、切割进度、备份全部一次性清掉。
        /// </summary>
        public void Retry()
        {
            if (currentClothingData == null)
            {
                Debug.LogError("[GemCut] 还没调过 SetData，无法重试。");
                return;
            }

            SetData(currentClothingData);
        }

        /// <summary>
        /// 编辑器下 AssetsManager 走的是 PrefabUtility.InstantiatePrefab，出来的是「连接态预制体实例」，
        /// 而 Unity 不允许销毁预制体实例内部的子物体 —— 切割时 Sliceable2D 要 Destroy 掉原宝石，会直接抛
        /// InvalidOperationException。真机走的是 Object.Instantiate，本来就是普通物体，没这个问题。
        /// 所以这里只在编辑器下解包一次，让编辑器和真机行为一致。
        /// </summary>
        private static void UnpackIfPrefabInstance(GameObject obj)
        {
#if UNITY_EDITOR
            if (obj == null || !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                return;
            }

            UnityEditor.PrefabUtility.UnpackPrefabInstance(
                obj,
                UnityEditor.PrefabUnpackMode.Completely,
                UnityEditor.InteractionMode.AutomatedAction);
#endif
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

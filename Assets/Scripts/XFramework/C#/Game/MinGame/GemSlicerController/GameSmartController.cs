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


        public void SetData(ClothingData clothingData)
        {
            var obj = AssetsManager.Instance.Instantiate(clothingData.SmartGemPaht);
            obj.transform.SetParent(SmartRootTran);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            
            var GameSmartData = obj.transform.GetComponent<GameSmartData>();
            
        }
    }
}


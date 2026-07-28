using UnityEngine;
using UnityEngine.UI;

namespace UrpLiquidShader
{
    [ExecuteAlways]
    public class FillAmountSetter : MonoBehaviour
    {
        [Range(0f, 1f)] public float fillAmount = 1;
        private Material liquidMaterial;
        private static readonly int FillAmount = Shader.PropertyToID("Fill_Amount");

        public void Update()
        {
            if (!liquidMaterial)
            {
                if (GetComponent<Image>()) liquidMaterial = GetComponent<Image>().material;
                if (GetComponent<SpriteRenderer>()) liquidMaterial = GetComponent<SpriteRenderer>().sharedMaterial;
                return;
            }

            liquidMaterial.SetFloat(FillAmount, fillAmount);
        }
    }
}

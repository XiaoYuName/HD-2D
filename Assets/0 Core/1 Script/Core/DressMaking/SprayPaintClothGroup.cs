using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 服饰片预制体根节点：按配置 PieceAnswerList 的顺序收集下属服饰片。
    /// </summary>
    public sealed class SprayPaintClothGroup : MonoBehaviour
    {
        [SerializeField] SprayPaintClothPiece[] pieces;

        public SprayPaintClothPiece[] Pieces => pieces;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (pieces == null || pieces.Length == 0)
            {
                pieces = GetComponentsInChildren<SprayPaintClothPiece>(true);
            }
        }
#endif
    }
}

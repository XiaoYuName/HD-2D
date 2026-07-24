using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    public class ApAreaUI : MonoBehaviour
    {
        [SerializeField] Image[] itemList;
        [SerializeField] Sprite apActiveSprite, apInActiveSprite;

        void Awake()
        {
            
        }

        void OnEnable()
        {
            GameDataManager.Instance.RegisterPlayerDataChange(UpdateAp);
        }

        void OnDisable()
        {
            GameDataManager.Instance.UnregisterPlayerDataChange(UpdateAp);
        }
        void UpdateAp(PlayerData data)
        {
            int ap = data.GetProperty(PropertyType.ActionPointsValue);

            for (int i = 0; i < itemList.Length; i++)
            {
                if(ap >= i)
                {
                    itemList[i].enabled = true;
                }
                else
                {
                    itemList[i].enabled = false;
                }
            }
        }
    }
}
using System;
using UnityEngine;
using XFramework;

public class WordMapController : MonoBehaviour
{
    public const string wordSceneItemPath = "Assets/AddressableAssets/Remote/Prefabs/GameScenes/WordSceneItem.prefab";
    
    private void Start()
    {
        CreatWordMapItem();
    }

    private void CreatWordMapItem()
    {
        for (int i = 0; i < GameDataManager.Instance.GameSceneData.DataList.Count; i++)
        {
            var obj =  AssetsManager.Instance.Instantiate(wordSceneItemPath);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localPosition = new Vector3(obj.transform.localPosition.x + (i * 1), obj.transform.localPosition.y, obj.transform.localPosition.z);
            if (obj.TryGetComponent<WordSceneItem>(out var wordSceneItem))
            {
                wordSceneItem.Initialize(GameDataManager.Instance.GameSceneData.DataList[i]);
            }
        }
    }
}

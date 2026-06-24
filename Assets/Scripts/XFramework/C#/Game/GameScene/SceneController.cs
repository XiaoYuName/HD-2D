using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using XFramework;

public class SceneController : GameBase
{
    private CinemachineCamera _camera;
    private SpriteRenderer sceneBackground;

    public MinSceneData minSceneData { get; private set; }
    public PlayerData user { get; private set; }
    
    public List<SceneCharacterController> characterControllers = new List<SceneCharacterController>();
    public List<SceneCharacterController> CustomCharacterControllers = new List<SceneCharacterController>();

    public void Initialized()
    {
        _camera = Get<CinemachineCamera>("CinemachineCamera");
        sceneBackground = Get<SpriteRenderer>("SceneBackground");
        characterControllers = new List<SceneCharacterController>();
        GameDataManager.Instance.BindPlayerDataSceneChange(PlayerDataChange);
        CharacterManager.Instance.BindCustomCharacterDataChange(CustomCharacterChange);
    }

    public void Release()
    {
        GameDataManager.Instance.UnBindPlayerDataSceneChange(PlayerDataChange);
        CharacterManager.Instance.UnBindCustomCharacterDataChange(CustomCharacterChange);
    }

    private void PlayerDataChange(PlayerData userChange)
    {
        user = userChange;
        minSceneData = GameDataManager.Instance.MinGameSceneData.GetDataByID(userChange.minSceneID);

        foreach (var item in characterControllers)
        {
            AssetsManager.Instance.FreeGameObject(item.gameObject);
        }
        characterControllers.Clear();
        
        CreateCharacter();
    }

    private void CustomCharacterChange(List<CustomCharacterData> customCharacterData)
    {
        foreach (var t in CustomCharacterControllers)
        {
            AssetsManager.Instance.FreeGameObject(t.gameObject);
        }
        CustomCharacterControllers.Clear();

        foreach (var item in customCharacterData)
        {
            if (item.CustomSceneData.SceneID != minSceneData.scene_id)
            {
                continue;
            }

            var obj = AssetsManager.Instance.Instantiate(
                "Assets/AddressableAssets/Remote/Prefabs/Character/SceneCharacter/SceneCharacter.prefab");
            obj.transform.SetParent(sceneBackground.transform);
            var controller = obj.GetComponent<SceneCharacterController>();
            controller.Init(item.CharacterData,item.ShowingData,item.CustomSceneData);
            CustomCharacterControllers.Add(controller);
        }
    }

    private void CreateCharacter()
    {
        var characterDataList = CharacterManager.Instance.CharacterData.DataList;
        var configDataList = CharacterDataManager.Instance.DataList;
        ShowingWeek curWeek = user.Week switch
        {
            1 => ShowingWeek.Monday,
            2 => ShowingWeek.Tuesday,
            3 => ShowingWeek.Wednesday,
            4 => ShowingWeek.Thursday,
            5 => ShowingWeek.Friday,
            6 => ShowingWeek.Saturday,
            7 => ShowingWeek.Sunday,
            _ => ShowingWeek.Monday,
        };
        
        ShowingTime curTime = user.EnvironmentMode switch
        {
            EnvironmentMode.Morning => ShowingTime.Morning,
            EnvironmentMode.Noon => ShowingTime.Noon,
            EnvironmentMode.Evening => ShowingTime.Evening,
            EnvironmentMode.Midnight => ShowingTime.Midnight,
            _ => ShowingTime.Morning,
        };
        
        
        string targetSceneId = minSceneData.scene_id;

        int count = characterDataList.Count;

        for (int i = 0; i < count; i++)
        {
            var characterData = characterDataList[i];
            var showingDataList = configDataList[i].ShowingDataList;

            for (int j = 0; j < showingDataList.Count; j++)
            {
                var showingData = showingDataList[j];
                if (!showingData.ShowWeek.HasFlag(curWeek))
                {
                    continue;
                }
                
                if(!showingData.ShowTime.HasFlag(curTime))
                    continue;

                if (showingData.ShowingModel != ShowingModel.Fixed)
                    continue;

                if (showingData.FixedSceneData.SceneID != targetSceneId)
                    continue;


                var obj = AssetsManager.Instance.Instantiate(
                    "Assets/AddressableAssets/Remote/Prefabs/Character/SceneCharacter/SceneCharacter.prefab");
                obj.transform.SetParent(sceneBackground.transform);
                var controller = obj.GetComponent<SceneCharacterController>();
                controller.Init(characterData,showingData);
                characterControllers.Add(controller);
                break;
            }
        }
        
    }
}

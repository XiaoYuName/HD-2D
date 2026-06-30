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

    public GameSceneData SceneData { get; private set; }
    public PlayerData  PlayerData { get; private set; }

    public List<SceneCharacterController> characterControllers = new List<SceneCharacterController>();

    public void Initialized()
    {
        _camera = Get<CinemachineCamera>("CinemachineCamera");
        sceneBackground = Get<SpriteRenderer>("SceneBackground");
        characterControllers = new List<SceneCharacterController>();
        GameSceneManager.Instance.BindSceneChange(GameSceneChange);
        GameDataManager.Instance.BindPlayerDataChange(PlayerSceneChange);
    }

    public void Release()
    {
        GameSceneManager.Instance.UnBindSceneChange(GameSceneChange);
        GameDataManager.Instance.UnBindPlayerDataChange(PlayerSceneChange);
    }

    private void GameSceneChange(SceneData sceneData)
    {
        SceneData = GameSceneManager.Instance.GetGameSceneData(sceneData.SceneID);
        UpdateCharacter();
    }

    private void PlayerSceneChange(PlayerData playerData)
    {
        PlayerData = playerData;
        UpdateCharacter();
    }

    private void UpdateCharacter()
    {
        if (SceneData == null || PlayerData == null) return;
        
        
        for (int i = 0; i < characterControllers.Count; i++)
        {
            characterControllers[i].Release();
            AssetsManager.Instance.FreeGameObject(characterControllers[i].gameObject);
        }
        ShowRuleWeekType currentWeek =  PlayerData.Week switch
        {
            1=> ShowRuleWeekType.Monday,
            2 => ShowRuleWeekType.Tuesday,
            3 => ShowRuleWeekType.Wednesday,
            4 => ShowRuleWeekType.Thursday,
            5 => ShowRuleWeekType.Friday,
            6 => ShowRuleWeekType.Saturday,
            7 => ShowRuleWeekType.Sunday,
            _=> ShowRuleWeekType.All
        };
        ShowRuleTimeType curTime = PlayerData.EnvironmentMode switch
        {
            EnvironmentMode.Morning => ShowRuleTimeType.Morning,
            EnvironmentMode.Noon => ShowRuleTimeType.Noon,
            EnvironmentMode.Evening => ShowRuleTimeType.Evening,
            EnvironmentMode.Midnight => ShowRuleTimeType.Midnight,
            _ => ShowRuleTimeType.Morning,
        };

        foreach (var ID in SceneData.ActiveNpcID)
        {
            //1.查看是否满足场景要求
            NpcData npcData = CharacterManager.Instance.GetNpcDataByID(ID);
            //2.查看是否满足日期要求
            if(!npcData.WeekType.HasFlag(currentWeek))continue;
            //3.查看是否满足时间段要求
            if (!npcData.AppearanceTime.HasFlag(curTime)) continue;
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.SceneCharacterPath);
            obj.transform.SetParent(sceneBackground.transform);
            var controller = obj.GetComponent<SceneCharacterController>();
            controller.Init(npcData);
            characterControllers.Add(controller);
            
        }
    }

}

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

    public void Initialized()
    {
        _camera = Get<CinemachineCamera>("CinemachineCamera");
        sceneBackground = Get<SpriteRenderer>("SceneBackground");
        characterControllers = new List<SceneCharacterController>();
        GameDataManager.Instance.BindPlayerDataSceneChange(PlayerDataChange);
    }

    public void Release()
    {
        GameDataManager.Instance.UnBindPlayerDataSceneChange(PlayerDataChange);
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

    private void CreateCharacter()
    {
        for (int i = 0; i < characterControllers.Count; i++)
        {
            characterControllers[i].Release();
            AssetsManager.Instance.FreeGameObject(characterControllers[i].gameObject);
        }
        ShowRuleWeekType currentWeek =  user.Week switch
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
        ShowRuleTimeType curTime = user.EnvironmentMode switch
        {
            EnvironmentMode.Morning => ShowRuleTimeType.Morning,
            EnvironmentMode.Noon => ShowRuleTimeType.Noon,
            EnvironmentMode.Evening => ShowRuleTimeType.Evening,
            EnvironmentMode.Midnight => ShowRuleTimeType.Midnight,
            _ => ShowRuleTimeType.Morning,
        };
        
        foreach (var item in LubanManager.Instance.TbCharacterData.DataList)
        {
            if(item.ShowRule.Count <= 0)continue;
            
            foreach (var id in item.ShowRule)
            {
                //1.查看是否满足场景要求
                CharacterShowRuleData ruleData = CharacterManager.Instance.GetCharacterShowRule(id);
                if (ruleData.SceneLocation.ToString() != minSceneData.SceneID) continue;
                //2.查看是否满足日期要求
                if(ruleData.WeekType != ShowRuleWeekType.All || !ruleData.WeekType.HasFlag(currentWeek))continue;
                //3.查看是否满足时间段要求
                if (ruleData.AppearanceTime != ShowRuleTimeType.All || !ruleData.AppearanceTime.HasFlag(curTime)) continue;
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.SceneCharacterPath);
                obj.transform.SetParent(sceneBackground.transform);
                var controller = obj.GetComponent<SceneCharacterController>();
                controller.Init(item,ruleData);
                characterControllers.Add(controller);
            }
            
        }
    }

}

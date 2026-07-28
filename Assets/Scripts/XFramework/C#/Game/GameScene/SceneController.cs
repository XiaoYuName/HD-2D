using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using XFramework;

public class SceneController : GameBase
{
    private CinemachineCamera _camera;
    private SpriteRenderer sceneBackground;

    public GameSceneData SceneData { get; private set; }
    public PlayerData  PlayerData { get; private set; }
    private bool isShowing;
    public List<SceneCharacterController> characterControllers = new List<SceneCharacterController>();

    public void Initialized()
    {
        _camera = Get<CinemachineCamera>("CinemachineCamera");
        sceneBackground = Get<SpriteRenderer>("SceneBackground");
        characterControllers = new List<SceneCharacterController>();
        GameSceneManager.Instance.RegisterSceneChange(GameSceneChange);
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerSceneChange);
        isShowing = true;
    }

    public void Release()
    {
        GameSceneManager.Instance.UnregisterSceneChange(GameSceneChange);
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerSceneChange);
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

    /// 用于外部手动调用刷新
    public void RefreshCharacter()
    {
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
        characterControllers.Clear();

        // 场景控制器只负责表现层：
        // “当前场景应该出现哪些 NPC”统一交给 CharacterManager 计算，
        // 这样固定 NPC、随机 NPC、好感/心情条件、存档缓存都集中在角色系统里维护。
        List<NpcData> sceneNpcDataList = CharacterManager.Instance.GetSceneNpcDataList(SceneData, PlayerData);
        foreach (NpcData npcData in sceneNpcDataList)
        {
            ShowNpc(npcData);
        }
    }

    /// <summary>
    /// 实例化并初始化一个场景 NPC。
    /// 资源创建、挂到背景节点、控制器初始化都属于 SceneController 的表现职责。
    /// </summary>
    private void ShowNpc(NpcData npcData)
    {
        if (npcData == null) return;

        var obj = AssetsManager.Instance.Instantiate(AssetKeys.SceneCharacterPath);
        obj.transform.SetParent(sceneBackground.transform);
        var controller = obj.GetComponent<SceneCharacterController>();
        controller.Init(npcData);
        controller.gameObject.SetActive(isShowing);

        characterControllers.Add(controller);
    }
    

    public void OpenAllNpc()
    {
        foreach (SceneCharacterController controller in characterControllers)
        {
            isShowing = true;
            controller.gameObject.SetActive(true);
        }
    }

    public void CloseAllNpc()
    {
        foreach (SceneCharacterController controller in characterControllers)
        {
            isShowing = false;
            controller.gameObject.SetActive(false);
        }
    }
}

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

        // ★ 不能无脑 true：剧情期间会切场景，那时候 NPC 该保持藏着。
        //   显隐是跨场景保持的意图，记在 GameSceneManager 上，这里取当前值当初值 ——
        //   这样 NPC 生成出来就直接是隐藏的，不会先冒出来再被收掉（会闪一帧）
        isShowing = GameSceneManager.Instance.SceneNpcVisible;
    }

    public void Release()
    {
        GameSceneManager.Instance.UnregisterSceneChange(GameSceneChange);
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerSceneChange);
        ReleaseCharacter();
    }

    /// 回收当前场景已经生成的 NPC 实例
    private void ReleaseCharacter()
    {
        for (int i = 0; i < characterControllers.Count; i++)
        {
            characterControllers[i].Release();
            AssetsManager.Instance.FreeGameObject(characterControllers[i].gameObject);
        }
        characterControllers.Clear();
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
        ReleaseCharacter();

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
        // 保持局部缩放：回池和挂载都用世界坐标跟随会把 localScale 除一遍背景缩放，复用几次就越来越小
        obj.transform.SetParent(sceneBackground.transform, false);
        obj.transform.localScale = Vector3.one;
        obj.transform.localRotation = Quaternion.identity;
        var controller = obj.GetComponent<SceneCharacterController>();
        controller.Init(npcData);
        controller.gameObject.SetActive(isShowing);

        characterControllers.Add(controller);
    }
    

    public void OpenAllNpc() => SetAllNpcActive(true);

    public void CloseAllNpc() => SetAllNpcActive(false);

    /// <summary>
    /// ★ <c>isShowing</c> 要在循环<b>外面</b>改。
    /// 写在循环里的话，场景里一个 NPC 都还没生成时这一句根本跑不到，
    /// 状态没记住 —— 之后 <see cref="RefreshCharacter"/> 生成出来的 NPC 会按旧状态显示出来。
    /// </summary>
    private void SetAllNpcActive(bool active)
    {
        isShowing = active;

        foreach (SceneCharacterController controller in characterControllers)
        {
            if (controller != null)
            {
                controller.gameObject.SetActive(active);
            }
        }
    }
}

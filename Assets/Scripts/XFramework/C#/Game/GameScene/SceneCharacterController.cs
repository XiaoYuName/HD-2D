using UnityEngine;
using XFramework;

/// <summary>
/// 场景角色控制器 - 框架存根版本
/// </summary>
public class SceneCharacterController : GameBase
{
    private NpcData npcData;

    public void Init(NpcData data)
    {
        npcData = data;
        // 根据项目需求实现NPC初始化逻辑
    }

    public void Release()
    {
        // 根据项目需求实现释放逻辑
    }
}

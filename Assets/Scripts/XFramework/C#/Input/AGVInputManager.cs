using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using XFramework;

public class AGVInputManager : MonoSingleton<AGVInputManager>,IGameInitialized
{
    private AGV_InputAction agvInputAction;
    
    public event Action OnSpace; 
    
    public event Action OnEsc;
    
    public event Action OnClick;
    
    public event Action OnRightClick;
    
    

    /// <summary>
    /// 初始化脚本函数
    /// </summary>
    /// <returns></returns>
    public async UniTask Initialized()
    {
        agvInputAction = new AGV_InputAction();
        agvInputAction.Game.Enable();
        agvInputAction.Game.Space.performed += OnSpaceInvoke;
        agvInputAction.Game.Esc.performed += OnEscInvoke;
        agvInputAction.Game.Click.performed += OnClickInvoke;
        agvInputAction.Game.RightClick.performed += OnRightClickInvoke;
        await UniTask.CompletedTask;
    }
    
    void OnSpaceInvoke(InputAction.CallbackContext context)
    {
        OnSpace?.Invoke();
    }

    void OnClickInvoke(InputAction.CallbackContext context)
    {
        OnClick?.Invoke();
    }
    void OnRightClickInvoke(InputAction.CallbackContext context)
    {
        OnRightClick?.Invoke();
    }
    void OnEscInvoke(InputAction.CallbackContext context)
    {
        OnEsc?.Invoke();
    }

    /// <summary>
    /// 释放脚本函数
    /// </summary>
    public async UniTask Release()
    {
        await UniTask.CompletedTask;
    }
}

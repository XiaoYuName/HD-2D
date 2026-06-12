using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    private static PlayerInputManager st;
    public static PlayerInputManager St => st != null ? st : st = FindAnyObjectByType<PlayerInputManager>();
    PlayerInputActions input;

    public event Action OnSpace, OnEsc;
    public event Action OnClick, OnRightClick;

    void Awake()
    {
        st = this;
        
        input = new();

        input.Game.Enable();
        input.Game.Space.performed += OnSpaceInvoke;
        input.Game.Esc.performed += OnEscInvoke;
        input.Game.Click.performed += OnClickInvoke;
        input.Game.RightClick.performed += OnRightClickInvoke;
    }

    void OnDestroy()
    {
        if(st == this)
            st = null;

        input.Dispose();
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
}

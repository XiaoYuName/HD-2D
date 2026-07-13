using UnityEngine;
using System;
using Sirenix.OdinInspector;

public class CasinoGameManager : MonoBehaviour
{
    [SerializeField] GameEnterPanelConfig config;
    
    public static CasinoGameManager St => st;
    static CasinoGameManager st;
    
    void Awake()
    {
        st = this;
    }

}

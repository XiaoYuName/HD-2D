using UnityEngine;
using System;
using Sirenix.OdinInspector;

public class CasinoGameManager : MonoBehaviour
{
    [SerializeField] CasinoGameConfig config;
    
    public static CasinoGameManager St => st;
    static CasinoGameManager st;
    
    void Awake()
    {
        st = this;
    }

}

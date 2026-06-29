using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFramework;

public class WordMapController : MonoBehaviour
{
    private List<WordSceneItem> sceneControllers = new List<WordSceneItem>();
    
    private void Start()
    {
        sceneControllers = transform.GetComponentsInChildren<WordSceneItem>().ToList();
        foreach (WordSceneItem sceneController in sceneControllers)
        {
            sceneController.Init();
        }
    }
}

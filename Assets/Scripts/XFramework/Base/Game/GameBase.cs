using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    public abstract class GameBase : SerializedMonoBehaviour
    {
        /// <summary>
        /// 获取子物体对象
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        protected GameObject Get(string path)
        {
            return transform.Find(path).gameObject;
        }

        /// <summary>
        /// 获取自身子物体组件
        /// </summary>
        /// <param name="path">路径</param>
        /// <typeparam name="T">组件</typeparam>
        /// <returns></returns>
        protected T Get<T>(string path) where T: Component
        {
            try
            {
                if (String.IsNullOrEmpty(path))
                {
                    return transform.GetComponent<T>();
                }
                return transform.Find(path).GetComponent<T>();
            }
            catch (Exception)
            {
                Debug.LogError("Paht :" +path + "路径不存在");
                throw;
            }
            
        }
    }
}


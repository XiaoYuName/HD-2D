using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace XFramework
{
    /// <summary>
    /// 数据加载完成后的回调委托
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public delegate void LoadCallBack<in T>(T t);
    
    /// <summary>
    /// 全局单例资源管理器
    /// </summary>
    public class AssetsManager : Singleton<AssetsManager>
    {
        #region UnitygGameObject
        /// <summary>
        /// 缓存对象的跟节点
        /// </summary>
        public Transform PoolRoot;
        /// <summary>
        /// GameObjectLoader 对象池
        /// </summary>
        private Dictionary<string, GameObjectLoader> pools = new Dictionary<string, GameObjectLoader>();
        /// <summary>
        /// 缓存查找表
        /// </summary>
        private Dictionary<GameObject, GameObjectLoader> lookup = new Dictionary<GameObject, GameObjectLoader>();
        public AssetsManager()
        {
            UnityEngine.Transform poolNode = new GameObject("[Asset Pool]").transform;
            poolNode.transform.localPosition = Vector3.zero;
            poolNode.transform.localScale = Vector3.one;
            poolNode.transform.localRotation = Quaternion.identity;
            Object.DontDestroyOnLoad(poolNode);
            PoolRoot = poolNode;
            //TODO: 开启定时器,定时清理缓存
        }
        /// <summary>
        /// 定时清理缓存
        /// </summary>
        public void UpdateTimeReleaseAll()
        {
            foreach (var item in this.pools.Values)
            {
                item.Release();
            }
        }
        /// <summary>
        /// 同步实例化GameObject
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public GameObject Instantiate(string key)
        {
            GameObjectLoader loader;
            if (this.pools.TryGetValue(key, out loader)) //如果对象池中有该对象
            {
                var obj = loader.Instantiate();
                this.lookup.Add(obj,loader);
                return obj;
            }
            else //如果池中没有该对象,则实例化后放入池中
            {
                loader = new GameObjectLoader(key);
                var obj = loader.Instantiate();
                this.pools.Add(key,loader);
                this.lookup.Add(obj,loader);
                return obj;
            }
        }
        /// <summary>
        /// 异步实例化GameObject
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="OnComponet">回调函数</param>
        public void InstantiateAsync(string key, LoadCallBack<GameObject> OnComponet)
        {
            GameObjectLoader loader;
            if (this.pools.TryGetValue(key, out loader)) //如果对象池中有该对象
            {
                var obj = loader.Instantiate();
                this.lookup.Add(obj,loader);
            }
            else //如果池中没有该对象,则实例化后放入池中
            {
                loader = new GameObjectLoader(key);
                loader.InstantiateAsync((OBJGame) =>
                {
                    this.pools.Add(key,loader);
                    this.lookup.Add(OBJGame,loader);
                    OnComponet?.Invoke(OBJGame);
                });
                
            }
        }
        /// <summary>
        /// 获取预制体对象
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public GameObject GetTemplate(string key)
        {
            if (this.pools.TryGetValue(key, out var loader))
            {
                return loader.prefab;
            }
            return null;
        }
        /// <summary>
        /// 将资源释放回缓存池
        /// </summary>
        /// <param name="obj"></param>
        public void FreeGameObject(GameObject obj)
        {
            GameObjectLoader loader;
            if (lookup.TryGetValue(obj, out loader))
            {
                loader.Free(obj);
                lookup.Remove(obj);
            }
        }

        public void RemovePools(string key)
        {
            if (pools.ContainsKey(key))
            {
                pools.Remove(key);
            }
        }

        #endregion

        #region AssetsLoader

        private Dictionary<string, AssetsLoader> AssetsDic = new Dictionary<string, AssetsLoader>();

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <param name="key">键</param>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        public T LoadAssets<T>(string key) where T : Object
        {
            AssetsLoader loader;
            if (AssetsDic.TryGetValue(key, out loader))
            {
                T Assets = loader.LoadAsset<T>();
                return Assets;
            }
            else
            {
                loader = new AssetsLoader(key);
                T Assets = loader.LoadAsset<T>();
                AssetsDic.Add(key,loader);
                return Assets;
            }
        }

        /// <summary>
        /// UniTask 异步加载资源
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public UniTask<T> LoadAssetsUniTask<T>(string key) where T : Object
        {
            AssetsLoader loader;
            if (AssetsDic.TryGetValue(key, out loader))
            {
                return loader.LoadAssetUniTask<T>();
            }
            else
            {
                loader = new AssetsLoader(key);
                UniTask<T> Assets = loader.LoadAssetUniTask<T>();
                AssetsDic.Add(key,loader);
                return Assets;
            }
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="OnComplete">回调函数</param>
        /// <typeparam name="T"></typeparam>
        public void LoadAssetsAsync<T>(string key,LoadCallBack<T> OnComplete) where T : Object
        {
            AssetsLoader loader;
            if (AssetsDic.TryGetValue(key, out loader))
            {
                loader.LoadAssetAsync(OnComplete);
            }
            else
            {
                loader = new AssetsLoader(key);
                loader.LoadAssetAsync<T>((t) =>
                {
                    OnComplete?.Invoke(t);
                });
                AssetsDic.Add(key,loader);
            }
        }

        /// <summary>
        /// Task异步加载资源
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public Task<T> LoadAssetTask<T>(string key) where T : Object
        {
            AssetsLoader loader;
            if (AssetsDic.TryGetValue(key, out loader))
            {
                return loader.LoadAssetTask<T>();
            }
            loader = new AssetsLoader(key);
            AssetsDic.Add(key,loader);
            return loader.LoadAssetTask<T>();//??
        }

        /// <summary>
        /// 携程加载资源
        /// </summary>
        /// <param name="key">Addressable Key键</param>
        /// <param name="OnComplete">加载完成后的回调</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerator LoadAssetsCoroutine<T>(string key, LoadCallBack<T> OnComplete) where T : Object
        {
            AssetsLoader loader;
            if (AssetsDic.TryGetValue(key, out loader))
            {
                yield return loader.LoadAssetCoroutine(OnComplete);
            }
            else
            {
                loader = new AssetsLoader(key);
                yield return loader.LoadAssetCoroutine(OnComplete);
                AssetsDic.Add(key, loader);
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="key"></param>
        public void FreeAsset(string key)
        {
            AssetsLoader loader;
            if (AssetsDic.TryGetValue(key,out loader))
            {
                loader.Free();
            }
        }
        /// <summary>
        /// 资源池移除对象
        /// </summary>
        /// <param name="key"></param>
        public void RemoveAssetsDic(string key)
        {
            if (AssetsDic.ContainsKey(key))
            {
                AssetsDic.Remove(key);
            }
        }

        public void FreeAssets()
        {
            for (int i = 0; i < AssetsDic.Count; i++)
            {
                (string key, AssetsLoader loader) = AssetsDic.ElementAt(i);
                loader.Release();
            }
            AssetsDic.Clear();
        }

        #endregion

        #region SceneLoader

        private Dictionary<string, SceneLoader> SceneDic = new Dictionary<string, SceneLoader>();

        public void LoadScene(string key, LoadSceneMode _mode)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                loader.LoadScene();
            }
            else
            {
                loader = new SceneLoader(key, _mode);
                loader.LoadScene();
                SceneDic.Add(key,loader);
            }
        }

        /// <summary>
        /// 异步加载场景,并将它设置为活动场景
        /// </summary>
        /// <param name="key">Addressable Key</param>
        /// <param name="OnComplete">加载回调</param>
        /// <param name="_mode">加载模式</param>
        public void LoadSceneAsync(string key, Action OnComplete,LoadSceneMode _mode)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                loader.LoadSceneAsync(OnComplete);
            }
            else
            {
                loader = new SceneLoader(key, _mode);
                loader.LoadSceneAsync(OnComplete);
                SceneDic.Add(key,loader);
            }
        }
        
        /// <summary>
        /// 携程加载场景
        /// </summary>
        /// <param name="key">Addressable Key</param>
        /// <param name="_mode">加载模式</param>
        /// <returns></returns>
        public IEnumerator LoadSceneCoroutine(string key, LoadSceneMode _mode)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                yield return loader.LoadSceneCoroutine();
            }
            else
            {
                loader = new SceneLoader(key, _mode);
                yield return loader.LoadSceneCoroutine();
                SceneDic.Add(key,loader);
            }
        }
        
        /// <summary>
        /// UniTask异步加载场景
        /// </summary>
        /// <param name="key"></param>
        /// <param name="_mode"></param>
        public async UniTask LoadSceneUniTask(string key, LoadSceneMode _mode)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                await loader.LoadSceneUniTask();
            }
            else
            {
                loader = new SceneLoader(key, _mode);
                await loader.LoadSceneUniTask();
                SceneDic.Add(key,loader);
            }
        }

        public async UniTask LoadSceneUniTask(string key, LoadSceneMode _mode,IProgress<float> progress)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                await loader.LoadSceneUniTask(progress);
            }
            else
            {
                loader = new SceneLoader(key, _mode);
                await loader.LoadSceneUniTask(progress);
                SceneDic.Add(key,loader);
            }
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="key"></param>
        public void ULoadSceneAsync(string key)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                loader.ULoadSceneAsync();
                SceneDic.Remove(key);
                loader.Release();
            }
        }
        
        /// <summary>
        /// 协程卸载场景
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IEnumerator ULoadSceneCoroutine(string key)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                yield return loader.ULoadSceneCoroutine();
                SceneDic.Remove(key);
                loader.Release();
            }
        }

        /// <summary>
        /// UniTask卸载场景
        /// </summary>
        /// <param name="key"></param>
        public async UniTask ULoadSceneUniTask(string key)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                await loader.ULoadSceneUniTask();
                SceneDic.Remove(key);
                loader.Release();
            }
        }


        #endregion
    }
}

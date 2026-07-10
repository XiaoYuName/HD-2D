using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace XFramework
{
    /// <summary>
    /// 编辑器下资源加载模式。
    /// LocalAssetDatabase 用 AssetDatabase/EditorSceneManager 直接读本地资源，速度快，适合日常开发。
    /// Addressables 使用真实 Addressables 加载链路，适合验证分组、Key、依赖、远程包和发布环境问题。
    /// </summary>
    public enum AssetsLoadMode
    {
        LocalAssetDatabase,
        Addressables,
    }

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
        #region LoadMode

#if UNITY_EDITOR
        private AssetsLoadMode _loadMode = AssetsLoadMode.LocalAssetDatabase;
#else
        private AssetsLoadMode _loadMode = AssetsLoadMode.Addressables;
#endif

        /// <summary>
        /// 当前资源加载模式。打包后始终会使用 Addressables。
        /// </summary>
        public AssetsLoadMode LoadMode => _loadMode;

        /// <summary>
        /// 编辑器下是否使用本地 AssetDatabase/EditorSceneManager 加载。
        /// </summary>
        public bool UseLocalAssetDatabase
        {
            get
            {
#if UNITY_EDITOR
                return _loadMode == AssetsLoadMode.LocalAssetDatabase;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 设置资源加载模式。建议在任何资源加载前设置，避免已有缓存和新模式混用。
        /// </summary>
        public void SetLoadMode(AssetsLoadMode loadMode)
        {
#if UNITY_EDITOR
            if (_loadMode == loadMode)
            {
                return;
            }

            if (HasLoadedCache())
            {
                Debug.LogWarning("切换 AssetsManager 加载模式时已有资源缓存，建议重新进入 Play 或在游戏初始化前设置，避免本地资源和 Addressables 资源混用。");
            }

            _loadMode = loadMode;
#else
            _loadMode = AssetsLoadMode.Addressables;
#endif
        }

        private bool HasLoadedCache()
        {
            return pools.Count > 0
                   || lookup.Count > 0
                   || AssetsDic.Count > 0
                   || SceneDic.Count > 0
                   || AssetReferenceDic.Count > 0;
        }

        #endregion

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
        /// 释放 AssetReference 资源
        /// </summary>
        public void FreeAsset(AssetReference assetReference)
        {
            if (assetReference == null)
            {
                return;
            }

            var key = GetAssetReferenceKey(assetReference);
            AssetReferenceLoader loader;
            if (AssetReferenceDic.TryGetValue(key, out loader))
            {
                loader.Free();
            }
        }
        /// <summary>
        /// 资源池移除对象(非释放方法)
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

        internal void RemoveAssetReferenceDic(string key)
        {
            if (AssetReferenceDic.ContainsKey(key))
            {
                AssetReferenceDic.Remove(key);
            }
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

        public void ULoadScene(string key)
        {
            if (SceneDic.TryGetValue(key, out var loader))
            {
                loader.ULoadSceneAsync();
                SceneDic.Remove(key);
                loader.Release();
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

        #region AssetReferenceLoader
        private Dictionary<string, AssetReferenceLoader> AssetReferenceDic = new Dictionary<string, AssetReferenceLoader>();
        
        /// <summary>
        /// 同步加载 AssetReference 资源
        /// </summary>
        public T LoadAssets<T>(AssetReference assetReference) where T : Object
        {
            if (assetReference == null)
            {
                return null;
            }

            var key = GetAssetReferenceKey(assetReference);
            AssetReferenceLoader loader;
            if (AssetReferenceDic.TryGetValue(key, out loader))
            {
                return loader.LoadAsset<T>();
            }

            loader = new AssetReferenceLoader(assetReference);
            AssetReferenceDic.Add(key, loader);
            return loader.LoadAsset<T>();
        }

        /// <summary>
        /// UniTask 异步加载 AssetReference 资源
        /// </summary>
        public UniTask<T> LoadAssetsUniTask<T>(AssetReference assetReference) where T : Object
        {
            if (assetReference == null)
            {
                return UniTask.FromResult<T>(null);
            }

            var key = GetAssetReferenceKey(assetReference);
            AssetReferenceLoader loader;
            if (AssetReferenceDic.TryGetValue(key, out loader))
            {
                return loader.LoadAssetUniTask<T>();
            }

            loader = new AssetReferenceLoader(assetReference);
            AssetReferenceDic.Add(key, loader);
            return loader.LoadAssetUniTask<T>();
        }

        /// <summary>
        /// 异步加载 AssetReference 资源
        /// </summary>
        public void LoadAssetsAsync<T>(AssetReference assetReference, LoadCallBack<T> OnComplete) where T : Object
        {
            if (assetReference == null)
            {
                OnComplete?.Invoke(null);
                return;
            }

            var key = GetAssetReferenceKey(assetReference);
            AssetReferenceLoader loader;
            if (AssetReferenceDic.TryGetValue(key, out loader))
            {
                loader.LoadAssetAsync(OnComplete);
            }
            else
            {
                loader = new AssetReferenceLoader(assetReference);
                AssetReferenceDic.Add(key, loader);
                loader.LoadAssetAsync(OnComplete);
            }
        }

        /// <summary>
        /// Task 异步加载 AssetReference 资源
        /// </summary>
        public Task<T> LoadAssetTask<T>(AssetReference assetReference) where T : Object
        {
            if (assetReference == null)
            {
                return Task.FromResult<T>(null);
            }

            var key = GetAssetReferenceKey(assetReference);
            AssetReferenceLoader loader;
            if (AssetReferenceDic.TryGetValue(key, out loader))
            {
                return loader.LoadAssetTask<T>();
            }

            loader = new AssetReferenceLoader(assetReference);
            AssetReferenceDic.Add(key, loader);
            return loader.LoadAssetTask<T>();
        }

        /// <summary>
        /// 携程加载 AssetReference 资源
        /// </summary>
        public IEnumerator LoadAssetsCoroutine<T>(AssetReference assetReference, LoadCallBack<T> OnComplete) where T : Object
        {
            if (assetReference == null)
            {
                OnComplete?.Invoke(null);
                yield break;
            }

            var key = GetAssetReferenceKey(assetReference);
            AssetReferenceLoader loader;
            if (AssetReferenceDic.TryGetValue(key, out loader))
            {
                yield return loader.LoadAssetCoroutine(OnComplete);
            }
            else
            {
                loader = new AssetReferenceLoader(assetReference);
                AssetReferenceDic.Add(key, loader);
                yield return loader.LoadAssetCoroutine(OnComplete);
            }
        }

        private static string GetAssetReferenceKey(AssetReference assetReference)
        {
            return assetReference == null ? string.Empty : assetReference.AssetGUID;
        }

        #endregion
    }
}

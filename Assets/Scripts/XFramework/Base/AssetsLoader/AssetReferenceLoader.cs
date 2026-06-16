    using System.Collections;
    using System.Threading.Tasks;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using XFramework;

    /// <summary>
    /// AssetReference 加载器
    /// </summary>
    internal sealed class AssetReferenceLoader
    {
        private readonly string key;
        private readonly AssetReference assetReference;
        private int count;
        private bool isLoader;
        private AsyncOperationHandle _handle;

        public AssetReferenceLoader(AssetReference assetReference)
        {
            this.assetReference = assetReference;
            key = assetReference.AssetGUID;
            count = 0;
            isLoader = false;
        }

        public T LoadAsset<T>() where T : Object
        {
            count++;
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    return _handle.Result as T;
                }
            }
            else
            {
                isLoader = true;
                _handle = assetReference.LoadAssetAsync<T>();
                if (_handle.IsValid())
                {
                    return _handle.WaitForCompletion() as T;
                }
                throw new UnityException("WaitForCompletion not isValid");
            }

            return _handle.Result as T;
        }

        public void LoadAssetAsync<T>(LoadCallBack<T> onComplete) where T : Object
        {
            count++;
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    onComplete?.Invoke(_handle.Result as T);
                }
                else
                {
                    _handle.Completed += result =>
                    {
                        if (result.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                        {
                            onComplete?.Invoke(result.Result as T);
                        }
                        else
                        {
                            Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
                            onComplete?.Invoke(null);
                        }
                    };
                }
                return;
            }

            isLoader = true;
            _handle = assetReference.LoadAssetAsync<T>();
            _handle.Completed += result =>
            {
                if (result.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    onComplete?.Invoke(result.Result as T);
                }
                else
                {
                    Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
                    onComplete?.Invoke(null);
                }
            };
        }

        public Task<T> LoadAssetTask<T>() where T : Object
        {
            count++;
            return LoadAssetTaskInternal<T>();
        }

        private async Task<T> LoadAssetTaskInternal<T>() where T : Object
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    return _handle.Result as T;
                }

                await _handle.Task;
                if (_handle.IsDone && _handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    return _handle.Result as T;
                }

                Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
                return null;
            }

            isLoader = true;
            _handle = assetReference.LoadAssetAsync<T>();
            await _handle.Task;
            if (_handle.IsDone && _handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                return _handle.Result as T;
            }

            Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
            return null;
        }

        public UniTask<T> LoadAssetUniTask<T>() where T : Object
        {
            count++;
            return LoadAssetUniTaskInternal<T>();
        }

        private async UniTask<T> LoadAssetUniTaskInternal<T>() where T : Object
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    return _handle.Result as T;
                }

                await _handle.ToUniTask();
                if (_handle is { IsDone: true, Status: UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded })
                {
                    return _handle.Result as T;
                }

                Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
                return null;
            }

            isLoader = true;
            _handle = assetReference.LoadAssetAsync<T>();
            await _handle.ToUniTask();
            if (_handle is { IsDone: true, Status: UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded })
            {
                return _handle.Result as T;
            }

            Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
            return null;
        }

        public IEnumerator LoadAssetCoroutine<T>(LoadCallBack<T> onComplete) where T : Object
        {
            count++;
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    onComplete?.Invoke(_handle.Result as T);
                    yield break;
                }

                yield return _handle;
                if (_handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    onComplete?.Invoke(_handle.Result as T);
                }
                else
                {
                    Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
                    onComplete?.Invoke(null);
                }

                yield break;
            }

            isLoader = true;
            _handle = assetReference.LoadAssetAsync<T>();
            yield return _handle;
            if (_handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                onComplete?.Invoke(_handle.Result as T);
            }
            else
            {
                Debug.LogError($"资源下载失败Key : {key} ,类型为: {typeof(T)}");
                onComplete?.Invoke(null);
            }
        }

        public void Free()
        {
            count--;
            if (count > 0)
            {
                return;
            }

            if (isLoader && _handle.IsValid())
            {
                Addressables.Release(_handle);
            }

            isLoader = false;
            AssetsManager.Instance.RemoveAssetReferenceDic(key);
        }
    }
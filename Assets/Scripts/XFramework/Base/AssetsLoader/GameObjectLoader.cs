using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XFramework
{
    /// <summary>
    /// Unity 实例化GameObject 资源管理类
    /// </summary>
    public class GameObjectLoader: BaseLoader
    {
        /// <summary>
        /// 资源缓存列表
        /// </summary>
        private Stack<GameObject> caches = new Stack<GameObject>();

        /// <summary>
        /// 正在使用的列表
        /// </summary>
        private HashSet<GameObject> references = new HashSet<GameObject>();

        public GameObject prefab;

        public GameObjectLoader(string Key) : base(Key)
        {
            prefab = null;
        }

        public GameObjectLoader(GameObject prefab,string Key) : base(Key)
        {
            this.prefab = prefab;
            
        }

        public GameObject Instantiate(Transform parent)
        {
            GameObject obj = null;
            if (caches.Count > 0)
            {
                obj = caches.Pop();
                obj.transform.SetParent(parent, false);
                obj.SetActive(true);
            }
            else
            {
                obj = InstantiatePrefab(parent);
                obj.name = this.key;
            }
            this.references.Add(obj);
            return obj;
        }

        public GameObject Instantiate()
        {
            if (caches.Count > 0)
            {
                var Obj = caches.Pop();
                this.references.Add(Obj);
                Obj.SetActive(true);
                return Obj;
            }
            
            if (this.prefab != null)
            {
                var obj = InstantiatePrefab();
                obj.name = key;
                obj.SetActive(true);
                references.Add(obj);
                return obj;
            }
            else
            {
                this.prefab = base.Load<GameObject>();
                var obj = InstantiatePrefab();
                obj.SetActive(true);
                obj.name = key;
                base.Release();
                return obj;
            }
        }

        public void InstantiateAsync(LoadCallBack<GameObject> Call)
        {
            if (caches.Count > 0)
            {
                var Obj = caches.Pop();
                this.references.Add(Obj);
                Obj.SetActive(true);
                Call?.Invoke(Obj);
                return;
            }
            
            if (prefab != null)
            {
                var obj = InstantiatePrefab();
                obj.name = key;
                obj.SetActive(true);
                references.Add(obj);
                Call?.Invoke(obj);
                return;
            }

            base.LoadAsync<GameObject>((obj) =>
            {
                this.prefab = obj;
                var OBJ = InstantiatePrefab();
                OBJ.SetActive(true);
                OBJ.name = key;
                base.Release();
                Call?.Invoke(OBJ);
            });
        }

        public void Free(GameObject obj)
        {
            this.caches.Push(obj);
            this.references.Remove(obj);
            obj.transform.SetParent(AssetsManager.Instance.PoolRoot);
            obj.SetActive(false);
        }

        public override void Release()
        {
            foreach (var obj in this.caches)
            {
                Object.Destroy(obj.gameObject);
            }
            if (this.references.Count <= 0)
            {
                base.Release();
            }
        }

        private GameObject InstantiatePrefab(Transform parent = null)
        {
#if UNITY_EDITOR
            if (this.prefab != null && AssetDatabase.Contains(this.prefab))
            {
                return PrefabUtility.InstantiatePrefab(this.prefab, parent) as GameObject;
            }
#endif
            return parent == null
                ? Object.Instantiate(this.prefab)
                : Object.Instantiate(this.prefab, parent);
        }
    }
}

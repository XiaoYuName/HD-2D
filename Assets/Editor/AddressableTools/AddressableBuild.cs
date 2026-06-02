using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniTaskFramework
{
    /// <summary>
    /// Addressable 自动打包
    /// </summary>
    public class AddressableBuild : Editor
    {
        private static BuildConfiguration _config;
        private const string path = "Assets/Editor/AddressableTools/BuildConfiguration.asset";
        private static string xluagroup;
        /// <summary>
        /// 当前Group组名字
        /// </summary>
        private static string groupName;
        /// <summary>
        /// 当前的Group;
        /// </summary>
        private static AddressableAssetGroup currentGroup;
        
        [MenuItem("Tools/4.自动打包Addressable")]
        public static void Build()
        {
            GetConfiguration();
            if (_config == null) return;
            //currentGroup = _config.LocalAssetSettings;
            //BuildPath("Local-",_config.LocalAssetPath);
            currentGroup = _config.RemoteAssetSettings;
            BuildPath("Remote-",_config.RemoteAssetPath);
        }
        

        
        /// <summary>
        /// 对所给定路径进行遍历
        /// </summary>
        /// <param name="groupprefix">组的前缀</param>
        /// <param name="path">路径</param>
        private static void BuildPath(string groupprefix,string path)
        {
            //1.遍历到所有的文件夹
            string[] Directores = Directory.GetDirectories(path);
            for (int i = 0; i < Directores.Length; i++)
            {
                string fullpath =  Path.GetFullPath(Directores[i]);
                DirectoryInfo directoryInfo = new DirectoryInfo(Directores[i]);
                groupName = groupprefix+directoryInfo.Name;
                DirectoriesBuild(Directores[i],groupName);
            }
        }

        /// <summary>
        /// 对所给定路径进行递归遍历和文件打包
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="groupName">组名字</param>
        private static void DirectoriesBuild(string path,string groupName)
        {
            //1.对路径内的文件进行打包操作
            string[] files = Directory.GetFiles(path);
            files = files.Where(file => !file.EndsWith(".meta")).ToArray();
            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    FileInfo fileInfo = new FileInfo(files[i]);
                    DirectoryInfo directoryInfo = fileInfo.Directory;
                    if (directoryInfo != null)
                    {
                        Object Obj = AssetDatabase.LoadAssetAtPath<Object>(ToUnityPath(files[i]));
                        Debug.Log($"创建Addressable标签: 物体=>{Obj.name} groupname=>{groupName}");
                        SetAddressableTag(Obj,groupName);
                    }
                }
            }
            
            //2.对文件夹进行递归遍历操作
            string[] paths = Directory.GetDirectories(path);
            if (paths.Length > 0)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(paths[i]);
                    string newgroup = groupName + "-"+directoryInfo.Name;
                    DirectoriesBuild(paths[i],newgroup);
                }
            }
            
        }

        private static void GetConfiguration()
        {
            _config = AssetDatabase.LoadAssetAtPath<BuildConfiguration>(path);
            if (_config == null)
            {
                EditorUtility.DisplayDialog("提示", "配置目录不存在!", "关闭");
            }
        }
        
        /// <summary>
        /// 设置Object 物体的标签
        /// </summary>
        /// <param name="UnityObject">Untiy 可识别的Object</param>
        /// <param name="groupname">组名字</param>
        /// <param name="lable">标签 默认不打标签</param>
        private static void SetAddressableTag(Object UnityObject,string groupname = "", string lable = "")
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group;
            //获取group
            if (String.IsNullOrEmpty(groupname))
            {
                group = currentGroup;
            }
            else
            {
                group = settings.FindGroup(groupname);
                if (group == null)
                {
                    if (currentGroup == null)
                    {
                        currentGroup = settings.DefaultGroup;
                    }

                    group = settings.CreateGroup(groupname, false, false, false, currentGroup.Schemas);
                }
            }
            //获取Object 的guid
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(UnityObject));
            //创建AddressableAssetEntry 
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = AssetDatabase.GetAssetPath(UnityObject);
            if (!String.IsNullOrEmpty(lable))
            {
                entry.SetLabel(lable, true, true);
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, settings, true, true);
        }
        
        /// <summary>
        /// 将绝对路径转化为Unity 路径
        /// </summary>
        /// <param name="path">绝对路径</param>
        /// <returns></returns>
        private static string ToUnityPath(string path)
        {
            int past = path.IndexOf("Assets", StringComparison.Ordinal);
            if (past == 0) 
            {
                Console.WriteLine("Empty path!");
                return path;
            }
       
            if (past == -1) 
            {
                Console.WriteLine("The corresponding Assets folder was not found!");
                return path;
            }

            string UnityPath = path.Substring(past);
            return UnityPath;
        }
        
        [MenuItem("Tools/6.清空Addressable标签内容")]
        public static void ClearBuild()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group != null)
                {
                    // 获取group 中所有的entry
                    List<AddressableAssetEntry> entriesToRemove = new List<AddressableAssetEntry>(group.entries);
   
                    foreach (var entry in entriesToRemove)
                    {
                        group.RemoveAssetEntry(entry);
                    }
   
                    // 保存设置
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, group, true, true);
                }
            }
        }
    }
}


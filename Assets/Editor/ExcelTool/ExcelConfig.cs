using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Excel 导入设置
    /// </summary>
    [CreateAssetMenu(fileName = "ExcelConfig",menuName = "Configs/ExcelSettings")]
    public class ExcelConfig : ScriptableObject
    {
        [LabelText("读取Excel 路径"),FolderPath(AbsolutePath = true)]
        public string ExcelPath;

        [Space,Header("CSharp输出路径"),FolderPath]
        public string CSharpExportPath;
        
        [Header("Json输出路径"),FolderPath]
        public string JsonPath;

        [LabelText("Winform路径"),Sirenix.OdinInspector.FilePath]
        public string ExcelToolExEPath;
        
        [Header("是否序列化成数组 : 推荐选择True")]
        public bool isArray = true;

        [Header("生成的管理器命名空间")]
        public string CSharpNameSpace;
    }
}
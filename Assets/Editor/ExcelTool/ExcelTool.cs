
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XFramework
{
    public class ExcelTool : Editor
    {
        private static ExcelConfig Config;
        private const string paht = "Assets/Editor/ExcelTool/ExcelConfig.asset";

        public static void ExportExcel()
        {
            Config = AssetDatabase.LoadAssetAtPath<ExcelConfig>(paht);
            //1.检查目录是否完整
            CheckPaht();
            //2.读取目录内的全部Excel 文件
            string[] ExcelFiles = Directory.GetFiles(Config.ExcelPath);
            if (ExcelFiles is not { Length: > 0 })
            {
                EditorUtility.DisplayDialog("提示", "Excel目录不存在文件", "关闭");
                return;
            }

            //3.
            StarGenerateExcel(ExcelFiles);
            //4. 自动打包Json 文件到Addressables
            AssetDatabase.Refresh();
        }

        private static void CheckPaht()
        {
            if (!Directory.Exists(Config.ExcelPath))
            {
                EditorUtility.DisplayDialog("提示", "Excel目录不存在请检查配置表", "关闭");
                return;
            }

            if (!Directory.Exists(Config.CSharpExportPath))
            {
                Directory.CreateDirectory(Config.CSharpExportPath);
            }

            if (!Directory.Exists(Config.JsonPath))
            {
                Directory.CreateDirectory(Config.JsonPath);
            }

            if (!File.Exists(Config.ExcelToolExEPath))
            {
                EditorUtility.DisplayDialog("提示", "Winform程序不存在,请检查", "关闭");
                return;
            }
        }

        /// <summary>
        /// 开始执行命令行生成
        /// </summary>
        private static void StarGenerateExcel(string[] ExcelFiles)
        {
            Process p = new Process();
            p.StartInfo.FileName = Config.ExcelToolExEPath;
            string args1 = $"--e {Config.ExcelPath}";
            string args2 = $"--j {Config.JsonPath}";
            string args3 = $"--p {Config.CSharpExportPath}";
            string args4 = "--h 2";
            string args5 = "--d dd / MM / yyy hh: mm:ss";
            string args6 = $"--s {Config.ExcelPath}";
            string args7 = $"--a {Config.isArray}";
            string args8 = $"--n {Config.CSharpNameSpace}";
            var format = $"{args1} {args2} {args3} {args4} {args5} {args6} {args7} {args8}";
            p.StartInfo.Arguments = format;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardError = true;
            p.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Debug.LogError(args.Data);
                }
            };
            p.Start();
            p.BeginOutputReadLine();
            p.WaitForExit();
            p.Close();
            p.Dispose();
        }

    }
}


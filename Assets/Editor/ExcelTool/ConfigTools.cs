using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XFramework;
using Debug = UnityEngine.Debug;

public class ConfigTools : EditorWindow
{
    private static string xlsxFolder = string.Empty;
    private static string protoFolder = string.Empty;

    private bool xlsxGenLuaFinished;
    private bool protoGenLuaFinished;
    private List<string> xlsxPathList = new List<string>();
    private Vector2 scrollPos = Vector2.zero;

    private void OnEnable()
    {
        ReadPath();
        RefreshExcelInfo();
    }

    [MenuItem("Tools/LuaConfig _F6")]
    private static void Init()
    {
        GetWindow(typeof(ConfigTools));
        ReadPath();
    }

    private void RefreshExcelInfo()
    {
        xlsxPathList.Clear();
        if (!Directory.Exists(xlsxFolder))
        {
            Debug.LogError("excel路径不存在");
            return;
        }

        string[] pathArray = Directory.GetFiles(xlsxFolder);
        foreach (string pathInfo in pathArray)
        {
            xlsxPathList.Add(pathInfo.Replace("\\", "/"));
        }
    }

    private void DrawExcelInfo()
    {
        foreach (string pathInfo in xlsxPathList)
        {
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
//            GUILayout.Label(pathInfo);
            string excelName = pathInfo.Split('/').Last().Split('.').First();
            if (GUILayout.Button($"打开({excelName})", GUILayout.MaxWidth(500)))
            {
                Process.Start(pathInfo);
            }

            GUILayout.EndHorizontal();
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("生成Excel", EditorStyles.toolbarButton))
        {
            Excel2Lua();
        }

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
        {
            RefreshExcelInfo();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("xlsx path : ", EditorStyles.boldLabel, GUILayout.Width(80));
        xlsxFolder = GUILayout.TextField(xlsxFolder, GUILayout.MaxWidth(800));
        if (GUILayout.Button("...", GUILayout.MaxWidth(200)))
        {
            SelectXlsxFolder();
        }

        GUILayout.EndHorizontal();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.BeginVertical();
        DrawExcelInfo();
        GUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }
    
    private void Excel2Lua()
    {
        if (!CheckXlsxPath(xlsxFolder))
        {
            return;
        }
        
        ExcelTool.ExportExcel();
    }

    private bool CheckXlsxPath(string xlsxPath)
    {
        if (string.IsNullOrEmpty(xlsxPath))
        {
            return false;
        }

        return true;
    }

    private void SelectXlsxFolder()
    {
        var selXlsxPath = EditorUtility.OpenFolderPanel("Select xlsx folder", "", "");
        if (!CheckXlsxPath(selXlsxPath))
        {
            return;
        }

        xlsxFolder = selXlsxPath;
        SavePath();
    }

    private static void SavePath()
    {
        EditorPrefs.SetString("xlsxFolder_" + PlayerSettings.applicationIdentifier, xlsxFolder);
        EditorPrefs.SetString("protoFolder_" + PlayerSettings.applicationIdentifier, protoFolder);
    }

    private static void ReadPath()
    {
        xlsxFolder = EditorPrefs.GetString("xlsxFolder_" + PlayerSettings.applicationIdentifier);
        protoFolder = EditorPrefs.GetString("protoFolder_" + PlayerSettings.applicationIdentifier);
    }
    
    

}
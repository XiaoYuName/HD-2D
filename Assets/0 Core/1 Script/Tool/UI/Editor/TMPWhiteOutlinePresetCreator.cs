using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// 一键生成「白色描边」的 TMP Material Preset。
    /// 描边是材质层面的设置，单独建预设避免污染同字体的其他文本。
    /// 用法：
    ///   1) 在 Project 里选中字体的基础材质（.mat），右键 -> TMP/创建白色描边预设；
    ///   2) 或在 Hierarchy 里选中 TMP 文本（TMP_Text），菜单 -> Tools/TMP/为选中文本创建并应用白色描边预设。
    /// </summary>
    public static class TMPWhiteOutlinePresetCreator
    {
        // 描边白色；宽度 0.2（0~1，受字体 Atlas Padding 限制，过大会被裁切）。
        private const float DefaultOutlineWidth = 0.2f;
        // 纯外描边的关键：Face Dilate 向外推等量，字身边界先扩出去，
        // 描边再叠在外面，这样字身不会被描边吃细（图2效果，而非图1的内描边）。
        private const float DefaultFaceDilate = 0.2f;
        private static readonly Color OutlineColor = Color.white;

        [MenuItem("Assets/TMP/创建白色描边预设", true)]
        private static bool ValidateFromMaterial()
        {
            return Selection.activeObject is Material mat && IsTMPMaterial(mat);
        }

        [MenuItem("Assets/TMP/创建白色描边预设", false, 1100)]
        private static void CreateFromSelectedMaterial()
        {
            var source = Selection.activeObject as Material;
            if (source == null || !IsTMPMaterial(source))
            {
                EditorUtility.DisplayDialog("TMP 白色描边", "请先在 Project 中选中一个 TMP 字体材质（.mat）。", "好");
                return;
            }

            var preset = CreatePreset(source);
            if (preset != null)
            {
                Selection.activeObject = preset;
                EditorGUIUtility.PingObject(preset);
            }
        }

        [MenuItem("Tools/TMP/为选中文本创建并应用白色描边预设")]
        private static void CreateAndApplyToSelectedText()
        {
            var texts = Selection.GetFiltered<TMP_Text>(SelectionMode.Deep);
            if (texts == null || texts.Length == 0)
            {
                EditorUtility.DisplayDialog("TMP 白色描边", "请先在 Hierarchy 里选中带 TMP_Text 的物体。", "好");
                return;
            }

            // 同一个共享材质只生成一份预设，复用以避免增加 Draw Call。
            var cache = new System.Collections.Generic.Dictionary<Material, Material>();
            int applied = 0;
            foreach (var text in texts)
            {
                var source = text.fontSharedMaterial;
                if (source == null || !IsTMPMaterial(source))
                    continue;

                if (!cache.TryGetValue(source, out var preset))
                {
                    preset = CreatePreset(source);
                    cache[source] = preset;
                }

                if (preset != null)
                {
                    Undo.RecordObject(text, "Apply White Outline Preset");
                    text.fontSharedMaterial = preset;
                    EditorUtility.SetDirty(text);
                    applied++;
                }
            }

            Debug.Log($"[TMP 白色描边] 已为 {applied} 个文本应用白色描边预设，生成 {cache.Count} 份材质预设。");
        }

        private static Material CreatePreset(Material source)
        {
            string srcPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(srcPath))
            {
                EditorUtility.DisplayDialog("TMP 白色描边", "源材质不是项目资源，无法生成预设。", "好");
                return null;
            }

            string dir = Path.GetDirectoryName(srcPath);
            string baseName = Path.GetFileNameWithoutExtension(srcPath);
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(dir, $"{baseName} - White Outline.mat").Replace('\\', '/'));

            // 复制源材质，保留字体图集贴图与所有原有属性，仅改描边。
            var preset = new Material(source);
            preset.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, DefaultOutlineWidth);
            preset.SetFloat(ShaderUtilities.ID_FaceDilate, DefaultFaceDilate);

            AssetDatabase.CreateAsset(preset, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMP 白色描边] 已生成预设：{targetPath}\n" +
                      "若描边发虚/被裁切，请重新生成 Font Asset 并调高 Atlas Padding。");
            return preset;
        }

        private static bool IsTMPMaterial(Material mat)
        {
            // TMP 的 SDF shader 都带有 _OutlineColor / _OutlineWidth 属性。
            return mat != null && mat.shader != null && mat.HasProperty(ShaderUtilities.ID_OutlineWidth);
        }
    }
}

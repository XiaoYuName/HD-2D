using UnityEngine;
using UnityEditor;

namespace UrpLiquidShader
{
    public class LiquidShaderGUI : ShaderGUI
    {
        public float CurrentControlValue;
        public int CurrentPropertyIndex;
        public Color MainColor;
        public GUIStyle TitleStyle;
        public GUIStyle IntroStyle;
        public GUIStyle MainStyle;
        public GUIStyle MainInputStyle;
        public Vector2 scrollPos1;

        public bool MainSettingsOpen = true;
        public bool WaveSettingsOpen = true;
        public bool EdgeSettingsOpen = true;
        public bool LightSettingsOpen = true;
        public bool CausticsSettingsOpen = true;
        private GUILayoutOption[] options = new[] { GUILayout.MinWidth(120), GUILayout.MinHeight(50) };

        override public void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            SetStyles();
            //base.OnGUI(materialEditor, properties);
            CheckDrag();
            DrawIntro();
            DrawMainSettings(properties);
            DrawWaveSettings(properties);
            DrawEdgeSettings(properties);
            DrawLightSettings(properties);
            DrawCausticsSettings(properties);
        }

        private void SetStyles()
        {
            TitleStyle = new GUIStyle();
            TitleStyle.fontSize = 20;
            TitleStyle.fontStyle = FontStyle.Bold;
            TitleStyle.richText = true;
            TitleStyle.wordWrap = true;
            TitleStyle.alignment = TextAnchor.MiddleCenter;
            TitleStyle.normal.background = Texture2D.whiteTexture;
            TitleStyle.border = new RectOffset(3, 3, 3, 3);

            IntroStyle = new GUIStyle();
            IntroStyle.fontSize = 16;
            IntroStyle.fontStyle = FontStyle.BoldAndItalic;
            IntroStyle.richText = true;
            IntroStyle.wordWrap = true;
            IntroStyle.alignment = TextAnchor.MiddleCenter;
            IntroStyle.normal.background = Texture2D.normalTexture;
            IntroStyle.border = new RectOffset(5, 5, 5, 5);

            MainStyle = new GUIStyle();
            MainStyle.fontSize = 16;
            MainStyle.normal.textColor = new Color(0.7f, 0.7f, 1f);
            MainStyle.fontStyle = FontStyle.Normal;
            MainStyle.wordWrap = true;
            MainStyle.richText = true;
            MainStyle.alignment = TextAnchor.MiddleCenter;
            MainStyle.normal.background = Texture2D.grayTexture;
            MainStyle.border = new RectOffset(15, 15, 15, 15);
            MainStyle.margin = new RectOffset(3, 3, 3, 3);

            MainInputStyle = new GUIStyle();
            MainInputStyle.fontSize = 18;
            MainInputStyle.normal.textColor = Color.white;
            MainInputStyle.fontStyle = FontStyle.Bold;
            MainInputStyle.wordWrap = true;
            MainInputStyle.richText = true;
            MainInputStyle.alignment = TextAnchor.MiddleCenter;
            MainInputStyle.normal.background = Texture2D.grayTexture;
            MainInputStyle.border = new RectOffset(15, 15, 15, 15);
            MainInputStyle.margin = new RectOffset(3, 3, 3, 3);
        }

        private void DrawIntro()
        {
            if (GUILayout.Button("URP 2D Liquid Shader settings are below. Click here for my other assets!", IntroStyle,
                    GUILayout.ExpandWidth(true)))
            {
                Application.OpenURL("https://assetstore.unity.com/publishers/19358");
            }
        }

        private void CheckDrag()
        {
            if (Event.current.type == EventType.MouseDrag ||
                Event.current.type == EventType.ScrollWheel && CurrentPropertyIndex != -1)
            {
                CurrentControlValue = Event.current.delta.x / 30;
                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.SlideArrow);
            }
            else
            {
                CurrentControlValue = 0;
            }

            if (Event.current.type == EventType.MouseUp)
            {
                CurrentControlValue = 0;
                CurrentPropertyIndex = -1;
            }
        }

        private void DrawWaveSettings(MaterialProperty[] properties)
        {
            EditorGUILayout.Separator();
            string s = WaveSettingsOpen
                ? "<size=20>Wave & Flow Settings ▲</size>"
                : "<size=20>Wave & Flow Settings ▼</size>";
            EditorGUILayout.Separator();
            if (GUILayout.Button(s, TitleStyle, GUILayout.ExpandWidth(true)))
            {
                WaveSettingsOpen = !WaveSettingsOpen;
            }

            if (WaveSettingsOpen)
            {
                //Main Box
                EditorGUILayout.BeginVertical();
                scrollPos1 = EditorGUILayout.BeginScrollView(scrollPos1);
                //First Set
                EditorGUILayout.BeginVertical(MainStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Wave Frequency<size=12><color=#fff>(⟷)</color></size>",
                        "The frequency of the waves. Increased values mean you get more waves mixing in together. On high numbers, some repetitions may be noticed."),
                    MainStyle, options);
                CheckMouseOverLabel(4);
                properties[4].floatValue = EditorGUILayout.FloatField(
                    properties[4].floatValue + (CurrentPropertyIndex == 4 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Wave Speed<size=12><color=#fff>(⟷)</color></size>",
                    "Speed of the wave. Can go very high or negative but ideal range is 0-1"), MainStyle, options);
                CheckMouseOverLabel(5);
                properties[5].floatValue = EditorGUILayout.FloatField("",
                    properties[5].floatValue + (CurrentPropertyIndex == 5 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Flow Speed<size=12><color=#fff>(⟷)</color></size>",
                    "Speed of the inner texture mixing. Adjusts the flow of the main texture."), MainStyle, options);
                CheckMouseOverLabel(3);
                properties[3].floatValue = EditorGUILayout.FloatField("",
                    properties[3].floatValue + (CurrentPropertyIndex == 3 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                //End First Set

                EditorGUILayout.Space();

                //Second Set
                EditorGUILayout.BeginVertical(MainStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Distortion Speed",
                    "Speed of the inside distortion applied to the main texture."), MainStyle, options);
                CheckMouseOverLabel(21);
                properties[21].floatValue = EditorGUILayout.FloatField(
                    properties[21].floatValue + (CurrentPropertyIndex == 21 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Distortion Scale",
                    "Scale of the inside distortion applied to the main texture."), MainStyle, options);
                CheckMouseOverLabel(22);
                properties[22].floatValue = EditorGUILayout.FloatField(
                    properties[22].floatValue + (CurrentPropertyIndex == 22 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                //End Second Set

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                //End Main Box
            }
        }

        private void DrawEdgeSettings(MaterialProperty[] properties)
        {
            EditorGUILayout.Separator();
            string s = EdgeSettingsOpen
                ? "<size=20>Edge Settings ▲</size>"
                : "<size=20>Edge Settings Settings ▼</size>";
            EditorGUILayout.Separator();
            if (GUILayout.Button(s, TitleStyle, GUILayout.ExpandWidth(true)))
            {
                EdgeSettingsOpen = !EdgeSettingsOpen;
            }

            if (EdgeSettingsOpen)
            {
                //Main Box
                EditorGUILayout.BeginVertical();
                scrollPos1 = EditorGUILayout.BeginScrollView(scrollPos1);
                //First Set
                EditorGUILayout.BeginVertical(MainStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Edge Alpha Shape",
                    "This is the main shape that creates the waves. Well known trigonometrical shapes like sine, saw or triangle are recommended here but " +
                    "practically any texture with an alpha channel can be used."), MainStyle, options);
                GUILayout.FlexibleSpace();
                properties[6].textureValue = TextureField("", properties[6].textureValue, GUILayout.Width(80),
                    GUILayout.Height(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Edge Scale<size=12><color=#fff>(⟷)</color></size>",
                        "Scale (on Y axis) of the wave. Basically makes the waves bigger or smaller. Vertically"),
                    MainStyle, options);
                CheckMouseOverLabel(7);
                properties[7].floatValue = EditorGUILayout.FloatField("",
                    properties[7].floatValue + (CurrentPropertyIndex == 7 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Edge Foam Offset<size=12><color=#fff>(⟷)</color></size>",
                    "This determines the scale of the edge foam/glow. Limited at 0-2."), MainStyle, options);
                CheckMouseOverLabel(8);
                properties[8].floatValue = Mathf.Clamp(EditorGUILayout.FloatField("",
                    properties[8].floatValue + (CurrentPropertyIndex == 8 ? CurrentControlValue : 0), MainInputStyle,
                    options), 0, 2);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Edge Foam Color",
                    "Color of the foam. Can go HDR so it can glow under bloom."), MainStyle, options);
                properties[9].colorValue =
                    EditorGUILayout.ColorField(new GUIContent(), properties[9].colorValue, true, false, true,
                        options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                //End First Set



                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                //End Main Box
            }
        }

        private void DrawLightSettings(MaterialProperty[] properties)
        {
            EditorGUILayout.Separator();
            string s = LightSettingsOpen
                ? "<size=20>Light & Glow Settings ▲</size>"
                : "<size=20>Light & Glow Settings ▼</size>";
            EditorGUILayout.Separator();
            if (GUILayout.Button(s, TitleStyle, GUILayout.ExpandWidth(true)))
            {
                LightSettingsOpen = !LightSettingsOpen;
            }

            if (LightSettingsOpen)
            {
                //Main Box
                EditorGUILayout.BeginVertical();
                scrollPos1 = EditorGUILayout.BeginScrollView(scrollPos1);
                //First Set
                EditorGUILayout.BeginVertical(MainStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Light Texture",
                        "Texture to apply as a light source for a more 3D effect. Usually just a centered white radial gradient is enough."),
                    MainStyle, options);
                GUILayout.FlexibleSpace();
                properties[12].textureValue = TextureField("", properties[12].textureValue, GUILayout.Width(80),
                    GUILayout.Height(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Light Position", "Position of the light texture. Wraps around."), MainStyle,
                    options);
                properties[13].vectorValue = EditorGUILayout.Vector2Field("", properties[13].vectorValue, options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Light Tiling", "Tiling to adjust the scale of the light texture."), MainStyle,
                    options);
                properties[14].vectorValue = EditorGUILayout.Vector2Field("", properties[14].vectorValue, options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Light Opacity<size=12><color=#fff>(⟷)</color></size>",
                    "Opacity of the light texture. Can go above 1."), MainStyle, options);
                CheckMouseOverLabel(15);
                properties[15].floatValue = EditorGUILayout.FloatField("",
                    properties[15].floatValue + (CurrentPropertyIndex == 15 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                //End First Set

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                //End Main Box
            }
        }

        private void DrawCausticsSettings(MaterialProperty[] properties)
        {
            EditorGUILayout.Separator();
            string s = CausticsSettingsOpen
                ? "<size=20>Caustics Settings ▲</size>"
                : "<size=20>Caustics Settings Settings ▼</size>";
            EditorGUILayout.Separator();
            if (GUILayout.Button(s, TitleStyle, GUILayout.ExpandWidth(true)))
            {
                CausticsSettingsOpen = !CausticsSettingsOpen;
            }

            if (CausticsSettingsOpen)
            {
                //Main Box
                EditorGUILayout.BeginVertical();
                scrollPos1 = EditorGUILayout.BeginScrollView(scrollPos1);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Caustics Density<size=12><color=#fff>(⟷)</color></size>",
                    "Creates caustics from the provided main texture and adjusts its density."), MainStyle, options);
                CheckMouseOverLabel(16);
                properties[16].floatValue = EditorGUILayout.FloatField("",
                    properties[16].floatValue + (CurrentPropertyIndex == 16 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Caustics Color",
                        "Color of the caustics. Usually just white is fine but can go different. Go HDR to make it glow under bloom."),
                    MainStyle, options);
                properties[17].colorValue =
                    EditorGUILayout.ColorField(new GUIContent(), properties[17].colorValue, true, false, true,
                        options);
                EditorGUILayout.EndHorizontal();
                //First Set
                EditorGUILayout.BeginVertical(MainStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Lens Distortion Texture",
                        "Texture for applying a lens distortion. Whites are distorted, alphas are not."), MainStyle,
                    options);
                GUILayout.FlexibleSpace();
                properties[24].textureValue = TextureField("", properties[24].textureValue, GUILayout.Width(80),
                    GUILayout.Height(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent(
                    "Lens Distortion Amount<size=12><color=#fff>(⟷)</color></size>",
                    "Amount of the lens distortion."), MainStyle, options);
                CheckMouseOverLabel(23);
                properties[23].floatValue = EditorGUILayout.FloatField("",
                    properties[23].floatValue + (CurrentPropertyIndex == 23 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                //End First Set

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                //End Main Box
            }
        }

        private void DrawMainSettings(MaterialProperty[] properties)
        {
            EditorGUILayout.Separator();
            string s = MainSettingsOpen ? "<size=20>Main Settings ▲</size>" : "<size=20>Main Settings ▼</size>";
            EditorGUILayout.Separator();
            if (GUILayout.Button(s, TitleStyle, GUILayout.ExpandWidth(true)))
            {
                MainSettingsOpen = !MainSettingsOpen;
            }

            if (MainSettingsOpen)
            {
                //Main Box
                EditorGUILayout.BeginVertical();
                scrollPos1 = EditorGUILayout.BeginScrollView(scrollPos1);
                //First Set
                EditorGUILayout.BeginVertical(MainStyle);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("DefaultColor",
                        "The main tint of the image you get from the Sprite Renderer or UI Image component"), MainStyle,
                    options);
                properties[1].colorValue =
                    EditorGUILayout.ColorField(new GUIContent(), properties[1].colorValue, true, false, true,
                        options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Texture Tiling",
                        "Main tiling of he sprite texture coming in. Note that this does not affect the wave or light or other textures used."),
                    MainStyle,
                    options);
                properties[25].vectorValue =
                    EditorGUILayout.Vector2Field(new GUIContent(), properties[25].vectorValue, options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Contrast<size=12><color=#fff>(⟷)</color></size>",
                        "Overall contrast. Can go negative or very high but will produce unexpected results at those numbers. Ideally around 0.5 to 2"),
                    MainStyle, options);
                CheckMouseOverLabel(18);
                properties[18].floatValue = EditorGUILayout.FloatField("",
                    properties[18].floatValue + (CurrentPropertyIndex == 18 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Random Seed<size=12><color=#fff>(⟷)</color></size>",
                        "Random seed for various calculations. Advised to change if you're copying a material to create another liquid."),
                    MainStyle, options);
                CheckMouseOverLabel(19);
                properties[19].floatValue = EditorGUILayout.FloatField(
                    properties[19].floatValue + (CurrentPropertyIndex == 19 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                //End First Set

                EditorGUILayout.Space();

                //Second Set
                EditorGUILayout.BeginVertical(MainStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Fill Amount",
                    "Fill amount from bottom to top. 0 is Empty, 1 is Full."), MainStyle, options);
                properties[2].floatValue = EditorGUILayout.Slider(properties[2].floatValue, 0, 1, options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Alpha Mask",
                        "Mask to use. Use this to fit the liquid inside an image like a potion or sphere. Check the examples."),
                    MainStyle, options);
                GUILayout.FlexibleSpace();
                properties[11].textureValue = TextureField("", properties[11].textureValue, GUILayout.Width(80),
                    GUILayout.Height(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Transparency<size=12><color=#fff>(⟷)</color></size>",
                        "Overall transparency. While the main color of the Sprite Renderer or UI Image component also applies transparency," +
                        " this setting can go higher than 1 to produce overflowing results. Ideal range is 0-2 but can go higher."),
                    MainStyle, options);
                CheckMouseOverLabel(10);
                properties[10].floatValue = EditorGUILayout.FloatField("",
                    properties[10].floatValue + (CurrentPropertyIndex == 10 ? CurrentControlValue : 0), MainInputStyle,
                    options);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                //End Second Set

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                //End Main Box
            }
        }

        public void AddHorizontalWrapIfNecessary(int requiredWidth)
        {
            if (EditorGUIUtility.currentViewWidth < requiredWidth)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }

        public void CheckMouseOverLabel(int index)
        {
            var rect = GUILayoutUtility.GetLastRect();
            var pos = Event.current.mousePosition;
            if (rect.Contains(pos))
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.SlideArrow);
                if (Event.current.type == EventType.MouseDown)
                {
                    CurrentControlValue = 0;
                    CurrentPropertyIndex = index;
                    EditorGUI.FocusTextInControl(null);
                }
            }
        }

        private static Texture TextureField(string name, Texture texture, params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical();
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 70;
            GUILayout.Label(name, style);
            var result = (Texture)EditorGUILayout.ObjectField(texture, typeof(Texture), false, options);
            GUILayout.EndVertical();
            return result;
        }
    }
}
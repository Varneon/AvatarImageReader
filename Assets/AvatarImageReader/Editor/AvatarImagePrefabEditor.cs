using System.IO;
using System.Linq;
using AvatarImageDecoder;
using BocuD.VRChatApiTools;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace AvatarImageReader.Editor
{
    [CustomEditor(typeof(AvatarImagePrefab))]
    public class AvatarImagePrefabEditor : UnityEditor.Editor
    {
        private AvatarImagePrefab reader;
        private string text = "";
        private Texture2D output;

        private Vector2 scrollview;

        private int imageWidth;
        private int imageHeight;
        private int imageByteCount;
        private int imageContent;

        private bool init = true;

        private TextStorageObject textStorageObject;

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            reader = (AvatarImagePrefab) target;
            
            if (init)
            {
                if (reader.GetComponentInChildren<TextStorageObject>())
                {
                    textStorageObject = reader.GetComponentInChildren<TextStorageObject>();
                    text = textStorageObject.text;
                }
                else
                {
                    GameObject container = new GameObject("TextStorageObject") {tag = "EditorOnly"};
                    container.transform.SetParent(reader.transform);
                    container.AddComponent<TextStorageObject>();
                    container.hideFlags = HideFlags.HideInHierarchy;
                    init = false;
                }
            }

            GUIStyle bigHeaderStyle = new GUIStyle(EditorStyles.label) {richText = true, fontSize = 15};
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label) {richText = true};

            EditorGUILayout.LabelField("<b>Avatar Image Reader</b>", bigHeaderStyle);

            EditorGUILayout.LabelField("<b>Linked Avatar</b>", headerStyle);

            if (reader.linkedAvatar.IsNullOrWhitespace())
            {
                EditorGUILayout.HelpBox("No avatar is currently selected. AvatarImageReader will not work without linking an avatar.", MessageType.Info);
            }
            VRChatApiToolsEditor.DrawAvatarInspector(reader.linkedAvatar);
            
            if (GUILayout.Button("Change Avatar"))
            {
                AvatarPicker.ApiAvatarSelector(AvatarSelected);
            }
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("<b>Image Options</b>", headerStyle);
            int pixelCount = 0;
            
            reader.imageMode = EditorGUILayout.Popup("Image mode: ", reader.imageMode, new [] {"Cross Platform", "PC Only"});
            
            switch (reader.imageMode)
            {
                case 0:
                    EditorGUILayout.LabelField("Target resolution: ", "128x96");
                    pixelCount = 128 * 96;
                    break;
                
                case 1:
                    EditorGUILayout.HelpBox("You should only use PC Only mode if you are absolutely sure you are going to use all of the space it allows you to use.", MessageType.Warning);
                    EditorGUILayout.LabelField("Target resolution: ", "1200x900");
                    pixelCount = 1200 * 900;
                    break;
            }
            EditorGUILayout.Space(4);


            EditorGUILayout.LabelField("<b>Data encoding</b>", headerStyle);
            reader.dataMode = EditorGUILayout.Popup("Data mode: ", reader.dataMode, new [] {"UTF16 Text", "ASCII Text (not available yet)", "Binary data (not available yet)"});
            reader.dataMode = 0;

            //remove one pixel (header)
            int byteCount = (pixelCount - 1) * 3;

            switch (reader.dataMode)
            {
                case 0:
                    EditorGUILayout.LabelField("Remaining characters: ", $"{byteCount / 2 - text.Length:n0} / {byteCount / 2:n0} ({((float)byteCount / 2 - text.Length) / ((float)byteCount / 2) * 100:n0}%)");

                    EditorGUILayout.BeginVertical();
                    scrollview = EditorGUILayout.BeginScrollView(scrollview);
                    text = EditorGUILayout.TextArea(text);
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    
                    if (text.Length > byteCount / 2)
                    {
                        EditorGUILayout.HelpBox("You are using more characters than the image can fit. Excess characters will be trimmed off.", MessageType.Error);
                    }

                    if (GUILayout.Button("Encode Image"))
                    {
                        imageWidth = reader.imageMode == 0 ? 128 : 1200;
                        imageHeight = reader.imageMode == 0 ? 96 : 900;

                        Texture2D img = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
                        Color[] initialPixels = Enumerable.Repeat(Color.white, imageWidth * imageHeight).ToArray();
                        img.SetPixels(initialPixels);

                        imageByteCount = text.Length * 2 + 1;
                        output = AvatarImageEncoder.EncodeUTF16Text(text, img);
                    }

                    if (output != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUIContent texturePreview = new GUIContent(output) {};
                        GUILayout.Box(texturePreview, GUILayout.Width(128), GUILayout.Height(96));
                        
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField("Image dimensions: ", $"{imageWidth} x {imageHeight}");
                        EditorGUILayout.LabelField("Image contents: ", $"{imageByteCount:n0} Bytes");
                        EditorGUILayout.LabelField("Image data type: ", "UTF16 Characters");

                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Save Image"))
                        {
                            string path = EditorUtility.SaveFilePanel(
                                "Save texture as PNG",
                                Application.dataPath,
                                "output.png",
                                "png");

                            if (path.Length != 0)
                            {
                                byte[] pngData = output.EncodeToPNG();
                                if (pngData != null)
                                    File.WriteAllBytes(path, pngData);

                                path = "Assets" + path.Substring(Application.dataPath.Length);

                                AssetDatabase.WriteImportSettingsIfDirty(path);
                                AssetDatabase.ImportAsset(path);

                                TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(path);
                                importer.npotScale = TextureImporterNPOTScale.None;
                                importer.textureCompression = TextureImporterCompression.Uncompressed;
                                importer.maxTextureSize = 2048;
                                EditorUtility.SetDirty(importer);
                                AssetDatabase.WriteImportSettingsIfDirty(path);

                                AssetDatabase.ImportAsset(path);
                            }
                        }
                        
                        EditorGUI.BeginDisabledGroup(!APIUser.IsLoggedIn || !VRChatApiTools.avatarCache.ContainsKey(reader.linkedAvatar));
                        if (GUILayout.Button("Upload Image to Avatar"))
                        {
                            GameObject temp = new GameObject("ImageUploader") {tag = "EditorOnly"};
                            VRChatApiUploader uploader = temp.AddComponent<VRChatApiUploader>();

                            uploader.SetupAvatarImageUpdate(VRChatApiTools.avatarCache[reader.linkedAvatar], output);
                        }
                        EditorGUI.EndDisabledGroup();
                        
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.HelpBox($"This image will take {imageByteCount/3/reader.stepLength/72} seconds ({imageByteCount/3/reader.stepLength} frames) to decode with the current step length.", MessageType.Info);
                    }
                    break;
                case 1:
                    EditorGUILayout.LabelField("Available characters: ", $"{pixelCount * 3:n0}");
                    break;
                case 2:
                    EditorGUILayout.LabelField("Available data: ", $"{pixelCount * 3:n0} Bytes");
                    break;
            }
            EditorGUILayout.Space(4);
            
            
            EditorGUILayout.LabelField("<b>General Options</b>", headerStyle);
            GUIContent tooltip = new GUIContent()
                {text = "Decode step size", tooltip = "Increasing step size decreases decode time but increases frametimes"};
            reader.stepLength = EditorGUILayout.IntSlider(tooltip, reader.stepLength, 100, 5000);
            EditorGUILayout.Space(2);
            
            EditorGUILayout.LabelField("<i>On decode finish:</i>", headerStyle);
            reader.outputToText = EditorGUILayout.Toggle("Output to TextMeshPro", reader.outputToText);
            if (reader.outputToText)
            {
                reader.outputText = (TextMeshPro) EditorGUILayout.ObjectField("Target TextMeshPro: ", reader.outputText,
                    typeof(TextMeshPro), true);
            }
            reader.callBackOnFinish = EditorGUILayout.Toggle("Send Custom Event", reader.callBackOnFinish);
            if (reader.callBackOnFinish)
            {
                reader.callbackBehaviour = (UdonBehaviour) EditorGUILayout.ObjectField("Target Behaviour: ",
                    reader.callbackBehaviour, typeof(UdonBehaviour), true);
                if (reader.callbackBehaviour != null)
                {
                    reader.callbackEventName = EditorGUILayout.TextField("Event name: ", reader.callbackEventName);
                }
            }
            EditorGUILayout.Space(4);
            
            
            EditorGUILayout.LabelField("<b>Debugging</b>", headerStyle);
            reader.debugLogger = EditorGUILayout.Toggle("Enable debug logging", reader.debugLogger);
            
            if (reader.debugLogger)
            {
                reader.debugTMP = EditorGUILayout.Toggle("Enable logging to TextMeshPro", reader.debugTMP);
                if (reader.debugTMP)
                {
                    reader.loggerText = (TextMeshPro) EditorGUILayout.ObjectField("Target TextMeshPro: ",
                        reader.loggerText, typeof(TextMeshPro), true);
                }
            }
            else
            {
                reader.debugTMP = false;
            }
            EndChangeCheck();
        }
        
        private void EndChangeCheck()
        {
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(reader, "Modify Avatar Image Reader");
                PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));

                textStorageObject.text = text;
                PrefabUtility.RecordPrefabInstancePropertyModifications(textStorageObject);
            }
        }

        private void AvatarSelected(ApiAvatar avatar)
        {
            if (reader == null)
            {
                Debug.LogError("[AvatarImagePrefabEditor] Avatar was selected but inspector target is null; inspector was likely closed.");
            }
            reader.UpdateProxy();
            reader.linkedAvatar = avatar.id;
            reader.ApplyProxyModifications();
        }
    }
}
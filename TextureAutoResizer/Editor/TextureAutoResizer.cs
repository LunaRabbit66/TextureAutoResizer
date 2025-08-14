using UnityEngine;
using UnityEditor;
using System.IO;
using static TreeEditor.TextureAtlas;

public class TextureAutoResizer : EditorWindow
{
    private GameObject targetObject;
    private int thresholdIndex = 7; // デフォルト: 4096
    private int newSizeIndex = 6;   // デフォルト: 2048

    private readonly int[] textureSizes = new int[]
    {
        32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384
    };

    [MenuItem("Tools/TextureAutoResizer")]
    public static void ShowWindow()
    {
        GetWindow<TextureAutoResizer>("TextureAutoResizer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("対象オブジェクト設定", EditorStyles.boldLabel);
        targetObject = (GameObject)EditorGUILayout.ObjectField("対象オブジェクト", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("サイズ設定 (MaxSize)", EditorStyles.boldLabel);
        thresholdIndex = EditorGUILayout.Popup("しきい値", thresholdIndex, GetSizeLabels());
        newSizeIndex = EditorGUILayout.Popup("変更後サイズ", newSizeIndex, GetSizeLabels());

        EditorGUILayout.Space(20);

        if (GUILayout.Button("テクスチャサイズを自動修正する", GUILayout.Height(50)))
        {
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("エラー", "対象オブジェクトを指定してください。", "OK");
                return;
            }
            if (thresholdIndex < 0 || newSizeIndex < 0 || thresholdIndex >= textureSizes.Length || newSizeIndex >= textureSizes.Length)
            {
                EditorUtility.DisplayDialog("エラー", "無効なサイズインデックスです。", "OK");
                return;
            }
            if (thresholdIndex <= newSizeIndex)
            {
                EditorUtility.DisplayDialog("エラー", "変更後サイズはしきい値より小さくなければなりません。", "OK");
                return;
            }

            ProcessTextures();
        }
    }

    private string[] GetSizeLabels()
    {
        string[] labels = new string[textureSizes.Length];
        for (int i = 0; i < textureSizes.Length; i++)
        {
            labels[i] = textureSizes[i].ToString();
        }
        return labels;
    }

    private void ProcessTextures()
    {
        int thresholdSize = textureSizes[thresholdIndex];
        int newSize = textureSizes[newSizeIndex];

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        int processedCount = 0;

        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                Shader shader = mat.shader;
                int propertyCount = ShaderUtil.GetPropertyCount(shader);

                for (int i = 0; i < propertyCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string propName = ShaderUtil.GetPropertyName(shader, i);
                        Texture tex = mat.GetTexture(propName);
                        if (tex is Texture2D tex2D)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(tex2D);
                            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                            if (importer != null && importer.maxTextureSize >= thresholdSize)
                            {
                                importer.maxTextureSize = newSize;
                                importer.SaveAndReimport();

                                processedCount++;
                            }
                        }
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完了", $"処理が完了しました。変更対象: {processedCount} 個", "OK");
    }
}

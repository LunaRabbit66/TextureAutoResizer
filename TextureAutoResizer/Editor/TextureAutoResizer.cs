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

        int processedCount = 0;

        try
        {
            // 対象オブジェクト配下の全ての GameObject/Component を収集
            var objectsToScan = new System.Collections.Generic.List<Object>();
            objectsToScan.Add(targetObject);
            var allComponents = targetObject.GetComponentsInChildren<Component>(true);
            objectsToScan.AddRange(allComponents);

            // 依存関係から使用されている全アセットを取得
            Object[] dependencies = EditorUtility.CollectDependencies(objectsToScan.ToArray());

            // 重複パス排除のためのセット
            var uniqueAssetPaths = new System.Collections.Generic.HashSet<string>();

            foreach (var dep in dependencies)
            {
                if (dep == null) continue;

                try
                {
                    Texture2D tex2D = null;

                    if (dep is Texture2D directTex)
                    {
                        tex2D = directTex;
                    }
                    else if (dep is Sprite sprite && sprite.texture != null)
                    {
                        // Sprite の元テクスチャも対象
                        tex2D = sprite.texture;
                    }
                    else
                    {
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(tex2D);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        // ビルトイン/生成テクスチャ等はスキップ
                        continue;
                    }

                    // 重複処理防止
                    if (!uniqueAssetPaths.Add(assetPath)) continue;

                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null)
                    {
                        // TextureImporter でない場合はスキップ（RenderTexture 等）
                        continue;
                    }

                    // MaxSize がしきい値以上なら縮小
                    if (importer.maxTextureSize >= thresholdSize)
                    {
                        importer.maxTextureSize = newSize;
                        try
                        {
                            importer.SaveAndReimport();
                            processedCount++;
                        }
                        catch (System.Exception reimportEx)
                        {
                            Debug.LogWarning($"[TextureAutoResizer] Reimport 失敗: {assetPath} -> {reimportEx.Message}");
                        }
                    }
                }
                catch (System.Exception perDepEx)
                {
                    Debug.LogWarning($"[TextureAutoResizer] 依存関係処理中に例外: {dep.name} ({dep.GetType().Name}) -> {perDepEx.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TextureAutoResizer] 処理の初期化に失敗しました: {ex.Message}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完了", $"処理が完了しました。変更対象: {processedCount} 個", "OK");
    }
}

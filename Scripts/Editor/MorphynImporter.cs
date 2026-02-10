using UnityEngine;
using UnityEditor;
using System.IO;

namespace Morphyn.Unity.Editor
{
    public class MorphynImporter : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string assetPath in importedAssets)
            {
                if (assetPath.EndsWith(".morphyn") || 
                    assetPath.EndsWith(".mrph") || 
                    assetPath.EndsWith(".morph"))
                {
                    // Force Unity to recognize it as text
                    TextAsset txt = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                    if (txt != null)
                    {
                        Debug.Log($"[Morphyn] Imported script: {assetPath}");
                    }
                }
            }
        }
    }
}
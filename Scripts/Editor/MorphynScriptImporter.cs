#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEngine;
using System.IO;

namespace Morphyn.Unity.Editor
{
    [ScriptedImporter(1, new[] { "morphyn", "mrph", "morph" })]
    public class MorphynScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            TextAsset textAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("main obj", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
}
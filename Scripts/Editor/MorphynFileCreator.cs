using UnityEngine;
using UnityEditor;
using UnityEditor.ProjectWindowCallback; 
using System.IO;

public class MorphynFileCreator
{
    private const string extension = ".morphyn";

    [MenuItem("Assets/Create/Morphyn File", false, 1)]
    public static void CreateFile()
    {
        Texture2D icon = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;

        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<DoCreateMorphynFile>(),
            "NewMorphynScript" + extension,
            icon,
            null);
    }
}

class DoCreateMorphynFile : EndNameEditAction
{
    public override void Action(int instanceId, string pathName, string resourceFile)
    {
        File.WriteAllText(pathName, "# New Morphyn script\n");
        
        AssetDatabase.ImportAsset(pathName);
        Object asset = AssetDatabase.LoadAssetAtPath<Object>(pathName);
        ProjectWindowUtil.ShowCreatedAsset(asset);
    }
}
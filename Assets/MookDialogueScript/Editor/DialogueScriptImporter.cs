using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;

namespace MookDialogueScript.Editor
{
    [ScriptedImporter(1, "mds")]
    public class DialogueScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            TextAsset textAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("text", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
} 
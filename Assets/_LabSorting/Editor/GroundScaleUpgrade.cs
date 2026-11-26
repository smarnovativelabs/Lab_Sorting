using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    // Bakes ItemDefinition.groundScale once in the editor, so the game does no size measuring at runtime.
    // Readable size is the visible face (the two largest model dimensions). Faces below targetFace grow
    // toward it on the floor only, capped at maxScale; racks and the hand keep the model size.
    public static class GroundScaleUpgrade
    {
        public const float targetFace=.2f,maxScale=1.4f;
        public static float ScaleFor(ItemDefinition definition)
        {
            if(!definition||definition.isRat)return 1f;
            var d=new[]{Mathf.Abs(definition.size.x),Mathf.Abs(definition.size.y),Mathf.Abs(definition.size.z)}.OrderByDescending(v=>v).ToArray();
            float face=Mathf.Sqrt(d[0]*d[1]);if(face<=.001f)return 1f;
            float scale=Mathf.Clamp(targetFace/face,1f,maxScale);
            scale=Mathf.Round(scale*100f)/100f;
            return scale<1.03f?1f:scale;
        }
        [MenuItem("Lab Sorting/Bake Ground Scale For Small Items")]
        public static string Apply()
        {
            var report=new StringBuilder();int changed=0;
            foreach(var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                var definition=AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                float scale=ScaleFor(definition);
                if(scale>1f)report.AppendLine(definition.id+" x"+scale.ToString("F2"));
                if(Mathf.Approximately(definition.groundScale,scale))continue;
                Undo.RecordObject(definition,"Bake ground scale");definition.groundScale=scale;EditorUtility.SetDirty(definition);changed++;
            }
            AssetDatabase.SaveAssets();
            var text="GROUND_SCALE_OK changed="+changed+"\n"+report;
            System.IO.File.WriteAllText("Logs/GroundScaleReport.txt",text);Debug.Log(text);
            return text;
        }
    }
}

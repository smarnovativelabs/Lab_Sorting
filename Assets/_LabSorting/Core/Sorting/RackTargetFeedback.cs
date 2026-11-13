using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    // Only rack geometry is cached. Contents and world-space labels retain their materials.
    public sealed class RackTargetFeedback : MonoBehaviour
    {
        struct Surface { public Renderer renderer; public Material[] materials; }
        readonly List<Surface> surfaces=new List<Surface>();
        Material current;
        SortRack owner;
        ItemDefinition preview;
        float completionUntil;
        public bool CompletionActive=>completionUntil>Time.unscaledTime;
        public void CompletionPulse(SortRack rack){owner=rack;completionUntil=Time.unscaledTime+.75f;Show(rack,preview);}
        public void StopCompletion(){completionUntil=0;if(owner)Show(owner,preview);}
        void Update(){if(completionUntil>0&&Time.unscaledTime>=completionUntil){completionUntil=0;if(owner)Show(owner,preview);}}
        public int SurfaceCount=>surfaces.Count;
        public bool Highlighted=>current;
        void Cache(SortRack rack)
        {
            if(surfaces.Count>0)return;
            foreach(var r in rack.GetComponentsInChildren<Renderer>(true))
                if(r!=rack.indicator&&r!=rack.labelIcon&&!r.GetComponentInParent<SortItem>()&&!r.GetComponent<TextMesh>())
                    surfaces.Add(new Surface{renderer=r,materials=r.sharedMaterials});
        }
        public void Show(SortRack rack,ItemDefinition type)
        {
            owner=rack;preview=type;
            // An actionable red/green target always takes priority over a celebration.
            var material=type?(rack.CanAccept(type)?rack.valid:rack.invalid):CompletionActive?rack.valid:null;
            if(current==material)return;
            Cache(rack);current=material;
            foreach(var s in surfaces)if(s.renderer)
            {
                if(!material){s.renderer.sharedMaterials=s.materials;continue;}
                var replacement=new Material[s.materials.Length];
                for(int i=0;i<replacement.Length;i++)replacement[i]=material;
                s.renderer.sharedMaterials=replacement;
            }
        }
        void OnDisable(){foreach(var s in surfaces)if(s.renderer)s.renderer.sharedMaterials=s.materials;current=null;completionUntil=0;preview=null;}
    }
}

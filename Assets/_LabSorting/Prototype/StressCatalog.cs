using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public sealed class StressCatalog:MonoBehaviour
    {
        public SortingSession session;
        public SortItem[] templates;
        public Mesh[] atlasMeshes;
        public Material atlasMaterial;
        public int itemCount=3500,spawnPerFrame=64;
        public bool autoStart=true;
        public Transform generated;
        public bool Ready {get;private set;}
        IEnumerator Start(){if(autoStart)yield return Populate(itemCount);}
        public IEnumerator Populate(int count)
        {
            Ready=false;bool blocked=session.player.blocked;session.player.SetBlocked(true);var batch=session.GetComponent<ItemBatchRenderer>();if(batch)batch.enabled=false;
            if(generated){generated.gameObject.SetActive(false);Destroy(generated.gameObject);yield return null;}
            foreach(var item in session.items)if(item)item.gameObject.SetActive(false);
            generated=new GameObject("StressItems").transform;generated.SetParent(transform,false);
            var items=new SortItem[count];
            for(int i=0;i<count;i++)
            {
                var item=Instantiate(templates[i%templates.Length],generated);item.name="StressItem "+i;item.id="stress-"+i;item.location=ItemLocation.Loose;item.rack=null;item.slot=-1;item.originalScale=new Vector3(.24f,.3f,.24f);
                item.gameObject.SetActive(true);item.Snap(new Vector3((i%70-34.5f)*.7f,.17f,2+(i/70)*.7f),Quaternion.identity);
                item.visual.GetComponent<MeshFilter>().sharedMesh=atlasMeshes[i%atlasMeshes.Length];item.visual.sharedMaterial=atlasMaterial;item.visual.shadowCastingMode=ShadowCastingMode.Off;item.visual.receiveShadows=false;item.visual.lightProbeUsage=LightProbeUsage.Off;item.visual.reflectionProbeUsage=ReflectionProbeUsage.Off;items[i]=item;
                if((i+1)%Mathf.Max(1,spawnPerFrame)==0)yield return null;
            }
            session.items=items;session.RefreshSortingColliders();session.InitializeSaveCache();if(batch){batch.enabled=true;batch.Rebuild();}session.player.SetBlocked(blocked);Physics.SyncTransforms();Ready=true;
        }
    }
}


using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    // The designer catalog grows with new content (4032 -> 4042 items, 46 -> 47 types with lab-rat), so checks
    // compare the session against the scene instead of a fixed count. The baselines are the original content,
    // which no later change may drop below.
    public static class DesignerCatalog
    {
        public const int BaselineItems=4032,BaselineTypes=46;
        public static int SceneItems()=>Object.FindObjectsByType<SortItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        // Every scene item is registered in the session exactly once, with a unique id, and none of the original content is lost.
        public static bool Complete(SortingSession s)=>s.items.Length>=BaselineItems&&s.items.Length==SceneItems()&&s.items.Select(i=>i.id).Distinct().Count()==s.items.Length;
        public static string Describe(SortingSession s)=>s.items.Length+" items ("+SceneItems()+" in scene, "+s.items.Select(i=>i.id).Distinct().Count()+" unique ids)";
    }
}

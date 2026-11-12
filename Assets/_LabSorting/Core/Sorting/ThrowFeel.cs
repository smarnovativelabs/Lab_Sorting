using UnityEngine;
namespace LabSorting.Sorting
{
    // Bounded gameplay weights for packaged props, not solid-material density.
    public static class ThrowFeel
    {
        public static float Weight(ItemDefinition item)
        {
            if(!item)return .25f;
            float size=Mathf.Clamp01(item.size.x*item.size.y*item.size.z/.012f);
            float material=item.placementMaterial==PlacementMaterial.Metal?.3f:item.placementMaterial==PlacementMaterial.Glass?.15f:0;
            return Mathf.Clamp01(size*.7f+material);
        }
        public static float Mass(ItemDefinition item)=>Mathf.Lerp(.12f,2.4f,Weight(item));
        public static float Pitch(ItemDefinition item)=>Mathf.Lerp(1.12f,.82f,Weight(item));
        public static float Gain(ItemDefinition item,float speed)=>Mathf.Lerp(.25f,.65f,Mathf.Clamp01(speed/4))*Mathf.Lerp(.8f,1,Weight(item));
    }
}

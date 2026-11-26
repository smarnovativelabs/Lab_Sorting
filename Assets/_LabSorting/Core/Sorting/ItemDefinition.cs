using UnityEngine;
namespace LabSorting.Sorting
{
    public enum PlacementMaterial { Default, Glass, Plastic, Metal }
    [CreateAssetMenu(menuName="Lab Sorting/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool isRat;
        [Range(1f,1.5f)] public float heldSizeMultiplier=1f;
        public Sprite icon;
        public PlacementMaterial placementMaterial;
        public Color color=Color.white;
        public Vector3 shelfEuler;
        [Tooltip("Model bounds at prefab scale, relative to the pivot. Size-based racks use these to work out how many fit.")]
        public Vector3 center, size;
        [Tooltip("Size multiplier only while the item lies loose on the floor, so tiny products stay readable there. Racks and the hand keep the model size. Baked by GroundScaleUpgrade.")]
        [Range(1f,1.5f)] public float groundScale=1f;
    }
}

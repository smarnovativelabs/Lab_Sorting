using UnityEngine;
namespace LabSorting.Sorting
{
    public enum PlacementMaterial { Default, Glass, Plastic, Metal }
    [CreateAssetMenu(menuName="Lab Sorting/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        public PlacementMaterial placementMaterial;
        public Color color=Color.white;
        public Vector3 shelfEuler;
        [Tooltip("Model bounds at prefab scale, relative to the pivot. Size-based racks use these to work out how many fit.")]
        public Vector3 center, size;
    }
}

using UnityEngine;
namespace LabSorting.Sorting
{
    [CreateAssetMenu(menuName="Lab Sorting/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Color color=Color.white;
        public Vector3 shelfEuler;
    }
}

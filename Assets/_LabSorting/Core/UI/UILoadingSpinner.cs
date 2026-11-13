using UnityEngine;
namespace LabSorting.UI
{
    public sealed class UILoadingSpinner:MonoBehaviour
    {
        void Update(){transform.localRotation=Quaternion.Euler(0,0,-Time.unscaledTime*180);}
        void OnDisable(){transform.localRotation=Quaternion.identity;}
    }
}

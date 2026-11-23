#if UNITY_EDITOR
using UnityEngine;
namespace LabSorting.FeelPreview
{
    // Separate from the preview controller so Reset can cancel animations without cancelling verification.
    public sealed class PreviewVerificationHost : MonoBehaviour { }
}
#endif

using LabSorting.Services;
using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    [DefaultExecutionOrder(10000),RequireComponent(typeof(Text))]
    public sealed class LabLocalizedLabel:MonoBehaviour
    {
        Text label;string source,last;int revision=-1;
        void Awake(){label=GetComponent<Text>();source=label.text;}
        void LateUpdate(){if(label.text!=last)source=label.text;if(label.text==last&&revision==LabLocalization.Revision)return;revision=LabLocalization.Revision;last=LabLocalization.Text(source);label.text=last;}
    }
}

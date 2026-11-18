using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public static class TextFit
    {
        // Width a Text needs for this string on one line at a fixed size (best-fit off), widest line for
        // multi-line strings, in the Text's own canvas units.
        public static float Width(Text text,string value,int size)
        {
            if(!text||string.IsNullOrEmpty(value))return 0;
            var settings=text.GetGenerationSettings(new Vector2(10000,10000));
            settings.resizeTextForBestFit=false;settings.fontSize=size;settings.horizontalOverflow=HorizontalWrapMode.Overflow;settings.verticalOverflow=VerticalWrapMode.Overflow;
            return text.cachedTextGeneratorForLayout.GetPreferredWidth(value,settings)/text.pixelsPerUnit;
        }
        public static int MaxSize(Text text)=>text.resizeTextForBestFit?text.resizeTextMaxSize:text.fontSize;
    }
}

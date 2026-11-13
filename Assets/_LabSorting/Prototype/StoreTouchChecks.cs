#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class StoreTouchChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("STORE TOUCH: "+message);Debug.Log("STORE TOUCH PASS: "+message);}
        public static void Prices(DesignerMenus menus)
        {
            foreach(var label in menus.prices)
            {
                string original=label.text;
                try
                {
                    foreach(string value in new[]{"$1,299.99","PKR 123,456.00","IDR 1.234.567.890","CHF 12’345.90","USD 123,456,789.99"})
                    {
                        label.text=value;Canvas.ForceUpdateCanvases();var generator=new TextGenerator();var settings=label.GetGenerationSettings(label.rectTransform.rect.size);generator.Populate(value,settings);
                        Check(generator.characterCountVisible>=value.Length,"Complete currency string rendered: "+value+" visible="+generator.characterCountVisible);
                        Check(label.resizeTextMinSize>=14&&generator.fontSizeUsedForBestFit>=Mathf.FloorToInt(14*settings.scaleFactor),"Price remains readable at minimum font size: actual="+generator.fontSizeUsedForBestFit+" min="+label.resizeTextMinSize+" scale="+settings.scaleFactor+" rect="+label.rectTransform.rect.size);
                    }
                }
                finally{label.text=original;}
            }
            var ad=menus.cashAdButton.transform.Find("RewardVideoIcon").GetComponent<Image>();Check(ad.sprite&&ad.preserveAspect&&!ad.raycastTarget,"Watch Ad icon is visible without stealing taps");
        }
        public static IEnumerator Run(DesignerMenus menus)
        {
            var guard=menus.storeScrollGuard;Check(guard&&guard.scroll,"Store drag guard connected");
            var test=new GameObject("Isolated store touch probe",typeof(RectTransform),typeof(Image),typeof(Button),typeof(StoreScrollGuard));test.transform.SetParent(guard.transform,false);var relay=test.GetComponent<StoreScrollGuard>();relay.owner=guard;int purchases=0;test.GetComponent<Button>().onClick.AddListener(()=>{if(!guard.Blocked)purchases++;});
            var data=new PointerEventData(EventSystem.current){pointerId=17,button=PointerEventData.InputButton.Left,position=new Vector2(600,350),pressPosition=new Vector2(600,350)};
            float position=guard.scroll.horizontalNormalizedPosition;
            try
            {
                guard.scroll.StopMovement();yield return new WaitForSecondsRealtime(.3f);
                ExecuteEvents.Execute(test,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerClickHandler);Check(purchases==1,"Stationary tap is accepted once");
                ExecuteEvents.Execute(test,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.initializePotentialDrag);ExecuteEvents.Execute(test,data,ExecuteEvents.beginDragHandler);data.position+=new Vector2(-100,0);ExecuteEvents.Execute(test,data,ExecuteEvents.dragHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.endDragHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerClickHandler);Check(purchases==1&&guard.Blocked,"Swipe starting on purchase button cannot buy, even with stray click event");
                guard.scroll.StopMovement();yield return new WaitForSecondsRealtime(.3f);guard.scroll.velocity=new Vector2(500,0);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerClickHandler);Check(purchases==1&&guard.scroll.velocity==Vector2.zero,"Touch stops inertia without accidental purchase");
                yield return new WaitForSecondsRealtime(.3f);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(test,data,ExecuteEvents.pointerClickHandler);Check(purchases==2,"Fresh deliberate tap works after scrolling settles");
                ExecuteEvents.Execute(test,data,ExecuteEvents.beginDragHandler);guard.SendMessage("OnDisable");Check(!guard.Blocked,"Panel closure clears stuck drag state");
            }
            finally{UnityEngine.Object.Destroy(test);guard.scroll.StopMovement();guard.scroll.horizontalNormalizedPosition=position;}
        }
    }
}
#endif

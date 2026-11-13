#if UNITY_EDITOR
using System;
using UnityEngine;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class StoreReferenceChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("STORE REFERENCE: "+message);Debug.Log("STORE REFERENCE PASS: "+message);}
        public static void Run(DesignerMenus menus)
        {
            var card=menus.storeRoot.transform.Find("Card").GetComponent<UnityEngine.UI.Image>();Check(card.sprite.name=="StoreReferencePanel"&&card.useSpriteMesh&&card.preserveAspect,"Store uses designer panel at natural aspect");
            foreach(var button in menus.purchases)
            {
                var tile=(RectTransform)button.transform.parent;var image=tile.GetComponent<UnityEngine.UI.Image>();Check(image.sprite.name=="StoreReferenceProduct"&&Mathf.Abs(tile.rect.width/tile.rect.height-image.sprite.rect.width/image.sprite.rect.height)<.01f,"Product artwork aspect preserved");
                var badge=(RectTransform)tile.Find("PriceBadge");var label=button.transform.Find("PurchaseLabel").GetComponent<UnityEngine.UI.Text>();Check(label.text=="PURCHASE"&&label.alignment==TextAnchor.MiddleCenter&&label.alignByGeometry,"Purchase action label stays centered and independent of price");
                var buy=(RectTransform)button.transform;Check(badge.anchoredPosition.y-badge.rect.height*.5f>buy.anchoredPosition.y+buy.rect.height*.5f,"Price and purchase button never overlap");Check(buy.anchoredPosition.y-buy.rect.height*.5f>tile.rect.yMin+8,"Purchase button stays inside card with padding");Check(button.onClick.GetPersistentEventCount()>0,"Existing purchase callback preserved");
            }
            foreach(var price in menus.prices)Check(price.transform.parent.name=="PriceBadge"&&price.alignment==TextAnchor.MiddleCenter,"Store-provided price is shown in separate centered badge");
        }
    }
}
#endif

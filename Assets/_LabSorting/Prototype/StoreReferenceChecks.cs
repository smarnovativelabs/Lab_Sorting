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
                // One buy button per card shows the store price (the separate badge + PURCHASE pair was merged on request).
                var badge=tile.Find("PriceBadge");var label=button.transform.Find("PurchaseLabel");Check((!badge||!badge.gameObject.activeSelf)&&(!label||!label.gameObject.activeSelf),"Single buy button: no separate price badge or PURCHASE label");
                var buy=(RectTransform)button.transform;Check(buy.anchoredPosition.y-buy.rect.height*.5f>tile.rect.yMin+8,"Buy button stays inside card with padding");Check(button.onClick.GetPersistentEventCount()>0,"Existing purchase callback preserved");
            }
            for(int i=0;i<menus.prices.Length;i++)Check(menus.prices[i].transform.parent==menus.purchases[i].transform&&menus.prices[i].alignment==TextAnchor.MiddleCenter,"Store-provided price is centered on its own buy button");
        }
    }
}
#endif

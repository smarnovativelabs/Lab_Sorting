using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class InventoryPolishChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,limit=Time.realtimeSinceStartup+120;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Inventory check frame clock stopped");yield return null;}}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check,Action<SortingSession,string> capture)
        {
            var initial=s.Capture();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="ThrowTestFloor";floor.transform.position=new Vector3(80,-.15f,80);floor.transform.localScale=new Vector3(10,.3f,10);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="ThrowTestWall";wall.transform.position=new Vector3(80,1.5f,81);wall.transform.localScale=new Vector3(6,3,.2f);wall.SetActive(false);
            var definitions=new ItemDefinition[15];var temporary=new ItemDefinition[15];for(int i=0;i<15;i++)definitions[i]=s.items[i].definition;
            int rejects=0;Action<SortingCue> listener=cue=>{if(cue==SortingCue.Reject)rejects++;};s.Feedback+=listener;
            try
            {
                s.TryPick(s.items[0]);s.TryPick(s.items[1]);check(s.Active.Moving,"Selection fixture starts during pickup");s.Select(0);check(s.Active==s.items[0]&&!s.Active.Moving&&!s.items[1].Moving,"Selection during pickup completes motion safely");
                yield return Wait(.15f);hud.inventoryButtons[1].onClick.Invoke();check(s.Active==s.items[1],"Inventory category button selects its carried type");yield return Wait(.15f);check(economy.magnetLabel.text.Contains(s.Active.definition.displayName)&&economy.searchLabel.text.Contains(s.Active.definition.displayName),"Both paid ability labels follow selected type");
                s.player.SetBlocked(true);hud.inventoryButtons[0].onClick.Invoke();check(s.Active==s.items[1],"Inventory selection cannot bypass modal block");s.player.SetBlocked(false);
                s.Restore(initial);s.capacity=15;
                for(int i=0;i<15;i++){temporary[i]=ScriptableObject.CreateInstance<ItemDefinition>();temporary[i].id="selection-test-"+i;temporary[i].displayName="Type "+i;s.items[i].definition=temporary[i];s.TryPick(s.items[i]);DOTween.CompleteAll();}
                yield return Wait(.15f);check(hud.nextPage.gameObject.activeSelf&&hud.nextPage.interactable,"Inventory paginates categories beyond ten [held="+s.carried.Count+", buttons="+hud.inventoryButtons.Length+", page="+hud.pageLabel.text+"]");hud.nextPage.onClick.Invoke();yield return Wait(.15f);hud.inventoryButtons[4].onClick.Invoke();check(s.Active==s.items[14]&&hud.pageLabel.text=="2 / 2","Last upgraded inventory category remains selectable");
                hud.previousPage.onClick.Invoke();yield return Wait(.15f);hud.inventoryButtons[0].onClick.Invoke();yield return Wait(.15f);check(s.Active==s.items[0]&&hud.inventoryLabels[0].text.StartsWith("> "),"Selected inventory category is visibly marked");capture(s,"Logs/InventorySelection.png");
                int before=rejects;int revision=s.Revision;s.TryPick(s.items[20]);s.TryPick(s.items[20]);s.TryPick(s.items[20]);check(rejects==before+1&&s.Revision==revision+1&&s.MessageIsError,"Repeated identical failures produce one message and feedback event");yield return Wait(.9f);s.TryPick(s.items[20]);check(rejects==before+2,"Error feedback can repeat after cooldown");
                for(int i=0;i<15;i++)s.items[i].definition=definitions[i];s.Restore(initial);
                s.player.character.Motor.SetPosition(new Vector3(80,.01f,80));s.player.look.SetYawPitch(Vector2.zero);s.player.Clear();yield return Wait(.3f);
                s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPick(s.items[1]);DOTween.CompleteAll();s.Select(0);yield return Wait(.3f);Physics.SyncTransforms();
                check(s.TryThrowPosition(s.Active,out var end),"Safe throw finds clear floor landing");var thrown=s.Active;int count=s.carried.Count,cash=s.Cash;s.ThrowActive();yield return Wait(.4f);check(s.carried.Count==count-1&&thrown.location==ItemLocation.Loose&&Vector3.Distance(thrown.transform.position,end)<.01f&&thrown.hitbox.enabled,"Throw releases exactly the selected item to validated destination");check(s.Cash==cash&&!Physics.CheckBox(thrown.hitbox.bounds.center,thrown.hitbox.bounds.extents*.95f,Quaternion.identity,1<<2,QueryTriggerInteraction.Ignore),"Throw preserves cash and stays outside player capsule");
                wall.SetActive(true);Physics.SyncTransforms();yield return Wait(.3f);var held=s.Active;count=s.carried.Count;s.ThrowActive();check(s.Active==held&&s.carried.Count==count&&s.Message.Contains("No safe throw space"),"Wall blocks throw without consuming held item");
                wall.SetActive(false);floor.SetActive(false);Physics.SyncTransforms();s.ThrowActive();check(s.Active==held&&s.carried.Count==count,"Missing floor rejects throw without inventory loss");
                floor.SetActive(true);wall.SetActive(true);wall.transform.position=held.transform.position;wall.transform.localScale=Vector3.one*.15f;Physics.SyncTransforms();check(!s.TryThrowPosition(held,out _),"Obstruction overlapping held item prevents through-wall release");
                wall.SetActive(false);Physics.SyncTransforms();var box=held.hitbox as BoxCollider;
                if(box){var center=box.center;try{box.center=new Vector3(.3f,.4f,0);check(s.TryThrowPosition(held,out _),"Offset collider pivot supports safe throw measurement");check(!box.enabled&&held.location==ItemLocation.Hand,"Throw probing restores held collider state");}finally{box.center=center;}}
            }
            finally
            {
                s.Feedback-=listener;for(int i=0;i<15;i++){s.items[i].definition=definitions[i];if(temporary[i])UnityEngine.Object.Destroy(temporary[i]);}s.player.SetBlocked(false);s.Restore(initial);floor.SetActive(false);wall.SetActive(false);UnityEngine.Object.Destroy(floor);UnityEngine.Object.Destroy(wall);
            }
        }
    }
}




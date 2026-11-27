
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Services;
using UnityEngine.Localization.Settings;

namespace LabSorting.Prototype
{
    // Verification only: current approved natural-drop and English-only contracts.
    public static class CurrentGameplayChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("CURRENT GAMEPLAY: "+message);Debug.Log("CURRENT PASS: "+message);}
        public static IEnumerator Until(Func<bool> ready,string label,float seconds=20)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!ready()&&Time.realtimeSinceStartup<end)yield return null;
            Check(ready(),label);
        }
        // EditMode coroutine ticks can occur between player frames. Wait for actual
        // rendered frames before inspecting Update/LateUpdate-driven presentation.
        public static IEnumerator Frames(int count=2)
        {
            int target=Time.frameCount+count;
            yield return Until(()=>Time.frameCount>=target,"Gameplay frames advance",30);
        }
        public static IEnumerator NaturalDrop()=>Drop(false);
        public static IEnumerator DropSave()=>Drop(true);
        static IEnumerator Drop(bool save)
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();
            bool motor=s.player.character.Motor.enabled,persistence=s.persistence;string oldName=s.saveName;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);var origin=new Vector3(1500,0,1500);
            floor.name="QA natural-drop floor";floor.transform.position=origin+Vector3.down*.1f;floor.transform.localScale=new Vector3(30,.2f,30);
            GameObject blocker=null;string path=null;
            try
            {
                s.persistence=false;s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);s.player.SendMessage("OnApplicationFocus",true);
                s.player.character.Motor.SetPosition(origin+Vector3.up*.02f);s.player.look.SetYawPitch(Vector2.zero);
                yield return new WaitForSecondsRealtime(.4f);s.player.character.Motor.enabled=false;
                // Resume may leave a full magnet collection in hand. Exercise dropping that
                // real held item instead of asking an already-full inventory to pick another.
                var item=s.Active&&!s.Active.definition.isRat?s.Active:s.items.First(i=>!i.definition.isRat&&i.location==ItemLocation.Loose&&!i.Moving);
                if(item.location!=ItemLocation.Hand)Check(s.TryPick(item),"Product pickup accepted");
                yield return Until(()=>!item.Moving,"Pickup finishes");
                blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=s.player.view.transform.position;blocker.transform.localScale=Vector3.one*3;Physics.SyncTransforms();
                int hand=s.carried.Count,cash=s.Cash;s.ThrowActive();
                Check(item.location==ItemLocation.Hand&&s.carried.Count==hand&&s.Cash==cash,"Blocked release preserves held item and wallet");
                blocker.SetActive(false);Physics.SyncTransforms();s.ThrowActive();
                Check(item.location==ItemLocation.Loose&&s.carried.Count==hand-1,"Unblocked drop releases exactly one product");
                Check(Vector3.Distance(item.transform.localScale,item.GroundScale)<.001f,"Successful release restores floor size");
                Check(item.GetComponent<NaturalItemSettling>()&&item.GetComponent<Rigidbody>()&&!item.GetComponent<Rigidbody>().isKinematic,"Release starts real physics");
                if(save)
                {
                    s.saveName="current-drop-"+Guid.NewGuid().ToString("N")+".json";path=s.SavePath;s.persistence=true;
                    s.SendMessage("OnApplicationPause",true);Check(s.FlushSaves()&&s.LastSaveError==null,"Pause during drop writes durable checkpoint");
                    s.persistence=false;Check(s.Restore(initial)&&s.Load(),"Mid-drop checkpoint reloads");s.SendMessage("OnApplicationPause",false);s.player.SendMessage("OnApplicationFocus",true);
                    Check(item.location==ItemLocation.Loose&&s.Cash==cash&&s.carried.Count==hand-1,"Mid-drop reload preserves ownership, cash and hand count");
                }
                yield return Until(()=>!item.Moving,"Natural drop settles",25);yield return null;Physics.SyncTransforms();
                Check(!ItemSupport.Floating(item)&&item.transform.position.y>-.1f,"Released product rests above the floor");
                Check(item.hitbox.enabled&&item.GetComponent<Rigidbody>().isKinematic,"Settled product is targetable and physics sleeps");
                if(save)
                {
                    var position=item.transform.position;var rotation=item.transform.rotation;s.persistence=true;s.Save();Check(s.FlushSaves(),"Settled pose checkpoint durable");s.persistence=false;
                    Check(s.Restore(initial)&&s.Load(),"Settled pose reload succeeds");yield return null;
                    Check(Vector3.Distance(position,item.transform.position)<.02f&&Quaternion.Angle(rotation,item.transform.rotation)<.2f,"Actual settled pose survives reload");
                    Check(s.Cash==cash&&s.carried.Count==hand-1,"Reload never duplicates product or changes cash");
                }
                Check(s.TryPick(item),"Ordinary dropped product remains pickable");item.CompleteMotion();
            }
            finally
            {
                s.FlushSaves();s.persistence=false;foreach(var item in s.items)item.CompleteMotion();s.player.character.Motor.enabled=motor;s.Restore(initial);s.saveName=oldName;s.InitializeSaveCache();s.persistence=persistence;
                floor.SetActive(false);UnityEngine.Object.Destroy(floor);if(blocker)UnityEngine.Object.Destroy(blocker);
                if(path!=null)foreach(var suffix in new[]{"",".bak",".tmp"})if(System.IO.File.Exists(path+suffix))System.IO.File.Delete(path+suffix);
            }
        }
        public static IEnumerator SupportRemoval()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var snapshot=s.Capture();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1500,-.1f,1500);floor.transform.localScale=new Vector3(20,.2f,20);
            var interpolation=new System.Collections.Generic.Dictionary<Rigidbody,RigidbodyInterpolation>();
            try
            {
                s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);s.player.SendMessage("OnApplicationFocus",true);
                var support=s.items.First(i=>i.definition.id=="lab-first-aid-box"&&i.location==ItemLocation.Loose);
                var top=s.items.First(i=>i.definition.id.Contains("bandage")&&i.location==ItemLocation.Loose);
                NaturalItemSettling.Fit(support);NaturalItemSettling.Fit(top);
                // Teleport fixture bodies coherently: interpolated kinematic transforms can
                // otherwise expose their previous pose while these EditMode coroutines run.
                foreach(var item in new[]{support,top}){var body=item.GetComponent<Rigidbody>();interpolation.Add(body,body.interpolation);body.interpolation=RigidbodyInterpolation.None;}
                FixturePose(support,new Vector3(1500,0,1500),Quaternion.identity);
                var bounds=support.GetComponent<NaturalItemSettling>().WorldBounds;
                FixturePose(support,support.transform.position+Vector3.up*(.003f-bounds.min.y),support.transform.rotation);bounds=support.GetComponent<NaturalItemSettling>().WorldBounds;
                Check(Mathf.Abs(bounds.min.y-.003f)<.005f,"Support fixture rests above the floor");
                FixturePose(top,bounds.center+Vector3.up,Quaternion.identity);var b=top.GetComponent<NaturalItemSettling>().WorldBounds;
                FixturePose(top,top.transform.position+new Vector3(bounds.center.x-b.center.x,bounds.max.y+.003f-b.min.y,bounds.center.z-b.center.z),Quaternion.identity);
                top.GetComponent<NaturalItemSettling>().Begin(null);yield return Until(()=>!top.Moving,"Stack settles before support removal");
                float height=top.transform.position.y;Check(!ItemSupport.Floating(top),"Top product supported before pickup");
                Check(top.hitbox.bounds.min.y>ItemSupport.Gap+.03f,"Stack fixture is high enough to require support after removal");
                Check(s.TryPick(support),"Stable support can be picked");support.CompleteMotion();
                float deadline=Time.realtimeSinceStartup+25;
                while(!(top.transform.position.y<height-.03f&&!top.Moving)&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(top.transform.position.y<height-.03f&&!top.Moving,"Unsupported product falls and settles: before="+height+", after="+top.transform.position.y+", moving="+top.Moving+", eligible="+ItemSupport.Eligible(top)+", floating="+ItemSupport.Floating(top)+", bounds="+top.hitbox.bounds+", support bounds="+bounds);
                Check(!ItemSupport.Floating(top),"Top product rests on floor after support removal");
            }
            finally{foreach(var item in s.items)item.CompleteMotion();s.Restore(snapshot);foreach(var pair in interpolation)if(pair.Key)pair.Key.interpolation=pair.Value;floor.SetActive(false);UnityEngine.Object.Destroy(floor);}
        }
        static void FixturePose(SortItem item,Vector3 position,Quaternion rotation)
        {
            item.Snap(position,rotation);var body=item.GetComponent<Rigidbody>();
            if(body){body.position=position;body.rotation=rotation;}Physics.SyncTransforms();
        }
        public static IEnumerator EnglishLanguage()
        {
            yield return LocalizationSettings.InitializationOperation;
            yield return Until(()=>LabLocalization.Ready,"Localization initialized");
            Check(LabLocalization.EnglishOnly,"Current build intentionally uses English only");
            LabLocalization.NextLanguage();yield return null;
            Check(LocalizationSettings.SelectedLocale.Identifier.Code=="en"&&LabLocalization.Text("SETTINGS")=="SETTINGS","Language control preserves English UI");
            Check(LabLocalization.Text("SORTED  12 / 4042")=="SORTED  12 / 4042","Numbers and labels preserved");
            var spanish=LocalizationSettings.AvailableLocales.GetLocale("es");if(spanish){LocalizationSettings.SelectedLocale=spanish;yield return null;Check(LocalizationSettings.SelectedLocale.Identifier.Code=="en","Stale Spanish selection safely returns to English");}
        }
    }
}


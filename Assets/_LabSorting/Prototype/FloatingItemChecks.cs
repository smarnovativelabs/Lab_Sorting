using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    // Loose items are static colliders and never fall by themselves. These checks cover every path that could
    // leave one hanging in the air: authored layout, picking the item underneath, old saves, and the recovery scan.
    public static class FloatingItemChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("FLOATING ITEMS: "+text);Debug.Log("FLOATING PASS: "+text);}
        static IEnumerator Wait(float seconds){float end=Time.time+seconds,limit=Time.realtimeSinceStartup+30;while(Time.time<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Clock stalled");yield return null;}}
        // Distance from the item's bottom to the first thing under its centre.
        static float Clearance(SortItem item)
        {
            Physics.SyncTransforms();var b=item.hitbox.bounds;float best=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(b.center,Vector3.down,20,~0,QueryTriggerInteraction.Ignore))if(!hit.collider.transform.IsChildOf(item.transform))best=Mathf.Min(best,hit.distance-b.extents.y);
            return best;
        }
        static bool ClearColumn(SortItem item,float height)
        {
            var b=item.hitbox.bounds;var column=new Vector3(b.extents.x+.02f,height*.5f,b.extents.z+.02f);
            foreach(var other in Physics.OverlapBox(new Vector3(b.center.x,b.max.y+.02f+height*.5f,b.center.z),column,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))if(!other.transform.IsChildOf(item.transform))return false;
            return true;
        }
        public static IEnumerator Run()=>CurrentGameplayChecks.SupportRemoval();
        static IEnumerator LegacyRun()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var recovery=s.GetComponent<ItemRecovery>();var initial=s.Capture();var fixes=s.layoutFixes;
            try
            {
                s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.3f);Physics.SyncTransforms();
                var floating=s.items.Where(ItemSupport.Floating).Select(i=>i.id).ToList();
                Check(floating.Count==0,"No authored loose item hangs in the air"+(floating.Count>0?": "+string.Join(", ",floating):""));
                Check(s.layoutFixes!=null&&s.layoutFixes.Length>0&&s.layoutFixes.All(f=>s.items.Any(i=>i.id==f.id)),"Scene records the collider-clearance moves for existing saves; fixes="+(s.layoutFixes==null?0:s.layoutFixes.Length));
                // Ground-floor items with nothing above them, away from the player, so fixtures stay clear.
                var feet=s.player.transform.position;
                var pool=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat&&i.transform.position.y<.2f&&Vector3.Distance(i.transform.position,feet)>2).ToList();
                var open=pool.Where(i=>i.hitbox.bounds.size.y<.35f&&ClearColumn(i,1.6f)).Take(40).ToList();
                Check(open.Count>=6,"Enough open ground-floor items for fixtures; found="+open.Count);

                // 1) Picking the item underneath sets the one above it down.
                // The item underneath must be taller than the support gap, or the one above would still count as resting.
                var under=pool.Where(i=>i.hitbox.bounds.size.y>.14f&&i.hitbox.bounds.size.y<.8f&&ClearColumn(i,1.6f)).OrderByDescending(i=>i.hitbox.bounds.size.x*i.hitbox.bounds.size.z).FirstOrDefault();
                Check(under,"A tall enough item to stack on");
                var top=open.Where(i=>i!=under).OrderBy(i=>i.hitbox.bounds.size.x*i.hitbox.bounds.size.z).First();
                var a=under.hitbox.bounds;var t=top.hitbox.bounds;
                top.Snap(top.transform.position+new Vector3(a.center.x-t.center.x,a.max.y+.004f-t.min.y,a.center.z-t.center.z),top.transform.rotation);Physics.SyncTransforms();
                Check(!ItemSupport.Floating(top)&&Clearance(top)<.02f,"An item resting on another item counts as supported");
                var stacked=top.transform.position;int settledBefore=s.SettledItems;
                Check(s.TryPick(under),"Pick the item underneath");yield return Wait(.15f);
                Check(s.SettledItems==settledBefore+1,"Item left above a picked item starts settling");DOTween.CompleteAll();yield return Wait(.1f);
                var landed=top.transform.position;
                Check(!top.Moving&&top.hitbox.enabled&&top.location==ItemLocation.Loose&&!ItemSupport.Floating(top)&&Clearance(top)<.02f,"It comes to rest on the floor with its collider back; clearance="+Clearance(top));
                Check(Mathf.Abs(landed.x-stacked.x)<.001f&&Mathf.Abs(landed.z-stacked.z)<.001f&&landed.y<stacked.y-.02f,"It drops straight down without sliding sideways");
                Check(top.SavedPosition==top.transform.position,"Settled pose is what gets saved");
                s.Restore(initial);DOTween.CompleteAll();yield return null;

                // 2) Old saves: an untouched item at its previous authored pose follows the layout fix; a moved one stays.
                var moved=open[1];var playerMoved=open[2];var hanging=open[3];
                var authoredMoved=moved.transform.position;var authoredPlayer=playerMoved.transform.position;var authoredHanging=hanging.transform.position;
                var oldPose=authoredMoved+new Vector3(.5f,3,0);
                s.layoutFixes=new[]{new SortingSession.LayoutFix{id=moved.id,previous=oldPose},new SortingSession.LayoutFix{id=playerMoved.id,previous=authoredPlayer+new Vector3(.5f,3,0)}};
                var data=s.Capture();
                data.items.First(e=>e.id==moved.id).position=oldPose;
                data.items.First(e=>e.id==playerMoved.id).position=authoredPlayer+new Vector3(.02f,0,0);
                // 3) ...and anything an older build left in the air is set down before it is shown.
                data.items.First(e=>e.id==hanging.id).position=authoredHanging+Vector3.up*1.2f;
                Check(s.RestoreLoadedLayout(data),"Old save with relocated and hanging items loads");
                Check(Vector3.Distance(moved.transform.position,authoredMoved)<.001f&&s.LastLoadRelocated==1,"Untouched relocated item takes its new authored pose; relocated="+s.LastLoadRelocated);
                Check(Vector3.Distance(playerMoved.transform.position,authoredPlayer+new Vector3(.02f,0,0))<.001f,"Item the player moved keeps the player's position");
                var settled=hanging.transform.position;
                Check(s.LastLoadSettled>=1&&!ItemSupport.Floating(hanging)&&Clearance(hanging)<.02f&&Mathf.Abs(settled.y-authoredHanging.y)<.02f,"Hanging item from an old save rests on the floor after load; y="+settled.y+" authored="+authoredHanging.y);
                Check(Mathf.Abs(settled.x-authoredHanging.x)<.001f&&Mathf.Abs(settled.z-authoredHanging.z)<.001f,"Load settles straight down");
                var reload=s.Capture();Check(s.RestoreLoadedLayout(reload)&&s.LastLoadRelocated==0&&s.LastLoadSettled==0,"A fixed save is stable on the next load");
                s.layoutFixes=fixes;s.Restore(initial);DOTween.CompleteAll();yield return null;

                // 4) Recovery scan safety net lowers a hanging item (only after its grace period when automatic).
                var drifting=open[4];var authoredDrifting=drifting.transform.position;
                drifting.Snap(authoredDrifting+Vector3.up*.9f,drifting.transform.rotation);Physics.SyncTransforms();
                Check(ItemSupport.Floating(drifting),"Fixture item hangs in the air");
                Check(recovery.SettleFloating(drifting,true)==false&&ItemSupport.Floating(drifting),"Recovery waits for the grace period first");
                Check(recovery.SettleFloating(drifting,false),"Recovery sets the hanging item down");DOTween.CompleteAll();yield return null;
                Check(!ItemSupport.Floating(drifting)&&Clearance(drifting)<.02f&&Mathf.Abs(drifting.transform.position.y-authoredDrifting.y)<.02f,"Recovered item rests on the floor");
                Check(!recovery.SettleFloating(drifting,false),"A resting item is never moved again");
                // Carried and racked items are never touched.
                Check(s.TryPick(open[5]),"Pick a fixture item");DOTween.CompleteAll();Check(!ItemSupport.Floating(open[5])&&!recovery.SettleFloating(open[5],false)&&open[5].location==ItemLocation.Hand,"Carried items are ignored");
                Debug.Log("FLOATING_ITEMS_OK");
            }
            finally{s.layoutFixes=fixes;DOTween.CompleteAll();s.Restore(initial);s.InitializeSaveCache();}
        }
    }
}

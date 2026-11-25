#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class PhysicsDropEdgeChecks
    {
        const string Report="Logs/PhysicsDropReview/edge-results.txt";
        static void Check(bool ok,string message)
        {
            System.IO.File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");
            if(!ok)throw new Exception("PHYSICS_DROP_EDGE: "+message);
        }
        public static IEnumerator Run(SortingSession s)
        {
            System.IO.File.WriteAllText(Report,"");var saved=s.Capture();bool persistence=s.persistence,motor=s.player.character.Motor.enabled;
            var props=new List<GameObject>();var services=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>b.GetType().Name=="SupplyCommerce"&&b.enabled).ToArray();
            Application.LogCallback log=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception)System.IO.File.AppendAllText("Logs/PhysicsDropReview/errors.txt",message+"\n"+stack+"\n");};
            Application.logMessageReceived+=log;
            Func<string,Vector3,Vector3,GameObject> cube=(name,pos,scale)=>{var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=pos;go.transform.localScale=scale;props.Add(go);return go;};
            try
            {
                s.persistence=false;foreach(var service in services)service.enabled=false;s.player.Clear();s.player.SetBlocked(false);
                var origin=new Vector3(1500,0,1500);cube("Edge test floor",origin+Vector3.down*.1f,new Vector3(30,.2f,30));
                s.player.character.Motor.SetPosition(origin+Vector3.up*.02f);s.player.look.SetYawPitch(Vector2.zero);
                yield return new WaitForSecondsRealtime(.4f);s.player.character.Motor.enabled=false;
                var catalog=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat&&!i.Moving).GroupBy(i=>i.definition.id).Select(g=>g.First()).ToArray();
                int count=0;
                foreach(var item in catalog)
                {
                    // Distribute drops across clear floor while testing every authored shape.
                    s.player.look.SetYawPitch(new Vector2(count*137.5f, count%3==0?35:count%3==1?-25:0));
                    yield return null;
                    Check(s.TryPick(item),"catalog pick "+item.definition.id);
                    // Deliberately release during pickup, the same path as a fast player tap.
                    yield return new WaitForSecondsRealtime(.1f);
                    s.ThrowActive();Check(item.location==ItemLocation.Loose,"catalog immediate drop "+item.definition.id);
                    float end=Time.realtimeSinceStartup+15;while(item.Moving&&Time.realtimeSinceStartup<end)yield return null;
                    Check(!item.Moving&&!ItemSupport.Floating(item),"catalog settles and supported "+item.definition.id);
                    Check(item.transform.position.y>-.05f,"catalog stays above floor "+item.definition.id);
                    count++;
                }
                Check(count>20,"Catalog coverage: "+count+" different item types");
                s.player.look.SetYawPitch(Vector2.zero);yield return null;
                var test=catalog.First(i=>i.definition.id.Contains("bandage"));Check(s.TryPick(test),"Blocked release pickup");test.CompleteMotion();yield return null;
                var blocker=cube("Blocked release enclosure",s.player.view.transform.position,new Vector3(3,3,3));Physics.SyncTransforms();
                s.ThrowActive();Check(test.location==ItemLocation.Hand&&s.Active==test,"Blocked release retains inventory without teleporting");
                blocker.SetActive(false);s.ThrowActive();Check(test.location==ItemLocation.Loose,"Release succeeds after obstruction removed");
                float until=Time.realtimeSinceStartup+15;while(test.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(!test.Moving,"Unblocked item settles");
                var baseItem=catalog.First(i=>i.definition.id=="lab-first-aid-box");
                var supportRotation=Quaternion.Euler(90,0,0);
                baseItem.Snap(origin+new Vector3(4,0,0),supportRotation);
                // Snap suspends compound colliders until LateUpdate. Read bounds only after that refresh.
                yield return null;Physics.SyncTransforms();
                var bounds=baseItem.GetComponent<NaturalItemSettling>().WorldBounds;
                baseItem.Snap(baseItem.transform.position+Vector3.up*(.003f-bounds.min.y),supportRotation);yield return null;Physics.SyncTransforms();
                bounds=baseItem.GetComponent<NaturalItemSettling>().WorldBounds;
                test.Snap(new Vector3(bounds.center.x,bounds.max.y+.1f,bounds.center.z),Quaternion.identity);
                yield return null;Physics.SyncTransforms();
                var topBounds=test.GetComponent<NaturalItemSettling>().WorldBounds;
                test.Snap(test.transform.position+new Vector3(bounds.center.x-topBounds.center.x,bounds.max.y+.003f-topBounds.min.y,bounds.center.z-topBounds.center.z),Quaternion.identity);
                yield return null;
                test.GetComponent<NaturalItemSettling>().Begin(null);until=Time.realtimeSinceStartup+10;
                while(test.Moving&&Time.realtimeSinceStartup<until)yield return null;
                float before=test.transform.position.y;
                Check(!test.Moving&&test.GetComponent<NaturalItemSettling>().WorldBounds.min.y>=bounds.max.y-.02f,"Product rests on flat support before removal; height="+before+" supportTop="+bounds.max.y);
                Check(s.TryPick(baseItem),"Pick product supporting another item");
                yield return new WaitForSecondsRealtime(3);
                Check(test.transform.position.y<before-.03f&&!ItemSupport.Floating(test),"Removing support lets top product fall");
                baseItem.CompleteMotion();s.ThrowActive();until=Time.realtimeSinceStartup+15;while(baseItem.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(baseItem.location==ItemLocation.Loose&&!baseItem.Moving,"Large support product can be dropped again");
                // Rack placement still uses its own animation and state, independent of floor physics.
                var rack=s.racks.First(r=>r.Count==0);var rackItem=catalog.First(i=>!i.Moving&&rack.CanAccept(i.definition));
                Check(s.TryPick(rackItem),"Pick for rack regression");rackItem.CompleteMotion();
                Check(s.TryPlace(rack),"Rack placement accepted");until=Time.realtimeSinceStartup+12;while(rackItem.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(rackItem.location==ItemLocation.Rack&&!rackItem.Moving,"Rack animation finishes normally");
                Check(NaturalItemSettling.ActiveBodies<5,"Physics body activity returns near idle: "+NaturalItemSettling.ActiveBodies);
                System.IO.File.AppendAllText(Report,"COMPLETE catalog="+count+" plus blocked release, support removal, rack placement\n");
            }
            finally
            {
                foreach(var i in s.items)i.CompleteMotion();
                foreach(var go in props){go.SetActive(false);UnityEngine.Object.Destroy(go);}
                s.player.character.Motor.enabled=motor;s.Restore(saved);s.persistence=persistence;
                foreach(var service in services)if(service)service.enabled=true;
                Application.logMessageReceived-=log;
            }
        }
    }
}
#endif

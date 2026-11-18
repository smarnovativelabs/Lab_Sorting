#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using LabSorting.UI;
using LabSorting.Player;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    /// Thumb controls on the real CanvasScaler layout of common phone and tablet ratios (Game view resized),
    /// plus a landscape notch inset. Writes Logs/ThumbControls/<ratio>.png and report.txt.
    public static class ThumbControlsChecks
    {
        static readonly (int w,int h,string name)[] Ratios={(1280,720,"16x9"),(1440,720,"18x9"),(1560,720,"19.5x9"),(1600,720,"20x9"),(1680,720,"21x9"),(1152,720,"16x10"),(960,720,"4x3")};
        const string Folder="Logs/ThumbControls";
        static StringBuilder report;
        static void Check(bool ok,string message){report?.AppendLine((ok?"PASS ":"FAIL ")+message);if(!ok){Flush();throw new Exception("THUMB: "+message);}Debug.Log("THUMB PASS: "+message);}
        static void Flush(){if(report!=null)File.WriteAllText(Folder+"/report.txt",report.ToString());}
        static readonly Vector3[] corners=new Vector3[4];
        // HUD canvas is Screen Space - Overlay, so world corners are screen pixels.
        static Rect Screen(RectTransform r){r.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
        static GameObject Hit(Vector2 position)
        {
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            return hits.Count>0?hits[0].gameObject:null;
        }
        static string Path(GameObject go){if(!go)return "nothing";var t=go.transform;string p=t.name;while(t.parent&&t.parent.name!="SafeArea"){t=t.parent;p=t.name+"/"+p;}return p;}
        static PointerEventData Pointer(int id,Vector2 position,GameObject target)
        {
            var e=new PointerEventData(EventSystem.current){pointerId=id,position=position,button=PointerEventData.InputButton.Left};
            e.pointerPressRaycast=e.pointerCurrentRaycast=new RaycastResult{gameObject=target,module=target.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>()};return e;
        }
        static void Send<T>(GameObject target,PointerEventData e,ExecuteEvents.EventFunction<T> function) where T:IEventSystemHandler=>ExecuteEvents.ExecuteHierarchy(target,e,function);

        // ---------- Game view size (editor only) ----------
        static object view;static System.Reflection.PropertyInfo selected;static int originalIndex=-1;
        static readonly List<string> added=new List<string>();
        static object Group(out Type groupType,out Type sizeType)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetProperty("currentGroup").GetValue(sizes);groupType=group.GetType();sizeType=assembly.GetType("UnityEditor.GameViewSize");return group;
        }
        // Put the Game view back and drop the sizes this run added, so the user's size list stays as it was.
        static void RestoreGameView()
        {
            if(view==null)return;
            var group=Group(out var groupType,out var sizeType);
            for(int i=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null)-1;i>=0;i--)
            {var size=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});if(added.Contains((string)sizeType.GetProperty("baseText").GetValue(size)))groupType.GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}
            if(originalIndex>=0)selected.SetValue(view,originalIndex);
            added.Clear();originalIndex=-1;view=null;
        }
        static void GameView(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var group=Group(out var groupType,out var sizeType);var fixedMode=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            int count=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null),index=-1;
            for(int i=0;i<count&&index<0;i++)
            {
                var size=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
                if(Equals(sizeType.GetProperty("sizeType").GetValue(size),fixedMode)&&(int)sizeType.GetProperty("width").GetValue(size)==width&&(int)sizeType.GetProperty("height").GetValue(size)==height)index=i;
            }
            if(index<0){var name="Thumb check "+width+"x"+height;groupType.GetMethod("AddCustomSize").Invoke(group,new[]{Activator.CreateInstance(sizeType,new object[]{fixedMode,width,height,name})});added.Add(name);index=count;}
            view=UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            selected=view.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if(originalIndex<0)originalIndex=(int)selected.GetValue(view);
            selected.SetValue(view,index);
        }
        static IEnumerator Resize(int width,int height)
        {
            GameView(width,height);float limit=Time.realtimeSinceStartup+8;
            while((UnityEngine.Screen.width!=width||UnityEngine.Screen.height!=height)&&Time.realtimeSinceStartup<limit)yield return null;
            Check(UnityEngine.Screen.width==width&&UnityEngine.Screen.height==height,"Game view resized to "+width+"x"+height);
            for(int i=0;i<3;i++)yield return null;Canvas.ForceUpdateCanvases();
        }
        static IEnumerator Capture(string name){UnityEngine.ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");for(int i=0;i<3;i++)yield return null;yield return new WaitForSecondsRealtime(.15f);}

        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            Directory.CreateDirectory(Folder);report=new StringBuilder("Thumb controls review "+DateTime.Now+"\n");
            s.player.SendMessage("OnApplicationFocus",true);s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var canvas=hud.GetComponent<Canvas>();var safePanel=hud.GetComponentInChildren<SafeAreaPanel>();var safe=(RectTransform)safePanel.transform;
            var sizes=hud.GetComponent<ControlSizes>();var actions=hud.GetComponent<ContextActionHUD>();var economy=hud.GetComponent<EconomyHUD>();var task=hud.GetComponent<TaskCardLayout>();
            var touches=hud.GetComponentsInChildren<LabTouchArea>(true);var stick=touches.Single(t=>t.movement);var look=touches.Single(t=>!t.movement);
            var stickRect=(RectTransform)stick.transform;var zone=stick.floatingZone;var group=stick.GetComponent<CanvasGroup>();
            Check(zone&&zone.GetComponent<LabTouchZone>()?.joystick==stick&&group,"Floating joystick zone wired");
            Check(zone.GetSiblingIndex()>look.transform.GetSiblingIndex()&&zone.GetSiblingIndex()<stickRect.GetSiblingIndex(),"Zone sits above the look area and below the stick");
            var pick=(RectTransform)actions.pick.transform;var place=(RectTransform)actions.place.transform;var drop=(RectTransform)actions.drop.transform;
            var search=(RectTransform)economy.searchButton.transform;var magnet=(RectTransform)economy.magnetButton.transform;
            var pagers=new[]{"InventoryNext","InventoryPage"}.Select(n=>safe.Find(n) as RectTransform).Where(r=>r&&r.gameObject.activeInHierarchy).ToArray();
            var top=new[]{"CashOffers","DesignerStoreButton","Wallet","DesignerTitle_Progress"}.Select(n=>safe.Find(n) as RectTransform).Where(r=>r&&r.gameObject.activeInHierarchy).ToArray();
            float joy=sizes.JoystickScale,act=sizes.ActionScale;
            try
            {
                var runs=new List<(int w,int h,string name,bool notch)>(Ratios.Select(r=>(r.w,r.h,r.name,false))){(1600,720,"20x9-notch",true)};
                foreach(var ratio in runs)
                {
                    yield return Resize(ratio.w,ratio.h);
                    if(ratio.notch){safePanel.enabled=false;safePanel.ApplySample(new Rect(90,0,ratio.w-180,ratio.h),new Vector2Int(ratio.w,ratio.h));}else{safePanel.enabled=true;safePanel.Apply();}
                    Canvas.ForceUpdateCanvases();yield return null;
                    float unit=canvas.scaleFactor;var area=Screen(safe);string at=" at "+ratio.name+" (canvas "+Mathf.RoundToInt(UnityEngine.Screen.width/unit)+"x"+Mathf.RoundToInt(UnityEngine.Screen.height/unit)+")";
                    report.AppendLine("--- "+ratio.name+" "+ratio.w+"x"+ratio.h+" scale="+unit.ToString("F3"));

                    // Layout at every control-size setting the player can pick.
                    foreach(float scale in new[]{.85f,1f,1.15f})
                    {
                        sizes.Apply(scale,scale);Canvas.ForceUpdateCanvases();
                        var controls=new[]{stickRect,pick,place,drop,search,magnet};
                        foreach(var c in controls){var r=Screen(c);Check(r.xMin>=area.xMin-1&&r.xMax<=area.xMax+1&&r.yMin>=area.yMin-1&&r.yMax<=area.yMax+1,c.name+" inside safe area x"+scale+at);}
                        for(int i=0;i<controls.Length;i++)for(int j=i+1;j<controls.Length;j++)Check(!Screen(controls[i]).Overlaps(Screen(controls[j])),controls[i].name+" clear of "+controls[j].name+" x"+scale+at);
                        foreach(var (a,b) in new[]{(pick,place),(pick,drop),(place,drop),(drop,magnet)})
                        {float gap=(Vector2.Distance(Screen(a).center,Screen(b).center)-(Screen(a).width+Screen(b).width)*.5f)/unit;Check(gap>=10,a.name+"-"+b.name+" finger gap "+gap.ToString("F0")+" units x"+scale+at);}
                        foreach(var c in new[]{pick,place,drop,search,magnet})Check(Screen(c).width/unit>=52,c.name+" touch size "+(Screen(c).width/unit).ToString("F0")+" units x"+scale+at);
                        foreach(var pager in pagers)foreach(var c in new[]{pick,place,drop})Check(!Screen(c).Overlaps(Screen(pager)),c.name+" clear of "+pager.name+" x"+scale+at);
                        var card=Screen(task.card);
                        Check(card.xMin>=area.xMin-1&&card.xMax<=area.xMax+1&&card.yMin>=area.yMin-1&&card.yMax<=area.yMax+1,"Task card inside safe area x"+scale+at);
                        foreach(var tool in new[]{search,magnet})Check(card.yMin>Screen(tool).yMax+8*unit||card.xMax+8*unit<Screen(tool).xMin,"Task card leaves "+tool.name+" clear x"+scale+at);
                        foreach(var other in top)Check(!card.Overlaps(Screen(other)),"Task card clear of "+other.name+at);
                    }
                    sizes.Apply(1,1);Canvas.ForceUpdateCanvases();yield return null;
                    var cardSize=Screen(task.card).size/unit;Check(Mathf.Abs(cardSize.x-352)<2,"Task card is 80% width ("+cardSize.x.ToString("F0")+"x"+cardSize.y.ToString("F0")+" units)"+at);

                    // Who receives a touch where.
                    Vector2 P(float x,float y)=>new Vector2(Mathf.Lerp(area.xMin,area.xMax,x),Mathf.Lerp(area.yMin,area.yMax,y));
                    var stickCenter=Screen(stickRect).center;Check(Hit(stickCenter)?.transform.IsChildOf(stickRect)==true,"Stick itself takes a press on it"+at);
                    var zonePoint=P(.22f,.45f);var zoneHit=Hit(zonePoint);Check(zoneHit==zone.gameObject,"Left-thumb area goes to the floating zone, got "+Path(zoneHit)+at);
                    // Right-thumb rest spot (300 x 260 units in from the corner), screen middle and the upper left all turn the camera.
                    var lookPoints=new[]{new Vector2(area.xMax-300*unit,area.yMin+260*unit),P(.75f,.6f),P(.3f,.86f),P(.62f,.4f)};
                    foreach(var point in lookPoints){var hit=Hit(point);Check(hit==look.gameObject,"Camera drag reaches "+((Vector2)point).ToString("F0")+", got "+Path(hit)+at);}

                    // Floating stick: press beside it, it jumps under the thumb, walks, and returns home on release.
                    Check(!s.player.blocked,"Player input free for touch checks"+at);
                    var home=stickRect.anchoredPosition;Check(Mathf.Abs(group.alpha-stick.idleAlpha)<.01f,"Stick rests faded"+at);
                    var press=Pointer(41,zonePoint,zone.gameObject);Send(zone.gameObject,press,ExecuteEvents.pointerDownHandler);
                    Check(stick.Floated&&Vector2.Distance(Screen(stickRect).center,zonePoint)<2,"Stick jumps under the thumb"+at);Check(group.alpha>.99f,"Stick fully visible while held"+at);
                    press.position=zonePoint+Vector2.right*stickRect.rect.width*.35f*stickRect.lossyScale.x;Send(zone.gameObject,press,ExecuteEvents.dragHandler);
                    Check(s.player.Move.x>.95f,"Floating stick reaches full speed"+at);
                    // Second finger on the camera at the same time.
                    float yaw=s.player.look.Yaw;var lookPoint=lookPoints[0];var swipe=Pointer(42,lookPoint,look.gameObject);
                    Send(look.gameObject,swipe,ExecuteEvents.pointerDownHandler);swipe.delta=new Vector2(70,0);swipe.position+=swipe.delta;Send(look.gameObject,swipe,ExecuteEvents.dragHandler);
                    yield return null;yield return null;
                    Check(Mathf.Abs(Mathf.DeltaAngle(yaw,s.player.look.Yaw))>.5f&&s.player.Move.x>.95f,"Walk and turn together (floating stick + camera finger)"+at);
                    if(!ratio.notch&&ratio.name=="20x9")yield return Capture("20x9-floating");
                    Send(look.gameObject,swipe,ExecuteEvents.pointerUpHandler);Send(zone.gameObject,press,ExecuteEvents.pointerUpHandler);
                    Check(!stick.Floated&&stickRect.anchoredPosition==home&&s.player.Move==Vector2.zero&&Mathf.Abs(group.alpha-stick.idleAlpha)<.01f,"Release returns stick home, stops and fades"+at);
                    // Press on the stick itself keeps the fixed-stick feel.
                    var direct=Pointer(43,stickCenter,stickRect.gameObject);Send(stickRect.gameObject,direct,ExecuteEvents.pointerDownHandler);Check(!stick.Floated,"Press on the stick does not move it"+at);
                    direct.position=stickCenter+Vector2.up*stickRect.rect.width*.35f*stickRect.lossyScale.x;Send(stickRect.gameObject,direct,ExecuteEvents.dragHandler);Check(s.player.Move.y>.95f,"Fixed stick still walks forward"+at);
                    Send(stickRect.gameObject,direct,ExecuteEvents.pointerUpHandler);Check(s.player.Move==Vector2.zero,"Fixed stick release stops"+at);

                    // Screenshot with every action button showing.
                    actions.enabled=false;foreach(var g in new[]{actions.pick,actions.place,actions.drop}){g.alpha=1;}
                    yield return Capture(ratio.name);actions.enabled=true;actions.Refresh();
                    report.AppendLine("pick "+Screen(pick).center/unit+" place "+Screen(place).center/unit+" drop "+Screen(drop).center/unit+" search "+Screen(search).center/unit+" magnet "+Screen(magnet).center/unit+" card "+Screen(task.card));
                }
                report.AppendLine("ALL PASS");Debug.Log("THUMB_CONTROLS_OK ratios="+(Ratios.Length+1));
            }
            finally
            {
                Flush();actions.enabled=true;sizes.Apply(joy,act);safePanel.enabled=true;safePanel.Apply();
                RestoreGameView();
            }
        }
    }
}
#endif

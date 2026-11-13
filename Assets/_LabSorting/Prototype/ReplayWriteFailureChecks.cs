#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Prototype
{
    public static class ReplayWriteFailureChecks
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string text){if(!ok)throw new Exception("REPLAY WRITE: "+text);Debug.Log("REPLAY WRITE PASS: "+text);}
        static void Writer(SortingSession s,DurableSaveWriter writer){typeof(SortingSession).GetField("saveWriter",Private).SetValue(s,writer);typeof(SortingSession).GetField("writerPath",Private).SetValue(s,s.SavePath);}
        static void Compare(SortingSession s,SortingSession.Snapshot expected,int sorted)
        {
            var now=s.Capture();Check(s.SortedCount==sorted&&now.cash==expected.cash&&now.capacity==expected.capacity&&now.searchCredits==expected.searchCredits&&now.magnetCredits==expected.magnetCredits,"Cash, progress, tools and upgrades unchanged");
            Check(now.completionCount==expected.completionCount&&now.completedContentVersion==expected.completedContentVersion&&now.supplyReceipts.OrderBy(x=>x).SequenceEqual(expected.supplyReceipts.OrderBy(x=>x)),"Completion history and purchase receipts unchanged");CommerceEntitlements.ResetCacheForTests();Check(CommerceEntitlements.RemoveAds,"Remove Ads remains owned");
        }
        public static void BeforeReplay(SortingSession s,LabCompletionPanel panel)
        {
            Check(s.persistence&&s.saveName.StartsWith("offers-replay-test-"),"Failure injection is isolated from player save");var expected=s.Capture();Check(s.FlushSaves(),"All prior writes complete before injection");
            try
            {
                foreach(int failedWrite in new[]{1,2,3})
                {
                    int calls=0;var writer=new DurableSaveWriter(s.SavePath,(path,json,preserve)=>{if(++calls==failedWrite){File.WriteAllText(path+".tmp","partial replay write");throw new IOException("Injected replay write failure");}DurableSaveWriter.AtomicWrite(path,json,preserve);});Writer(s,writer);
                    if(!panel.IsOpen)typeof(LabCompletionPanel).GetMethod("Show",Private).Invoke(panel,null);
                    if(!panel.ConfirmingReplay)panel.PlayAgain();panel.PlayAgain();
                    Check(panel.IsOpen&&panel.ConfirmingReplay&&panel.summary.text.Contains("unchanged"),"Failed replay keeps confirmation open with retry feedback: write "+failedWrite);Compare(s,expected,4032);
                    Check(s.RequestSave(true)&&s.FlushSaves(),"Retry succeeds after write failure clears");Check(s.Load(),"Completed run reloads after failed replay");Compare(s,expected,4032);
                    Check(!File.Exists(s.SavePath+".tmp"),"Partial temporary file is replaced, never loaded");
                }
                bool blocked=true;int attempts=0;
                var persistent=new DurableSaveWriter(s.SavePath,(path,json,preserve)=>{if(++attempts>1&&blocked)throw new IOException("Persistent storage failure");DurableSaveWriter.AtomicWrite(path,json,preserve);});Writer(s,persistent);
                if(!panel.IsOpen)typeof(LabCompletionPanel).GetMethod("Show",Private).Invoke(panel,null);
                if(!panel.ConfirmingReplay)panel.PlayAgain();panel.PlayAgain();
                Check(panel.IsOpen&&panel.ConfirmingReplay,"Persistent failure keeps replay retry available");Compare(s,expected,4032);
                blocked=false;persistent.Retry();Check(persistent.Flush(),"Pending rollback persists when storage returns");Check(s.Load(),"Persistent failure recovers completed run");Compare(s,expected,4032);
            }
            finally{Writer(s,new DurableSaveWriter(s.SavePath));if(!panel.IsOpen)typeof(LabCompletionPanel).GetMethod("Show",Private).Invoke(panel,null);if(panel.ConfirmingReplay)panel.ContinueExploring();}
        }
        public static void AfterReplay(SortingSession s)
        {
            var expected=s.Capture();Check(s.FlushSaves(),"Replay checkpoint durable");string backup=File.ReadAllText(s.SavePath+".bak");
            File.WriteAllText(s.SavePath,"{ corrupted primary");
            Check(s.Load(),"Corrupt replay primary recovers from backup");Compare(s,expected,0);
            Check(s.RequestSave(true)&&s.FlushSaves(),"Recovered state repairs primary");Check(File.ReadAllText(s.SavePath+".bak")==backup,"Repair does not rotate corrupted primary over valid backup");
            var before=s.Capture();Check(s.GrantSupply(SupplyKind.Cash,75,"backup-purchase-check"),"Purchase committed to redundant checkpoint");var purchase=s.Capture();
            File.WriteAllText(s.SavePath,"{ corrupted purchase primary");Check(s.Load(),"Latest verified purchase recovers from backup");Compare(s,purchase,0);Check(s.GrantSupply(SupplyKind.Cash,75,"backup-purchase-check")&&s.Cash==purchase.cash,"Recovered purchase callback cannot pay twice");
            bool blocked=true;var delivery=new DurableSaveWriter(s.SavePath,(path,json,preserve)=>{if(blocked)throw new IOException("Purchase write unavailable");DurableSaveWriter.AtomicWrite(path,json,preserve);});Writer(s,delivery);
            try
            {
                int cash=s.Cash;Check(!s.GrantSupply(SupplyKind.Cash,25,"failed-purchase-check"),"Purchase not acknowledged while save fails");
                Check(!s.GrantSupply(SupplyKind.Cash,25,"failed-purchase-check")&&s.Cash==cash+25,"Failed callback retry cannot double grant");
                blocked=false;Check(s.GrantSupply(SupplyKind.Cash,25,"failed-purchase-check")&&s.Cash==cash+25,"Purchase acknowledged once durable");
                var delivered=s.Capture();File.WriteAllText(s.SavePath,"{ corrupt retried delivery");Check(s.Load(),"Retried purchase backed up durably");Compare(s,delivered,0);
            }
            finally{blocked=false;delivery.Retry();delivery.Flush();Writer(s,new DurableSaveWriter(s.SavePath));}
            Check(s.Restore(before),"Restore enclosing test checkpoint");s.Save();Check(s.FlushSaves(),"Enclosing checkpoint durable");
            Check(LabLocalization.EnglishOnly&&UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code=="en","English-only release uses English");LabLocalization.NextLanguage();Check(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code=="en","Language switching disabled for current release");
        }
    }
}
#endif

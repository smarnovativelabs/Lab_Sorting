using System;
using System.Linq;
using NUnit.Framework;
public class ServiceRecoveryTests
{
    [TestCase("TenjinRevenueSerialization")]
    [TestCase("ReceiptToken")]
    [TestCase("IndependentTelemetryRetry")]
    [TestCase("DurableTelemetry")]
    [TestCase("TelemetryCapacity")]
    [TestCase("RetryAndAttTimeout")]
    [TestCase("PendingQueue")]
    [TestCase("ConfirmationWatchdog")]
    [TestCase("SdkConfirmationTimeout")]
    [TestCase("PurchaseJournalCrashRecovery")]
    [TestCase("ConsentCallbackTimeout")]
    [TestCase("DebugCheckpointControls")]
    [TestCase("DebugSdkStallInjection")]
    public void Recovery(string scenario)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.ServiceRecoveryChecks")).First(t=>t!=null);
        try{type.GetMethod(scenario).Invoke(null,null);}catch(System.Reflection.TargetInvocationException e){throw e.InnerException;}
    }
}

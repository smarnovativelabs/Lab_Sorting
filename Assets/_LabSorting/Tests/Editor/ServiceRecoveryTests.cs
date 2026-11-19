using System;
using System.Linq;
using NUnit.Framework;
public class ServiceRecoveryTests
{
    [TestCase("ReceiptToken")]
    [TestCase("IndependentTelemetryRetry")]
    [TestCase("DurableTelemetry")]
    [TestCase("TelemetryCapacity")]
    [TestCase("RetryAndAttTimeout")]
    [TestCase("PendingQueue")]
    public void Recovery(string scenario)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.ServiceRecoveryChecks")).First(t=>t!=null);
        try{type.GetMethod(scenario).Invoke(null,null);}catch(System.Reflection.TargetInvocationException e){throw e.InnerException;}
    }
}

using System;
using System.Text;
using UnityEngine;
namespace LabSorting.Services
{
    // This is an identity/quantity check, NOT a standalone JWS signature verifier.
    // Only call it on orders delivered by Unity IAP's native StoreKit 2 verified callbacks.
    // Unity IAP 5.5 rejects .unverified transactions before delivering those callbacks.
    public static class ApplePurchaseBinding
    {
        [Serializable] sealed class Payload
        {
            public string bundleId,productId,transactionId;
            public int quantity;
            public long revocationDate;
        }
        public static bool Matches(string jws,string bundleId,string productId,string transactionId)
        {
            if(string.IsNullOrWhiteSpace(jws)||string.IsNullOrWhiteSpace(bundleId)||string.IsNullOrWhiteSpace(productId)||string.IsNullOrWhiteSpace(transactionId))return false;
            try
            {
                var parts=jws.Split('.');if(parts.Length!=3||parts[0].Length==0||parts[2].Length==0)return false;
                string encoded=parts[1].Replace('-','+').Replace('_','/');encoded=encoded.PadRight((encoded.Length+3)/4*4,'=');
                var data=JsonUtility.FromJson<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
                return data!=null&&data.bundleId==bundleId&&data.productId==productId&&data.transactionId==transactionId&&data.quantity==1&&data.revocationDate==0;
            }
            catch{return false;}
        }
    }
}

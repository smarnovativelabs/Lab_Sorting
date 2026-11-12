using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace LabSorting.Sorting
{
    // Explicit version-3 JSON schema. Only immutable snapshot data reaches this writer.
    // Numbers use a reusable formatting buffer; no full JSON string or per-number string.
    public sealed class SaveJsonStream
    {
        readonly char[] number=new char[64];
        TextWriter output;
        void Int(int value){if(!value.TryFormat(number,out int length,default,CultureInfo.InvariantCulture))throw new IOException("Integer formatting failed");output.Write(number,0,length);}
        void Float(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new IOException("Non-finite save coordinate");if(!value.TryFormat(number,out int length,"R",CultureInfo.InvariantCulture))throw new IOException("Float formatting failed");output.Write(number,0,length);}
        void String(string value)
        {
            if(value==null){output.Write("null");return;}
            output.Write('"');
            foreach(char c in value)
            {
                switch(c)
                {
                    case '"':output.Write("\\\"");break;
                    case '\\':output.Write("\\\\");break;
                    case '\n':output.Write("\\n");break;
                    case '\r':output.Write("\\r");break;
                    case '\t':output.Write("\\t");break;
                    default:
                        if(c<32){const string hex="0123456789abcdef";output.Write("\\u00");output.Write(hex[c>>4]);output.Write(hex[c&15]);}
                        else output.Write(c);
                        break;
                }
            }
            output.Write('"');
        }
        void Strings(List<string> values){if(values==null){output.Write("null");return;}output.Write('[');for(int i=0;i<values.Count;i++){if(i>0)output.Write(',');String(values[i]);}output.Write(']');}
        void Vector(Vector3 value){output.Write("{\"x\":");Float(value.x);output.Write(",\"y\":");Float(value.y);output.Write(",\"z\":");Float(value.z);output.Write('}');}
        void Rotation(Quaternion value){output.Write("{\"x\":");Float(value.x);output.Write(",\"y\":");Float(value.y);output.Write(",\"z\":");Float(value.z);output.Write(",\"w\":");Float(value.w);output.Write('}');}
        public void Write(TextWriter destination,SortingSession.Snapshot snapshot)
        {
            output=destination;
            try
            {
                output.Write("{\"version\":");Int(snapshot.version);
                output.Write(",\"layoutRevision\":");Int(snapshot.layoutRevision);
                output.Write(",\"completedRacks\":");Strings(snapshot.completedRacks);
                output.Write(",\"searchId\":");String(snapshot.searchId);
                output.Write(",\"searchRemaining\":");Float(snapshot.searchRemaining);
                output.Write(",\"completionAcknowledged\":");output.Write(snapshot.completionAcknowledged?"true":"false");
                output.Write(",\"milestones\":");Strings(snapshot.milestones);
                output.Write(",\"cash\":");Int(snapshot.cash);
                output.Write(",\"searchCredits\":");Int(snapshot.searchCredits);
                output.Write(",\"magnetCredits\":");Int(snapshot.magnetCredits);
                output.Write(",\"slotCredits\":");Int(snapshot.slotCredits);
                output.Write(",\"supplyReceipts\":");Strings(snapshot.supplyReceipts);
                output.Write(",\"rewarded\":");Strings(snapshot.rewarded);
                output.Write(",\"capacity\":");Int(snapshot.capacity);
                output.Write(",\"playerPosition\":");Vector(snapshot.playerPosition);
                output.Write(",\"look\":{\"x\":");Float(snapshot.look.x);output.Write(",\"y\":");Float(snapshot.look.y);output.Write('}');
                output.Write(",\"active\":");String(snapshot.active);
                output.Write(",\"hand\":");Strings(snapshot.hand);
                output.Write(",\"tutorialRewarded\":");output.Write(snapshot.tutorialRewarded?"true":"false");
                output.Write(",\"items\":[");
                for(int i=0;i<snapshot.items.Count;i++)
                {
                    if(i>0)output.Write(',');var item=snapshot.items[i];
                    output.Write("{\"id\":");String(item.id);output.Write(",\"rack\":");String(item.rack);
                    output.Write(",\"slot\":");Int(item.slot);output.Write(",\"location\":");Int((int)item.location);
                    output.Write(",\"position\":");Vector(item.position);output.Write(",\"rotation\":");Rotation(item.rotation);output.Write('}');
                }
                output.Write("]}");
            }
            finally{output=null;}
        }
    }
}

using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        int hintRevision=-1;
        bool hintBlocked;
        ItemDefinition hintType;
        void UpdateRackHints()
        {
            var type=!player.blocked&&Active?Active.definition:null;
            if(hintRevision==Revision&&hintBlocked==player.blocked&&hintType==type)return;
            hintRevision=Revision;hintBlocked=player.blocked;hintType=type;
            // One shared material, no per-frame item scan, meshes, lights or wall-visible outlines.
            foreach(var rack in racks)if(rack)rack.SetCompatibleHint(type&&rack.CanAccept(type));
        }
    }
}

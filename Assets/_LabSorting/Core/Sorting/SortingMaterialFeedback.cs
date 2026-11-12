using DG.Tweening;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingFeedback
    {
        public AudioClip glassPlace,plasticPlace,metalPlace,rackChime;
        public int Celebrations {get;private set;}
        public bool IsCelebrating=>shine!=null&&shine.IsActive()&&shine.IsPlaying();
        public ItemDefinition LastPlacedDefinition=>lastPlaced;
        ItemDefinition lastPlaced;
        TextMesh shiningLabel;
        Color labelColor;
        Vector3 labelScale;
        Tween shine;
        AudioSource chimeSource;
        RackTargetFeedback completedRackFeedback;
        void Placed(SortItem item){lastPlaced=item.definition;}
        public AudioClip PlacementClip(ItemDefinition definition)
        {
            var clip=definition?definition.placementMaterial switch{
                PlacementMaterial.Glass=>glassPlace,PlacementMaterial.Plastic=>plasticPlace,
                PlacementMaterial.Metal=>metalPlace,_=>place}:place;
            return clip?clip:place;
        }
        void Celebrate(SortRack rack)
        {
            if(!isActiveAndEnabled||!rack)return;
            StopCelebration();Celebrations++;
            completedRackFeedback=rack.GetComponent<RackTargetFeedback>()??rack.gameObject.AddComponent<RackTargetFeedback>();completedRackFeedback.CompletionPulse(rack);
            if(rack.label)
            {
                shiningLabel=rack.label;labelColor=shiningLabel.color;labelScale=shiningLabel.transform.localScale;
                // One gentle mint-white glow; only the label animates, never the sorted items.
                shine=DOTween.To(()=>0f,t=>{
                    if(!shiningLabel)return;
                    float pulse=Mathf.Sin(t*Mathf.PI);
                    shiningLabel.color=Color.Lerp(labelColor,new Color(.65f,1f,.88f,labelColor.a),pulse*.65f);
                    shiningLabel.transform.localScale=labelScale*(1+.025f*pulse);
                },1f,.75f).SetEase(Ease.Linear).OnComplete(RestoreLabel);
            }
            if(SoundEnabled&&rackChime)
            {
                if(!chimeSource){chimeSource=gameObject.AddComponent<AudioSource>();chimeSource.playOnAwake=false;chimeSource.spatialBlend=0;chimeSource.outputAudioMixerGroup=source.outputAudioMixerGroup;}
                chimeSource.PlayOneShot(rackChime,volume*.55f);PlayedSounds++;
            }
        }
        void RestoreLabel(){if(shiningLabel){shiningLabel.color=labelColor;shiningLabel.transform.localScale=labelScale;}shiningLabel=null;}
        void StopCelebration(){shine?.Kill();shine=null;RestoreLabel();if(completedRackFeedback)completedRackFeedback.StopCompletion();completedRackFeedback=null;if(chimeSource)chimeSource.Stop();}
    }
}

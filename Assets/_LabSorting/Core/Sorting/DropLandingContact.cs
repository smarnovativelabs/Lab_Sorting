using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed class DropLandingContact : MonoBehaviour
    {
        public DropLandingPool pool;
        void OnCollisionEnter(Collision collision){if(pool)pool.RecordContact(gameObject,collision.relativeVelocity.magnitude);}
    }
}

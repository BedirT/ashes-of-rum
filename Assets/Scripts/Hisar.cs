using UnityEngine;

namespace AshesOfRum
{
    public sealed class Hisar : MonoBehaviour
    {
        public Vector3 DropOffPoint => transform.position + transform.forward * -3.2f;
    }
}

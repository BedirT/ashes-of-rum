using UnityEngine;

namespace AshesOfRum
{
    public sealed class AuthoredEquipmentAttachment : MonoBehaviour
    {
        [SerializeField] private string attachmentId;
        [SerializeField] private HumanBodyBones socketBone;

        public string AttachmentId => attachmentId;
        public HumanBodyBones SocketBone => socketBone;

        public void Configure(string id, HumanBodyBones bone)
        {
            attachmentId = id;
            socketBone = bone;
        }
    }
}

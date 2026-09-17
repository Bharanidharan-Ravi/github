using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// ECDH P-256 public key signed by the device's identity key.
    /// Rotated every week; old rows are kept with IsActive = false.
    /// </summary>
    [Table("ChatSignedPreKeys")]
    public class ChatSignedPreKey
    {
        [Key]
        public Guid PreKeyId { get; set; }
        public Guid IdentityKeyId { get; set; }
        public Guid UserId { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Client-side sequence number so the device can find the private key.</summary>
        public int KeyId { get; set; }

        /// <summary>Base64 SubjectPublicKeyInfo.</summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>Base64 ECDSA-SHA256 signature (IEEE P1363) over the SPKI bytes.</summary>
        public string Signature { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RotatedAt { get; set; }
    }
}

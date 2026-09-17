using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// The user's chat recovery code, encrypted with a server-held admin key (see
    /// ChatRecoveryEscrowCipher) so support can re-issue it if the user loses it. Written once,
    /// at key registration, alongside the zero-knowledge blobs in ChatUserKeys.
    /// </summary>
    [Table("ChatUserKeyRecoveryEscrow")]
    public class ChatUserKeyRecoveryEscrow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid UserId { get; set; }

        /// <summary>Base64 [12-byte IV | 16-byte AES-GCM tag | ciphertext] of the recovery code.</summary>
        public string EncryptedRecoveryCode { get; set; } = string.Empty;

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }
    }
}

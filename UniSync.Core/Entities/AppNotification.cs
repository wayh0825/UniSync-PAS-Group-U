using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniSync.Core.Enums;

namespace UniSync.Core.Entities
{
    public class AppNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Recipient")]
        [MaxLength(450)]
        public string RecipientId { get; set; } = string.Empty;

        public ApplicationUser Recipient { get; set; } = null!;

        [ForeignKey("Sender")]
        [MaxLength(450)]
        public string? SenderId { get; set; }

        public ApplicationUser? Sender { get; set; }

        [Required]
        public AlertCategory Type { get; set; } = AlertCategory.System;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ActionUrl { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }
    }
}
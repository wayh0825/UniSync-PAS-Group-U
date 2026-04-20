using System;
using System.ComponentModel.DataAnnotations;

namespace UniSync.Core.Entities
{
    public class SystemApiKey
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(250)]
        public string ApplicationName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Reader";

        [Required]
        public string KeyHash { get; set; } = string.Empty; // Store hashed key only
        
        [Required]
        [MaxLength(20)]
        public string Prefix { get; set; } = string.Empty; // e.g. "ak_live_"

        public string? MaskedKey { get; set; } // e.g. "*******2A4C"

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? LastAccessedUtc { get; set; }

        public bool IsRevoked { get; set; } = false;
        
        public DateTime? RevokedAtUtc { get; set; }
    }
}

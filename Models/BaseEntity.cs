using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StickyFlow.Models
{
    /// <summary>
    /// Base class for all entities with common audit properties
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Date when the entity was created (UTC)
        /// </summary>
        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the entity was last modified (UTC)
        /// </summary>
        [Column(TypeName = "datetime2")]
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Flag indicating whether the entity is soft-deleted
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// User ID who created the entity (nullable)
        /// </summary>
        [MaxLength(450)]
        public string? CreatedById { get; set; }

        /// <summary>
        /// User ID who last modified the entity (nullable)
        /// </summary>
        [MaxLength(450)]
        public string? LastModifiedById { get; set; }

        /// <summary>
        /// Timestamp for optimistic concurrency control
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Updates audit fields before saving
        /// </summary>
        public void UpdateAuditFields(string? userId, bool isNew = false)
        {
            if (isNew)
            {
                CreatedAt = DateTime.UtcNow;
                CreatedById = userId;
            }
            else
            {
                LastModified = DateTime.UtcNow;
                LastModifiedById = userId;
            }
        }
    }

    /// <summary>
    /// Generic base entity with typed ID
    /// </summary>
    /// <typeparam name="TKey">Type of the primary key</typeparam>
    public abstract class BaseEntity<TKey> : BaseEntity
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        public TKey Id { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace HCMS4.Models.Common
{
    /// <summary>
    /// Base class for entities that need optimistic concurrency control.
    /// EF Core uses this token to detect concurrent modifications.
    /// </summary>
    public abstract class ConcurrencyEntity
    {
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

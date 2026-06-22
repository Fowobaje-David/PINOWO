using System.ComponentModel.DataAnnotations.Schema;
using Pinowo.Models.Enums;

namespace Pinowo.Models
{
    /// <summary>
    /// Cached exchange rate so we don't hit CoinGecko on every request
    /// (see PRD Section 9, Risks - rate limiting).
    /// </summary>
    public class ExchangeRateSnapshot
    {
        public int Id { get; set; }

        public CurrencyType Currency { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UsdRate { get; set; }

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    }
}

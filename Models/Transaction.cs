using System;

namespace SkillSwapAI.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        
        public int Amount { get; set; } // +1 or -1
        public string Type { get; set; } = string.Empty; // "Booking", "Refund", "Earned"
        public string Description { get; set; } = string.Empty;
        
        public int? RelatedSessionId { get; set; }
        public Session? RelatedSession { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

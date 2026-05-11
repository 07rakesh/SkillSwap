namespace SkillSwapAI.Models
{
    public class MentorAvailability
    {
        public int Id { get; set; }
        public int MentorUserId { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool IsBooked { get; set; } = false;
        public int? BookedByUserId { get; set; }

        public int? SkillRequestId { get; set; }   // optional if tied to request
        public string Status { get; set; } = "Available"; // Available, Reserved, Booked
    } 
}

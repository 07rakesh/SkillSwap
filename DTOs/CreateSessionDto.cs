using System;

namespace SkillSwap.API.DTOs
{
    public class CreateSessionDto
    {
        public int SkillRequestId { get; set; }
        public DateTime ScheduledAt { get; set; }
    }
}
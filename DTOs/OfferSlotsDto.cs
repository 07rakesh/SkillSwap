namespace SkillSwapAI.DTOs
{
    public class OfferSlotsDto
    {
        public int SkillRequestId { get; set; }
        public List<TimeSlotDto> Slots { get; set; } = new();
    }
}

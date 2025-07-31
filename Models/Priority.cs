namespace StickyFlow.Models
{
    public class Priority
    {
        public int PriorityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Color { get; set; }
        public decimal Weight { get; set; }
    }
}
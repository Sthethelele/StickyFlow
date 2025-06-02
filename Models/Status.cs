namespace StickyFlow.Models
{
    public class Status
    {
        public int StatusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }
}
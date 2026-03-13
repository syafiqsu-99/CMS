namespace CMS.Server.Models
{
    public class calendar
    {
        public DateOnly production_date { get; set; }
        public int shift { get; set; }
        public string day_type { get; set; } = "NORMAL";
        public float planned_hours { get; set; } = 12;
        public string? start_time { get; set; }
        public string? finish_time { get; set; }
    }
}
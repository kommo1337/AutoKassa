namespace AutoKassa.Helpers
{
    public class DisplayItem
    {
        public string Value { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;

        public override string ToString() => Display;
    }
}

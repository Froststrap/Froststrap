namespace Bloxstrap.Models
{
    public class HttpResponse<T>
    {
        public T Data { get; set; } = default!;
        public List<string> Cookies { get; set; } = new();
    }
}
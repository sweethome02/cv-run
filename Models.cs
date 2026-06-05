namespace ClipNestWpf;

public class ClipboardItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "text"; // text | image
    public string Content { get; set; } = "";
    public string Format { get; set; } = "png"; // png | jpeg | gif | bmp | tiff
    public bool IsPinned { get; set; }
    public long CreatedAt { get; set; }
}

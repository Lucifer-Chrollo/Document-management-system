namespace DocumentManagementSystem.Models;

public class CompressionOptions
{
    public bool Enabled { get; set; } = true;
    public int MinimumFileSizeKB { get; set; } = 1;
    public int CompressionLevel { get; set; } = 9;
    public string[] SkipExtensions { get; set; } = { ".zip", ".rar", ".7z", ".mp4", ".avi", ".jpg", ".jpeg", ".png", ".gif" };
}

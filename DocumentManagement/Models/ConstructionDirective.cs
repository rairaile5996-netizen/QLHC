namespace DocumentManagement.Models;

public sealed class ConstructionDirective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Project { get; set; } = "";
    public DateTime ReceivedDate { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Chưa xử lý";
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
}

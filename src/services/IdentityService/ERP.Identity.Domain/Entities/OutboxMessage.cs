public class OutboxMessage
{
    public Guid Id { get; set; }
    public string? Type { get; set; }      // Tên loại sự kiện (để biết đường mà Deserialize)
    public string? Content { get; set; }   // Dữ liệu JSON của sự kiện
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; } // Đánh dấu khi đã gửi thành công
    public string? Error { get; set; }    // Lưu lỗi nếu gửi thất bại
}
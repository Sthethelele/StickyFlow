using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Dtos
{
    public record NoteDto(
    Guid NoteId,
    string Title,
    string Content,
    int PriorityId,
    string PriorityName,
    string PriorityColor, // Matches Priority.Color, not ColorCode
    int StatusId,
    string StatusName,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? LastModified);

    public record NoteCreateDto(
        [property: Required, MaxLength(200)] string Title,
        [property: MaxLength(5000)] string Content,
        [property: Range(1, 5)] int PriorityId,
        [property: Range(1, 5)] int StatusId,
        DateTime? DueDate);

    public record NoteUpdateDto(
        [property: MaxLength(200)] string? Title,
        [property: MaxLength(5000)] string? Content,
        [property: Range(1, 5)] int? PriorityId,
        [property: Range(1, 5)] int? StatusId,
        DateTime? DueDate);
}
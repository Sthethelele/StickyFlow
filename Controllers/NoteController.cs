using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StickyFlow.Data;
using StickyFlow.Dtos;
using StickyFlow.Models;
using StickyFlow.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AutoMapper;

namespace StickyFlow.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IReminderService _reminderService;
        private readonly ILogger<NotesController> _logger;
        private readonly IMapper _mapper;

        public NotesController(
            ApplicationDbContext context,
            IReminderService reminderService,
            IMapper mapper,
            ILogger<NotesController> logger)
        {
            _context = context;
            _reminderService = reminderService;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NoteDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetNotes(
            [FromQuery] NoteStatus? status = null,
            [FromQuery] int? priorityLevel = null,
            [FromQuery] bool includeDeleted = false)
        {
            var userId = User.GetUserId();
            var query = _context.Notes
                .Where(n => n.UserId == userId)
                .Include(n => n.Priority)
                .Include(n => n.Status)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(n => n.Status.Name == status.ToString());
            }

            if (priorityLevel.HasValue)
            {
                query = query.Where(n => n.Priority.Level == priorityLevel.Value);
            }

            if (!includeDeleted)
            {
                query = query.Where(n => !n.IsDeleted);
            }

            var notes = await query
                .OrderByDescending(n => n.DueDate.HasValue)
                .ThenBy(n => n.DueDate)
                .ThenByDescending(n => n.Priority.Level)
                .ToListAsync();

            return Ok(_mapper.Map<IEnumerable<NoteDto>>(notes));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(NoteDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetNote(Guid id)
        {
            var note = await _context.Notes
                .Include(n => n.Priority)
                .Include(n => n.Status)
                .FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == User.GetUserId());

            if (note == null || note.IsDeleted)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<NoteDto>(note));
        }

        [HttpPost]
        [ProducesResponseType(typeof(NoteDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateNote([FromBody] NoteCreateDto dto)
        {
            _logger.LogInformation("CreateNote called with DTO: {@Dto}", dto);
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ModelState invalid: {Errors}", string.Join("; ", errors));
                return BadRequest(new { message = "Validation failed", errors });
            }

            var userId = User.GetUserId();
            _logger.LogInformation("UserId: {UserId}", userId);

            var priority = await _context.Priorities.FindAsync(dto.PriorityId);
            var status = await _context.Statuses.FindAsync(dto.StatusId);
            if (priority == null || status == null)
            {
                _logger.LogWarning("Invalid PriorityId {PriorityId} or StatusId {StatusId}", dto.PriorityId, dto.StatusId);
                return BadRequest(new { message = "Invalid priority or status" });
            }

            var note = new Note
            {
                NoteId = Guid.NewGuid(),
                UserId = userId,
                Title = dto.Title,
                Content = dto.Content,
                PriorityId = dto.PriorityId,
                StatusId = dto.StatusId,
                DueDate = dto.DueDate
            };

            note.UpdateAuditFields(userId, isNew: true);
            _context.Notes.Add(note);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Note saved: {NoteId}", note.NoteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving note: {Message}", ex.Message);
                return StatusCode(500, new { message = "Error saving note", error = ex.Message });
            }

            if (note.DueDate.HasValue)
            {
                try
                {
                    _reminderService.ScheduleReminder(note.NoteId, userId, note.DueDate.Value);
                    _logger.LogInformation("Reminder scheduled for note: {NoteId}", note.NoteId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scheduling reminder for note: {NoteId}", note.NoteId);
                }
            }

            _logger.LogInformation("Note created: {NoteId} by user {UserId}", note.NoteId, userId);
            return CreatedAtAction(nameof(GetNote), new { id = note.NoteId }, _mapper.Map<NoteDto>(note));
        }


        [HttpPut("{id}")]
        [ProducesResponseType(typeof(NoteDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateNote(Guid id, [FromBody] NoteUpdateDto dto)
        {
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == User.GetUserId());

            if (note == null || note.IsDeleted)
            {
                return NotFound();
            }

            if (dto.PriorityId.HasValue)
            {
                var priority = await _context.Priorities.FindAsync(dto.PriorityId.Value);
                if (priority == null)
                {
                    return BadRequest(new { message = "Invalid priority" });
                }
            }

            if (dto.StatusId.HasValue)
            {
                var status = await _context.Statuses.FindAsync(dto.StatusId.Value);
                if (status == null)
                {
                    return BadRequest(new { message = "Invalid status" });
                }
            }

            note.Title = dto.Title ?? note.Title;
            note.Content = dto.Content ?? note.Content;
            note.PriorityId = dto.PriorityId ?? note.PriorityId;
            note.StatusId = dto.StatusId ?? note.StatusId;
            note.DueDate = dto.DueDate ?? note.DueDate;
            note.UpdateAuditFields(User.GetUserId());

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating note: {NoteId}", note.NoteId);
                return StatusCode(500, new { message = "Error updating note" });
            }

            if (dto.DueDate.HasValue && dto.DueDate != note.DueDate)
            {
                _reminderService.RescheduleReminder(note.NoteId, dto.DueDate.Value);
            }

            return Ok(_mapper.Map<NoteDto>(note));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteNote(Guid id)
        {
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == User.GetUserId());

            if (note == null || note.IsDeleted)
            {
                return NotFound();
            }

            note.IsDeleted = true;
            note.UpdateAuditFields(User.GetUserId());
            await _context.SaveChangesAsync();

            _reminderService.CancelReminder(note.NoteId);

            return NoContent();
        }

        [HttpPatch("{id}/restore")]
        [ProducesResponseType(typeof(NoteDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RestoreNote(Guid id)
        {
            var note = await _context.Notes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == User.GetUserId());

            if (note == null)
            {
                return NotFound();
            }

            if (!note.IsDeleted)
            {
                return BadRequest("Note is not deleted");
            }

            note.IsDeleted = false;
            note.UpdateAuditFields(User.GetUserId());
            await _context.SaveChangesAsync();

            if (note.DueDate.HasValue)
            {
                _reminderService.ScheduleReminder(note.NoteId, note.UserId, note.DueDate.Value);
            }

            return Ok(_mapper.Map<NoteDto>(note));
        }
    }

    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("User ID not found in claims");
        }
    }
}

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
            _mapper = mapper;
            _reminderService = reminderService;
            _logger = logger;
        }

        /// <summary>
        /// Get all notes for the current user with optional filtering
        /// </summary>
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

            // Apply filters
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

            var noteDtos = _mapper.Map<IEnumerable<NoteDto>>(notes); // Use AutoMapper
            return Ok(noteDtos);
        }

        /// <summary>
        /// Get a specific note by ID
        /// </summary>
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

        /// <summary>
        /// Create a new note
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(NoteDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateNote([FromBody] NoteCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.GetUserId();
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
            await _context.SaveChangesAsync();

            if (note.DueDate.HasValue)
            {
                _reminderService.ScheduleReminder(note.NoteId, userId, note.DueDate.Value);
            }

            _logger.LogInformation("Note created: {NoteId} by user {UserId}", note.NoteId, userId);

            return CreatedAtAction(nameof(GetNote), new { id = note.NoteId }, _mapper.Map<NoteDto>(note));
        }

        /// <summary>
        /// Update an existing note
        /// </summary>
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

            note.Title = dto.Title ?? note.Title;
            note.Content = dto.Content ?? note.Content;
            note.PriorityId = dto.PriorityId ?? note.PriorityId;
            note.StatusId = dto.StatusId ?? note.StatusId;
            note.DueDate = dto.DueDate ?? note.DueDate;
            note.UpdateAuditFields(User.GetUserId());

            await _context.SaveChangesAsync();

            // Reschedule reminder if due date changed
            if (dto.DueDate.HasValue && dto.DueDate != note.DueDate)
            {
                _reminderService.RescheduleReminder(note.NoteId, dto.DueDate.Value);
            }

            return Ok(_mapper.Map<NoteDto>(note));
        }

        /// <summary>
        /// Soft delete a note
        /// </summary>
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

            // Cancel any pending reminders
            _reminderService.CancelReminder(note.NoteId);

            return NoContent();
        }

        /// <summary>
        /// Restore a soft-deleted note
        /// </summary>
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

            // Reschedule reminder if due date exists
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
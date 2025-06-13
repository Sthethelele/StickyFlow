using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StickyFlow.Data;
using StickyFlow.Models;

namespace StickyFlow.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PrioritiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PrioritiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Priority>>> GetPriorities()
        {
            var priorities = await _context.Priorities.ToListAsync();
            return Ok(priorities);
        }
    }
}

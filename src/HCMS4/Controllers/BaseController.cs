using HCMS4.Data;
using HCMS4.Shared.Common;

namespace HCMS4.Controllers
{
    // Transitional: still exposes the DbContext while Phase 5 moves the remaining queries into module services.
    // The "current user" helpers now live in HcmsControllerBase.
    public abstract class BaseController : HcmsControllerBase
    {
        protected readonly ApplicationDbContext _context;
        protected readonly ILogger _logger;

        protected BaseController(ApplicationDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }
    }
}

using HCMS4.Modules.Feedback.Services;
using HCMS4.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HCMS4.Modules.Portal.Controllers
{
    // The public pages. Views stay in /Views/Home (the module folder has none, so the default location is used).
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private const int ArticlesShown = 6;

        private readonly IMedicalArticleService _articleService;

        public HomeController(IMedicalArticleService articleService)
        {
            _articleService = articleService;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _articleService.GetPublishedAsync(ArticlesShown));
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

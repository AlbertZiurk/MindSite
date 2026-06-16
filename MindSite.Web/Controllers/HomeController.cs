using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MindSite.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return User.FindFirstValue(ClaimTypes.Role) switch
                {
                    "Admin"      => RedirectToAction("Index", "Admin"),
                    "Fornecedor" => RedirectToAction("Index", "Supplier"),
                    _            => RedirectToAction("Index", "Client")
                };
            }
            return View();
        }

        public IActionResult SobreNos()    => View();
        public IActionResult Termos()      => View();
        public IActionResult Privacidade() => View();
        public IActionResult Error()       => View();
    }
}

using Microsoft.AspNetCore.Mvc;

namespace SeuProjeto.Controllers
{
    public class ServicosController : Controller
    {
        public IActionResult Index()
        {
            return View("Index");
        }
    }
}
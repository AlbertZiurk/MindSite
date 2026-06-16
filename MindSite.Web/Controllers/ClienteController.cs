using Microsoft.AspNetCore.Mvc;

namespace MindSite.Web.Controllers;

public class ClienteController: Controller
{
        public IActionResult Chat()
        {
            return View("Chat");
        }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MindSite.Web.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View("Auth");
        }

        public IActionResult Cadastro()
        {
            return View("Cadastro");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
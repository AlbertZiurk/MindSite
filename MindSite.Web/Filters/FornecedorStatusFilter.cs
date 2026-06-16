using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MindSite.Entities;
using MindSite.Enums;
using System.Security.Claims;

namespace MindSite.Filters
{
    public class FornecedorStatusFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated == true ||
                user.FindFirstValue(ClaimTypes.Role) != UserRole.Fornecedor.ToString())
            {
                await next();
                return;
            }

            var statusClaim = user.FindFirstValue("Status");
            if (!Enum.TryParse<UserStatus>(statusClaim, out var status))
            {
                await next();
                return;
            }

            if (status == UserStatus.Inativo || status == UserStatus.Bloqueado)
            {
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (status == UserStatus.Pendente && context.HttpContext.Request.Method == "POST")
            {
                var controller = context.Controller as Controller;
                if (controller != null)
                    controller.TempData["Erro"] = "Sua conta ainda está pendente de aprovação. Você não pode realizar ações no momento.";

                context.Result = new RedirectToActionResult("Index", "Supplier", null);
                return;
            }

            await next();
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MindSite.Filters
{
    public class FiltroConsomeTempData : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context) { }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Controller is Controller controller)
            {
                // Aplica a regra APENAS se o retorno for uma renderização direta de View (ex: return View("Auth"))
                // Se for um Redirect (ex: return RedirectToAction), ignora e deixa o TempData ir para a próxima página normalmente
                if (context.Result is ViewResult)
                {
                    var tempData = controller.TempData;

                    if (tempData.Count > 0)
                    {
                        // Forçamos a LEITURA interna de todas as chaves presentes no TempData.
                        // No ASP.NET Core, acessar o valor de uma chave faz com que o provedor (Session/Cookie)
                        // marque essa chave como "lida/consumida", agendando sua exclusão automática para a próxima requisição.
                        foreach (var key in tempData.Keys.ToList())
                        {
                            var _ = tempData[key]; // Apenas lê o valor para acionar o gatilho de descarte nativo
                        }
                    }
                }
            }
        }
    }
}
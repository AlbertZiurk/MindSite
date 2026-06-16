namespace MindSite.Interfaces
{
    public interface IStripeService
    {
        Task<string> CriarSessaoCheckoutAsync(decimal valor, long servicoId);
    }
}
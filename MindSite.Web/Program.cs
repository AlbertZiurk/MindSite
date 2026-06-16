using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Filters;
using MindSite.Hubs;
using MindSite.Services;
using Stripe;
using MindSite.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// MVC + SignalR
builder.Services.AddControllersWithViews(opt =>
{ 
    opt.Filters.Add<FiltroConsomeTempData>();
    opt.Filters.Add<FornecedorStatusFilter>();
});
builder.Services.AddSignalR();

// Entity Framework
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

// Console.WriteLine("TESTEEEEEEEEEEEEEEEEEEEEEE: ", builder.Configuration["Authentication:Google:ClientId"]);
// Console.WriteLine("TESTEEEEEEEEEEEEEEEEEEEEEE: ", builder.Configuration["Authentication:Google:ClientSecret"]);

// Auth por Cookies + Google OAuth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/AcessoNegado";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
    })
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        googleOptions.CallbackPath = "/signin-google";
    });

builder.Services.AddAuthorization();

// Session
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});

// TempData via Session (server-side) — resolve condição de corrida com requests paralelas
builder.Services.AddMvc().AddSessionStateTempDataProvider();


// HttpClient (Usado para o Resend)
builder.Services.AddHttpClient("Resend");

// Services Globais
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<IEmailService, ResendEmailService>(); // Resend para ambos os ambientes

// Configuração Global do SDK do Stripe
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]!;
StripeConfiguration.ApiKey = stripeSecretKey;
builder.Services.AddSingleton<IStripeClient>(new StripeClient(stripeSecretKey));

// Alternância Unificada de Storage
if (builder.Environment.IsDevelopment())
{
    // Local: Grava na pasta do HD via WSL
    builder.Services.AddScoped<IArquivoStorageService, LocalArquivoStorageService>();
}
else
{
    builder.Services.AddScoped<IArquivoStorageService, LocalArquivoStorageService>();
    // Produção: Grava no Azure Blob Storage
    // var azureStorageConn = builder.Configuration["Azure:BlobStorage:ConnectionString"]!;
    // builder.Services.AddSingleton(x => new Azure.Storage.Blobs.BlobServiceClient(azureStorageConn));
    // builder.Services.AddScoped<IArquivoStorageService, ArquivoStorageService>();
}

// Build & Middlewares
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
// app.UseMiddleware<RealTimeTempDataMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();
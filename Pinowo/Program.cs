using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Hubs;
using Pinowo.Models;
using Pinowo.Services;

var builder = WebApplication.CreateBuilder(args);

// --- EF Core (Code-First) against local SQL Server / LocalDB ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- ASP.NET Core Identity (int-keyed, backed by ApplicationDbContext) ---
builder.Services
    .AddIdentity<User, IdentityRole<int>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        // Relaxed password rules for the MVP demo - tighten for production.
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// --- Domain services ---
builder.Services.AddHttpClient();
builder.Services.AddScoped<IExchangeRateService, CoinGeckoExchangeRateService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IBalanceCalculatorService, BalanceCalculatorService>();
builder.Services.AddScoped<ISettlementService, SettlementService>();

// --- MVC (UI) + API controllers + SignalR (hub wired in a later phase) ---
builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(o =>
        // Serialize/accept CurrencyType as "BTC"/"STABLECOIN" rather than 0/1.
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSignalR();

var app = builder.Build();

// Dev-only demo data seeder: `dotnet run -- seed`.
if (args.Contains("seed"))
{
    await Pinowo.SeedRunner.RunAsync(app.Services);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// REST API endpoints (attribute-routed, e.g. /api/groups/...).
app.MapControllers();

// SignalR real-time balances hub.
app.MapHub<BalancesHub>("/hubs/balances");

// MVC UI endpoints.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using Microsoft.EntityFrameworkCore;
using Serilog;
using SportsStore.Models;

var builder = WebApplication.CreateBuilder(args);

// Bootstrap logger: captures startup failures before DI is ready
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web host");

    // Use Serilog for ASP.NET Core logging (configured via appsettings.json)
    builder.Host.UseSerilog((context, services, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services));

    builder.Services.AddControllersWithViews();

    builder.Services.AddDbContext<StoreDbContext>(opts =>
    {
        opts.UseSqlServer(
            builder.Configuration["ConnectionStrings:SportsStoreConnection"]);
    });

    builder.Services.AddScoped<IStoreRepository, EFStoreRepository>();
    builder.Services.AddScoped<IOrderRepository, EFOrderRepository>();

    builder.Services.AddRazorPages();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession();
    builder.Services.AddScoped<Cart>(sp => SessionCart.GetCart(sp));
    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    builder.Services.AddServerSideBlazor();

    var app = builder.Build();

    // Logs HTTP request info (method/path/status/elapsed), structured
    app.UseSerilogRequestLogging();

    // Logs unhandled exceptions with structured request info
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Unhandled exception for {RequestMethod} {RequestPath}",
                context.Request.Method,
                context.Request.Path);

            throw;
        }
    });

    app.UseStaticFiles();
    app.UseSession();

    app.MapControllerRoute("catpage",
        "{category}/Page{productPage:int}",
        new { Controller = "Home", action = "Index" });

    app.MapControllerRoute("page", "Page{productPage:int}",
        new { Controller = "Home", action = "Index", productPage = 1 });

    app.MapControllerRoute("category", "{category}",
        new { Controller = "Home", action = "Index", productPage = 1 });

    app.MapControllerRoute("pagination",
        "Products/Page{productPage}",
        new { Controller = "Home", action = "Index", productPage = 1 });

    app.MapDefaultControllerRoute();
    app.MapRazorPages();
    app.MapBlazorHub();
    app.MapFallbackToPage("/admin/{*catchall}", "/Admin/Index");

    SeedData.EnsurePopulated(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
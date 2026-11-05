using HALRouteCardApp.Data;
using HALRouteCardApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot"
});
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5081));
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddDbContext<RouteCardDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RouteCard")));
builder.Services.AddScoped<IRouteCardService, RouteCardService>();
builder.Services.AddScoped<IOpcenterLookupService, OpcenterLookupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RouteCardDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseStaticFiles();
app.MapRazorPages();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/Index"));
app.Run();

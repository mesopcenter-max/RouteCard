using System.Security.Claims;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using HALRouteCardApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot"
});
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5081));
builder.Services.AddRazorPages(options => options.Conventions.AuthorizePage("/Index"));
builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddDbContext<RouteCardDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RouteCard")));
builder.Services.AddScoped<IRouteCardService, RouteCardService>();
builder.Services.AddScoped<IOpcenterLookupService, OpcenterLookupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RouteCardDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[AppUsers]
            (
                [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [DisplayName] nvarchar(100) NOT NULL,
                [Username] nvarchar(200) NOT NULL UNIQUE,
                [PasswordHash] nvarchar(500) NOT NULL,
                [PhotoUrl] nvarchar(500) NOT NULL
            );
        END
        IF OBJECT_ID(N'dbo.UserStamps', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[UserStamps]
            (
                [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [AppUserId] int NOT NULL,
                [StampValue] nvarchar(200) NOT NULL,
                [SignatureValue] nvarchar(200) NOT NULL,
                CONSTRAINT [FK_UserStamps_AppUsers] FOREIGN KEY ([AppUserId]) REFERENCES [dbo].[AppUsers]([Id])
            );
        END
        IF OBJECT_ID(N'dbo.RouteCardApprovals', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[RouteCardApprovals]
            (
                [Id] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [RouteCardId] bigint NOT NULL,
                [AppUserId] int NOT NULL,
                [UserStampId] int NOT NULL,
                [OperationNo] nvarchar(100) NOT NULL,
                [StampValue] nvarchar(200) NOT NULL,
                [SignatureValue] nvarchar(200) NOT NULL,
                [ApprovedAt] datetime2 NOT NULL,
                CONSTRAINT [FK_RouteCardApprovals_RouteCards] FOREIGN KEY ([RouteCardId]) REFERENCES [dbo].[RouteCards]([Id]),
                CONSTRAINT [FK_RouteCardApprovals_AppUsers] FOREIGN KEY ([AppUserId]) REFERENCES [dbo].[AppUsers]([Id]),
                CONSTRAINT [FK_RouteCardApprovals_UserStamps] FOREIGN KEY ([UserStampId]) REFERENCES [dbo].[UserStamps]([Id])
            );
        END
        IF OBJECT_ID(N'dbo.RouteCardLookupSamples', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[RouteCardLookupSamples]
            (
                [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [OrderNo] nvarchar(100) NOT NULL,
                [WorkOrderNo] nvarchar(100) NOT NULL,
                [JcQty] decimal(18,3) NULL,
                [Project] nvarchar(200) NULL,
                [ProductType] nvarchar(100) NULL,
                [PslNo] nvarchar(100) NULL,
                [PartNo] nvarchar(100) NOT NULL,
                [PartName] nvarchar(300) NULL,
                [HalDrawingNo] nvarchar(200) NULL,
                [ReferenceDrawingNo] nvarchar(max) NULL,
                [MaterialSpecification] nvarchar(200) NULL,
                [MaterialDescription] nvarchar(300) NULL,
                [MrNo] nvarchar(100) NULL,
                [MaterialDate] date NULL,
                [RawMaterialDia] nvarchar(100) NULL,
                [RawMaterialLength] nvarchar(100) NULL,
                [RawMaterialBatchNo] nvarchar(100) NULL,
                [BsWeight] nvarchar(100) NULL,
                [ForgingWeight] nvarchar(100) NULL,
                [ForgingPlant] nvarchar(100) NULL,
                [ForgingBatchNo] nvarchar(100) NULL
            );
            CREATE INDEX [IX_RouteCardLookupSamples_WorkOrderNo] ON [dbo].[RouteCardLookupSamples] ([WorkOrderNo]);
            CREATE INDEX [IX_RouteCardLookupSamples_PartNo] ON [dbo].[RouteCardLookupSamples] ([PartNo]);
        END
        """);

    var users = new[]
    {
        new { Name = "Mani", Username = "mani@gmail.com", Photo = "/images/user-avatar.svg", Prefix = "MANI" },
        new { Name = "Nithin", Username = "nithin@gmail.com", Photo = "/images/user-avatar.svg", Prefix = "NITHIN" },
        new { Name = "Uday", Username = "uday@gmail.com", Photo = "/images/user-avatar.svg", Prefix = "UDAY" }
    };
    foreach (var seed in users)
    {
        var user = await db.AppUsers.Include(account => account.Stamps).SingleOrDefaultAsync(account => account.Username == seed.Username);
        if (user is null)
        {
            user = new AppUser
            {
                DisplayName = seed.Name,
                Username = seed.Username,
                PasswordHash = PasswordService.Hash("Admin@123"),
                PhotoUrl = seed.Photo
            };
            for (var index = 1; index <= 3; index++)
                user.Stamps.Add(new UserStamp { StampValue = $"{seed.Prefix}-STAMP-{index}", SignatureValue = $"{seed.Name.ToUpperInvariant()} SIGN-{index}" });
            db.AppUsers.Add(user);
        }
        else if (user.Stamps.Count < 3)
        {
            for (var index = user.Stamps.Count + 1; index <= 3; index++)
                user.Stamps.Add(new UserStamp { StampValue = $"{seed.Prefix}-STAMP-{index}", SignatureValue = $"{seed.Name.ToUpperInvariant()} SIGN-{index}" });
        }
        user.PhotoUrl = seed.Photo;
    }
    await db.SaveChangesAsync();

    // Existing lookup-table setup remains below.
    if (!await db.RouteCardLookupSamples.AnyAsync(record => record.PartNo == "NQF009764"))
    {
        db.RouteCardLookupSamples.Add(new RouteCardLookupSample
        {
            OrderNo = "5610801", WorkOrderNo = "167014", JcQty = 18, Project = "ROLLS ROYCE", ProductType = "3310-RR", PslNo = "94217", PartNo = "NQF009764", PartName = "Forging,seal, Outer", HalDrawingNo = "FD-9025", ReferenceDrawingNo = "1. MOM-FD-9025\n2. FDM-9025\n3. FDT-9025\n4. LP-9025", MaterialSpecification = "AMS 5886", MaterialDescription = "Nickel", MrNo = "47760", MaterialDate = new DateTime(2025, 10, 3), RawMaterialDia = "200", RawMaterialLength = "230", RawMaterialBatchNo = "F259-123", BsWeight = "57", ForgingWeight = "53", ForgingPlant = "500250", ForgingBatchNo = "RR-25-0T-01"
        });
        await db.SaveChangesAsync();
    }
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/Index"));
app.Run();

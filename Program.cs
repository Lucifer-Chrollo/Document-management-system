using DocumentManagementSystem.Components;
using DocumentManagementSystem.Data;
using DocumentManagementSystem.Data.Repositories;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using Radzen;
using Fluxor;
using Fluxor.Blazor.Web.ReduxDevTools;

var builder = WebApplication.CreateBuilder(args);

// Add Radzen services
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

// Add database context
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Enterprise Library Database
builder.Services.AddScoped<Database>(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("DefaultConnection");
    return new SqlDatabase(connectionString);
});

// Add Database Initializer
builder.Services.AddScoped<DatabaseInitializer>();

// Add Identity with Custom Stores (ADO.NET)
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
   options.Password.RequireDigit = false; // Relaxed for dev
   options.Password.RequireLowercase = false;
   options.Password.RequireUppercase = false;
   options.Password.RequireNonAlphanumeric = false;
   options.Password.RequiredLength = 4;
   options.SignIn.RequireConfirmedAccount = false;
})
.AddUserStore<DmsUserStore>()
.AddRoleStore<DmsRoleStore>()
.AddDefaultTokenProviders();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.AccessDeniedPath = "/access-denied";
});

// Add External Authentication (Google)
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "MISSING_CLIENT_ID";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "MISSING_CLIENT_SECRET";
        options.CallbackPath = "/signin-google";
    });

// Add repositories
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Add application services
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<ICompressionService, CompressionService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ISearchService, LuceneSearchService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IUserGroupService, UserGroupService>();
builder.Services.AddSingleton<IMfaService, MfaService>();
builder.Services.AddScoped<IUploadSessionService, UploadSessionService>();
builder.Services.AddScoped<IAutoCategorizationService, AutoCategorizationService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IDocumentConversionService, DocumentConversionService>();
builder.Services.AddHttpContextAccessor();

// Add session support for MFA
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5); // Short timeout for MFA
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add OCR services removed


// Add controllers for API endpoints
builder.Services.AddControllers();

// Add Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true;
        // Increase SignalR message size to 100MB to allow large InputFile streams to pass through
        options.RootComponents.MaxJSRootComponents = 100;
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
    })
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // 100MB 
    });

// Add Fluxor State Management
builder.Services.AddFluxor(options =>
{
    options.ScanAssemblies(typeof(Program).Assembly);
    options.UseReduxDevTools();
});

// Add HTTP context accessor
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Initialize Database Schema (ADO.NET)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        initializer.EnsureSchemaAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database initialization failed at startup.");
        // We continue anyway, but the app might fail later if DB is down.
    }

    try
    {
        // Rebuild Search Index on Startup
        var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        
        // This might be slow for massive DBs, but fine for this scale
        var allDocs = documentService.GetAllAsync().GetAwaiter().GetResult(); 
        searchService.RebuildIndex(allDocs);
        logger.LogInformation("Search index rebuilt successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to rebuild search index.");
    }
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RPGSessionManager.Ai;
using RPGSessionManager.Config;
using RPGSessionManager.Data;
using RPGSessionManager.Hubs;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using RPGSessionManager.Middleware;
using RPGSessionManager.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Services
// -----------------------------------------------------------------------------

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity + Roles
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // Disable for MVP
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// AI Config
builder.Services.Configure<AiConfig>(builder.Configuration.GetSection("Ai"));

// Infra básica
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserResolverService, UserResolverService>();

// AI Providers
builder.Services.AddScoped<AiProviderFactory>();
builder.Services.AddScoped<AiProviderService>();
builder.Services.AddScoped<IAiProvider>(sp => sp.GetRequiredService<AiProviderService>());

// Compat legada
builder.Services.AddScoped<IAiClient, LocalAiClient>();
builder.Services.AddScoped<AiOrchestrator>();
builder.Services.AddScoped<JsonSchemaValidationService>();

// Context Service (usar SOMENTE typed HttpClient)
builder.Services.AddHttpClient<IContextService, ContextServiceClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("ContextService:BaseUrl") ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("ContextService:TimeoutSeconds", 30));
});
// (Removido: AddScoped<IContextService, ContextServiceClient>() duplicado)

// Chat & afins
builder.Services.AddScoped<IChatMessageService, ChatMessageService>();
builder.Services.AddScoped<IConversationMemoryService, ConversationMemoryService>();
builder.Services.AddScoped<IRollService, RollService>();

// Cache & performance
builder.Services.AddMemoryCache();

// Evitar duplicatas: deixar APENAS 1 registro
builder.Services.AddScoped<INpcStateCache, NpcStateCache>();
builder.Services.AddSingleton<IAiConnectionPool, AiConnectionPool>();

// Fila de IA (uma única instância, reusada como IAiResponseQueue e IHostedService)
// Em Testing, não registrar workers reais
var isTesting = builder.Environment.IsEnvironment("Testing");

if (!isTesting)
{
    // Registra o CONCRETO uma única vez
    builder.Services.AddSingleton<AiResponseQueue>();

    // Faz o wire-up para as interfaces apontarem para a MESMA instância
    builder.Services.AddSingleton<IAiResponseQueue>(sp => sp.GetRequiredService<AiResponseQueue>());
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<AiResponseQueue>());
}

// Remova qualquer registro duplicado depois (evite isto):
// builder.Services.AddSingleton<IAiResponseQueue, AiResponseQueue>();


// Demais serviços do domínio
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INpcMemoryService, NpcMemoryService>();
builder.Services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();
builder.Services.AddScoped<INpcTelemetryService, NpcTelemetryService>();
builder.Services.AddScoped<INpcAuditService, NpcAuditService>();
builder.Services.AddScoped<IPresenceService, InMemoryPresenceService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<IChatStreamManager, ChatStreamManager>();
builder.Services.AddScoped<ICharacterCardinalityService, CharacterCardinalityService>();

// MVC / Swagger / HealthChecks
builder.Services.AddControllers();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IBattlemapService, BattlemapService>();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddCheck<SignalRHealthCheck>("SignalR");

// Rate limiting
builder.Services.ConfigureRateLimit(builder.Configuration);

// -----------------------------------------------------------------------------
// App pipeline
// -----------------------------------------------------------------------------

var app = builder.Build();

// Em Dev, manter MigrationsEndPoint. Em Testing, não precisa.
// Ativar Swagger em Dev **e** Testing (os smoke tests dependem disso).
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}

if (app.Environment.IsDevelopment() || isTesting)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Desabilitado por enquanto
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<SignalRConnectionMiddleware>();
app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Hubs
app.MapHub<ChatHub>("/chathub");
app.MapHub<BattlemapHub>("/battlemaphub");

// Seeding: apenas em Dev e fora de Testing
if (app.Environment.IsDevelopment() && !isTesting)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Garantir DB
    await context.Database.MigrateAsync();

    var seeder = new DevSeeder(
        context,
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<ILogger<DevSeeder>>());

    await seeder.SeedAsync();
    await NpcSystemSeeder.SeedNpcSystemDataAsync(context);
}

app.Run();

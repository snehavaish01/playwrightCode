using MooseBrowserAutomationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Add CORS for frontend calls
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://secure.mooseintl.org/fruadminlcl/login.aspx")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register browser automation service
builder.Services.AddScoped<IBrowserAutomationService, BrowserAutomationService>();

// Configure logging (optional)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(); // For Windows Event Log

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MooseBrowserAutomationService";
});

var app = builder.Build();

// Configure pipeline
app.UseCors("AllowAngular");
app.UseRouting();
app.MapControllers();

// Add a simple health check endpoint
app.MapGet("/", () => "Moose Browser Automation Service is running");

// Ensure Playwright is installed (run this once during deployment)
try
{
    Microsoft.Playwright.Program.Main(new[] { "install" });
}
catch (Exception ex)
{
    Console.WriteLine($"Playwright install warning: {ex.Message}");
}

await app.RunAsync();
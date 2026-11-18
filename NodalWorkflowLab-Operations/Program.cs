using NodalWorkflowLab.Components;
using NodalWorkflowLab.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Shared server-side repositories; never attach production credentials or controlled data to this demo.
builder.Services.AddSingleton<WorkflowLibraryStore>();
builder.Services.AddSingleton<OperationsStore>();
builder.Services.AddSingleton<ProcessOperationsStore>();
builder.Services.AddSingleton<ProcessMapStore>();
builder.Services.AddSingleton<FleetIntelligenceStore>();
builder.Services.AddSingleton<OperationalCoordinationService>();
builder.Services.AddScoped<WorkflowRunner>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
// Ensure module-backed action contracts are applied before the legacy action-item page
// is available, preventing a status edit from impersonating a finding / inspection event.
await app.Services.GetRequiredService<OperationalCoordinationService>().ReadAsync();
app.Run();

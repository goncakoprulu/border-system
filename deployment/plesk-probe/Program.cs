using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(
    "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>BORDER Panel Hosting Probe</title></head><body><p>BORDER Panel hosting probe is running.</p></body></html>",
    "text/html; charset=utf-8"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    runtime = RuntimeInformation.FrameworkDescription,
    environment = app.Environment.EnvironmentName
}));

app.Run();

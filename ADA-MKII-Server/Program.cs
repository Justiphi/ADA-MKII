var builder = WebApplication.CreateBuilder(args);

// Phase 2 adds AddAdaCore(), AddAdaProviders(), AddAdaData(), bearer auth,
// ProblemDetails and Serilog here. This project is the ONLY process that holds
// SQL credentials and provider API keys - see CLAUDE.md.

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

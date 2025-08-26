var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ERP Stub API", Version = "v1", Description = "Mock ERP service for testing Integration Gateway" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP Stub API v1"));
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/", () => new
{
    Service = "ERP Stub",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Description = "Mock ERP service for Integration Gateway testing"
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ERP Stub", Timestamp = DateTime.UtcNow }));

app.Run();
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Warehouse Stub API", Version = "v1", Description = "Mock Warehouse service for testing Integration Gateway" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Warehouse Stub API v1"));
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/", () => new
{
    Service = "Warehouse Stub",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Description = "Mock Warehouse service for Integration Gateway testing"
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Warehouse Stub", Timestamp = DateTime.UtcNow }));

app.Run();
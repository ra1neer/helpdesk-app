using Serilog;
using Polly;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/helpdesk.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=helpdesk.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SlaDeadline { get; set; }
    public string Status { get; set; } = "";
    public string? Solution { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Ticket> Tickets { get; set; }
}

[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TicketsController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create(string title, int categoryId, string solutionText)
    {
        try
        {
            Log.Information("Creating ticket: {Title}", title);
            var ticket = new Ticket
            {
                Title = title,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow,
                Status = "Closed",
                SlaDeadline = DateTime.UtcNow.AddHours(categoryId == 1 ? 4 : 24),
                Solution = solutionText
            };
            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();
            Log.Information("Ticket {Id} created", ticket.Id);
            return Ok(ticket);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create ticket");
            return BadRequest();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        Log.Information("Fetching all tickets");
        return Ok(await _db.Tickets.ToListAsync());
    }

    [HttpGet("check-sla")]
    public async Task<IActionResult> CheckSla()
    {
        try
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(2),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        Log.Warning("Retry {RetryCount} after {Delay}s - {Error}", 
                            retryCount, timeSpan.TotalSeconds, exception.Message);
                    });

            var circuitBreaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30),
                    onBreak: (ex, delay) => Log.Error("Circuit broken for {Delay}s", delay.TotalSeconds),
                    onReset: () => Log.Information("Circuit reset"));

            await circuitBreaker.ExecuteAsync(async () =>
                await retryPolicy.ExecuteAsync(async () =>
                {
                    using var client = new HttpClient();
                    var response = await client.GetAsync("https://httpbin.org/status/500");
                    response.EnsureSuccessStatusCode();
                    return response;
                }));

            return Ok("SLA check passed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SLA check failed after retries");
            return StatusCode(503, "SLA service unavailable");
        }
    }
}
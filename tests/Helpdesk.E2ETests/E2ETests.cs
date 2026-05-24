using Microsoft.EntityFrameworkCore;
using Helpdesk;

namespace Helpdesk.E2ETests;

public class E2ETests
{
    [Fact]
    public async Task CreateTicket_ThenCheckDatabase_EndToEnd()
    {
        // Arrange: создаём БД
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=e2e_test.db")
            .Options;

        using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Act: создаём тикет напрямую через репозиторий (имитация UI или API)
        var ticket = new Ticket
        {
            Title = "E2E Test Ticket",
            CategoryId = 1,
            CreatedAt = DateTime.UtcNow,
            Status = "Closed",
            SlaDeadline = DateTime.UtcNow.AddHours(4),
            Solution = "E2E Solution"
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        // Assert: проверяем, что тикет сохранился и доступен
        var savedTicket = await db.Tickets.FirstOrDefaultAsync(t => t.Title == "E2E Test Ticket");
        Assert.NotNull(savedTicket);
        Assert.Equal("Closed", savedTicket.Status);
        Assert.Equal("E2E Solution", savedTicket.Solution);
    }
}

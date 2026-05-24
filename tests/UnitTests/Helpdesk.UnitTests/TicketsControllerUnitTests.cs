using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Helpdesk;

namespace Helpdesk.UnitTests;

public class TicketsControllerUnitTests
{
    [Fact]
    public async Task CreateTicket_ValidInput_ReturnsOkWithTicket()
    {
        // Создаём тестовую БД в памяти
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=test.db")
            .Options;

        using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var controller = new TicketsController(db);

        var result = await controller.Create("Test title", 1, "Test solution");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var ticket = Assert.IsType<Ticket>(okResult.Value);
        Assert.Equal("Test title", ticket.Title);
        Assert.Equal(1, ticket.CategoryId);
        Assert.Equal("Closed", ticket.Status);
    }
}

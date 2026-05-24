using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Helpdesk;

namespace Helpdesk.IntegrationTests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Удаляем реальную БД
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                // Добавляем тестовую БД
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite("Data Source=test_helpdesk.db"));
            });
        });
    }

    [Fact]
    public async Task CreateTicket_ThenGetAll_ReturnsCreatedTicket()
    {
        var client = _factory.CreateClient();

        // Создаём тикет
        var postResponse = await client.PostAsync("/api/tickets?title=IntegrationTest&categoryId=1&solutionText=TestSolution", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, postResponse.StatusCode);

        // Получаем список тикетов
        var getResponse = await client.GetAsync("/api/tickets");
        var content = await getResponse.Content.ReadAsStringAsync();

        // Проверяем, что наш тикет есть в списке
        Assert.Contains("IntegrationTest", content);
    }
}

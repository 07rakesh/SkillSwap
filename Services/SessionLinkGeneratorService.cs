using Microsoft.EntityFrameworkCore;
using SkillSwapAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class SessionLinkGeneratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SessionLinkGeneratorService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var meetingService = scope.ServiceProvider.GetRequiredService<IMeetingService>();

                var now = DateTime.UtcNow;
                var next30Minutes = now.AddMinutes(30);

                var sessions = await context.Sessions
                    .Where(s =>
                        s.Status == "Scheduled" &&
                        string.IsNullOrEmpty(s.MeetingLink) &&
                        s.ScheduledAt > now &&
                        s.ScheduledAt <= next30Minutes)
                    .ToListAsync(stoppingToken);

                foreach (var session in sessions)
                {
                    session.MeetingLink = meetingService.GenerateMeetingLink(session.SessionId);
                }

                if (sessions.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SessionLinkGeneratorService error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
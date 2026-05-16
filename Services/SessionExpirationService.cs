using Microsoft.EntityFrameworkCore;
using SkillSwapAI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SkillSwapAI.Services
{
    public class SessionExpirationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SessionExpirationService> _logger;

        public SessionExpirationService(IServiceScopeFactory scopeFactory, ILogger<SessionExpirationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionExpirationService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var now = DateTime.UtcNow;

                    // Find sessions that are Scheduled but past their end time
                    var expiredSessions = await context.Sessions
                        .Include(s => s.SkillRequest)
                            .ThenInclude(r => r.Skill)
                        .Where(s => s.Status == "Scheduled" && s.ScheduledEndTime.HasValue && now > s.ScheduledEndTime.Value)
                        .ToListAsync(stoppingToken);

                    if (expiredSessions.Any())
                    {
                        _logger.LogInformation("Found {Count} expired sessions. Processing refunds...", expiredSessions.Count);

                        foreach (var session in expiredSessions)
                        {
                            using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                            try
                            {
                                session.Status = "Expired";
                                if (session.SkillRequest != null)
                                {
                                    session.SkillRequest.Status = "Expired";

                                    // Refund credit to learner
                                    var learner = await context.Users.FirstOrDefaultAsync(u => u.UserId == session.SkillRequest.RequesterUserId, stoppingToken);
                                    if (learner != null)
                                    {
                                        learner.Credits += 1;
                                        
                                        // Log transaction
                                        context.Transactions.Add(new Transaction
                                        {
                                            UserId = learner.UserId,
                                            Amount = 1,
                                            Type = "Refund",
                                            Description = $"Automatic refund for expired session: {session.SkillRequest.Skill?.SkillName ?? "Unknown Skill"}",
                                            RelatedSessionId = session.SessionId,
                                            Timestamp = DateTime.UtcNow
                                        });
                                        
                                        _logger.LogInformation("Refunded 1 credit to Learner {LearnerId} for session {SessionId}", learner.UserId, session.SessionId);
                                    }
                                }

                                await context.SaveChangesAsync(stoppingToken);
                                await transaction.CommitAsync(stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                await transaction.RollbackAsync(stoppingToken);
                                _logger.LogError(ex, "Error processing refund for session {SessionId}", session.SessionId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SessionExpirationService error occurred.");
                }

                // Run every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

            _logger.LogInformation("SessionExpirationService is stopping.");
        }
    }
}

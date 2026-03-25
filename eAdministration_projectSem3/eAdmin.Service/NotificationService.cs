using System;
using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace eAdmin.Service
{
    /// <summary>
    /// Notification service supporting InApp and SMS channels.
    /// SMS: mock (ready for Twilio integration).
    /// All notifications are persisted in the Notifications table.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IUnitOfWork uow, ILogger<NotificationService> logger)
        { _uow = uow; _logger = logger; }

        public async Task SendAsync(int recipientId, string type, string subject,
            string message, string? entityType = null, int? entityId = null)
        {
            var notification = new Notification
            {
                RecipientUserId = recipientId, NotificationType = type,
                Subject = subject, Message = message,
                RelatedEntityType = entityType, RelatedEntityId = entityId,
                IsSent = false, IsRead = false, CreatedAt = DateTime.UtcNow
            };

            try
            {
                switch (type)
                {
                    case "SMS":
                        // TODO: Twilio SDK integration
                        _logger.LogInformation("[SMS-MOCK] → User#{Id} | {Msg}", recipientId, message);
                        break;
                    default: // InApp
                        _logger.LogInformation("[INAPP] → User#{Id} | {Msg}", recipientId, message);
                        break;
                }
                notification.IsSent = true;
                notification.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {Type} to User#{Id}", type, recipientId);
            }

            await _uow.Notifications.AddAsync(notification);
            await _uow.SaveChangesAsync();
        }

        public async Task CheckSoftwareExpiryAndNotifyAsync(int daysBeforeExpiry = 30)
        {
            var threshold = DateTime.Today.AddDays(daysBeforeExpiry);

            var expiringSoon = await _uow.Softwares.FindAsync(s =>
                s.LicenseExpiry.HasValue &&
                s.LicenseExpiry.Value.Date <= threshold.Date &&
                !s.IsNotificationSent);

            if (!expiringSoon.Any()) return;

            var admins = await _uow.Users.FindAsync(u => u.Role.RoleName == "Admin" && u.IsActive);

            foreach (var sw in expiringSoon)
            {
                var daysLeft = (sw.LicenseExpiry!.Value.Date - DateTime.Today).Days;
                var urgency = daysLeft <= 0 ? "EXPIRED" : $"{daysLeft} days remaining";

                foreach (var admin in admins)
                {
                    await SendAsync(admin.UserId, "InApp",
                        $"[License Alert] {sw.SoftwareName} – {urgency}",
                        $"Software: {sw.SoftwareName} v{sw.Version}\nLicense expiry: {sw.LicenseExpiry:dd/MM/yyyy} ({urgency})\nPlease renew or update.",
                        "Software", sw.SoftwareId);
                    await SendAsync(admin.UserId, "SMS",
                        $"License Alert: {sw.SoftwareName}",
                        $"{sw.SoftwareName} license {urgency}. Please renew.",
                        "Software", sw.SoftwareId);
                }

                var swEntity = await _uow.Softwares.GetByIdAsync(sw.SoftwareId);
                if (swEntity != null) { swEntity.IsNotificationSent = true; _uow.Softwares.Update(swEntity); }
            }

            await _uow.SaveChangesAsync();
            _logger.LogInformation("Software expiry check: {Count} software items notified.", expiringSoon.Count());
        }
    }
}

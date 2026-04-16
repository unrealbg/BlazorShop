namespace BlazorShop.Tests.Presentation.Services
{
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.Models.Notifications;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    using Moq;

    using Xunit;

    public class NotificationServiceTests
    {
        private readonly NotificationService _notificationService;
        private readonly Mock<IToastService> _toastServiceMock;

        public NotificationServiceTests()
        {
            _toastServiceMock = new Mock<IToastService>();
            _notificationService = new NotificationService(_toastServiceMock.Object);
        }

        [Fact]
        public void NotifySuccess_AddsInboxItem_AndShowsToast()
        {
            // Act
            _notificationService.NotifySuccess("Signed in successfully.", "Signed in", NotificationKind.Authentication);

            // Assert
            Assert.Single(_notificationService.Notifications);
            Assert.Equal(1, _notificationService.UnreadCount);
            Assert.Equal("Signed in", _notificationService.Notifications[0].Heading);
            Assert.Equal(NotificationKind.Authentication, _notificationService.Notifications[0].Kind);

            _toastServiceMock.Verify(
                x => x.ShowToast(
                    ToastLevel.Success,
                    "Signed in successfully.",
                    "Signed in",
                    ToastIcon.Success,
                    ToastPosition.TopRight,
                    false,
                    5000),
                Times.Once);
        }

        [Fact]
        public void Notify_WithToastOnly_DoesNotAddInboxItem()
        {
            // Act
            _notificationService.Notify(new NotificationRequest
            {
                Heading = "Cart",
                Message = "Product removed from cart",
                Level = ToastLevel.Warning,
                Kind = NotificationKind.Order,
                AddToInbox = false,
            });

            // Assert
            Assert.Empty(_notificationService.Notifications);
            Assert.Equal(0, _notificationService.UnreadCount);

            _toastServiceMock.Verify(
                x => x.ShowToast(
                    ToastLevel.Warning,
                    "Product removed from cart",
                    "Cart",
                    ToastIcon.Warning,
                    ToastPosition.TopRight,
                    false,
                    5000),
                Times.Once);
        }

        [Fact]
        public void Notify_BoundsInbox_AndKeepsNewestItems()
        {
            // Act
            for (var index = 1; index <= 25; index++)
            {
                _notificationService.Notify(new NotificationRequest
                {
                    Heading = $"Notification {index}",
                    Message = $"Message {index}",
                    ShowToast = false,
                });
            }

            // Assert
            Assert.Equal(20, _notificationService.Notifications.Count);
            Assert.Equal("Notification 25", _notificationService.Notifications[0].Heading);
            Assert.DoesNotContain(_notificationService.Notifications, x => x.Heading == "Notification 1");
            Assert.DoesNotContain(_notificationService.Notifications, x => x.Heading == "Notification 5");
        }

        [Fact]
        public void MarkAllAsRead_ClearsUnreadCount()
        {
            // Arrange
            _notificationService.NotifySuccess("Order placed successfully.", "Order placed", NotificationKind.Order, showToast: false);
            _notificationService.NotifyWarning("Payment was canceled.", "Payment canceled", NotificationKind.Payment, showToast: false);

            // Act
            _notificationService.MarkAllAsRead();

            // Assert
            Assert.Equal(0, _notificationService.UnreadCount);
            Assert.All(_notificationService.Notifications, notification => Assert.True(notification.IsRead));
        }
    }
}
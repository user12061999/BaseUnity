using System;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using Unity.Notifications;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Mobile Notifications")]
    public sealed class MobileNotificationProvider : LocalNotificationProvider
    {
        public override ILocalNotificationBackend Create() => new Backend();
        private sealed class Backend : ILocalNotificationBackend
        {
            public NotificationPermission Permission
            {
                get {
#if UNITY_ANDROID && !UNITY_EDITOR
                    return Unity.Notifications.Android.AndroidNotificationCenter.UserPermissionToPost == Unity.Notifications.Android.PermissionStatus.Allowed ? NotificationPermission.Granted : NotificationPermission.Denied;
#elif UNITY_IOS && !UNITY_EDITOR
                    var status = Unity.Notifications.iOS.iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus;
                    return status == Unity.Notifications.iOS.AuthorizationStatus.Authorized || status == Unity.Notifications.iOS.AuthorizationStatus.Provisional ? NotificationPermission.Granted : NotificationPermission.Denied;
#else
                    return NotificationPermission.Denied;
#endif
                }
            }
            public void Initialize(LocalNotificationSettings settings)
            {
                if (Application.isEditor || (!Application.isMobilePlatform)) throw new PlatformNotSupportedException("Notifications require an Android/iOS player.");
                NotificationCenter.Initialize(new NotificationCenterArgs { AndroidChannelId = settings.channelId, AndroidChannelName = settings.channelName,
                    AndroidChannelDescription = settings.channelName, AndroidSmallIcon = settings.smallIcon,
                    PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Sound | NotificationPresentation.Badge });
            }
            public async Task<NotificationPermission> RequestPermissionAsync(CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                var request = NotificationCenter.RequestPermission();
                while (request.Status == NotificationsPermissionStatus.RequestPending) await Task.Delay(50, cancellation);
                return request.Status == NotificationsPermissionStatus.Granted ? NotificationPermission.Granted : NotificationPermission.Denied;
            }
            public void Schedule(LocalNotification notification, LocalNotificationSettings settings)
            {
                var native = new Notification { Identifier = notification.id, Title = notification.title, Text = notification.message, Data = notification.data, ShowInForeground = false };
                NotificationCenter.ScheduleNotification(native, new NotificationDateTimeSchedule(notification.fireUtc.ToLocalTime()));
            }
            public void Cancel(int id) { NotificationCenter.CancelScheduledNotification(id); NotificationCenter.CancelDeliveredNotification(id); }
            public void Dispose() { }
        }
    }
}

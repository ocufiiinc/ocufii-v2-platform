using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Options;
using OcufiiAPI.Configs;
using OcufiiAPI.Models;
using Polly;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OcufiiAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly NotificationSettings _settings;

        public NotificationService(IOptions<NotificationSettings> settings)
        {
            _settings = settings.Value;
            // Initialize Firebase (one-time)
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(_settings.ServiceAccountJson)
                });
            }
        }

        public async Task SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string> data = null)
        {
            var message = new Message
            {
                Token = deviceToken,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = data ?? new Dictionary<string, string>()
            };

            var retryPolicy = Policy.Handle<FirebaseMessagingException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            await retryPolicy.ExecuteAsync(async () => await FirebaseMessaging.DefaultInstance.SendAsync(message));
        }

        public async Task SendToMultipleAsync(List<string> deviceTokens, string title, string body, Dictionary<string, string>? data = null)
        {
            if (deviceTokens == null || deviceTokens.Count == 0)
            {
                Console.WriteLine("No device tokens provided for multicast send.");
                return;
            }

            var multicastMessage = new MulticastMessage
            {
                Tokens = deviceTokens,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data ?? new Dictionary<string, string>()
            };

            var retryPolicy = Policy
                .Handle<FirebaseMessagingException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            await retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage);

                    Console.WriteLine($"Multicast send: Success={response.SuccessCount}, Failure={response.FailureCount}");

                    if (response.FailureCount > 0)
                    {
                        for (int i = 0; i < response.Responses.Count; i++)
                        {
                            if (!response.Responses[i].IsSuccess)
                            {
                                var error = response.Responses[i].Exception?.Message ?? "Unknown error";
                                Console.WriteLine($"Failed for token {deviceTokens[i]}: {error}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Multicast send exception: {ex.Message}");
                }
            });
        }
    }
}
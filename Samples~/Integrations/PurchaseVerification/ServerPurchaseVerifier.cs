using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaseUnity.Features;
using UnityEngine;

namespace BaseUnity.Integrations
{
    [CreateAssetMenu(menuName = "BaseUnity/Providers/Server Receipt Verifier")]
    public sealed class ServerPurchaseVerifier : PurchaseVerifier
    {
        [Tooltip("Your trusted server validates the receipt against Google/Apple and binds the transaction to the player/app/product.")]
        public string endpoint;
        [Min(1)] public int timeoutSeconds = 15;
        [NonSerialized] public Func<string> authorizationToken;
        public override IPurchaseVerifier Create() => new Backend(endpoint, timeoutSeconds, authorizationToken);
        [Serializable] private sealed class Response { public bool valid; public string transactionId, productId; }
        private sealed class Backend : IPurchaseVerifier
        {
            private readonly string endpoint;
            private readonly Func<string> token;
            private readonly HttpClient client;
            public Backend(string endpoint, int timeout, Func<string> token)
            {
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("Configure an HTTPS receipt verification endpoint.");
                this.endpoint = endpoint; this.token = token; client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(Math.Max(1, timeout)) };
            }
            public async Task<bool> VerifyAsync(StorePurchase purchase, CancellationToken cancellation)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Content = new StringContent(JsonUtility.ToJson(purchase), Encoding.UTF8, "application/json");
                    string auth = token?.Invoke(); if (!string.IsNullOrEmpty(auth)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
                    using (var response = await client.SendAsync(request, cancellation))
                    {
                        if (!response.IsSuccessStatusCode) return false;
                        var result = JsonUtility.FromJson<Response>(await response.Content.ReadAsStringAsync());
                        return result != null && result.valid && result.transactionId == purchase.transactionId && result.productId == purchase.productId;
                    }
                }
            }
            public void Dispose() { client.Dispose(); }
        }
    }
}

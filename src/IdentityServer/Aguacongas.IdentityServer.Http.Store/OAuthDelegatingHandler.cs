﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Aguacongas.IdentityServer.Http.Store
{
    public class OAuthDelegatingHandler : DelegatingHandler
    {
        private readonly OAuthTokenManager _manager;

        public OAuthDelegatingHandler(OAuthTokenManager manager, HttpMessageHandler innerHandler):base(innerHandler)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await _manager.GetTokenAsync();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

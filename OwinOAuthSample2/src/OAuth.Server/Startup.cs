﻿using Microsoft.Owin;
using Microsoft.Owin.Security.Jwt;
using Microsoft.Owin.Security.OAuth;
using OAuth.Server;
using OAuth.Server.Providers;
using OAuth.Server.Providers.OAuth;
using Owin;
using System;
using System.IdentityModel.Metadata;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

[assembly: OwinStartup(typeof(Startup))]
namespace OAuth.Server
{
    public class Startup
    {
        private const string OAuthTokenEndpoint = "https://localhost:5000/oauth/token";
        private const string FederationMetadataUrl = "/FederationMetadata/2007-06/FederationMetadata.xml";

        public void Configuration(IAppBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path == new PathString(FederationMetadataUrl))
                {
                    string message = Generate();
                    ctx.Response.Headers.Add("Content-Type", new[] { "text/xml" });
                    await ctx.Response.WriteAsync(message);
                }
                else
                {
                    await next();
                }
            });

            app.UseOAuthAuthorizationServer(new OAuthAuthorizationServerOptions 
            {
                AuthorizeEndpointPath = new PathString("/oauth/authorize"),
                TokenEndpointPath = new PathString("/oauth/token"),
                ApplicationCanDisplayErrors = true,
                AccessTokenFormat = new JwtFormat(new X509CertificateSigningCredentialsProvider(AppConstants.Issuer, GetSigningCertificate())),

                Provider = new AuthorizationServerProvider(),
                AuthorizationCodeProvider = new AuthorizationCodeProvider(),
                RefreshTokenProvider = new RefreshTokenProvider()
            });
        }

        // Helpers

        private string Generate()
        {
            var tokenServiceDescriptor = GetTokenServiceDescriptor();
            var id = new EntityId("https://tugberk.me");
            var entity = new EntityDescriptor(id);
            var certificate = GetSigningCertificate();
            entity.SigningCredentials = new X509SigningCredentials(certificate);
            entity.RoleDescriptors.Add(tokenServiceDescriptor);

            var ser = new MetadataSerializer();
            var sb = new StringBuilder(512);

            using (var sr = new StringWriter(sb))
            using (var xmlWriter = XmlWriter.Create(sr, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                ser.WriteMetadata(xmlWriter, entity);
                return sb.ToString();
            }
        }

        private SecurityTokenServiceDescriptor GetTokenServiceDescriptor()
        {
            var tokenService = new SecurityTokenServiceDescriptor();
            tokenService.TokenTypesOffered.Add(new Uri(TokenTypes.JsonWebToken));
            tokenService.ProtocolsSupported.Add(new Uri("http://docs.oasis-open.org/wsfed/federation/200706"));
            tokenService.SecurityTokenServiceEndpoints.Add(new EndpointReference(OAuthTokenEndpoint));

            return tokenService;
        }

        private KeyDescriptor GetSigningKeyDescriptor()
        {
            var certificate = GetSigningCertificate();
            var clause = new X509SecurityToken(certificate).CreateKeyIdentifierClause<X509RawDataKeyIdentifierClause>();
            var key = new KeyDescriptor(new SecurityKeyIdentifier(clause));
            key.Use = KeyType.Signing;

            return key;
        }

        private static X509Certificate2 GetSigningCertificate()
        {
            X509Store store = new X509Store("My");
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2 signingCert = store.Certificates[0];
            store.Close();

            return signingCert;
        }
    }
}

using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HTTPGateway
{
    public class JWTService
    {
        public string jwt = "";
        public async Task<string> Generate(JWTUser user, string privateKey)
        {
            var issuer = "http://localhost";
            var authority = "http://localhost";
            var daysValid = 365;

            var createJwt = await CreateJWTAsync(user, issuer, authority, privateKey, daysValid);
            this.jwt = createJwt;
            return createJwt;
        }

        public static async Task<string> CreateJWTAsync(
            JWTUser user,
            string issuer,
            string authority,
            string symSec,
            int daysValid)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var claims = await CreateClaimsIdentities(user);

            // Create JWToken
            var token = tokenHandler.CreateJwtSecurityToken(issuer: issuer,
                audience: authority,
                subject: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(daysValid),
                signingCredentials:
                new SigningCredentials(
                    new SymmetricSecurityKey(
                        Encoding.Default.GetBytes(symSec)),
                        SecurityAlgorithms.HmacSha256Signature));

            return tokenHandler.WriteToken(token);
        }

        public static Task<ClaimsIdentity> CreateClaimsIdentities(JWTUser user)
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.UserId));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, user.RoleId));
            return Task.FromResult(claimsIdentity);
        }

        public static async Task<string> Verify(string jwt)
        {
            var decodedJwt = await ReadTokenAsync(jwt);
            return decodedJwt;
        }

        public static string ReadToken(string jwtInput)
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtOutput = string.Empty;

            // Check Token Format
            if (!jwtHandler.CanReadToken(jwtInput)) throw new Exception("The token doesn't seem to be in a proper JWT format.");

            var token = jwtHandler.ReadJwtToken(jwtInput);

            // Re-serialize the Token Headers to just Key and Values
            var jwtHeader = JsonConvert.SerializeObject(token.Header.Select(h => new { h.Key, h.Value }));
            jwtOutput = $"{{\r\n\"Header\":\r\n{JToken.Parse(jwtHeader)},";

            // Re-serialize the Token Claims to just Type and Values
            var jwtPayload = JsonConvert.SerializeObject(token.Claims.Select(c => new { c.Type, c.Value }));
            jwtOutput += $"\r\n\"Payload\":\r\n{JToken.Parse(jwtPayload)}\r\n}}";

            // Output the whole thing to pretty Json object formatted.
            return JToken.Parse(jwtOutput).ToString(Formatting.Indented);
        }

        public static async Task<string> ReadTokenAsync(string jwtInput)
        {
            return await Task.Run(() =>
            {
                return ReadToken(jwtInput);
            });
        }

        public static async Task<bool> Validate(string token, string secret)
        {
            var validToken = false;

            try
            { validToken = await ValidateTokenAsync(token, secret); } // This section can be greatly expanded to give more custom error messages.
            catch (SecurityTokenExpiredException) { /* Handle */ }
            catch (SecurityTokenValidationException) { /* Handle */ }
            catch (SecurityTokenException) { /* Handle */ }
            catch (Exception) { /* Handle */ }

            if (validToken) // Happy Path For Valid JWT
            { return true; }
            
            return false;
        }

        // private static bool TokenExists(HttpRequestMessage request, out string token)
        // {
        //     var tokenFound = false;
        //     token = null;
        //     if (request.Headers.TryGetValue("_auth", out token))
        //     {
        //         token = token.StartsWith("Bearer ") ? token.Substring(7) : token;
        //         tokenFound = true;
        //     }

        //     return tokenFound;
        // }

        private static async Task<bool> ValidateTokenAsync(string token, string secret)
        {
            var userIsValid = true; // assumed user is good (but could be false)
            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

            var tvp = GetTokenValidationParameters(secret);

            //Extract and assigns the PrincipalId from JWT User/Claims.
            Thread.CurrentPrincipal = jwtSecurityTokenHandler.ValidateToken(token, tvp, out SecurityToken securityToken);

            // TODO: Extra Validate the UserId, check for user still exists, user isn't banned, user registered email etc.
            //userIsValid = await _userService.ApiUserGetValidationAsync(HttpContext.Current.User.GetUserId());
            // GetUserId() is an extension method I wrote so you will need to write something like
            //this for yourself.

            if (!userIsValid) throw new SecurityTokenValidationException();

            return await Task.FromResult(userIsValid);
        }

        private static TokenValidationParameters GetTokenValidationParameters(string secret)
        {
            // Cleanup
            return new TokenValidationParameters
            {
                ValidAudience = "http://localhost", // Remember to copy your own.
                ValidIssuer = "http://localhost", // Also don't leave them as magic strings, load them from the app.config.
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                LifetimeValidator = LifetimeValidator,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.Default // Seriously don't copy this string. It's bad for you. Stop. Go here: https://passwordsgenerator.net/
                    .GetBytes(secret))
            };
        }

        private static bool LifetimeValidator(
            DateTime? notBefore,
            DateTime? expires,
            SecurityToken securityToken,
            TokenValidationParameters validationParameters)
        {
            var valid = false;

            // Additional checks can be performed on the SecurityToken or the validationParameters.
            if ((expires.HasValue && DateTime.UtcNow < expires)
             && (notBefore.HasValue && DateTime.UtcNow > notBefore))
            { valid = true; }

            return valid;
        }

        public class JWTUser
        {
            public string UserId { get; set; }
            public string UserName { get; set; }

            public string RoleId { get; set; }
        }
    } 
}
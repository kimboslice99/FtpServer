// <copyright file="CustomMembershipProvider.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using Serilog;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Odbc;

using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer;
using Microsoft.Extensions.Configuration;

namespace TestFtpServer
{
    /// <summary>
    /// Custom membership provider
    /// </summary>
    public class CustomMembershipProvider : IMembershipProviderAsync
    {
        private readonly string _connectionString;
        private readonly IFtpConnectionAccessor _connectionAccessor;

        public CustomMembershipProvider(IConfiguration configuration, IFtpConnectionAccessor connectionAccessor)
        {
            _connectionString = configuration["custom:connectionString"];
            _connectionAccessor = connectionAccessor;
        }

        /// <inheritdoc />
        public async Task<MemberValidationResult> ValidateUserAsync(
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new NullReferenceException("Connection string has not been set.");
            }

            // Open the connection
            using (var connection = new OdbcConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Create command with parameterized query
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT user_name, user_password FROM users WHERE user_name = ?";
                cmd.Parameters.Add(new OdbcParameter("user_name", OdbcType.Text) { Value = username });

                // Execute query
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        // Retrieve the stored password hash from the database
                        var storedPasswordHash = reader.GetString(reader.GetOrdinal("user_password"));

                        // Validate the password using BCrypt
                        if (BCrypt.Net.BCrypt.Verify(password, storedPasswordHash))
                        {
                            var user = new ClaimsPrincipal(
                                new ClaimsIdentity(
                                    new[]
                                    {
                                        new Claim(ClaimsIdentity.DefaultNameClaimType, username),
                                        new Claim(ClaimsIdentity.DefaultRoleClaimType, "user"),
                                    },
                                    "custom"));

                            Log.Information($"Successful login for user {username} ip {_connectionAccessor.FtpConnection.RemoteEndPoint.Address}");

                            return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, user);
                        }
                    }
                }
            }
            Log.Information($"Failed login for user {username} ip {_connectionAccessor.FtpConnection.RemoteEndPoint.Address}");
            // Return invalid login if user not found or password mismatch
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }

        /// <inheritdoc />
        public Task LogOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<MemberValidationResult> ValidateUserAsync(string username, string password)
        {
            return ValidateUserAsync(username, password, CancellationToken.None);
        }
    }
}

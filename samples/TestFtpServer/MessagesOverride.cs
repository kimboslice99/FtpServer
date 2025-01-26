using System;
using System.Collections.Generic;

using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.Localization;

using Microsoft.Extensions.Configuration;

public class CustomFtpServerMessages : IFtpServerMessages
{
    private readonly IFtpConnectionAccessor _connectionAccessor;
    private readonly IConfiguration _configuration;

    public CustomFtpServerMessages(IFtpConnectionAccessor connectionAccessor, IConfiguration config)
    {
        _connectionAccessor = connectionAccessor;
        _configuration = config;
        Banner = config.GetSection("custom:banner").Get<string[]>() ??
            new string[] { "My Custom FTP Server", "Welcome!" };
    }

    private static string[] Banner { get; set; }

    public IEnumerable<string> GetBannerMessage()
    {
        return Banner;
    }

    public IEnumerable<string> GetDirectoryChangedMessage(Stack<IUnixDirectoryEntry> path)
    {
        return new[] { $"Directory changed to: {path.GetFullPath()}" };
    }

    public IEnumerable<string> GetPasswordAuthorizationSuccessfulMessage(IAccountInformation accountInformation)
    {
        return new[] { "Login successful.", $"Welcome {accountInformation.FtpUser.Identity.Name}" };
    }
}

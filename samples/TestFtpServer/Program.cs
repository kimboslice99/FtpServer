// <copyright file="Program.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using BCrypt.Net;

using System;
using System.IO;

using JKang.IpcServiceFramework;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;

using TestFtpServer.Configuration;
using FubarDev.FtpServer.Localization;
using FubarDev.FtpServer;
using TestFtpServer.Commands;
using FubarDev.FtpServer.CommandHandlers;

namespace TestFtpServer
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .CreateLogger();

            try
            {
                var host = CreateHostBuilder(args);
                
                host.Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static HostBuilder CreateHostBuilder(string[] args)
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SharpFtpServer");

            var hostBuilder = new HostBuilder();
            hostBuilder
               .UseConsoleLifetime()
               .ConfigureHostConfiguration(
                    configHost => { configHost.AddEnvironmentVariables("FTPSERVER_"); })
               .ConfigureAppConfiguration(
                    (hostContext, configApp) =>
                    {
                        configApp
                           .AddJsonFile("appsettings.json")
                           .AddJsonFile(Path.Combine(configPath, "appsettings.json"), true)
                           .AddJsonFile(
                                $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                                optional: true)
                           .AddJsonFile(
                                Path.Combine(
                                    configPath,
                                    $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json"),
                                optional: true)
                           .AddEnvironmentVariables("FTPSERVER_")
                           .Add(new OptionsConfigSource(args));
                    })
               .ConfigureLogging(
                    (hostContext, loggingBuilder) => { loggingBuilder.ClearProviders(); })
                .ConfigureServices((hostContext, services) =>
                {
                    // Load FTP options from configuration
                    var options = hostContext.Configuration.Get<FtpOptions>();
                    options.Validate();

                    // custom server banner
                    services.AddSingleton<IFtpServerMessages, CustomFtpServerMessages>();
                    // custom auth that logs failed logins. important to show examples like this.
                    services.AddSingleton<CustomMembershipProvider>();
                    // geoblock
                    services.AddSingleton<IFtpMiddleware, GeoblockMiddleware>();

                    // Now register FTP services and other services
                    services
                        .AddFtpServices(options)  // Configure other FTP services
                        .AddHostedService<HostedFtpService>()
                        .AddHostedService<HostedIpcService>()
                        .AddIpc(builder =>
                        {
                            builder
                                .AddNamedPipe(opt => opt.ThreadCount = 1)
                                .AddService<Api.IFtpServerHost, FtpServerHostApi>();
                        });
                })
               .UseSerilog(
                    (context, configuration) => { configuration.ReadFrom.Configuration(context.Configuration); });

            return hostBuilder;
        }
    }
}

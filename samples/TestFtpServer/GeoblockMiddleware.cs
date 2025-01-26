using FubarDev.FtpServer;

using Microsoft.Extensions.Configuration;

using System;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using System.Linq;
using FubarDev.FtpServer.ServerCommands;
using System.Threading;
using TestFtpServer.Utilities;

public class GeoblockMiddleware : IFtpMiddleware
{
    private IPUtilities _ipUtilities;
    private Serilog.ILogger _logger;

    public GeoblockMiddleware(IConfiguration config, Serilog.ILogger logger)
    {
        DbPath = config["geoip:dbPath"] ?? null;
        AllowMode = Convert.ToBoolean(config["geoip:allowMode"]);
        _ipUtilities = new IPUtilities();
        CountryCodes = config.GetSection("geoip:countryCodes").Get<string[]>() ?? Array.Empty<string>();
        _logger = logger;
    }

    public static string[] CountryCodes { get; set; }

    public static string DbPath { get; set; }

    public static bool AllowMode { get; set; }

    public Task InvokeAsync(FtpContext context, FtpRequestDelegate next)
    {
        // no path in config, move along
        if (string.IsNullOrEmpty(DbPath))
        {
            _logger.Information("dbpath null or empty");
            return next(context);
        }
        _logger.Debug("Geoblocking initalized with settings");
        _logger.Debug($"DbPath {DbPath}");
        _logger.Debug($"CountryCodes {CountryCodes}");
        _logger.Debug($"AllowMode {AllowMode}");
        var remoteAddr = context.Connection.RemoteEndPoint.Address;

        // if local allow the connection and skip geoip processing
        if (_ipUtilities.IsLocal(remoteAddr))
        {
            _logger.Debug("Address was local, skipping geoblocking processing.");
            return next(context);
        }

        try
        {
            using (var reader = new DatabaseReader(DbPath))
            {
                var country = reader.Country(remoteAddr); // Example IP address
                if (AllowMode == true)
                {
                    if (!CountryCodes.Contains(country.Country.IsoCode))
                    {
                        _logger.Information($"Denied remote client from {country.Country.IsoCode} ip {remoteAddr}");
                        return Deny(context);
                    }
                }
                else
                {
                    if (CountryCodes.Contains(country.Country.IsoCode))
                    {
                        _logger.Information($"Denied remote client from {country.Country.IsoCode} ip {remoteAddr}");
                        return Deny(context);
                    }
                }
                _logger.Information($"Allowed remote client from {country.Country.IsoCode} ip {remoteAddr}");
            }
        }
        catch (Exception e)
        {
            // log it
            _logger.Error($"Exception {e.Message}");
            return Deny(context); // Deny and return without calling next
        }

        return next(context); // Proceed to next if no denial
    }

    public async Task<IFtpResponse?> Deny(FtpContext context)
    {
        var response = new SendResponseServerCommand(new FtpResponse(530, "Unauthorized."));
        await context.ServerCommandWriter.WriteAsync(response, CancellationToken.None).ConfigureAwait(false);
        var close = new CloseConnectionServerCommand();
        await context.ServerCommandWriter.WriteAsync(close, CancellationToken.None).ConfigureAwait(false);
        return null;
    }
}

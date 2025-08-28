using System.Net;
using IntegrationGateway.Api.Configuration;

namespace IntegrationGateway.Api.Services;

/// <summary>
/// Service for validating URLs to prevent SSRF attacks
/// </summary>
public interface IUrlValidationService
{
    /// <summary>
    /// Validates if a URL is safe for outbound requests
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>True if the URL is safe, false otherwise</returns>
    Task<bool> IsUrlSafeAsync(string url);

    /// <summary>
    /// Validates if a URL is safe for outbound requests
    /// </summary>
    /// <param name="uri">The URI to validate</param>
    /// <returns>True if the URL is safe, false otherwise</returns>
    Task<bool> IsUrlSafeAsync(Uri uri);
}

/// <summary>
/// Implementation of URL validation service for SSRF protection
/// </summary>
public class UrlValidationService : IUrlValidationService
{
    private readonly SsrfProtectionOptions _options;
    private readonly ILogger<UrlValidationService> _logger;
    
    // Private network ranges to block
    private static readonly string[] DefaultPrivateRanges = {
        "10.0.0.0/8",      // Class A private
        "172.16.0.0/12",   // Class B private
        "192.168.0.0/16",  // Class C private
        "127.0.0.0/8",     // Loopback
        "169.254.0.0/16",  // Link-local
        "224.0.0.0/4",     // Multicast
        "::1/128",         // IPv6 loopback
        "fe80::/10",       // IPv6 link-local
        "fc00::/7"         // IPv6 unique local
    };

    public UrlValidationService(
        IConfiguration configuration,
        ILogger<UrlValidationService> logger)
    {
        _options = configuration.GetSection(SecurityOptions.SectionName)
            .GetSection(nameof(SecurityOptions.SsrfProtection))
            .Get<SsrfProtectionOptions>() ?? new SsrfProtectionOptions();
        _logger = logger;
    }

    public async Task<bool> IsUrlSafeAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Empty or null URL provided for validation");
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Invalid URL format: {Url}", url);
            return false;
        }

        return await IsUrlSafeAsync(uri);
    }

    public async Task<bool> IsUrlSafeAsync(Uri uri)
    {
        if (!_options.Enabled)
        {
            return true;
        }

        try
        {
            // Only allow HTTP and HTTPS schemes
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                _logger.LogWarning("Blocked non-HTTP scheme: {Scheme} for URL: {Url}", uri.Scheme, uri.ToString());
                return false;
            }

            // Check if domain is in allowed list
            if (_options.AllowedDomains?.Length > 0)
            {
                var host = uri.Host.ToLowerInvariant();
                var isAllowed = _options.AllowedDomains.Any(domain => 
                    host.Equals(domain.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith($".{domain.ToLowerInvariant()}", StringComparison.OrdinalIgnoreCase));

                if (!isAllowed)
                {
                    _logger.LogWarning("Domain not in allowed list: {Host}", host);
                    return false;
                }
            }

            // Resolve hostname to IP address
            var addresses = await GetHostAddressesAsync(uri.Host);
            if (addresses == null || addresses.Length == 0)
            {
                _logger.LogWarning("Could not resolve hostname: {Host}", uri.Host);
                return false;
            }

            // Check each resolved IP address
            foreach (var address in addresses)
            {
                if (!IsIpAddressSafe(address))
                {
                    _logger.LogWarning("Blocked unsafe IP address: {IpAddress} for hostname: {Host}", address, uri.Host);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating URL: {Url}", uri.ToString());
            return false;
        }
    }

    private async Task<IPAddress[]> GetHostAddressesAsync(string hostname)
    {
        try
        {
            // Add timeout for DNS resolution to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await Dns.GetHostAddressesAsync(hostname).WaitAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS resolution failed for hostname: {Hostname}", hostname);
            return Array.Empty<IPAddress>();
        }
    }

    private bool IsIpAddressSafe(IPAddress address)
    {
        // Block localhost if configured
        if (_options.BlockLocalhost && IPAddress.IsLoopback(address))
        {
            return false;
        }

        // Block private networks if configured
        if (_options.BlockPrivateNetworks && IsPrivateNetwork(address))
        {
            return false;
        }

        // Check custom blocked ranges
        if (_options.CustomBlockedRanges?.Length > 0)
        {
            foreach (var range in _options.CustomBlockedRanges)
            {
                if (IsInCidrRange(address, range))
                {
                    return false;
                }
            }
        }

        // Check default blocked ranges
        foreach (var range in DefaultPrivateRanges)
        {
            if (IsInCidrRange(address, range))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPrivateNetwork(IPAddress address)
    {
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return 
                // 10.0.0.0/8
                bytes[0] == 10 ||
                // 172.16.0.0/12
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                // 192.168.0.0/16
                (bytes[0] == 192 && bytes[1] == 168);
        }

        return false;
    }

    private static bool IsInCidrRange(IPAddress address, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IPAddress.TryParse(parts[0], out var networkAddress) || 
                !int.TryParse(parts[1], out var prefixLength))
                return false;

            if (address.AddressFamily != networkAddress.AddressFamily)
                return false;

            var addressBytes = address.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();
            
            if (addressBytes.Length != networkBytes.Length)
                return false;

            var bytesToCheck = prefixLength / 8;
            var bitsToCheck = prefixLength % 8;

            // Check full bytes
            for (int i = 0; i < bytesToCheck; i++)
            {
                if (addressBytes[i] != networkBytes[i])
                    return false;
            }

            // Check partial byte if necessary
            if (bitsToCheck > 0 && bytesToCheck < addressBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - bitsToCheck));
                if ((addressBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
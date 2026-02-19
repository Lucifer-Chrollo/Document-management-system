using OtpNet;
using System.Security.Cryptography;
using System.Text;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Service for Multi-Factor Authentication using TOTP
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Generate a new secret key for TOTP
    /// </summary>
    string GenerateSecret();
    Task<string> GenerateSecretAsync();
    
    /// <summary>
    /// Generate the provisioning URI for QR code
    /// </summary>
    string GetProvisioningUri(string secret, string email, string issuer = "DMS");
    Task<string> GetProvisioningUriAsync(string secret, string email, string issuer = "DMS");
    
    /// <summary>
    /// Validate a TOTP code
    /// </summary>
    bool ValidateCode(string secret, string code);
    Task<bool> ValidateCodeAsync(string secret, string code);
    
    /// <summary>
    /// Generate the current TOTP code (for testing)
    /// </summary>
    string GenerateCode(string secret);
    Task<string> GenerateCodeAsync(string secret);
}

/// <summary>
/// TOTP-based Multi-Factor Authentication Service
/// </summary>
public class MfaService : IMfaService
{
    private readonly ILogger<MfaService> _logger;
    private const int SecretSize = 20; // 160 bits for standard TOTP
    
    public MfaService(ILogger<MfaService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Generate a new random secret for TOTP
    /// </summary>
    public string GenerateSecret()
    {
        var secret = new byte[SecretSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(secret);
        return Base32Encoding.ToString(secret);
    }
    
    /// <summary>
    /// Generate provisioning URI for authenticator app QR code
    /// Format: otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}
    /// </summary>
    public string GetProvisioningUri(string secret, string email, string issuer = "DMS")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public Task<string> GetProvisioningUriAsync(string secret, string email, string issuer = "DMS")
    {
        return Task.Run(() => GetProvisioningUri(secret, email, issuer));
    }
    
    /// <summary>
    /// Validate a TOTP code against the secret
    /// </summary>
    public Task<string> GenerateSecretAsync()
    {
        return Task.Run(() => GenerateSecret());
    }

    /// <summary>
    /// Validate a TOTP code against the secret
    /// </summary>
    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
            return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating TOTP code");
            return false;
        }
    }

    public Task<bool> ValidateCodeAsync(string secret, string code)
    {
        return Task.Run(() => ValidateCode(secret, code));
    }
    
    /// <summary>
    /// Generate the current TOTP code (useful for testing)
    /// </summary>
    public string GenerateCode(string secret)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            return totp.ComputeTotp();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating TOTP code");
            return string.Empty;
        }
    }
    public Task<string> GenerateCodeAsync(string secret)
    {
        return Task.Run(() => GenerateCode(secret));
    }
}

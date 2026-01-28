# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them via:

- **Email**: Create a private security advisory on GitHub
- **GitHub Security Advisory**: https://github.com/kem768dev/kem768/security/advisories/new

### What to Include

When reporting a vulnerability, please include:

- Description of the vulnerability
- Steps to reproduce the issue
- Affected versions
- Potential impact
- Suggested fix (if available)

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days
- **Fix Timeline**: Varies by severity (Critical: 7 days, High: 30 days, Medium: 90 days)

## Security Best Practices

When using kem768 in production:

1. **Key Management**: Store ML-KEM private keys in HSM/KMS (Azure Key Vault, AWS KMS)
2. **Rate Limiting**: Implement rate limiting on validation endpoints
3. **Logging**: Enable structured logging for audit trails
4. **Updates**: Keep dependencies updated (BouncyCastle, .NET runtime)
5. **Network**: Run behind a firewall with proper network segmentation

## Known Limitations

- SQLite default storage is not suitable for high-concurrency production use
- Nonces are stored in-memory by default (use database persistence for production)
- No built-in rate limiting (implement at infrastructure level)

## Cryptographic Disclosure

This project uses:

- **ECDSA P-256** (NIST FIPS 186-4)
- **ML-KEM-768** (NIST FIPS 203)
- **HMAC-SHA256** (NIST FIPS 198-1)

All implementations rely on BouncyCastle Cryptography library.

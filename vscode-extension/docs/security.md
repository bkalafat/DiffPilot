# Security Scanning

DiffPilot includes a secret scanner that detects accidentally committed credentials in your code changes.

## Detected Secret Types

### API Keys

| Provider | Pattern |
|----------|---------|
| AWS Access Key | `AKIA[0-9A-Z]{16}` |
| AWS Secret Key | `aws.{0,20}secret.{0,20}[0-9a-zA-Z/+]{40}` |
| GitHub Token | `gh[pousr]_[A-Za-z0-9_]{36,}` |
| GitHub Classic | `ghp_[A-Za-z0-9_]{36}` |
| Slack Token | `xox[baprs]-[0-9a-zA-Z-]+` |
| Slack Webhook | `hooks.slack.com/services/T[A-Z0-9]+/B[A-Z0-9]+/[a-zA-Z0-9]+` |

### Private Keys

| Type | Detection |
|------|-----------|
| RSA Private Key | `-----BEGIN RSA PRIVATE KEY-----` |
| DSA Private Key | `-----BEGIN DSA PRIVATE KEY-----` |
| EC Private Key | `-----BEGIN EC PRIVATE KEY-----` |
| OpenSSH Private Key | `-----BEGIN OPENSSH PRIVATE KEY-----` |
| PGP Private Key | `-----BEGIN PGP PRIVATE KEY BLOCK-----` |

### Passwords & Secrets

| Pattern | Example |
|---------|---------|
| Password in URL | `https://user:password123@host.com` |
| Password variable | `password = "secret123"` |
| Secret variable | `secret_key = "abc123"` |
| API key variable | `api_key = "key123"` |

### Tokens

| Type | Pattern |
|------|---------|
| Bearer Token | `Bearer [A-Za-z0-9-._~+/]+=*` |
| JWT Token | `eyJ[A-Za-z0-9-_]+\.eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+` |
| Azure SAS Token | `sv=20[0-9]{2}-[0-9]{2}-[0-9]{2}&s[a-z]=[a-z]+&s[a-z]+=[^&]+` |
| Connection String | `DefaultEndpointsProtocol=https;AccountName=` |

## Using the Scanner

### Via Command Palette

1. Open Command Palette (`Ctrl+Shift+P`)
2. Run `DiffPilot: Scan for Secrets`
3. Review results in Output panel

### Via AI Assistant

Ask your AI assistant:
- "Scan my changes for secrets"
- "Check if I'm committing any API keys"
- "Are there any passwords in my staged files?"

### Automatic Scanning

Enable auto-scan in settings:

```json
{
  "diffpilot.scanOnSave": true
}
```

## Scan Scope

The scanner checks:
- **Staged changes** - Files added to git staging area
- **Unstaged changes** - Modified files not yet staged

Configure scope with parameters:
- `scanStaged: true/false`
- `scanUnstaged: true/false`

## Best Practices

1. **Scan before committing** - Run the scanner before every commit
2. **Use environment variables** - Store secrets in `.env` files (add to `.gitignore`)
3. **Use secret managers** - Azure Key Vault, AWS Secrets Manager, HashiCorp Vault
4. **Rotate exposed secrets** - If a secret is committed, rotate it immediately

## False Positives

The scanner may flag:
- Test fixtures with fake credentials
- Documentation examples
- Base64 encoded non-secret data

Review each finding before dismissing.

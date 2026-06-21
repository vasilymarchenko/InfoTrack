# SSL and corporate proxies

## Background

Some organisations route all outbound HTTPS traffic through an **SSL inspection proxy**.
The proxy intercepts each request, decrypts it, inspects the content, then re-encrypts and
forwards it. Because the proxy re-signs the traffic with its own certificate, every response
the client receives carries a certificate issued by the company's own root CA — not the
certificate the remote server originally sent.

On managed Windows workstations this is invisible: IT pre-installs the corporate root CA into
the Windows certificate store, so browsers and .NET applications trust it automatically.

Docker containers run Linux and have no knowledge of the host machine's certificate store.
When the API container makes an outbound HTTPS request (to fetch solicitor listings) and the
traffic passes through a corporate proxy, the container receives a certificate signed by an
unknown CA and refuses the connection with:

```
AuthenticationException: The remote certificate is invalid because of errors in the
certificate chain: UntrustedRoot
```

## How the project handles this

The `certs/` directory at the repository root acts as an injection point for extra CA
certificates. At image build time the Dockerfile copies that directory into the image and,
if any `.crt` files are present, registers them with the system trust store via
`update-ca-certificates`. If the directory is empty the step is a no-op and the build
proceeds normally.

`*.crt` files are gitignored — they are machine-local and must not be committed.

## If you hit this error

1. Identify your organisation's root CA certificate. It will be in the Windows certificate
   store under **Trusted Root Certification Authorities**. Look for the CA that appears as
   the top-level issuer when you inspect the certificate chain for any HTTPS site in your
   browser.

2. Export it as a PEM file. Open PowerShell and run:

   ```powershell
   $cert = Get-ChildItem Cert:\LocalMachine\Root |
       Where-Object { $_.Subject -like "*<YourCA>*" } |
       Select-Object -First 1

   $b64 = [System.Convert]::ToBase64String($cert.RawData, 'InsertLineBreaks').Replace("`r`n", "`n")
   $pem = "-----BEGIN CERTIFICATE-----`n$b64`n-----END CERTIFICATE-----`n"
   [System.IO.File]::WriteAllText("certs\corporate-ca.crt", $pem, [System.Text.Encoding]::ASCII)
   ```

   Replace `<YourCA>` with a distinguishing substring of your CA's subject name.

3. Rebuild the image:

   ```bash
   docker compose up --build
   ```

   The cert will be registered automatically. No changes to any tracked file are needed.

## Why this only affects Docker, not local runs

When you run the application directly on Windows (`dotnet run`) it uses the Windows
certificate store and trusts the proxy's CA automatically. The error only surfaces inside the
Linux container because it carries its own isolated trust store.

# Test Fixtures — real captured HTML

Captured live from `https://www.solicitors.com/conveyancing+{slug}.html` on 2026-06-17
(User-Agent: `Mozilla/5.0 (compatible; InfoTrackBot/1.0; +https://infotrack.example)`). These are the source of truth
for the parser — **derive every selector from these files, do not assume markup.**

| File | Slug | HTTP | result-item count | Notes |
|------|------|------|-------------------|-------|
| `conveyancing+london.html`       | london     | 200 | **75** | 37 featured + 38 small firm blocks; **1 banner-block sibling** between items |
| `conveyancing+leeds.html`        | leeds      | 200 | **50** | 7 featured + 43 small firm blocks |
| `conveyancing+bradford.html`     | bradford   | 200 | **23** | real Bradford firms (BD postcodes) |
| `conveyancing+notfound-404.html` | zzznowhere | 404 | 0     | tiny "Page not found" body; 0 firm blocks |

## E2E test baselines (locked)

| Fixture | Expected solicitors | Banner(s) |
|---------|---------------------|-----------|
| london  | 75                  | 1 (excluded by segmenter) |
| bradford | 23                 | 0 |
| notfound-404 | 0             | — |

Spot-check firm (London fixture, block 0): **Aspen Morris Solicitors**
- `Phone == "02083707750"`
- `Postcode == "N14 6BP"`
- `ReviewCount == 112`
- `WebsiteUrl` host is `www.aspenmorris.com` (not `solicitors.com`)

## Verified markup (read from the fixtures, not assumed)

- **Results container:** `<div class="result-section">`.
- **Per-firm block:** `<div class="result-item">` (featured) and
  `<div class="result-item item-small">` (compact). Both are firms; match the `result-item` prefix.
- **FirmName:** text inside `<span class="h2">` (stop at the first nested `<div>`/`<span>`).
- **ReviewCount:** integer in parentheses inside `<span class="rev-results">`, e.g. `(112)`,
  `&nbsp;(61)`. Nullable — many small blocks have no `rev-results`.
- **Phone:** `href="tel:..."` (featured: inside `.phone-block`; small: `<a class="tel" ...>`).
- **ProfileUrl + Address:** `<a href="/{firm}.html" class="link-map">` wrapping
  `<address>...</address>` (the postal address). Decode `&nbsp;`.
- **EnquiryUrl:** `<a ... href="/enquiry-form.asp?SiD=...&DiD=...">` (the "Email" link — NOT a real
  email address).
- **WebsiteUrl:** the `<a target="_blank" href="http(s)://...">` in `<ul class="list-item">` whose
  host is not `www.solicitors.com` and which is not the `tel:`/`enquiry-form` link.
- **Description:** the `<p>` blurb inside the block.
- **LogoUrl:** `<img src="/logos/...">` inside `.logo-holder` (featured blocks only; small blocks
  have no logo).
- **Postcode:** parse from the `<address>` text with a UK-postcode regex.

## How to re-capture

PowerShell (handles the Windows cert store; `curl` hit revocation-check failures):

```powershell
$ua = "Mozilla/5.0 (compatible; InfoTrackBot/1.0; +https://infotrack.example)"
Invoke-WebRequest "https://www.solicitors.com/conveyancing+london.html" -UserAgent $ua -OutFile conveyancing+london.html
Invoke-WebRequest "https://www.solicitors.com/conveyancing+leeds.html"  -UserAgent $ua -OutFile conveyancing+leeds.html
Invoke-WebRequest "https://www.solicitors.com/conveyancing+bradford.html" -UserAgent $ua -OutFile conveyancing+bradford.html
```

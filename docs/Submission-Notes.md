
## What the task asked for

The brief was clear: build a small tool that helps the CEO find conveyancing solicitors on solicitors.com, without using third-party libraries for the scraping part.

I followed that exactly. But I also tried to think about *why* the CEO needs this tool — not just what it should technically do.

---

## The scraping: no libraries, just code

The task specifically said no third-party HTML parsing libraries. So I wrote the parser from scratch using only what .NET gives you out of the box.

The first decision was what *not* to build. A complete HTML tokenizer that builds a full DOM tree would have been the general solution - but also significant overengineering for a single known page template. Instead the parser does only the structural work it actually needs, in two stages.

**Stage one: isolate the firm blocks.** The parser tracks the nesting depth of tags to find exactly where each firm's section of HTML begins and ends. This also naturally excludes the ad banners that sit between listings — they fall outside the boundary and are never read.

**Stage two: extract fields from each block.** With a clean block in hand, each field is pulled out by its own simple rule — find a tag, read its text, extract an attribute. A few targeted regexes handle fixed formats like postcodes and review counts. Name is required; no name means the block is skipped.

The result is a parser that is simple to reason about, easy to test field by field, and honest about what it does and does not handle. Scraping runs in parallel across all locations, with each location isolated so that one failure does not stop the others.

---

## The product thinking moment

While building the scraper I started asking: what does the CEO actually need from this?

The site already lets you search by location. If the tool just does the same thing - there is no real value added. The CEO can already do that. What takes time is not the search. It is everything around the search: checking eight cities one by one, comparing this week's results to last week's, noticing which firms are new, deciding who to call first.

So I stopped thinking about this as a search tool and started thinking about it as a **monitor**. The question the CEO is really asking is not "who is listed in London today?" — it is "who appeared this week that we haven't spoken to yet?"

That shift in framing changes what the backend needs to do.

---

## What the backend does

Every time a search runs, the results are not just returned and forgotten. They are saved to a PostgreSQL database in a structured way.

The database keeps track of three things separately:

**Firms** - each firm is stored once, with its latest known attributes (name, address, phone, website). If the same firm appears again in a future scrape, its record is updated in place. No duplicates.

**Search runs** - each time a search is triggered, a run record is created that captures which locations were searched, when, and the overall outcome.

**Sightings** - each time a firm appears in a search run for a particular location, a sighting row is recorded. A sighting captures the review count at that moment in time, because review count is the one attribute that changes. Everything else about the firm goes on the firm record.

This separation matters. It means we can ask questions like: "was this firm in the last three London searches?" or "is this firm's review count going up over time?" without re-scraping anything.

---

## The API

The backend exposes nine endpoints. Here is what each one is for.

**`POST /api/searches`** — the main operation. You send a list of locations, the backend scrapes them all in parallel, deduplicates the results across locations, builds a report, saves everything to the database, and returns the full result immediately. No polling needed. The report includes: top firms by review count, firms that appear in multiple cities, and a breakdown of how many firms have a phone number or website (so the CEO knows how contactable a batch of leads is before picking up the phone).

**`GET /api/searches`** — lists all past runs, newest first. Returns just the metadata (when it ran, how many locations, how many firms) — enough to navigate history without loading everything.

**`GET /api/searches/{id}`** — reopens a stored run. Returns the same shape as the search response, with the report recomputed fresh from the stored data. This means if the report logic improves, old runs benefit from it automatically.

**`GET /api/searches/{id}/changes`** — the change detection endpoint. Given a run, it compares each location's results to the most recent earlier successful run of the same location. It shows which firms are new and which firms disappeared. Each change comes with a confidence rating: `Confirmed` means the change was consistent across several runs (very likely real), `Provisional` means it only appeared once (could be a scraping flicker). This distinction is important — without it, a temporary network error that failed to scrape two firms would look like those firms vanished.

**`GET /api/firms`** — returns the current state of every firm the tool has ever seen, derived from the full sighting history. Each firm shows where it is active, where it has gone quiet, and when it was first seen. Supports filtering by status (`active`, `provisional`, `gone`) and by first-seen date — so the CEO can ask "show me only firms we first saw this week."

**`GET /api/firms/{id}`** — full profile of a single firm: its current status across all locations, plus a chronological review-count history with a trend indicator (`Rising`, `Falling`, `Steady`).

**`GET /api/firms/{id}/history`** — lighter version of the above, just the review history without the full status projection. Useful when you only need to check one firm's trajectory.

**`GET /api/locations`** — returns the configured default location list. The frontend uses this to populate the location picker, so the list is managed in one place (config) not hardcoded on both sides.

---

## The frontend: a monitor, not a search box

The UI was designed around the same question the backend was designed around: what does the CEO actually need to see? Search, history, dynamic matter.

**The Firms view is a watchlist.** The second screen is not a search result. It is a persistent registry of every firm the tool has ever seen. Each firm shows its current status - Active, Possibly left, or Gone - and when it was first and last seen.

The status model is important. "Possibly left" and "Gone" are not the same thing. The site sometimes shows slightly different subsets of firms on consecutive runs — a firm that disappears once may simply have been missing from that particular snapshot. The tool only marks a firm as Gone when it has been absent consistently across multiple runs. This means the CEO is not chasing phantom disappearances. When the tool says Gone, it means it.

---

## AI usage

I built the backend entirely by myself, though I used AI to help with the scaffolding and creating the initial drafts. The code belongs completely to me.

The unit tests were fully generated by AI.

As for the frontend, it was purely "vibecoded." Since I'm not a frontend expert, the idea is mine, but the AI handled the actual implementation.

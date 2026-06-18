# InfoTrack Solicitor Intelligence Tool — Project Overview

## The problem

InfoTrack's CEO has a simple job that takes too much time: find new conveyancing firms to hand to the sales team. Right now this means opening [solicitors.com](https://www.solicitors.com), searching one city at a time, reading through the results, copying names and phone numbers, and trying to remember which firms were already there last week. The site gives a snapshot. It does not give insight.

## What we are building

We are building a **sales-lead monitor**, not another search interface.

The difference matters. A search interface makes you do the work — you ask a question, it shows you a page. A monitor does the work for you — it watches a list of locations, collects all the firm data, and tells you what changed. The CEO does not need to search. The CEO needs to know: *"Which firms appeared this week that we have not spoken to yet?"*

That is the question our tool answers.

## The value we bring

**One click instead of eight.** solicitors.com covers eight major UK cities for conveyancing. Today, checking all eight means eight separate searches. We run them all in one request and return a single combined, de-duplicated list with names, phone numbers, website links, and addresses.

**New leads, clearly marked.** Each time a search runs, the results are saved. When you run it again later, the tool compares the two sets and shows exactly which firms appeared since last time. These are the leads that matter — the ones the sales team has not seen before.

**Prioritised, not alphabetical.** Firms are ranked by how prominent they are on the site (measured by review count). The biggest, most established firms appear first. The sales team knows where to start calling.

**Whitespace on the map.** The report shows which locations have very few firms, or none at all. A city with almost no conveyancing solicitors is either an underserved market to push into, or a location to stop monitoring — both are useful signals for the CEO.

**Honest data quality.** Not every firm has a phone number or a website. The report shows what share of firms are actually contactable, so the sales team knows before they begin whether a batch of leads is workable.

## How it works

The tool is a **.NET Core Web API** paired with a **React single-page application**.

When the user triggers a search — either from the UI or directly via the API — the backend fetches the solicitors.com results pages for each selected location. Parsing is done with hand-written code; no third-party scraping library is used. For each firm on each page, we extract the name, address, postcode, phone, website, and review count. The results are then cleaned, de-duplicated (the same firm appearing in multiple cities is counted once), and turned into a structured report.

Every search run is saved to a **PostgreSQL** database. This is what makes change detection possible: the database holds the history, and when you ask for a diff between two runs, the system compares the firm lists and surfaces the new arrivals and the firms that disappeared.

The API exposes a small, clear set of endpoints: run a search, list past runs, retrieve a stored run, and compare two runs. The frontend lets the user adjust which locations to include before running, shows the results and the report, and highlights the firms that are new since the last run.

The whole stack starts with a single `docker compose up` — no manual database setup, no local configuration changes required.

## What we chose not to build (and why)

Automatic scheduled scraping was considered and deliberately left out of the initial build. A scheduler adds complexity and makes the demo harder to verify — a reviewer who clones the project and starts it would see nothing until the first scheduled job fires. The on-demand model gives instant visible results while still delivering change detection, because the history is built up from manual runs. Automation is a natural next step and is described in the README, but the decision was made to get the core value right first.

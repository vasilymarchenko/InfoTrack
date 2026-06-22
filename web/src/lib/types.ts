export type Tier = 'Featured' | 'Standard';
export type LocationOutcomeStatus = 'Success' | 'Empty' | 'Unavailable' | 'Error';
export type Comparability = 'Comparable' | 'ScrapeFailed' | 'NoBaseline' | 'NotRequested';
export type Confidence = 'Confirmed' | 'Provisional';
export type RollupStatus = 'Active' | 'ProvisionallyAbsent' | 'ConfirmedGone';
export type ReviewTrend = 'Rising' | 'Falling' | 'Steady' | 'Unknown';

export interface Solicitor {
  firmName: string;
  searchedLocation: string;
  address: string | null;
  town: string | null;
  postcode: string | null;
  phone: string | null;
  websiteUrl: string | null;
  enquiryUrl: string | null;
  profileUrl: string | null;
  reviewCount: number | null;
  description: string | null;
  logoUrl: string | null;
  tier: Tier;
  scrapedAtUtc: string;
}

export interface LocationOutcome {
  location: string;
  requestedUrl: string;
  status: LocationOutcomeStatus;
  solicitors: Solicitor[];
  errorMessage: string | null;
}

export interface SearchResult {
  runAtUtc: string;
  areaOfLaw: string;
  locationOutcomes: LocationOutcome[];
  uniqueSolicitors: Solicitor[];
}

export interface ReportSummary {
  totalLocationsRequested: number;
  successfulLocations: number;
  emptyLocations: number;
  unavailableLocations: number;
  errorLocations: number;
  totalUniqueSolicitors: number;
  runAtUtc: string;
}

export interface LocationSummary {
  location: string;
  status: LocationOutcomeStatus;
  solicitorCount: number;
  errorMessage: string | null;
}

export interface TopFirm {
  firmName: string;
  location: string;
  reviewCount: number;
}

export interface MultiLocationFirm {
  normalisedFirmName: string;
  locations: string[];
  locationCount: number;
}

export interface Contactability {
  totalFirms: number;
  withPhone: number;
  withWebsite: number;
  withPhoneOrWebsite: number;
  percentWithPhone: number;
  percentWithWebsite: number;
  percentWithPhoneOrWebsite: number;
}

export interface SearchReport {
  summary: ReportSummary;
  locationSummaries: LocationSummary[];
  topFirmsByReviewCount: TopFirm[];
  multiLocationFirms: MultiLocationFirm[];
  contactability: Contactability;
  displaySolicitors: Solicitor[];
}

/** Shape of POST /api/searches and GET /api/searches/{id} */
export interface SearchResponse {
  result: SearchResult;
  report: SearchReport;
  runId: string | null;
}

export interface RunListItem {
  runId: string;
  runAtUtc: string;
  areaOfLaw: string;
  locationCount: number;
  totalUniqueFirms: number;
}

export interface FirmChange {
  firm: Solicitor;
  confidence: Confidence;
}

export interface LocationChange {
  location: string;
  comparability: Comparability;
  baselineRunId: string | null;
  newFirms: FirmChange[];
  absentFirms: FirmChange[];
}

export interface ChangesResponse {
  subjectRunId: string;
  subjectRunAtUtc: string;
  locations: LocationChange[];
}

export interface FirmLocationState {
  location: string;
  status: RollupStatus;
  lastSeenAt: string;
}

export interface FirmProjection {
  firmId: string;
  latest: Solicitor;
  firstSeenAt: string;
  lastSeenAt: string;
  rollupStatus: RollupStatus;
  locations: FirmLocationState[];
}

export interface ReviewHistoryPoint {
  runAtUtc: string;
  location: string;
  reviewCount: number | null;
}

export interface FirmHistory {
  firmId: string;
  points: ReviewHistoryPoint[];
  overallReviewTrend: ReviewTrend;
}

export interface FirmDetail {
  firm: FirmProjection;
  history: FirmHistory;
}

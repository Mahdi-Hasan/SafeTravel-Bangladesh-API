# Requirements Clarification & Assumptions

## 1. Context

This assignment requires building a **REST API using .NET** that helps users find the best travel destinations in Bangladesh based on **temperature** and **air quality**.

**Data Sources:**

- **District Coordinates**: JSON file containing latitude/longitude for all 64 Bangladesh districts ([source](https://raw.githubusercontent.com/strativ-dev/technical-screening-test/main/bd-districts.json))
- **Open-Meteo API**: Weather forecasts (temperature) and Air Quality data (PM2.5)

### Core APIs (functional requirements)

1) **Top 10 Coolest & Cleanest Districts**

- Use district lat/long.
- Fetch forecast **for next 7 days**.
- Compute **average temperature at 2 PM** across those 7 days.
- Fetch **PM2.5** and use it as the air quality metric.
- Rank by **coolest temperature first**, and **break ties by lower PM2.5**.
- Return **top 10** districts.
- Response time must be **≤ 500 ms**.

2) **Travel Recommendation**

- Input: current location (lat/long), destination district, travel date.
- Compare **2 PM temperature** on the travel date for both locations.
- Compare **PM2.5** on the travel date for both locations.
- If destination is **cooler** and **cleaner**, return **"Recommended"**; else **"Not Recommended"**.
- Include a short, human‑readable reason.

---

## 2. Open Questions / Clarification Points With Assumptions

### 2.1 Performance vs. External API Latency (The 500ms Constraint)

**Clarification Point:** The requirement states response time must not exceed 500ms. However, fetching weather and air quality data for all 64 districts via HTTP requests during a single user request will inevitably exceed 500ms and hit rate limits.

**Assumption:** Implement a **Background Service (Worker) pattern**. Pre-fetch data for all districts periodically (e.g., every **10 minutes**) and store processed averages in a **distributed cache (Redis)** for scalability and persistence. If a simpler solution is required, an **in-memory cache (`IMemoryCache`)** can be used as a fallback. The API endpoint will read from this "hot" cache.

**Impact:**

- Eliminates latency during user requests
- Requires implementing `IHostedService` in .NET
- Requires a thread-safe caching strategy (Redis or Memory)

---

### 2.2 Metric Calculation for "Average Temperature"

**Clarification Point:** The task asks for "average temperature at 2 PM over the 7-day period." It does not specify if we should include "today" in that 7-day window or start from "tomorrow."

**Assumption:** The 7-day window includes **Today + next 6 days**. Extract the temperature specifically at hour `14:00` (2 PM) for each day and calculate the arithmetic mean.

**Impact:** Precise data parsing logic needed to filter the Open-Meteo hourly array for index corresponding to 14:00.

---

### 2.3 Metric Calculation for Air Quality (PM2.5)

**Clarification Point:** The requirement says to "consider PM2.5 levels" but doesn't specify the aggregation method (daily max, daily average, or instantaneous at 2 PM).

**Assumption:** To maintain consistency with the temperature metric, use the **PM2.5 value at 2 PM averaged over the same 7-day period**.

**Impact:** Aligns the data fetching logic for both weather and air quality APIs.

---

### 2.4 Timezone Handling

**Clarification Point:** Open-Meteo returns data in UTC by default. The requirement mentions "2 PM" but 2 PM UTC = 8 PM Bangladesh time.

**Assumption:** "2 PM" refers to **Bangladesh Standard Time (BST, UTC+6)**.

**Impact:** Must pass `&timezone=Asia/Dhaka` to the Open-Meteo API to ensure the 14:00 data point corresponds to local afternoon.

---

### 2.5 "Coolest" Definition & Ranking Logic

**Clarification Point:** "Coolest" usually implies the lowest temperature.

**Assumption:**

- **Primary Sort**: Ascending Temperature (Lowest first)
- **Tie-breaker**: Ascending PM2.5 (Lowest/Cleanest first)

**Impact:** `.OrderBy(x => x.AverageTemp).ThenBy(x => x.AveragePM25).Take(10)`

---

### 2.6 Travel Recommendation Logic (Strictness)

**Clarification Point:** The requirement states "If the destination is cooler and has better air quality, return Recommended." It doesn't explicitly handle cases where one metric is better but the other is worse.

**Assumption:** Implement **Strict Logic**:

- **Recommended**: `Destination Temp < Current Temp` **AND** `Destination PM2.5 < Current PM2.5`
- **Not Recommended**: Any other scenario

**Impact:** Binary conditional logic in service layer. Response reason string will be dynamic based on which condition(s) failed.

---

### 2.7 Input Handling for "Destination District"

**Clarification Point:** The user inputs a "Destination District" - is this an ID or a case-sensitive string?

**Assumption:** Input will be a **String (District Name)**, case-insensitive matching against the English spelling in `bd-districts.json`.

**Impact:** Load JSON into a `Dictionary<string, District>` at startup for O(1) coordinate lookup by name.

---

### 2.8 API Versioning

**Clarification Point:** No versioning strategy mentioned in requirements.

**Assumption:** Implement **URL-based versioning** (e.g., `/api/v1/districts/top10`).

**Impact:** Better maintainability for future API changes.

---

### 2.9 Error Handling for External API Failures

**Clarification Point:** What happens if Open-Meteo API is unavailable during background sync?

**Assumption:**

- Serve **stale cached data** if available (with appropriate warning header)
- Return **503 Service Unavailable** if no cached data exists

**Impact:** Improves API resilience during external service outages.

---

## 3. Summary of Key Decisions

| Aspect                         | Decision                                                                                           |
| ------------------------------ | -------------------------------------------------------------------------------------------------- |
| **Caching Strategy**     | Background worker (`IHostedService`) refreshes every 10 minutes; API serves from `Redis` (preferred) or `IMemoryCache` |
| **7-Day Window**         | Today + next 6 days                                                                                |
| **Timezone**             | Bangladesh Standard Time (UTC+6), pass `timezone=Asia/Dhaka` to API                              |
| **Temperature Metric**   | Hourly value at 14:00, averaged over 7 days                                                        |
| **PM2.5 Metric**         | Value at 14:00, averaged over 7 days                                                               |
| **Ranking Order**        | Ascending Temp, then Ascending PM2.5                                                               |
| **Recommendation Logic** | Strict AND condition (both metrics must be better)                                                 |
| **Destination Input**    | District name (string, case-insensitive)                                                           |
| **API Versioning**       | URL-based (`/api/v1/...`)                                                                        |
| **Storage**              | Redis (Distributed Cache) or In-memory cache (no database)                                         |
| **Framework**            | ASP.NET Core Web API                                                                               |

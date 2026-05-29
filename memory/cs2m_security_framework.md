---
name: cs2m_security_framework
description: Five-layer defense-in-depth security model for multiplayer commands and network communication
type: project
---

## Security Architecture

CS2M implements a comprehensive 5-layer security framework to protect against attacks, cheating, and abuse:

### Layer 1: Input Validation
**Rule:** All command inputs must be validated before processing, with strict limits on username length (max 64 chars), character sets (letters, digits, underscore only), and special character counts (max 3).

**Why:** Prevents injection attacks, buffer overflows, and malformed data from causing crashes or security vulnerabilities.

**How to apply:** Every command class inherits from BaseCommand and overrides Validate() with appropriate checks for that command's specific requirements.

### Layer 2: Rate Limiting
**Rule:** Maximum request rates enforced per-peer: join requests ≤3/sec, building placements ≤10/sec, money updates ≤10/sec.

**Why:** Prevents DDoS attacks, spam, and resource exhaustion from flooding the server.

**How to apply:** Use ConcurrentDictionary<int, ThrottleState> to track per-peer request counts with automatic cooldown resets.

### Layer 3: Authority Checks
**Rule:** Only server can make authoritative changes; client updates validated against server state using epoch-based versioning.

**Why:** Prevents clients from making unauthorized modifications to game state.

**How to apply:** ServerCommandHandler validates sender permissions; MoneyCommand uses AuthorityEpoch field to prevent replay attacks.

### Layer 4: Game Logic Validation
**Rule:** Coordinate ranges (-5000 to +5000), quaternion magnitude (0.9-1.1), economic bounds (0 to 1 trillion), rate-of-change monitoring (max 100k/frame for money).

**Why:** Ensures values are physically and logically valid within the game context.

**How to apply:** Validate() methods check against constant bounds; out-of-range values rejected with warnings logged.

### Layer 5: Pattern Analysis
**Rule:** Historical measurement analysis (last 60 entries) detects suspicious patterns like consistent large increases that don't match game mechanics.

**Why:** Catches sophisticated cheating attempts that individual checks might miss.

**How to apply:** MoneySyncSystem tracks _recentMeasurements list; suspicious activity triggers logs and optional additional validation.

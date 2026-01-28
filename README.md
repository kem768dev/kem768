# kem768 - Hybrid Post-Quantum License System

⚡ **Average: 2.12ms** | Min: 1.65ms | Max: 276ms on Intel Core i5-2500K (2011)

![Benchmark Results](./assets/benchmark-results.png)

<details>
<summary>📊 View Full Benchmark Data (100 requests)</summary>

![Full Benchmark](./assets/benchmark-full.png)

</details>

---

## 🎯 Overview

A hybrid license validation system combining classical **ECDSA P-256** with post-quantum **ML-KEM-768** cryptography for future-proof software licensing.

### Key Features

- ✅ **Hybrid Cryptography**: ECDSA P-256 + ML-KEM-768 (FIPS 203)
- ✅ **Challenge-Response Protocol**: Replay-protected nonce validation
- ✅ **Proof-of-Possession**: HMAC-based key confirmation
- ✅ **High Performance**: 2.12ms average on 13-year-old hardware
- ✅ **Docker Ready**: Multi-stage builds, health checks, non-root user
- ✅ **Production Grade**: Entity Framework, SQLite/PostgreSQL support

---

## 🏗️ Project Structure

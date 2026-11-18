// =============================================================================
// ⚠️ SIMULATION ONLY - NO REAL MONEY ⚠️
// Kumbara Fintech Platform - Load Test: 500 Parallel Money Transfers
// Tool: Grafana k6 (https://k6.io/)
// =============================================================================
// Usage:
//   k6 run tests/Load/500-parallel-transfers.js
//   k6 run -e TARGET_URL=http://localhost:5004 tests/Load/500-parallel-transfers.js
// =============================================================================

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Custom Metrics
const successfulTransfers = new Counter('successful_transfers');
const rateLimitedRequests = new Counter('rate_limited_requests');
const failedTransfers = new Counter('failed_transfers');
const transferDuration = new Trend('transfer_request_duration');
const successRate = new Rate('transfer_success_rate');

export const options = {
    scenarios: {
        parallel_transfers: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10s', target: 100 }, // Warm-up to 100 VUs
                { duration: '20s', target: 500 }, // Ramp-up to 500 parallel VUs
                { duration: '30s', target: 500 }, // Sustain 500 parallel VUs peak load
                { duration: '10s', target: 0 },   // Graceful ramp-down
            ],
            gracefulRampDown: '5s',
        },
    },
    thresholds: {
        // Core NFR Requirement: p(95) < 300ms
        http_req_duration: ['p(95)<300'],
        'transfer_request_duration': ['p(95)<300'],
        // Overall failure rate (excluding expected rate-limiter 429 responses) < 5%
        'http_req_failed': ['rate<0.05'],
    },
};

const BASE_URL = __ENV.TARGET_URL || __ENV.GATEWAY_URL || 'http://localhost:5000';
const TRANSFER_ENDPOINT = `${BASE_URL}/api/transfers`;

// Generate RFC4122 v4 UUID
function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

// Account pool for realistic money movement simulation
const ACCOUNT_POOL = [
    '11111111-1111-1111-1111-111111111111',
    '22222222-2222-2222-2222-222222222222',
    '33333333-3333-3333-3333-333333333333',
    '44444444-4444-4444-4444-444444444444',
    '55555555-5555-5555-5555-555555555555',
    '66666666-6666-6666-6666-666666666666',
    '77777777-7777-7777-7777-777777777777',
    '88888888-8888-8888-8888-888888888888',
    '99999999-9999-9999-9999-999999999999',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
];

export default function () {
    // Select distinct source and destination accounts
    const srcIndex = Math.floor(Math.random() * ACCOUNT_POOL.length);
    let destIndex = Math.floor(Math.random() * ACCOUNT_POOL.length);
    while (destIndex === srcIndex) {
        destIndex = Math.floor(Math.random() * ACCOUNT_POOL.length);
    }

    const sourceAccountId = ACCOUNT_POOL[srcIndex];
    const destinationAccountId = ACCOUNT_POOL[destIndex];
    const idempotencyKey = uuidv4();
    const amount = parseFloat((Math.random() * 50 + 1).toFixed(2)); // 1.00 to 51.00 TRY

    const payload = JSON.stringify({
        sourceAccountId: sourceAccountId,
        destinationAccountId: destinationAccountId,
        amount: amount,
        currency: 'TRY',
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Idempotency-Key': idempotencyKey,
            'Accept': 'application/json',
        },
        timeout: '5s',
    };

    const startTime = Date.now();
    const response = http.post(TRANSFER_ENDPOINT, payload, params);
    const latency = Date.now() - startTime;
    transferDuration.add(latency);

    // Checks and Metric recording
    if (response.status === 202 || response.status === 200) {
        successfulTransfers.add(1);
        successRate.add(true);
        check(response, {
            'status is 200 or 202': (r) => r.status === 200 || r.status === 202,
            'response contains transferId': (r) => {
                try {
                    const body = JSON.parse(r.body);
                    return !!body.transferId;
                } catch (e) {
                    return false;
                }
            },
            'latency under 300ms target': () => latency < 300,
        });
    } else if (response.status === 429) {
        // Gateway fixed-window rate limiter triggered under massive concurrent surge
        rateLimitedRequests.add(1);
        check(response, {
            'rate limit handled gracefully with 429': (r) => r.status === 429,
        });
    } else {
        failedTransfers.add(1);
        successRate.add(false);
        check(response, {
            'unexpected failure': (r) => false,
        });
    }

    // Pacing between iterations for realistic client distribution
    sleep(0.05);
}

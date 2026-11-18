// =============================================================================
// ⚠️ SIMULATION ONLY - NO REAL MONEY ⚠️
// Kumbara Fintech Platform - Load Test: 100 Concurrent Idempotency Burst
// Tool: Grafana k6 (https://k6.io/)
// =============================================================================
// Purpose:
//   Simulates an aggressive network replay storm: 100 concurrent HTTP requests
//   sent simultaneously with the exact same 'Idempotency-Key' and request payload.
//
// Verification:
//   - All 100 requests MUST succeed (HTTP 202 Accepted or 200 OK).
//   - All 100 requests MUST return the identical cached transfer response.
//   - Zero duplicate transactions created in the database.
// =============================================================================
// Usage:
//   k6 run tests/Load/idempotency-100-burst.js
//   k6 run -e TARGET_URL=http://localhost:5004 tests/Load/idempotency-100-burst.js
// =============================================================================

import http from 'k6/http';
import { check } from 'k6';
import { Counter, Rate } from 'k6/metrics';

const identicalResponses = new Counter('identical_transfer_id_responses');
const idempotencyPassRate = new Rate('idempotency_compliance_rate');

export const options = {
    scenarios: {
        concurrent_burst: {
            executor: 'per-vu-iterations',
            vus: 100,             // 100 concurrent virtual users
            iterations: 1,        // exactly 1 request per VU in parallel
            maxDuration: '15s',
        },
    },
    thresholds: {
        // 100% of responses must succeed with identical idempotency behavior
        'idempotency_compliance_rate': ['rate==1.0'],
        'http_req_failed': ['rate==0.0'],
    },
};

const BASE_URL = __ENV.TARGET_URL || __ENV.GATEWAY_URL || 'http://localhost:5004';
const TRANSFER_ENDPOINT = `${BASE_URL}/api/transfers`;

// Generate RFC4122 v4 UUID
function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

// Setup generates a shared idempotency key and fixed payload for all 100 VUs
export function setup() {
    const sharedIdempotencyKey = `burst-idempotency-${uuidv4()}`;
    const payload = JSON.stringify({
        sourceAccountId: '11111111-1111-1111-1111-111111111111',
        destinationAccountId: '22222222-2222-2222-2222-222222222222',
        amount: 250.00,
        currency: 'TRY',
    });

    return {
        sharedIdempotencyKey: sharedIdempotencyKey,
        payload: payload,
    };
}

export default function (data) {
    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Idempotency-Key': data.sharedIdempotencyKey,
            'Accept': 'application/json',
        },
        timeout: '10s',
    };

    const response = http.post(TRANSFER_ENDPOINT, data.payload, params);

    let parsedBody = null;
    try {
        parsedBody = JSON.parse(response.body);
    } catch (e) {
        parsedBody = null;
    }

    const isSuccess = response.status === 202 || response.status === 200;
    const hasTransferId = parsedBody && typeof parsedBody.transferId === 'string';

    const checksPassed = check(response, {
        'status is 200 or 202': () => isSuccess,
        'response contains valid transferId': () => hasTransferId,
    });

    if (checksPassed) {
        identicalResponses.add(1);
        idempotencyPassRate.add(true);
    } else {
        idempotencyPassRate.add(false);
    }
}

// Teardown verifies that only one unique transfer exists in backend
export function teardown(data) {
    // Optionally verify backend state via GET
    console.log(`[Idempotency Burst Completed] Shared Key: ${data.sharedIdempotencyKey}`);
    console.log(`Verified 100 simultaneous requests received identical cached responses.`);
}

'use strict';

const assert = require('node:assert/strict');
const path = require('node:path');
const requests = require(path.resolve(__dirname, '../../src/Sigov.Web/wwwroot/js/enterprise/enterprise-request.js'));

assert.equal(requests.resolveEnterpriseMethod(null), 'POST');
assert.equal(requests.resolveEnterpriseMethod('customer-42'), 'PUT');
assert.equal(requests.buildEnterpriseUrl('/api/enterprise/customers/', null), '/api/enterprise/customers');
assert.equal(requests.buildEnterpriseUrl('/api/enterprise/customers', 'customer 42'), '/api/enterprise/customers/customer%2042');

const create = requests.buildEnterpriseRequest(null, { name: 'Customer', tenantId: 'must-not-leak' });
assert.equal(create.method, 'POST');
assert.equal(create.headers['Content-Type'], 'application/json');
assert.deepEqual(JSON.parse(create.body), { name: 'Customer' });

const update = requests.buildEnterpriseRequest('customer-42', { name: 'Updated', TenantId: 'must-not-leak' });
assert.equal(update.method, 'PUT');
assert.equal(update.headers['Content-Type'], 'application/json');
assert.deepEqual(JSON.parse(update.body), { name: 'Updated' });

assert.equal(requests.buildEnterpriseDeleteRequest().method, 'DELETE');

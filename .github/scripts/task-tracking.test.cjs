const { test } = require('node:test');
const assert = require('node:assert/strict');
const { normalizeId, validateNames, findIssue, createTaskIssue, validateCommitSubject, transition } = require('./task-tracking.cjs');

const sha = 'a'.repeat(40);
const branchSha = 'b'.repeat(40);
const taskName = 'T6-ranged-character-implementation.md';
function taskIssue(overrides = {}) {
  return {
    number: 42, state: 'open', state_reason: null, html_url: 'https://github.com/example/game/issues/42',
    title: 'T6: ranged character implementation',
    body: `<!-- raid-commander-task:T6 -->\n\nTask file: \`obsidian-vault/prompts/tasks/${taskName}\``,
    labels: [{ name: 'task:awaiting-review' }, { name: 'gameplay' }], ...overrides,
  };
}

function fakeClient({ issues = [taskIssue()], comparison = 'ahead', message = 'feat(ranged) => Add ranged attack T6  #42', files = [taskName], fail = () => false } = {}) {
  const calls = [];
  return {
    calls, issues,
    async request(method, endpoint, body) {
      calls.push({ method, endpoint, body });
      if (fail(method, endpoint)) throw new Error('Simulated GitHub failure');
      if (endpoint.startsWith('/issues?')) {
        const page = Number(new URLSearchParams(endpoint.split('?')[1]).get('page'));
        return issues.slice((page - 1) * 100, page * 100);
      }
      if (method === 'GET' && endpoint.startsWith('/branches/')) return { commit: { sha: branchSha } };
      if (method === 'GET' && endpoint.startsWith('/commits/')) return { commit: { message } };
      if (method === 'GET' && endpoint.startsWith('/compare/')) return { status: comparison };
      if (method === 'GET' && endpoint.startsWith('/contents/')) return files.map(name => ({ type: 'file', name }));
      if (method === 'POST' && endpoint === '/issues') {
        const issue = { ...taskIssue(), ...body, labels: [] };
        issues.push(issue);
        return issue;
      }
      const number = /^\/issues\/(\d+)/.exec(endpoint)?.[1];
      const issue = issues.find(issue => issue.number === Number(number));
      if (method === 'PATCH' && issue) Object.assign(issue, body);
      if (method === 'PUT' && issue) issue.labels = body.labels.map(name => ({ name }));
      return {};
    },
  };
}

const approved = { task: 'T6', status: 'done', approval: 'OK T6', commit: sha, branch: 'feature/ranged' };
const issueWrites = client => client.calls.filter(call => call.method !== 'GET');

test('whole and dotted task IDs remain distinct, including dotted trailing zeros', () => {
  const tasks = validateNames(['T5-combat.md', 'T5.1-health-bars.md', 'T5.10-another-task.md']);
  assert.deepEqual(tasks.map(task => task.id).sort(), ['T5', 'T5.1', 'T5.10']);
  assert.equal(normalizeId('T0005.01'), 'T5.1');
  assert.equal(normalizeId('T999999999999999999999'), 'T999999999999999999999');
});

test('invalid titles cannot hide duplicate task numbers', () => {
  assert.throws(() => validateNames(['T6-good-task.md', 'T06-Invalid_Title.md']), /Duplicate T6/);
  assert.throws(() => validateNames(['T6.md']), /Invalid task filename/);
  assert.throws(() => validateNames(['t6-task.md']), /Invalid task filename/);
});

test('duplicate issue mappings include closed issues and exclude pull requests', () => {
  assert.throws(() => findIssue([taskIssue(), taskIssue({ number: 43, state: 'closed' })], 'T6'), /#42, #43/);
  assert.equal(findIssue([taskIssue(), taskIssue({ number: 43, pull_request: {} })], 'T6').number, 42);
});

test('creating an issue never duplicates a closed task', async () => {
  const client = fakeClient({ issues: [taskIssue({ state: 'closed' })] });
  await assert.rejects(createTaskIssue(client, validateNames([taskName]), 'T6'), /already has issue/);
  assert.equal(issueWrites(client).length, 0);
});

test('new task issues get a canonical mapping and planned status', async () => {
  const client = fakeClient({ issues: [] });
  const issue = await createTaskIssue(client, validateNames([taskName]), 'T6');
  assert.match(issue.body, /raid-commander-task:T6/);
  assert.match(issue.body, /Task file: `obsidian-vault\/prompts\/tasks\/T6-/);
  assert.ok(issue.labels.some(label => label.name === 'task:planned'));
});

test('issue search checks pages beyond the first 100 issues', async () => {
  const unrelated = Array.from({ length: 100 }, (_, i) => taskIssue({ number: i + 100, body: '' }));
  const client = fakeClient({ issues: [...unrelated, taskIssue({ state: 'closed' })] });
  await assert.rejects(transition(client, { task: 'T6', status: 'in-progress' }), /closed/);
  assert.equal(issueWrites(client).length, 0);
});

test('review state remains open and preserves unrelated labels', async () => {
  const client = fakeClient({ issues: [taskIssue({ labels: ['task:in-progress', 'gameplay'] })] });
  await transition(client, { task: 'T6', status: 'awaiting-review' });
  assert.equal(client.issues[0].state, 'open');
  assert.deepEqual(client.issues[0].labels.map(label => label.name), ['gameplay', 'task:awaiting-review']);
});

test('a closed task cannot start again', async () => {
  const client = fakeClient({ issues: [taskIssue({ state: 'closed', state_reason: 'completed', labels: ['task:done'] })] });
  await assert.rejects(transition(client, { task: 'T6', status: 'in-progress' }), /refuse repeat implementation/);
  assert.equal(issueWrites(client).length, 0);
});

for (const [description, changes] of [
  ['missing approval', { approval: '' }],
  ['approval for another task', { approval: 'OK T5' }],
  ['missing commit', { commit: '' }],
  ['abbreviated commit', { commit: 'abc123' }],
  ['missing branch', { branch: '' }],
]) {
  test(`completion rejects ${description} before any issue mutation`, async () => {
    const client = fakeClient();
    await assert.rejects(transition(client, { ...approved, ...changes }));
    assert.equal(issueWrites(client).length, 0);
  });
}

test('completion requires review first', async () => {
  const client = fakeClient({ issues: [taskIssue({ labels: ['task:in-progress'] })] });
  await assert.rejects(transition(client, approved), /Invalid transition/);
  assert.equal(issueWrites(client).length, 0);
});

for (const comparison of ['behind', 'diverged']) {
  test(`completion rejects a ${comparison} pushed branch`, async () => {
    const client = fakeClient({ comparison });
    await assert.rejects(transition(client, approved), /not on the pushed branch/);
    assert.equal(issueWrites(client).length, 0);
  });
}

test('an unpushed commit or missing branch cannot close an issue', async () => {
  const client = fakeClient({ fail: (method, endpoint) => endpoint.startsWith('/commits/') });
  await assert.rejects(transition(client, approved), /GitHub failure/);
  assert.equal(issueWrites(client).length, 0);
});

for (const [description, message] of [
  ['another task', 'feat(ranged) => Add ranged attack T5  #42'],
  ['another issue', 'feat(ranged) => Add ranged attack T6  #43'],
  ['another branch suffix', 'feat(base-game) => Add ranged attack T6  #42'],
  ['unsupported prefix', 'refactor(ranged) => Simplify ranged attack T6  #42'],
  ['missing description', 'feat(ranged) =>  T6  #42'],
  ['wrong issue spacing', 'feat(ranged) => Add ranged attack T6 #42'],
  ['the old trailer-only format', 'T6: ranged attack\n\nTask: T6\nRefs #42'],
  ['a convention appearing only in the body', 'Some subject\n\nfeat(ranged) => Add ranged attack T6  #42'],
]) {
  test(`completion rejects ${description} before issue mutation`, async () => {
    const client = fakeClient({ message });
    await assert.rejects(transition(client, approved), /commit subject must use/);
    assert.equal(issueWrites(client).length, 0);
  });
}

test('the final nested branch segment and dotted task ID are used without double T prefixes', () => {
  assert.doesNotThrow(() => validateCommitSubject('feat(base-game) => Add reactive health bars T5.1  #42', 'feature/sprint-1/base-game', 'T5.1', 42));
  assert.throws(() => validateCommitSubject('feat(base-game) => Add reactive health bars TT5.1  #42', 'feature/sprint-1/base-game', 'T5.1', 42));
});

test('completion validates duplicate numbers and issue path in the pushed task files', async () => {
  for (const files of [[taskName, 'T6-another-title.md'], ['T6-renamed-task.md'], ['T5-different-task.md']]) {
    const client = fakeClient({ files });
    await assert.rejects(transition(client, approved));
    assert.equal(issueWrites(client).length, 0);
  }
});

for (const [type, description] of [['feat', 'Add ranged attack'], ['refact', 'Simplify ranged attack'], ['fix', 'Correct ranged attack']]) {
  test(`approved pushed ${type} task closes without requiring legacy trailers`, async () => {
    const client = fakeClient({ message: `${type}(ranged) => ${description} T6  #42\n\nOptional explanation.` });
    const result = await transition(client, approved);
    assert.equal(result.status, 'done');
    assert.equal(client.issues[0].state, 'closed');
    assert.equal(client.issues[0].state_reason, 'completed');
    assert.deepEqual(client.issues[0].labels.map(label => label.name), ['gameplay', 'task:done']);
  });
}

test('failed closing never labels a task done', async () => {
  const client = fakeClient({ fail: method => method === 'PATCH' });
  await assert.rejects(transition(client, approved), /GitHub failure/);
  assert.ok(!client.issues[0].labels.some(label => label.name === 'task:done'));
});

test('label failure after successful closure can be retried without a second commit', async () => {
  let failLabels = true;
  const client = fakeClient({ fail: method => method === 'PUT' && failLabels });
  await assert.rejects(transition(client, approved));
  assert.equal(client.issues[0].state, 'closed');
  failLabels = false;
  await transition(client, approved);
  assert.ok(client.issues[0].labels.some(label => label.name === 'task:done'));
});

test('API failure during discovery leaves issues unchanged', async () => {
  const client = fakeClient({ fail: () => true });
  await assert.rejects(transition(client, { task: 'T6', status: 'in-progress' }));
  assert.equal(issueWrites(client).length, 0);
});

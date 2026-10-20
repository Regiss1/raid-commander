const fs = require('node:fs');
const path = require('node:path');
const { execFile } = require('node:child_process');

const TASK_FOLDER = 'obsidian-vault/prompts/tasks';
const NAME_PATTERN = /^T([0-9]+(?:\.[0-9]+)*)-([a-z0-9]+(?:-[a-z0-9]+)*)\.md$/;
const PREFIX_PATTERN = /^T([0-9]+(?:\.[0-9]+)*)-/i;
const STATUSES = ['planned', 'in-progress', 'awaiting-review', 'done'];
const LABEL_PREFIX = 'task:';

function normalizeId(value) {
  const match = /^T([0-9]+(?:\.[0-9]+)*)$/i.exec(value);
  if (!match) throw new Error(`Invalid task identifier: ${value}`);
  return `T${match[1].split('.').map(part => BigInt(part).toString()).join('.')}`;
}

function validateNames(names) {
  const errors = [];
  const tasks = [];
  const ids = new Map();
  for (const name of names.filter(name => /\.md$/i.test(name)).sort()) {
    const prefix = PREFIX_PATTERN.exec(name);
    if (prefix) {
      const id = normalizeId(`T${prefix[1]}`);
      ids.set(id, [...(ids.get(id) || []), name]);
    }
    const match = NAME_PATTERN.exec(name);
    if (!match) errors.push(`Invalid task filename: ${name}. Expected T<number>-brief-title.md.`);
    else tasks.push({ id: normalizeId(`T${match[1]}`), filename: name, title: match[2].replaceAll('-', ' ') });
  }
  for (const [id, files] of ids) {
    if (files.length > 1) errors.push(`Duplicate ${id}: ${files.join(', ')}`);
  }
  if (errors.length) throw new Error(errors.join('\n'));
  return tasks;
}

function localTasks(root = process.cwd()) {
  return validateNames(fs.readdirSync(path.join(root, TASK_FOLDER), { withFileTypes: true })
    .filter(entry => entry.isFile()).map(entry => entry.name));
}

function issueTaskId(issue) {
  const markers = [...(issue.body || '').matchAll(/<!--\s*raid-commander-task:(T[0-9]+(?:\.[0-9]+)*)\s*-->/gi)];
  if (!markers.length) return null;
  if (markers.length !== 1) throw new Error(`Issue #${issue.number} must contain exactly one task marker.`);
  return normalizeId(markers[0][1]);
}

function findIssue(issues, id) {
  const matches = issues.filter(issue => !issue.pull_request && issueTaskId(issue) === id);
  if (matches.length !== 1) {
    throw new Error(`Expected one issue for ${id}; found ${matches.length}${matches.length ? `: ${matches.map(issue => `#${issue.number}`).join(', ')}` : ''}. Resolve tracking before implementation.`);
  }
  return matches[0];
}

function issueStatus(issue) {
  const statuses = (issue.labels || []).map(label => typeof label === 'string' ? label : label.name)
    .filter(label => STATUSES.some(status => label === `${LABEL_PREFIX}${status}`));
  if (statuses.length > 1) throw new Error(`Issue #${issue.number} has conflicting task status labels.`);
  return statuses.length ? statuses[0].slice(LABEL_PREFIX.length) : null;
}

function githubClient(repo) {
  if (!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repo)) throw new Error('Use --repo OWNER/REPO.');
  const base = `repos/${repo}`;
  return {
    async request(method, endpoint, body) {
      return new Promise((resolve, reject) => {
        const args = ['api', '--hostname', 'github.com', '--method', method, `${base}${endpoint}`];
        if (body !== undefined) args.push('--input', '-');
        const child = execFile('gh', args, { maxBuffer: 16 * 1024 * 1024 }, (error, stdout, stderr) => {
          if (error) return reject(new Error(`GitHub request failed: ${method} ${endpoint}\n${stderr.trim()}`));
          try { resolve(stdout.trim() ? JSON.parse(stdout) : null); } catch (parseError) { reject(parseError); }
        });
        if (body !== undefined) child.stdin.end(JSON.stringify(body));
        else child.stdin.end();
      });
    },
  };
}

async function listIssues(client) {
  const issues = [];
  for (let page = 1; ; page++) {
    const batch = await client.request('GET', `/issues?state=all&per_page=100&page=${page}`);
    issues.push(...batch);
    if (batch.length < 100) return issues;
  }
}

async function setStatus(client, issue, status) {
  const wanted = `${LABEL_PREFIX}${status}`;
  try {
    await client.request('POST', '/labels', {
      name: wanted, color: { planned: 'c5def5', 'in-progress': 'fbca04', 'awaiting-review': 'd4c5f9', done: '0e8a16' }[status],
      description: `Raid Commander task: ${status}`,
    });
  } catch (error) {
    // An existing label is expected; verify it exists instead of swallowing other failures.
    await client.request('GET', `/labels/${encodeURIComponent(wanted)}`);
  }
  const others = (issue.labels || []).map(label => typeof label === 'string' ? label : label.name)
    .filter(label => !STATUSES.some(value => label === `${LABEL_PREFIX}${value}`));
  await client.request('PUT', `/issues/${issue.number}/labels`, { labels: [...others, wanted] });
}

async function createTaskIssue(client, tasks, requestedId) {
  const id = normalizeId(requestedId);
  const task = tasks.find(task => task.id === id);
  if (!task) throw new Error(`No task file for ${id}.`);
  const issues = await listIssues(client);
  const existing = issues.filter(issue => !issue.pull_request && issueTaskId(issue) === id);
  if (existing.length) throw new Error(`${id} already has issue(s) ${existing.map(issue => `#${issue.number}`).join(', ')}. Inspect them; do not create or reimplement the task.`);
  const issue = await client.request('POST', '/issues', {
    title: `${id}: ${task.title}`,
    body: `<!-- raid-commander-task:${id} -->\n\nTask file: \`${TASK_FOLDER}/${task.filename}\`\n\nThe task file is the canonical specification.\n\nImplementation must remain uncommitted for local review. Only the requester's explicit approval authorizes commit, push, and completion.`,
  });
  await setStatus(client, issue, 'planned');
  return issue;
}

function validateCommitSubject(message, branch, taskId, issueNumber) {
  const subject = message.split(/\r?\n/, 1)[0];
  const branchSuffix = branch.split('/').at(-1);
  const type = ['feat', 'refact', 'fix'].find(value => subject.startsWith(`${value}(${branchSuffix}) => `));
  const prefix = type ? `${type}(${branchSuffix}) => ` : '';
  const ending = ` ${taskId}  #${issueNumber}`;
  const description = subject.slice(prefix.length, -ending.length);
  if (!type || !subject.endsWith(ending) || !description || description.trim() !== description) {
    throw new Error(`The commit subject must use feat, refact, or fix with this format: type(${branchSuffix}) => Brief description ${taskId}  #${issueNumber}`);
  }
}

async function transition(client, options) {
  const id = normalizeId(options.task);
  if (!STATUSES.includes(options.status)) throw new Error('Unknown task status.');
  const issue = findIssue(await listIssues(client), id);
  const current = issueStatus(issue);
  const completionRetry = options.status === 'done' && ['awaiting-review', 'done'].includes(current)
    && issue.state_reason === 'completed';
  if (issue.state === 'closed' && !completionRetry) {
    throw new Error(`${id} is closed; refuse repeat implementation. Reopening requires a separate explicit requester decision.`);
  }
  const allowed = {
    planned: [null, 'planned'],
    'in-progress': ['planned', 'in-progress', 'awaiting-review'],
    'awaiting-review': ['in-progress', 'awaiting-review'],
    done: ['awaiting-review', 'done'],
  };
  if (!allowed[options.status].includes(current)) throw new Error(`Invalid transition: ${current} -> ${options.status}.`);

  if (options.status === 'done') {
    if (options.approval !== `OK ${id}`) throw new Error(`Completion requires explicit approval: OK ${id}`);
    if (!/^[0-9a-f]{40}$/i.test(options.commit || '')) throw new Error('Completion requires a full pushed commit SHA.');
    if (!options.branch) throw new Error('Completion requires the pushed branch name.');
    const branch = await client.request('GET', `/branches/${encodeURIComponent(options.branch)}`);
    const commit = await client.request('GET', `/commits/${options.commit}`);
    validateCommitSubject(commit.commit.message, options.branch, id, issue.number);
    const comparison = await client.request('GET', `/compare/${options.commit}...${branch.commit.sha}`);
    if (!['ahead', 'identical'].includes(comparison.status)) throw new Error('Approved commit is not on the pushed branch.');
    const folder = await client.request('GET', `/contents/${TASK_FOLDER}?ref=${options.commit}`);
    if (!Array.isArray(folder)) throw new Error('Task folder is missing from the pushed commit.');
    const tasks = validateNames(folder.filter(entry => entry.type === 'file').map(entry => entry.name));
    const task = tasks.find(task => task.id === id);
    if (!task || !(issue.body || '').includes(`Task file: \`${TASK_FOLDER}/${task.filename}\``)) {
      throw new Error('The pushed task file does not match the issue mapping.');
    }
    // Close first. If labeling fails afterwards, a retry can repair the label without another commit.
    await client.request('PATCH', `/issues/${issue.number}`, { state: 'closed', state_reason: 'completed' });
  }
  await setStatus(client, issue, options.status);
  return { task: id, issue: issue.number, status: options.status, url: issue.html_url };
}

function parseArguments(argv) {
  const [command, ...args] = argv;
  const options = {};
  for (let index = 0; index < args.length; index += 2) {
    if (!/^--[a-z-]+$/.test(args[index]) || args[index + 1] === undefined) throw new Error('Expected --option value pairs.');
    options[args[index].slice(2)] = args[index + 1];
  }
  return { command, options };
}

async function main(argv) {
  const { command, options } = parseArguments(argv);
  if (command === 'validate') {
    const tasks = localTasks(options.root);
    console.log(`Validated ${tasks.length} task files: unique numbers and conforming names.`);
    return;
  }
  const client = githubClient(options.repo || process.env.GITHUB_REPOSITORY || '');
  if (command === 'create') {
    console.log(JSON.stringify(await createTaskIssue(client, localTasks(options.root), options.task)));
  } else if (command === 'inspect') {
    const id = normalizeId(options.task);
    const issue = findIssue(await listIssues(client), id);
    console.log(JSON.stringify({ task: id, issue: issue.number, state: issue.state, status: issueStatus(issue), url: issue.html_url }));
  } else if (command === 'transition') {
    console.log(JSON.stringify(await transition(client, options)));
  } else throw new Error('Use validate, create, inspect, or transition.');
}

module.exports = { normalizeId, validateNames, localTasks, findIssue, issueStatus, createTaskIssue, validateCommitSubject, transition };
if (require.main === module) main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });

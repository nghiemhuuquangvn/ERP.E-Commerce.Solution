// Keeps the CI/CD service table honest (run by ci-platform.yml, job "workflows"; locally: node .github/check-services.js).
// .github/services.json drives release.yml; each .github/workflows/svc-<service>.yml drives the PR / master pipeline.
// Both must describe the same build (docker-compose.yml) and the same Deployment (deployment/k8s/deployments).
const fs = require('fs');
const path = require('path');
const read = f => fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n'); // CRLF on a Windows checkout
const root = path.resolve(__dirname, '..');
const services = JSON.parse(read(path.join(__dirname, 'services.json')));
const compose = read(path.join(root, 'docker-compose.yml'));
const manifests = fs.readdirSync(path.join(root, 'deployment/k8s/deployments'))
  .map(f => read(path.join(root, 'deployment/k8s/deployments', f))).join('\n');
const errors = [];

for (const s of services) {
  const wf = path.join(__dirname, 'workflows', `svc-${s.service}.yml`);
  if (!fs.existsSync(wf)) { errors.push(`${s.service}: ${path.relative(root, wf)} is missing`); continue; }
  const text = read(wf);
  for (const key of ['service-path', 'project-file', 'assembly-name', 'k8s-deployment', 'k8s-container']) {
    if (!text.includes(`${key}: ${s[key]}\n`)) errors.push(`${s.service}: svc-${s.service}.yml does not say "${key}: ${s[key]}"`);
  }
  if (s['unit-test-project'] && !text.includes(`unit-test-project: ${s['unit-test-project']}`))
    errors.push(`${s.service}: unit-test-project differs between services.json and svc-${s.service}.yml`);
  if (s['unit-test-project'] && !fs.existsSync(path.join(root, s['unit-test-project'])))
    errors.push(`${s.service}: ${s['unit-test-project']} does not exist`);
  if (!fs.existsSync(path.join(root, s['service-path'], s['project-file'])))
    errors.push(`${s.service}: ${s['service-path']}/${s['project-file']} does not exist (case matters on Linux)`);
  if (!compose.includes(`SERVICE_PATH: ${s['service-path']}\n`)) errors.push(`${s.service}: SERVICE_PATH not found in docker-compose.yml`);
  if (!new RegExp(String.raw`kind: Deployment\s+metadata:\s+name: ${s['k8s-deployment']}\b`).test(manifests))
    errors.push(`${s.service}: no Deployment "${s['k8s-deployment']}" in deployment/k8s/deployments`);
  if (!manifests.includes(`- name: ${s['k8s-container']}\n`)) errors.push(`${s.service}: no container "${s['k8s-container']}"`);
}

const listed = new Set(services.map(s => `svc-${s.service}.yml`));
for (const f of fs.readdirSync(path.join(__dirname, 'workflows')).filter(f => /^svc-.*\.yml$/.test(f)))
  if (!listed.has(f)) errors.push(`${f} has no entry in .github/services.json (release.yml would never deploy it)`);

if (errors.length) { console.error(errors.join('\n')); process.exit(1); }
console.log(`OK - ${services.length} services consistent across services.json, svc-*.yml, docker-compose.yml and k8s manifests`);

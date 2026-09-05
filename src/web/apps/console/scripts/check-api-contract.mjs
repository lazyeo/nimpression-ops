#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');

// Paths to backend endpoints and frontend source
const serverEndpointsDir = path.resolve(projectRoot, '../../../../src/server/Nimpression.Api/Endpoints');
const frontendSrcDir = path.resolve(projectRoot, 'src');

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

// 1. Strip comments from C# source code
function stripCSharpComments(code) {
  let stripped = code.replace(/\/\*[\s\S]*?\*\//g, (match) => ' '.repeat(match.length));
  stripped = stripped.replace(/\/\/.*$/gm, (match) => ' '.repeat(match.length));
  return stripped;
}

// 2. Strip comments from JS/TS source code
function stripJsComments(code) {
  let stripped = code.replace(/\/\*[\s\S]*?\*\//g, (match) => ' '.repeat(match.length));
  stripped = stripped.replace(/\/\/.*$/gm, (match) => ' '.repeat(match.length));
  return stripped;
}

// 3. Normalize route paths: strip query strings, collapse slashes, normalize parameter placeholders
function normalizeRoute(route) {
  let r = route.trim();
  // Strip query string if any
  r = r.split('?')[0];
  // Replace multiple slashes with single slash
  r = r.replace(/\/+/g, '/');
  // Remove trailing slash if not root
  if (r.length > 1 && r.endsWith('/')) {
    r = r.slice(0, -1);
  }
  // Replace route parameter placeholders: {param:guid}, {param:int}, {param}, ${expr} with {param}
  r = r.replace(/\{[^}]+\}/g, '{param}');
  r = r.replace(/\$\{[^}]+\}/g, '{param}');
  return r;
}

// 4. Parse all backend routes declared in C# Minimal API Endpoints
function parseBackendRoutes(dir) {
  const routes = new Set();
  if (!fs.existsSync(dir)) {
    recordError(`C# Endpoints directory not found: ${dir}`);
    return routes;
  }

  const files = fs.readdirSync(dir).filter(
    (f) => f.endsWith('.cs') && f !== 'IEndpointModule.cs' && f !== 'EndpointModuleExtensions.cs'
  );

  for (const file of files) {
    const fullPath = path.join(dir, file);
    const content = stripCSharpComments(fs.readFileSync(fullPath, 'utf8'));

    const groupMap = new Map(); // varName -> prefix
    groupMap.set('routes', '');
    groupMap.set('app', '');

    // Match MapGroup calls including chained / nested groups:
    // var group = routes.MapGroup("/api/notifications");
    // var partnersGroup = group.MapGroup("/partner-contacts");
    const groupRegex = /(?:var|IEndpointRouteBuilder|RouteGroupBuilder)\s+([A-Za-z0-9_]+)\s*=\s*([A-Za-z0-9_]+)\.MapGroup\(\s*["']([^"']+)["']\s*\)/g;
    let gMatch;
    while ((gMatch = groupRegex.exec(content)) !== null) {
      const varName = gMatch[1];
      const parentVar = gMatch[2];
      const subPrefix = gMatch[3];
      const parentPrefix = groupMap.get(parentVar) || '';
      groupMap.set(varName, parentPrefix + subPrefix);
    }

    // Match endpoint mapping calls: group.MapGet("...", ...), app.MapPost("...", ...)
    const endpointRegex = /([A-Za-z0-9_]+)\.(MapGet|MapPost|MapPut|MapDelete|MapPatch)\s*(?:<[^>]+>)?\s*\(\s*["']([^"']*)["']/g;
    let eMatch;
    while ((eMatch = endpointRegex.exec(content)) !== null) {
      const targetVar = eMatch[1];
      const subRoute = eMatch[3];

      let prefix = '';
      if (groupMap.has(targetVar)) {
        prefix = groupMap.get(targetVar);
      } else if (targetVar === 'routes' || targetVar === 'app') {
        prefix = '';
      } else if (groupMap.size === 1) {
        prefix = Array.from(groupMap.values())[0];
      }

      let combined = prefix;
      if (subRoute && subRoute !== '/') {
        combined = prefix + (subRoute.startsWith('/') ? subRoute : '/' + subRoute);
      } else if (subRoute === '/' && !prefix) {
        combined = '/';
      }

      if (combined) {
        routes.add(normalizeRoute(combined));
      }
    }
  }

  return routes;
}

// 5. Parse all frontend /api/... calls in TypeScript and HTML files
function parseFrontendCalls(srcDir) {
  const calls = new Map(); // normalizedRoute -> { raw, file, line }
  const filesToScan = [];

  function walk(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (entry.isFile()) {
        if (
          (entry.name.endsWith('.ts') || entry.name.endsWith('.html')) &&
          !entry.name.endsWith('.spec.ts') &&
          !entry.name.endsWith('.d.ts')
        ) {
          filesToScan.push(full);
        }
      }
    }
  }

  walk(srcDir);

  for (const file of filesToScan) {
    const rawContent = fs.readFileSync(file, 'utf8');
    const cleanedContent = stripJsComments(rawContent);
    const lines = cleanedContent.split('\n');

    // Detect baseUrl definitions in services, e.g. baseUrl = '/api/incidents'
    let fileBaseUrl = null;
    const baseUrlMatch = /baseUrl\s*=\s*['"`]([^'"`]+)['"`]/.exec(cleanedContent);
    if (baseUrlMatch) {
      fileBaseUrl = baseUrlMatch[1];
    }

    lines.forEach((line, idx) => {
      // Direct string / template literals starting with /api/
      const apiLiteralRegex = /['"`](\/api\/[^'"`]+)['"`]/g;
      let match;
      while ((match = apiLiteralRegex.exec(line)) !== null) {
        const raw = match[1];
        // Skip standalone baseUrl declarations
        if (line.includes('baseUrl =') || line.includes('baseUrl=')) {
          continue;
        }
        const norm = normalizeRoute(raw);
        if (!calls.has(norm)) {
          calls.set(norm, {
            raw,
            file: path.relative(projectRoot, file),
            line: idx + 1,
          });
        }
      }

      // Template strings referencing this.baseUrl or baseUrl, e.g. `${this.baseUrl}/tasks`
      if (fileBaseUrl) {
        const templateRegex = /`\$\{(?:this\.)?baseUrl\}([^`]*)`/g;
        let tMatch;
        while ((tMatch = templateRegex.exec(line)) !== null) {
          const suffix = tMatch[1];
          const fullRaw = fileBaseUrl + suffix;
          const norm = normalizeRoute(fullRaw);
          if (!calls.has(norm)) {
            calls.set(norm, {
              raw: fullRaw,
              file: path.relative(projectRoot, file),
              line: idx + 1,
            });
          }
        }
      }
    });
  }

  return calls;
}

// 6. Verify Contract: frontend calls must map to existing backend routes
function verifyApiContract(backendRoutes, frontendCalls) {
  for (const [route, info] of frontendCalls.entries()) {
    if (!backendRoutes.has(route)) {
      recordError(
        `[Missing Backend Endpoint] Frontend calls '${info.raw}' (normalized: '${route}') at ${info.file}:${info.line}, but no matching backend endpoint exists in C# Minimal API.`
      );
    }
  }
}

// Execute Guard
console.log('--- [api-contract-guard] Running Frontend <-> Backend API Endpoint Parity Check ---');
const backendRoutes = parseBackendRoutes(serverEndpointsDir);
const frontendCalls = parseFrontendCalls(frontendSrcDir);

verifyApiContract(backendRoutes, frontendCalls);

if (hasErrors) {
  console.error(`\n[api-contract-guard] FAILED: Found ${errors.length} unmapped API call(s):`);
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to API contract violations.\n');
  process.exit(1);
} else {
  console.log(
    `[api-contract-guard] PASSED: All ${frontendCalls.size} frontend API calls successfully matched against ${backendRoutes.size} backend endpoints.\n`
  );
  process.exit(0);
}

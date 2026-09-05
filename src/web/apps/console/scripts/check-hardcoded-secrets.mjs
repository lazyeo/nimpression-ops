#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(projectRoot, '../../../../');

const IGNORE_DIRS = new Set([
  'node_modules',
  '.git',
  'bin',
  'obj',
  'dist',
  '.angular',
  'artifacts',
  '.data',
  '.turbo',
  '.alma',
  '_design',
  '.vscode',
]);

const IGNORE_FILES = new Set([
  'pnpm-lock.yaml',
  'package-lock.json',
]);

const SCANNED_EXTENSIONS = ['.cs', '.ts', '.js', '.mjs', '.json', '.yml', '.yaml', '.sh', '.env.example'];

const EXEMPTION_MARKERS = [
  'dev-only-insecure',
  'ZGV2LW9ubHk', // Base64 prefix of dev-only-insecure
  'allow-hardcoded:',
];

// Connection string Password pattern
const CONN_STR_PASSWORD_REGEX = /Password\s*=\s*([^;'"\s\\]+)/i;

const SECRET_KEYWORDS = [
  'password',
  'passwd',
  'secret',
  'jwtsecret',
  'clientsecret',
  'apisecret',
  'appsecret',
  'signingsecret',
  'apikey',
  'api_key',
  'secretkey',
  'secret_key',
  'privatekey',
  'private_key',
  'encryptionkey',
  'encryption_key',
  'signingkey',
  'signing_key',
  'accesstoken',
  'access_token',
  'refreshtoken',
  'refresh_token',
  'bearertoken',
  'bearer_token',
  'authtoken',
  'auth_token',
];

const NON_SECRET_IDENTIFIER_SUFFIXES = [
  'name',
  'header',
  'cookie',
  'param',
  'prefix',
  'suffix',
  'type',
  'claim',
  'policy',
  'path',
  'route',
  'url',
  'storagekey',
  'envvar',
  'scheme',
  'field',
];

function isSecretIdentifier(ident) {
  if (!ident) return false;
  const lower = ident.toLowerCase();

  // Exclude descriptor suffixes like TokenName, CookieName, PasswordField, etc.
  if (NON_SECRET_IDENTIFIER_SUFFIXES.some((suffix) => lower.endsWith(suffix))) {
    return false;
  }
  // Exclude prefixes like STORAGE_, COOKIE_, HEADER_
  if (/^(?:storage_|cookie_|header_|param_|env_)/i.test(ident)) {
    return false;
  }

  return SECRET_KEYWORDS.some((kw) => lower === kw || lower.endsWith(kw) || lower.startsWith(kw));
}

// Strict match of assignment: LHS (= or :) RHS (string literal)
const ASSIGN_REGEX =
  /^(?:.*?[,\s{(])?(?:([a-zA-Z0-9_]+)|["']([a-zA-Z0-9_-]+)["'])\s*(?:=|:)\s*@?(["'`])([^"'`\r\n]*)\3\s*[,;]?\s*(?:\/\/.*|\/\*.*\*\/|#.*)?$/;

const FALSE_POSITIVE_VALUES = new Set([
  '',
  'password',
  'current-password',
  'new-password',
  '密码',
  'text',
  'string',
  'bearer',
  'bearer ',
  'authorization',
  'x-api-key',
  'grant_type',
  'application/json',
]);

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

function getAllFiles(dir, results = []) {
  if (!fs.existsSync(dir)) return results;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    if (entry.isDirectory()) {
      if (!IGNORE_DIRS.has(entry.name)) {
        getAllFiles(path.join(dir, entry.name), results);
      }
    } else if (entry.isFile()) {
      if (IGNORE_FILES.has(entry.name)) continue;
      if (SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext))) {
        results.push(path.join(dir, entry.name));
      }
    }
  }
  return results;
}

function scanFilesForHardcodedSecrets() {
  const files = getAllFiles(repoRoot);

  for (const file of files) {
    if (file.endsWith('check-hardcoded-secrets.mjs')) continue;

    const content = fs.readFileSync(file, 'utf8');
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const lineNum = idx + 1;
      const trimmed = line.trim();

      if (!trimmed) return;

      // Check exemption marker on the current line
      if (EXEMPTION_MARKERS.some((m) => line.includes(m))) {
        return;
      }

      // Check exemption marker on the previous line (e.g. comment right above)
      if (idx > 0 && EXEMPTION_MARKERS.some((m) => lines[idx - 1].includes(m))) {
        return;
      }

      const relPath = path.relative(repoRoot, file);

      // 1. Connection string password check
      if (/Host=|Server=|Database=|Data Source=|User Id=|Username=/i.test(line)) {
        const connMatch = CONN_STR_PASSWORD_REGEX.exec(line);
        if (connMatch) {
          const pwdVal = connMatch[1].trim();
          if (
            pwdVal &&
            !pwdVal.startsWith('${') &&
            !pwdVal.startsWith('$') &&
            !pwdVal.startsWith('<') &&
            !pwdVal.startsWith('%') &&
            !pwdVal.startsWith('(') &&
            pwdVal !== '""' &&
            pwdVal !== "''"
          ) {
            recordError(
              `[Hardcoded ConnectionString Password] ${relPath}:${lineNum} -> "${trimmed}" (Must use environment variable or add // allow-hardcoded: <reason>)`
            );
            return;
          }
        }
      }

      // 2. Secret Assignment check
      const assignMatch = ASSIGN_REGEX.exec(trimmed);
      if (assignMatch) {
        const rawIdent = assignMatch[1] || assignMatch[2];
        if (isSecretIdentifier(rawIdent)) {
          const val = assignMatch[4].trim();

          // Check false positives
          if (
            !val ||
            val.length < 3 ||
            FALSE_POSITIVE_VALUES.has(val.toLowerCase()) ||
            val.startsWith('${') ||
            val.startsWith('$(') ||
            val.startsWith('%') ||
            val.startsWith('<') ||
            val.startsWith('{{') ||
            val.startsWith('env(') ||
            val.startsWith('process.env') ||
            trimmed.startsWith('//') ||
            trimmed.startsWith('*') ||
            trimmed.startsWith('<!--')
          ) {
            return;
          }

          // Filter out HTML attribute contexts
          if (trimmed.includes('type=') || trimmed.includes('autocomplete=') || trimmed.includes('grant_type')) {
            return;
          }

          // Filter out i18n JSON translation dictionaries
          if (file.endsWith('.json') && file.includes('i18n')) {
            return;
          }

          recordError(
            `[Hardcoded Secret] ${relPath}:${lineNum} (Identifier: ${rawIdent}) -> "${trimmed}" (Must use dev-only-insecure... or add // allow-hardcoded: <reason>)`
          );
        }
      }
    });
  }
}

console.log('--- [secrets-scanner] Running hardcoded credentials and secrets check ---');
scanFilesForHardcodedSecrets();

if (hasErrors) {
  console.error('\n[secrets-scanner] FAILED: Found ' + errors.length + ' hardcoded secret violation(s):');
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to hardcoded secrets policy. Use dev-only-insecure... or add // allow-hardcoded: <reason>.\n');
  process.exit(1);
} else {
  console.log('[secrets-scanner] PASSED: No hardcoded secrets or connection string credentials found across codebase.\n');
  process.exit(0);
}

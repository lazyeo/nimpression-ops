#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');

const i18nDir = path.join(projectRoot, 'src/assets/i18n');
const appDir = path.join(projectRoot, 'src/app');

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

// 1. Check Dictionary Key Symmetry
function getAllKeys(obj, prefix = '') {
  let keys = [];
  for (const [k, v] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${k}` : k;
    if (v && typeof v === 'object' && !Array.isArray(v)) {
      keys = keys.concat(getAllKeys(v, fullKey));
    } else {
      keys.push(fullKey);
    }
  }
  return keys;
}

function checkI18nKeySymmetry() {
  const enPath = path.join(i18nDir, 'en-NZ.json');
  const zhPath = path.join(i18nDir, 'zh-CN.json');

  if (!fs.existsSync(enPath)) {
    recordError(`Missing English dictionary at: ${enPath}`);
    return;
  }
  if (!fs.existsSync(zhPath)) {
    recordError(`Missing Chinese dictionary at: ${zhPath}`);
    return;
  }

  let enJson, zhJson;
  try {
    enJson = JSON.parse(fs.readFileSync(enPath, 'utf8'));
  } catch (err) {
    recordError(`Failed to parse ${enPath}: ${err.message}`);
    return;
  }
  try {
    zhJson = JSON.parse(fs.readFileSync(zhPath, 'utf8'));
  } catch (err) {
    recordError(`Failed to parse ${zhPath}: ${err.message}`);
    return;
  }

  const enKeys = new Set(getAllKeys(enJson));
  const zhKeys = new Set(getAllKeys(zhJson));

  for (const key of enKeys) {
    if (!zhKeys.has(key)) {
      recordError(`[Key Asymmetry] Key '${key}' exists in en-NZ.json but is missing in zh-CN.json`);
    }
  }

  for (const key of zhKeys) {
    if (!enKeys.has(key)) {
      recordError(`[Key Asymmetry] Key '${key}' exists in zh-CN.json but is missing in en-NZ.json`);
    }
  }
}

// 2. Scan Files for Hardcoded Chinese
const CHINESE_CHAR_REGEX = /[\u4e00-\u9fa5]/;

function getAllFiles(dir, exts, results = []) {
  if (!fs.existsSync(dir)) return results;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      getAllFiles(fullPath, exts, results);
    } else if (entry.isFile() && exts.some((ext) => entry.name.endsWith(ext))) {
      results.push(fullPath);
    }
  }
  return results;
}

function stripCommentsFromCode(code) {
  let stripped = code.replace(/\/\*[\s\S]*?\*\//g, (match) => ' '.repeat(match.length));
  stripped = stripped.replace(/\/\/.*$/gm, (match) => ' '.repeat(match.length));
  return stripped;
}

function stripHtmlComments(html) {
  return html.replace(/<!--[\s\S]*?-->/g, (match) => ' '.repeat(match.length));
}

function scanHtmlFilesForChinese() {
  const htmlFiles = getAllFiles(appDir, ['.html']);
  for (const file of htmlFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const cleaned = stripHtmlComments(content);
    const lines = cleaned.split('\n');
    lines.forEach((line, idx) => {
      if (CHINESE_CHAR_REGEX.test(line)) {
        const lineNum = idx + 1;
        const relPath = path.relative(projectRoot, file);
        recordError(`[Hardcoded Chinese in Template] ${relPath}:${lineNum} -> "${line.trim()}"`);
      }
    });
  }
}

function scanTsFilesForChinese() {
  const tsFiles = getAllFiles(appDir, ['.ts']).filter(
    (f) => !f.endsWith('.spec.ts') && !f.endsWith('.d.ts'),
  );
  for (const file of tsFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const cleaned = stripCommentsFromCode(content);
    const lines = cleaned.split('\n');

    lines.forEach((line, idx) => {
      if (CHINESE_CHAR_REGEX.test(line)) {
        const lineNum = idx + 1;
        const relPath = path.relative(projectRoot, file);
        recordError(`[Hardcoded Chinese in Code] ${relPath}:${lineNum} -> "${line.trim()}"`);
      }
    });
  }
}

// 3. Whitelist-based Scan for Unlocalized Visible Text Nodes & Static Attributes in HTML Templates
function scanHtmlTemplatesWhitelist() {
  const htmlFiles = getAllFiles(appDir, ['.html']);

  for (const file of htmlFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const relPath = path.relative(projectRoot, file);

    function getLineCol(offset) {
      let line = 1;
      let col = 1;
      for (let p = 0; p < offset; p++) {
        if (content[p] === '\n') {
          line++;
          col = 1;
        } else {
          col++;
        }
      }
      return { line, col };
    }

    let text = content;

    // 1. Mask HTML comments
    text = text.replace(/<!--[\s\S]*?-->/g, (m) => ' '.repeat(m.length));

    // 2. Mask <svg>...</svg>, <style>...</style>, <script>...</script>
    text = text.replace(/<(svg|style|script)\b[\s\S]*?<\/\1>/gi, (m) => ' '.repeat(m.length));

    // 3. Mask elements with data-no-i18n (nested)
    let prev;
    do {
      prev = text;
      text = text.replace(/<[a-zA-Z0-9_-]+[^>]*\bdata-no-i18n\b[^>]*\/>/gi, (m) => ' '.repeat(m.length));
      text = text.replace(/<([a-zA-Z0-9_-]+)[^>]*\bdata-no-i18n\b[^>]*>[\s\S]*?<\/\1>/gi, (m) =>
        ' '.repeat(m.length),
      );
    } while (text !== prev);

    // 4. Check attributes on tags: placeholder, aria-label, title (static strings without binding)
    let maskedTagsText = '';
    let i = 0;
    const len = text.length;

    while (i < len) {
      if (text[i] === '<' && i + 1 < len && /[a-zA-Z0-9_\/!-]/.test(text[i + 1])) {
        const tagStart = i;
        let inQuote = null;
        i++;
        while (i < len) {
          const ch = text[i];
          if (inQuote) {
            if (ch === inQuote) inQuote = null;
          } else if (ch === '"' || ch === "'") {
            inQuote = ch;
          } else if (ch === '>') {
            i++;
            break;
          }
          i++;
        }
        const tagContent = text.substring(tagStart, i);

        const staticAttrRegex = /(?:^|\s)(placeholder|title|aria-label)=["']([^"']+)["']/g;
        let attrMatch;
        while ((attrMatch = staticAttrRegex.exec(tagContent)) !== null) {
          const attrName = attrMatch[1];
          const attrVal = attrMatch[2].trim();
          const nonPunct = attrVal.replace(/[0-9\s.,:;!?'"`~@#$%^&*()_+\-=\[\]{}|<>/\\—–•·×✕]/g, '');
          if (nonPunct.length > 0) {
            const { line } = getLineCol(tagStart + attrMatch.index);
            recordError(
              `[Unlocalized Static Attribute] ${relPath}:${line} -> ${attrName}="${attrVal}" (Must use [${attrName}] with i18n pipe or add data-no-i18n)`,
            );
          }
        }

        maskedTagsText += ' '.repeat(tagContent.length);
      } else {
        maskedTagsText += text[i];
        i++;
      }
    }
    text = maskedTagsText;

    // 5. Mask Angular interpolations {{ ... }}
    text = text.replace(/\{\{[\s\S]*?\}\}/g, (m) => ' '.repeat(m.length));

    // 6. Mask Angular control flow: @if, @for, @switch, @case, @default, @defer, @placeholder, @loading, @error, @empty, @let
    text = text.replace(/@(if|for|switch|case|defer|placeholder|loading)\b[^{]*\{/g, (m) => ' '.repeat(m.length));
    text = text.replace(/@(default|empty|error)\b\s*\{?/g, (m) => ' '.repeat(m.length));
    text = text.replace(/}\s*@else\s+if\b[^{]*\{/g, (m) => ' '.repeat(m.length));
    text = text.replace(/}\s*@else\b\s*\{?/g, (m) => ' '.repeat(m.length));
    text = text.replace(/@let\s+[^;]+;/g, (m) => ' '.repeat(m.length));
    text = text.replace(/}/g, ' ');

    // 7. Find remaining visible text nodes
    const textLines = text.split('\n');
    textLines.forEach((tLine, lineIdx) => {
      const cleanEntities = tLine.replace(/&[a-zA-Z0-9#]+;/g, ' ');
      const trimmed = cleanEntities.trim();
      if (!trimmed) return;

      const nonPunct = trimmed.replace(/[0-9\s.,:;!?'"`~@#$%^&*()_+\-=\[\]{}|<>/\\—–•·×✕]/g, '');
      if (nonPunct.length > 0) {
        recordError(
          `[Unlocalized Text Node] ${relPath}:${lineIdx + 1} -> "${trimmed}" (Must use | i18n pipe or add data-no-i18n)`,
        );
      }
    });
  }
}

// Execute checks
console.log('--- [i18n-scanner] Running F13.2 & R5 bilingual & template whitelist checks ---');
checkI18nKeySymmetry();
scanHtmlFilesForChinese();
scanTsFilesForChinese();
scanHtmlTemplatesWhitelist();

if (hasErrors) {
  console.error('\n[i18n-scanner] FAILED: Found ' + errors.length + ' i18n violation(s):');
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to i18n violations.\n');
  process.exit(1);
} else {
  console.log(
    '[i18n-scanner] PASSED: All translation keys are symmetrical, no hardcoded Chinese found, and all template text nodes are properly localized.\n',
  );
  process.exit(0);
}

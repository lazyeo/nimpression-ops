#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');

// Path to C# domain enums and TS api-models
const serverEnumsDir = path.resolve(projectRoot, '../../../../src/server/Nimpression.Domain/Enums');
const apiModelsPath = path.resolve(projectRoot, 'src/app/core/api/models/api-models.ts');
const appDir = path.resolve(projectRoot, 'src/app');

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

// 2. Parse C# Enums
function parseCSharpEnums(dir) {
  const enums = new Map();
  if (!fs.existsSync(dir)) {
    recordError(`C# Domain Enums directory not found: ${dir}`);
    return enums;
  }

  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.cs'));
  for (const file of files) {
    const fullPath = path.join(dir, file);
    const content = fs.readFileSync(fullPath, 'utf8');
    const cleaned = stripCSharpComments(content);

    // Match enum declaration: public enum EnumName { ... }
    const enumRegex = /(?:public\s+)?enum\s+([A-Za-z0-9_]+)\s*\{([^}]*)\}/g;
    let match;
    while ((match = enumRegex.exec(cleaned)) !== null) {
      const enumName = match[1];
      const body = match[2];

      const members = body
        .split(',')
        .map((m) => m.trim())
        .filter((m) => m.length > 0)
        .map((m) => {
          // Remove attributes or values like '= 1'
          const noAttr = m.replace(/\[[^\]]*\]/g, '').trim();
          const memberName = noAttr.split('=')[0].trim();
          return memberName;
        })
        .filter((m) => /^[A-Za-z0-9_]+$/.test(m));

      enums.set(enumName, {
        file: path.relative(projectRoot, fullPath),
        members,
      });
    }
  }

  return enums;
}

// 3. Parse Frontend TypeScript Types in api-models.ts
function parseTsApiModels(filePath) {
  const types = new Map();
  if (!fs.existsSync(filePath)) {
    recordError(`Frontend api-models.ts not found: ${filePath}`);
    return types;
  }

  const content = fs.readFileSync(filePath, 'utf8');
  // Match `export type EnumName = ...;`
  const typeRegex = /export\s+type\s+([A-Za-z0-9_]+)\s*=\s*([^;]+);/g;
  let match;
  while ((match = typeRegex.exec(content)) !== null) {
    const typeName = match[1];
    const typeDef = match[2];

    // Check if it's a string union type: 'A' | 'B' | ...
    if (typeDef.includes("'")) {
      const members = [];
      const memberRegex = /'([^']+)'/g;
      let mMatch;
      while ((mMatch = memberRegex.exec(typeDef)) !== null) {
        members.push(mMatch[1]);
      }

      if (members.length > 0) {
        types.set(typeName, members);
      }
    }
  }

  return types;
}

// 4. Scan for Duplicate Enum Declarations across Frontend
function scanForDuplicateDeclarations(appPath, csharpEnumNames) {
  const duplicateRegexList = Array.from(csharpEnumNames).map((enumName) => ({
    name: enumName,
    // Catch `export type EnumName =` or `export enum EnumName`
    regex: new RegExp(`export\\s+(?:type|enum)\\s+${enumName}\\s*[={]`, 'g'),
  }));

  function walk(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (
        entry.isFile() &&
        entry.name.endsWith('.ts') &&
        !entry.name.endsWith('.spec.ts') &&
        !entry.name.endsWith('.d.ts') &&
        full !== apiModelsPath
      ) {
        const content = fs.readFileSync(full, 'utf8');
        for (const item of duplicateRegexList) {
          if (item.regex.test(content)) {
            const relPath = path.relative(projectRoot, full);
            recordError(
              `[Duplicate Declaration] ${relPath} declares ${item.name} directly. Must re-export from core/api/models/api-models.ts`,
            );
          }
        }
      }
    }
  }

  walk(appPath);
}

// 5. Compare Backend and Frontend Enums
function verifyEnumContracts(csharpEnums, tsTypes) {
  for (const [enumName, csData] of csharpEnums.entries()) {
    if (!tsTypes.has(enumName)) {
      recordError(
        `[Missing Enum] Backend enum '${enumName}' (in ${csData.file}) is not declared in api-models.ts`,
      );
      continue;
    }

    const tsMembers = tsTypes.get(enumName);
    const csMembers = csData.members;

    const missingInTs = csMembers.filter((m) => !tsMembers.includes(m));
    const extraInTs = tsMembers.filter((m) => !csMembers.includes(m));

    if (missingInTs.length > 0) {
      recordError(
        `[Enum Member Mismatch] Enum '${enumName}' is missing member(s) in frontend: [${missingInTs.join(', ')}]`,
      );
    }

    if (extraInTs.length > 0) {
      recordError(
        `[Enum Member Mismatch] Enum '${enumName}' has extra invalid member(s) in frontend: [${extraInTs.join(', ')}]`,
      );
    }
  }
}

// Execute Checks
console.log('--- [enum-contract-guard] Running C# <-> TypeScript Enum Contract Alignment Check ---');
const csEnums = parseCSharpEnums(serverEnumsDir);
const tsTypes = parseTsApiModels(apiModelsPath);

verifyEnumContracts(csEnums, tsTypes);
scanForDuplicateDeclarations(appDir, csEnums.keys());

if (hasErrors) {
  console.error('\n[enum-contract-guard] FAILED: Found ' + errors.length + ' enum contract violation(s):');
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to enum contract violations.\n');
  process.exit(1);
} else {
  console.log(
    `[enum-contract-guard] PASSED: All ${csEnums.size} domain enums verified with exact member parity against TypeScript single source of truth.\n`,
  );
  process.exit(0);
}

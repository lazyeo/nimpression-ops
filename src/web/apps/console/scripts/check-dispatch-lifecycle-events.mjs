#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');

// Default target file: JobTask aggregate root in domain
export const DEFAULT_JOB_TASK_PATH = path.resolve(
  projectRoot,
  '../../../../src/server/Nimpression.Domain/Entities/Dispatch/JobTask.cs',
);

/**
 * Strips comments from C# source code while preserving string lengths and newlines.
 */
export function stripCSharpComments(code) {
  let stripped = code.replace(/\/\*[\s\S]*?\*\//g, (match) => ' '.repeat(match.length));
  stripped = stripped.replace(/\/\/.*$/gm, (match) => ' '.repeat(match.length));
  return stripped;
}

/**
 * Extracts public methods and their bodies from a C# class file.
 */
export function extractPublicMethods(content) {
  const cleaned = stripCSharpComments(content);
  const methods = [];

  // Match method signature: public [async] <ReturnType> <MethodName>(<args>)
  // Exclude constructors (which match class name without return type, but here ReturnType is present)
  // Exclude properties with getter/setter blocks (e.g. public Kilometres? EffectiveDistanceKm => or { get ... })
  const methodRegex =
    /public\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?|[A-Za-z0-9_<>?,]+)\s+([A-Za-z0-9_]+)\s*\(([^)]*)\)\s*\{/g;

  let match;
  while ((match = methodRegex.exec(cleaned)) !== null) {
    const methodName = match[1];
    const startIndex = match.index + match[0].length - 1; // index of opening brace '{'

    // Extract body by tracking balanced braces
    let depth = 0;
    let endIndex = -1;
    for (let i = startIndex; i < cleaned.length; i++) {
      if (cleaned[i] === '{') {
        depth++;
      } else if (cleaned[i] === '}') {
        depth--;
        if (depth === 0) {
          endIndex = i;
          break;
        }
      }
    }

    if (endIndex !== -1) {
      const body = cleaned.substring(startIndex + 1, endIndex);
      methods.push({
        name: methodName,
        body,
      });
    }
  }

  return methods;
}

/**
 * Analyzes whether JobTask methods that modify Status emit domain events via AddDomainEvent.
 */
export function analyzeJobTaskLifecycleEvents(jobTaskPath = DEFAULT_JOB_TASK_PATH) {
  if (!fs.existsSync(jobTaskPath)) {
    return {
      success: false,
      errors: [`[File Not Found] JobTask entity not found at: ${jobTaskPath}`],
      scannedMethods: [],
      mutatingMethods: [],
      violations: [],
    };
  }

  const content = fs.readFileSync(jobTaskPath, 'utf8');
  const methods = extractPublicMethods(content);

  const scannedMethods = [];
  const mutatingMethods = [];
  const violations = [];
  const errors = [];

  for (const method of methods) {
    scannedMethods.push(method.name);

    // Check if the method modifies Status
    const modifiesStatus = /Status\s*=\s*(?:JobTaskStatus\.)?[A-Za-z0-9_]+/.test(method.body);
    if (modifiesStatus) {
      mutatingMethods.push(method.name);

      // Invariant: Method that transitions JobTask.Status MUST call AddDomainEvent
      const hasDomainEvent = /AddDomainEvent\s*\(/.test(method.body);
      if (!hasDomainEvent) {
        violations.push(method.name);
        errors.push(
          `[Missing Domain Event] JobTask.${method.name} transitions Status but does not call AddDomainEvent(...) to emit a lifecycle domain event.`,
        );
      }
    }
  }

  return {
    success: errors.length === 0,
    errors,
    scannedMethods,
    mutatingMethods,
    violations,
  };
}

export function runDispatchLifecycleEventsGuard(jobTaskPath = DEFAULT_JOB_TASK_PATH) {
  console.log(
    '--- [dispatch-lifecycle-guard] Running JobTask Dispatch Lifecycle Domain Event Verification ---',
  );

  const result = analyzeJobTaskLifecycleEvents(jobTaskPath);

  console.log(`[dispatch-lifecycle-guard] Scanned public methods: ${result.scannedMethods.join(', ')}`);
  console.log(`[dispatch-lifecycle-guard] Status-mutating methods: ${result.mutatingMethods.join(', ')}`);

  if (!result.success) {
    console.error(
      `\n[dispatch-lifecycle-guard] FAILED: Found ${result.errors.length} lifecycle domain event violation(s):`,
    );
    result.errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
    console.error(
      '\nBuild aborted: All JobTask status transition methods must publish a corresponding IDomainEvent for realtime synchronization.\n',
    );
    return { success: false, errors: result.errors, violations: result.violations };
  }

  console.log(
    `[dispatch-lifecycle-guard] PASSED: All ${result.mutatingMethods.length} JobTask lifecycle status transitions reliably emit domain events.\n`,
  );
  return { success: true, errors: [], violations: [] };
}

// Direct CLI execution
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const result = runDispatchLifecycleEventsGuard();
  if (!result.success) {
    process.exit(1);
  } else {
    process.exit(0);
  }
}

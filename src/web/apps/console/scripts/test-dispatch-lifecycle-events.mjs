#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  stripCSharpComments,
  extractPublicMethods,
  analyzeJobTaskLifecycleEvents,
  DEFAULT_JOB_TASK_PATH,
} from './check-dispatch-lifecycle-events.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function assert(condition, message) {
  totalTests++;
  if (condition) {
    passedTests++;
    console.log(`  [PASS] ${message}`);
  } else {
    failedTests++;
    console.error(`  [FAIL] ${message}`);
  }
}

console.log('--- [dispatch-lifecycle-tests] Running Guard 11 automated test suite ---');

const tmpTestDir = path.join('/tmp', `dispatch-guard-test-${Date.now()}`);
fs.mkdirSync(tmpTestDir, { recursive: true });

try {
  // 1. Comment Stripping
  console.log('\n[Suite 1] C# Comment Stripping');
  const codeWithComments = `
    // public void FakeMethod() { Status = JobTaskStatus.Cancelled; }
    /*
       public void AnotherFake() { Status = JobTaskStatus.Draft; }
    */
    public void RealMethod() {
      // AddDomainEvent(...)
      Status = JobTaskStatus.InProgress;
    }
  `;
  const stripped = stripCSharpComments(codeWithComments);
  const methodsFromComments = extractPublicMethods(stripped);
  assert(methodsFromComments.length === 1, 'Ignores commented out method signatures');
  assert(methodsFromComments[0].name === 'RealMethod', 'Only extracts active RealMethod');

  // 2. Synthetic Perfect JobTask Class (All mutating methods emit domain events)
  console.log('\n[Suite 2] Fully Compliant JobTask Verification');
  const perfectJobTaskCode = `
    namespace Nimpression.Domain.Entities.Dispatch;
    public sealed class JobTask {
      public void Assign(Guid driverId, Guid vehicleId) {
        Status = JobTaskStatus.Assigned;
        AddDomainEvent(new JobTaskAssigned(Id, driverId, vehicleId, DateTimeOffset.UtcNow));
      }
      public void Acknowledge(DateTimeOffset acknowledgedAt) {
        Status = JobTaskStatus.Acknowledged;
        AddDomainEvent(new JobTaskAcknowledged(Id, DriverId!.Value, acknowledgedAt));
      }
      public void Start(DateTimeOffset startedAt) {
        Status = JobTaskStatus.InProgress;
        AddDomainEvent(new JobTaskStarted(Id, DriverId!.Value, startedAt));
      }
      public void Complete(DateTimeOffset completedAt) {
        Status = JobTaskStatus.Completed;
        AddDomainEvent(new JobTaskCompleted(Id, DriverId!.Value, EffectiveDistanceKm, completedAt));
      }
      public void Cancel(string reason, DateTimeOffset cancelledAt) {
        Status = JobTaskStatus.Cancelled;
        AddDomainEvent(new JobTaskCancelled(Id, DriverId, reason, cancelledAt));
      }
      public void NonMutatingHelper() {
        // Read only
      }
    }
  `;
  const perfectPath = path.join(tmpTestDir, 'PerfectJobTask.cs');
  fs.writeFileSync(perfectPath, perfectJobTaskCode, 'utf8');

  const perfectResult = analyzeJobTaskLifecycleEvents(perfectPath);
  assert(perfectResult.success, 'Fully compliant JobTask passes guard with success: true');
  assert(perfectResult.violations.length === 0, 'No violations found on fully compliant class');
  assert(perfectResult.mutatingMethods.length === 5, 'Detects 5 status-mutating methods');
  assert(perfectResult.scannedMethods.length === 6, 'Detects 6 public methods including non-mutating helper');

  // 3. Negative Verification: Missing Event on Specific Methods
  console.log('\n[Suite 3] Missing Event Detection on Individual Methods');
  const missingCompleteCode = perfectJobTaskCode.replace(
    'AddDomainEvent(new JobTaskCompleted',
    '// Removed: AddDomainEvent(new JobTaskCompleted',
  );
  const missingCompletePath = path.join(tmpTestDir, 'MissingCompleteJobTask.cs');
  fs.writeFileSync(missingCompletePath, missingCompleteCode, 'utf8');

  const missingCompleteResult = analyzeJobTaskLifecycleEvents(missingCompletePath);
  assert(!missingCompleteResult.success, 'Fails when Complete method misses AddDomainEvent');
  assert(
    missingCompleteResult.violations.length === 1 && missingCompleteResult.violations[0] === 'Complete',
    'Accurately pinpoints Complete as the sole violating method',
  );

  // 4. Verification on Baseline / Pre-Fix JobTask.cs Pattern (Start & Cancel missing)
  console.log('\n[Suite 4] Pre-Fix Defect Exact Reproduction (Start and Cancel missing)');
  const preFixJobTaskCode = `
    public sealed class JobTask {
      public void Assign(Guid d, Guid v) {
        Status = JobTaskStatus.Assigned;
        AddDomainEvent(new JobTaskAssigned(Id, d, v, DateTimeOffset.UtcNow));
      }
      public void Acknowledge(DateTimeOffset a) {
        Status = JobTaskStatus.Acknowledged;
        AddDomainEvent(new JobTaskAcknowledged(Id, DriverId!.Value, a));
      }
      public void Start(DateTimeOffset s) {
        Status = JobTaskStatus.InProgress;
        // Missing AddDomainEvent
      }
      public void Complete(DateTimeOffset c) {
        Status = JobTaskStatus.Completed;
        AddDomainEvent(new JobTaskCompleted(Id, DriverId!.Value, null, c));
      }
      public void Cancel(string r, DateTimeOffset c) {
        Status = JobTaskStatus.Cancelled;
        // Missing AddDomainEvent
      }
    }
  `;
  const preFixPath = path.join(tmpTestDir, 'PreFixJobTask.cs');
  fs.writeFileSync(preFixPath, preFixJobTaskCode, 'utf8');

  const preFixResult = analyzeJobTaskLifecycleEvents(preFixPath);
  assert(!preFixResult.success, 'Pre-fix JobTask fails guard');
  assert(
    preFixResult.violations.length === 2 &&
      preFixResult.violations.includes('Start') &&
      preFixResult.violations.includes('Cancel'),
    'Pre-fix JobTask reports EXACTLY Start and Cancel (2 violations, nothing more, nothing less)',
  );
} finally {
  fs.rmSync(tmpTestDir, { recursive: true, force: true });
}

console.log(
  `\n--- Test Summary: ${passedTests}/${totalTests} tests passed (${failedTests} failures) ---\n`,
);

if (failedTests > 0) {
  process.exit(1);
} else {
  process.exit(0);
}

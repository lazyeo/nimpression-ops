#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const srcDir = path.join(projectRoot, 'src');

const DEFAULT_THEME_FILE = path.join(srcDir, 'styles', 'theme.scss');
const TOKENS_FILE = path.join(srcDir, 'styles', 'tokens.scss');

const WCAG_AA_NORMAL_TEXT_THRESHOLD = 4.5;

/**
 * Strips SCSS/CSS comments (block and line).
 */
export function stripComments(css) {
  return css.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n\r]*/g, '');
}

/**
 * Parses nested SCSS/CSS blocks and extracts variable declarations per theme.
 */
export function parseThemeTokens(themeContent, tokensContent = '') {
  const combined =
    (tokensContent ? stripComments(tokensContent) + '\n' : '') + stripComments(themeContent);

  const rootTokens = {};
  const lightTokens = {};
  const darkTokens = {};

  const selectorStack = [];
  let currentAccumulator = '';

  let i = 0;
  while (i < combined.length) {
    const char = combined[i];

    if (char === '{') {
      selectorStack.push(currentAccumulator.trim());
      currentAccumulator = '';
      i++;
    } else if (char === '}') {
      selectorStack.pop();
      currentAccumulator = '';
      i++;
    } else if (char === ';') {
      const decl = currentAccumulator.trim();
      currentAccumulator = '';

      if (decl.startsWith('--')) {
        const colonIdx = decl.indexOf(':');
        if (colonIdx !== -1) {
          const prop = decl.slice(0, colonIdx).trim();
          const val = decl.slice(colonIdx + 1).trim();
          const context = selectorStack.join(' > ');

          const isDark = context.includes('dark') || context.includes('prefers-color-scheme');
          const isExplicitLight =
            context.includes("data-theme='light'") || context.includes('data-theme="light"');

          if (isDark) {
            darkTokens[prop] = val;
          } else if (isExplicitLight) {
            lightTokens[prop] = val;
          } else {
            rootTokens[prop] = val;
            lightTokens[prop] = val;
          }
        }
      }
      i++;
    } else {
      currentAccumulator += char;
      i++;
    }
  }

  const resolvedLight = { ...rootTokens, ...lightTokens };
  // Dark mode inherits from light mode and applies dark overrides
  const resolvedDark = { ...resolvedLight, ...darkTokens };

  return { light: resolvedLight, dark: resolvedDark };
}

/**
 * Resolves CSS variable references recursively (with cycle detection).
 */
export function resolveVarReferences(
  value,
  tokenMap,
  maxDepth = 10,
  currentDepth = 0,
  visited = new Set(),
) {
  if (currentDepth > maxDepth) {
    throw new Error(`Exceeded max var() resolution depth (potential cycle) for value: "${value}"`);
  }

  const varRegex = /var\(\s*(--[a-zA-Z0-9_-]+)(?:\s*,\s*([^)]+))?\)/g;
  if (!varRegex.test(value)) {
    return value.trim();
  }

  return value.replace(varRegex, (_match, tokenName, fallback) => {
    if (visited.has(tokenName)) {
      throw new Error(`Cyclic var() reference detected involving "${tokenName}"`);
    }

    if (tokenMap[tokenName] !== undefined) {
      const nextVisited = new Set(visited);
      nextVisited.add(tokenName);
      return resolveVarReferences(
        tokenMap[tokenName],
        tokenMap,
        maxDepth,
        currentDepth + 1,
        nextVisited,
      );
    }

    if (fallback !== undefined) {
      return resolveVarReferences(fallback.trim(), tokenMap, maxDepth, currentDepth + 1, visited);
    }

    throw new Error(`Undefined token referenced in var(): "${tokenName}"`);
  });
}

/**
 * Parses CSS colors into standard RGBA structure { r: 0-255, g: 0-255, b: 0-255, a: 0-1 }.
 * Supports #rgb, #rgba, #rrggbb, #rrggbbaa, rgb(), rgba(), oklch(), and named colors.
 */
export function parseColor(colorStr) {
  const str = colorStr.trim();

  const named = {
    white: { r: 255, g: 255, b: 255, a: 1 },
    black: { r: 0, g: 0, b: 0, a: 1 },
    transparent: { r: 0, g: 0, b: 0, a: 0 },
  };
  if (named[str.toLowerCase()]) {
    return named[str.toLowerCase()];
  }

  // Hex colors
  if (str.startsWith('#')) {
    let hex = str.slice(1);
    if (hex.length === 3) {
      hex =
        hex
          .split('')
          .map((c) => c + c)
          .join('') + 'ff';
    } else if (hex.length === 4) {
      hex = hex
        .split('')
        .map((c) => c + c)
        .join('');
    } else if (hex.length === 6) {
      hex = hex + 'ff';
    } else if (hex.length !== 8) {
      throw new Error(`Invalid hex color string: "${str}"`);
    }

    const num = parseInt(hex, 16);
    return {
      r: (num >>> 24) & 255,
      g: (num >>> 16) & 255,
      b: (num >>> 8) & 255,
      a: Number(((num & 255) / 255).toFixed(4)),
    };
  }

  // rgb / rgba
  const rgbMatch = str.match(/^rgba?\(\s*([^)]+)\s*\)$/i);
  if (rgbMatch) {
    const raw = rgbMatch[1].trim();
    let parts = [];
    if (raw.includes(',')) {
      parts = raw.split(',').map((p) => p.trim());
    } else {
      const [rgbPart, alphaPart] = raw.split('/').map((p) => p.trim());
      parts = rgbPart.split(/\s+/).filter(Boolean);
      if (alphaPart) parts.push(alphaPart);
    }
    if (parts.length < 3 || parts.length > 4) {
      throw new Error(`Invalid rgb/rgba color string: "${str}"`);
    }

    const parseChannel = (val) => {
      if (val.endsWith('%')) return (parseFloat(val) / 100) * 255;
      return parseFloat(val);
    };
    const parseAlpha = (val) => {
      if (!val) return 1;
      if (val.endsWith('%')) return parseFloat(val) / 100;
      return parseFloat(val);
    };

    return {
      r: Math.max(0, Math.min(255, Math.round(parseChannel(parts[0])))),
      g: Math.max(0, Math.min(255, Math.round(parseChannel(parts[1])))),
      b: Math.max(0, Math.min(255, Math.round(parseChannel(parts[2])))),
      a: Math.max(0, Math.min(1, parseAlpha(parts[3]))),
    };
  }

  // oklch(L C H [/ A])
  const oklchMatch = str.match(/^oklch\(\s*([^)]+)\s*\)$/i);
  if (oklchMatch) {
    const raw = oklchMatch[1].trim();
    let parts = [];
    if (raw.includes('/')) {
      const [lchPart, alphaPart] = raw.split('/').map((p) => p.trim());
      parts = lchPart.split(/\s+/).filter(Boolean);
      if (alphaPart) parts.push(alphaPart);
    } else if (raw.includes(',')) {
      parts = raw.split(',').map((p) => p.trim());
    } else {
      parts = raw.split(/\s+/).filter(Boolean);
    }
    if (parts.length < 3 || parts.length > 4) {
      throw new Error(`Invalid oklch color string: "${str}"`);
    }

    let l = parseFloat(parts[0]);
    if (parts[0].endsWith('%')) l = l / 100;

    let c = parseFloat(parts[1]);
    if (parts[1].endsWith('%')) c = (c / 100) * 0.4;

    const h = parts[2];
    let hDeg = 0;
    if (h.endsWith('deg')) hDeg = parseFloat(h);
    else if (h.endsWith('rad')) hDeg = (parseFloat(h) * 180) / Math.PI;
    else if (h.endsWith('turn')) hDeg = parseFloat(h) * 360;
    else if (h.endsWith('grad')) hDeg = (parseFloat(h) * 360) / 400;
    else if (h === 'none') hDeg = 0;
    else hDeg = parseFloat(h);

    let a = 1;
    if (parts[3] !== undefined) {
      a = parseFloat(parts[3]);
      if (parts[3].endsWith('%')) a = a / 100;
    }

    const hRad = (hDeg * Math.PI) / 180;
    const oklabL = l;
    const oklabA = c * Math.cos(hRad);
    const oklabB = c * Math.sin(hRad);

    const l_ = oklabL + 0.3963377774 * oklabA + 0.2158037573 * oklabB;
    const m_ = oklabL - 0.1055613458 * oklabA - 0.0638541728 * oklabB;
    const s_ = oklabL - 0.0894841775 * oklabA - 1.291485548 * oklabB;

    const lLin = l_ * l_ * l_;
    const mLin = m_ * m_ * m_;
    const sLin = s_ * s_ * s_;

    const rLin = +4.0767416621 * lLin - 3.3077115913 * mLin + 0.2309699292 * sLin;
    const gLin = -1.2684380046 * lLin + 2.6097574011 * mLin - 0.3413193965 * sLin;
    const bLin = -0.0041960863 * lLin - 0.7034186147 * mLin + 1.707614701 * sLin;

    const linToSrgb = (val) => {
      const clamped = Math.max(0, Math.min(1, val));
      return clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055;
    };

    return {
      r: Math.max(0, Math.min(255, Math.round(linToSrgb(rLin) * 255))),
      g: Math.max(0, Math.min(255, Math.round(linToSrgb(gLin) * 255))),
      b: Math.max(0, Math.min(255, Math.round(linToSrgb(bLin) * 255))),
      a: Math.max(0, Math.min(1, a)),
    };
  }

  throw new Error(`Unsupported color format: "${str}"`);
}

/**
 * Standard Porter-Duff alpha compositing (source-over).
 * fg is layered over bg.
 */
export function compositeColors(fg, bg) {
  const alpha = fg.a + bg.a * (1 - fg.a);
  if (alpha === 0) {
    return { r: 0, g: 0, b: 0, a: 0 };
  }
  const r = (fg.r * fg.a + bg.r * bg.a * (1 - fg.a)) / alpha;
  const g = (fg.g * fg.a + bg.g * bg.a * (1 - fg.a)) / alpha;
  const b = (fg.b * fg.a + bg.b * bg.a * (1 - fg.a)) / alpha;
  return {
    r: Math.round(r),
    g: Math.round(g),
    b: Math.round(b),
    a: Number(alpha.toFixed(4)),
  };
}

/**
 * Converts standard sRGB 8-bit channel to linear RGB.
 */
export function srgbToLinear(c) {
  const norm = c / 255;
  return norm <= 0.04045 ? norm / 12.92 : Math.pow((norm + 0.055) / 1.055, 2.4);
}

/**
 * Calculates WCAG 2.1 relative luminance for an opaque/composited sRGB color.
 */
export function getRelativeLuminance(rgb) {
  const r = srgbToLinear(rgb.r);
  const g = srgbToLinear(rgb.g);
  const b = srgbToLinear(rgb.b);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/**
 * Calculates WCAG 2.1 contrast ratio between two colors (L1 + 0.05) / (L2 + 0.05).
 */
export function getContrastRatio(fgRgb, bgRgb) {
  const l1 = getRelativeLuminance(fgRgb);
  const l2 = getRelativeLuminance(bgRgb);
  const max = Math.max(l1, l2);
  const min = Math.min(l1, l2);
  return (max + 0.05) / (min + 0.05);
}

/**
 * Self-checks standard benchmark values (AC 4).
 */
export function runBenchmarkSelfCheck() {
  const benchmarks = [
    { fg: '#000', bg: '#fff', expected: '21.00' },
    { fg: '#767676', bg: '#fff', expected: '4.54' },
    { fg: '#949494', bg: '#fff', expected: '3.03' },
    { fg: '#0d6efd', bg: '#fff', expected: '4.50' },
  ];

  for (const item of benchmarks) {
    const fg = parseColor(item.fg);
    const bg = parseColor(item.bg);
    const ratio = getContrastRatio(fg, bg).toFixed(2);
    if (ratio !== item.expected) {
      throw new Error(
        `Benchmark verification failed: ${item.fg} on ${item.bg} gave ${ratio}:1, expected ${item.expected}:1`,
      );
    }
  }
}

/**
 * Discovers and builds high-signal assertion pairs for the given theme.
 */
export function buildAssertionPairs(tokens) {
  const pairs = [];

  // 1. Text on card and app backgrounds
  const textTokens = ['--text-primary', '--text-secondary', '--text-muted'];
  const bgTokens = ['--bg-card', '--bg-app'];
  for (const text of textTokens) {
    for (const bg of bgTokens) {
      if (tokens[text] && tokens[bg]) {
        pairs.push({
          fgToken: text,
          bgToken: bg,
          category: 'Text on Surface',
        });
      }
    }
  }

  // 2. Inverse text on primary brand color
  if (tokens['--text-inverse'] && tokens['--color-primary']) {
    pairs.push({
      fgToken: '--text-inverse',
      bgToken: '--color-primary',
      category: 'Inverse Text on Primary',
    });
  }

  // 3. Variant subtle text on variant subtle background
  const variants = ['primary', 'success', 'warning', 'danger', 'info', 'neutral'];
  for (const v of variants) {
    const fg = `--color-${v}-text`;
    const bg = `--color-${v}-subtle`;
    if (tokens[fg] && tokens[bg]) {
      pairs.push({
        fgToken: fg,
        bgToken: bg,
        category: `Variant Subtle (${v})`,
      });
    }
  }

  // 4. Badge text on badge background (8 variants or dynamically discovered)
  const badgeVariants = ['success', 'warning', 'danger', 'info', 'neutral', 'purple', 'orange'];
  for (const v of badgeVariants) {
    const fg = `--badge-${v}-text`;
    const bg = `--badge-${v}-bg`;
    if (tokens[fg] && tokens[bg]) {
      pairs.push({
        fgToken: fg,
        bgToken: bg,
        category: `Badge (${v})`,
      });
    }
  }

  // Also dynamically discover any additional badge variants defined in tokens
  for (const key of Object.keys(tokens)) {
    if (key.startsWith('--badge-') && key.endsWith('-bg')) {
      const variantName = key.slice('--badge-'.length, -'-bg'.length);
      const fg = `--badge-${variantName}-text`;
      if (tokens[fg] && !badgeVariants.includes(variantName)) {
        pairs.push({
          fgToken: fg,
          bgToken: key,
          category: `Badge (${variantName})`,
        });
      }
    }
  }

  return pairs;
}

/**
 * Evaluates contrast ratios for all assertion pairs in a theme.
 */
export function evaluateThemeContrast(themeName, tokens, options = {}) {
  const threshold = options.threshold ?? WCAG_AA_NORMAL_TEXT_THRESHOLD;
  const pairs = buildAssertionPairs(tokens);

  const baseSurface =
    themeName === 'dark' ? { r: 9, g: 13, b: 22, a: 1 } : { r: 248, g: 250, b: 252, a: 1 };
  const rawBgApp = tokens['--bg-app']
    ? parseColor(resolveVarReferences(tokens['--bg-app'], tokens))
    : baseSurface;
  const effectiveBgApp = rawBgApp.a < 1 ? compositeColors(rawBgApp, baseSurface) : rawBgApp;

  const rawBgCard = tokens['--bg-card']
    ? parseColor(resolveVarReferences(tokens['--bg-card'], tokens))
    : effectiveBgApp;
  const effectiveBgCard = rawBgCard.a < 1 ? compositeColors(rawBgCard, effectiveBgApp) : rawBgCard;

  const results = [];
  const violations = [];

  for (const pair of pairs) {
    const rawFgVal = resolveVarReferences(tokens[pair.fgToken], tokens);
    const rawBgVal = resolveVarReferences(tokens[pair.bgToken], tokens);

    const fgColor = parseColor(rawFgVal);
    const bgColor = parseColor(rawBgVal);

    // Determine container background for semi-transparent elements
    let containerBg = effectiveBgCard;
    if (pair.bgToken === '--bg-app') {
      containerBg = baseSurface;
    } else if (pair.bgToken === '--bg-card') {
      containerBg = effectiveBgApp;
    }

    const effectiveBg = bgColor.a < 1 ? compositeColors(bgColor, containerBg) : bgColor;
    const effectiveFg = fgColor.a < 1 ? compositeColors(fgColor, effectiveBg) : fgColor;

    const contrast = getContrastRatio(effectiveFg, effectiveBg);
    const formattedRatio = contrast.toFixed(2);
    const passes = contrast >= threshold;

    const result = {
      theme: themeName,
      category: pair.category,
      fgToken: pair.fgToken,
      bgToken: pair.bgToken,
      rawFgVal,
      rawBgVal,
      contrast,
      formattedRatio: `${formattedRatio}:1`,
      threshold: `${threshold.toFixed(2)}:1`,
      passes,
    };

    results.push(result);

    if (!passes) {
      violations.push({
        ...result,
        message: `[Contrast Violation] [${themeName}] ${pair.fgToken} (${rawFgVal}) on ${pair.bgToken} (${rawBgVal}) -> ${formattedRatio}:1 (minimum required: ${threshold.toFixed(2)}:1)`,
      });
    }
  }

  return { results, violations };
}

/**
 * Main execution function.
 */
export function runContrastGuard(themePath = DEFAULT_THEME_FILE, tokensPath = TOKENS_FILE) {
  console.log('--- [contrast-guard] Running color contrast verification ---');

  runBenchmarkSelfCheck();
  console.log('[contrast-guard] Benchmark formulas verified against WCAG reference values.');

  if (!fs.existsSync(themePath)) {
    console.error(`[contrast-guard] ERROR: Theme file not found at ${themePath}`);
    process.exit(1);
  }

  const themeContent = fs.readFileSync(themePath, 'utf8');
  const tokensContent = fs.existsSync(tokensPath) ? fs.readFileSync(tokensPath, 'utf8') : '';

  const { light, dark } = parseThemeTokens(themeContent, tokensContent);
  console.log(
    `[contrast-guard] Parsed ${Object.keys(light).length} tokens for light mode, ${Object.keys(dark).length} tokens for dark mode.`,
  );

  const lightEval = evaluateThemeContrast('light', light);
  const darkEval = evaluateThemeContrast('dark', dark);

  const totalEvaluations = lightEval.results.length + darkEval.results.length;
  const allViolations = [...lightEval.violations, ...darkEval.violations];

  console.log(
    `[contrast-guard] Evaluated ${totalEvaluations} high-signal color pairs across light and dark themes.`,
  );

  if (allViolations.length > 0) {
    console.error(
      `\n[contrast-guard] FAILED: Found ${allViolations.length} contrast violation(s):`,
    );
    allViolations.forEach((v, i) => console.error(`  ${i + 1}. ${v.message}`));
    console.error(
      '\nBuild aborted: All design token pairs must satisfy WCAG AA contrast ratio (>= 4.5:1).\n',
    );
    return { success: false, totalEvaluations, violations: allViolations };
  } else {
    console.log(
      '[contrast-guard] PASSED: All design token pairs satisfy WCAG AA contrast requirements (>= 4.5:1).\n',
    );
    return { success: true, totalEvaluations, violations: [] };
  }
}

// If executed directly from CLI
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const result = runContrastGuard();
  if (!result.success) {
    process.exit(1);
  } else {
    process.exit(0);
  }
}

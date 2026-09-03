/**
 * Okabe-Ito Colorblind Safe Palette and WCAG AA Contrast Utilities
 *
 * Okabe-Ito is the recognized standard 8-color palette designed to be distinguishable
 * across common forms of color vision deficiency (deuteranopia, protanopia, tritanopia).
 */

export interface RgbColor {
  r: number;
  g: number;
  b: number;
}

export interface ChartThemeConfig {
  name: 'light' | 'dark';
  backgroundColor: string;
  cardBackgroundColor: string;
  textColor: string;
  textSecondaryColor: string;
  textMutedColor: string;
  borderColor: string;
  splitLineColor: string;
  tooltipBackgroundColor: string;
  tooltipBorderColor: string;
  tooltipTextColor: string;
  palette: string[];
}

/**
 * Standard Okabe-Ito 8-color palette (HEX)
 */
export const OKABE_ITO_PALETTE = {
  orange: '#E69F00',
  skyBlue: '#56B4E9',
  bluishGreen: '#009E73',
  yellow: '#F0E442',
  blue: '#0072B2',
  vermilion: '#D55E00',
  reddishPurple: '#CC79A7',
  black: '#000000',
} as const;

/**
 * Ordered series palette derived from Okabe-Ito for multi-series charts.
 */
export const CHART_PALETTE: string[] = [
  OKABE_ITO_PALETTE.blue, // #0072B2
  OKABE_ITO_PALETTE.orange, // #E69F00
  OKABE_ITO_PALETTE.bluishGreen, // #009E73
  OKABE_ITO_PALETTE.vermilion, // #D55E00
  OKABE_ITO_PALETTE.skyBlue, // #56B4E9
  OKABE_ITO_PALETTE.reddishPurple, // #CC79A7
  OKABE_ITO_PALETTE.yellow, // #F0E442
  OKABE_ITO_PALETTE.black, // #000000
];

/**
 * Semantic categorical colors for fleet status, fine categories, etc.
 */
export const SEMANTIC_COLORS = {
  inTransit: OKABE_ITO_PALETTE.blue, // #0072B2
  idle: OKABE_ITO_PALETTE.skyBlue, // #56B4E9
  maintenance: OKABE_ITO_PALETTE.vermilion, // #D55E00 (Warning/Alert)
  success: OKABE_ITO_PALETTE.bluishGreen, // #009E73
  warning: OKABE_ITO_PALETTE.orange, // #E69F00
  danger: OKABE_ITO_PALETTE.vermilion, // #D55E00
  info: OKABE_ITO_PALETTE.blue, // #0072B2
  accent: OKABE_ITO_PALETTE.reddishPurple, // #CC79A7
  highlight: OKABE_ITO_PALETTE.yellow, // #F0E442
} as const;

/**
 * Non-color redundant cues for accessibility
 */
export const ACCESSIBILITY_MARKERS = {
  shapes: ['circle', 'rect', 'triangle', 'diamond', 'pin', 'arrow'] as const,
  lineStyles: ['solid', 'dashed', 'dotted'] as const,
  barPatterns: ['solid', 'striped', 'dots', 'grid'] as const,
};

/**
 * Light theme configuration
 */
export const LIGHT_THEME: ChartThemeConfig = {
  name: 'light',
  backgroundColor: '#FFFFFF',
  cardBackgroundColor: '#F8FAFC',
  textColor: '#0F172A', // Slate 900 (Contrast ~15.9:1 against #FFFFFF)
  textSecondaryColor: '#334155', // Slate 700 (Contrast ~8.5:1 against #FFFFFF)
  textMutedColor: '#64748B', // Slate 500 (Contrast ~4.6:1 against #FFFFFF)
  borderColor: '#E2E8F0', // Slate 200
  splitLineColor: '#F1F5F9', // Slate 100
  tooltipBackgroundColor: 'rgba(15, 23, 42, 0.92)', // Slate 900
  tooltipBorderColor: '#334155',
  tooltipTextColor: '#F8FAFC', // Slate 50 (Contrast > 14:1)
  palette: CHART_PALETTE,
};

/**
 * Dark theme configuration
 */
export const DARK_THEME: ChartThemeConfig = {
  name: 'dark',
  backgroundColor: '#0F172A', // Slate 900
  cardBackgroundColor: '#1E293B', // Slate 800
  textColor: '#F8FAFC', // Slate 50 (Contrast ~15.8:1 against #0F172A)
  textSecondaryColor: '#CBD5E1', // Slate 300 (Contrast ~11.1:1 against #0F172A)
  textMutedColor: '#94A3B8', // Slate 400 (Contrast ~6.3:1 against #0F172A)
  borderColor: '#334155', // Slate 700
  splitLineColor: '#1E293B', // Slate 800
  tooltipBackgroundColor: 'rgba(30, 41, 59, 0.95)', // Slate 800
  tooltipBorderColor: '#475569',
  tooltipTextColor: '#F8FAFC', // Slate 50
  palette: CHART_PALETTE,
};

/**
 * Parses a hex color (#RGB, #RRGGBB, #RRGGBBAA) into RGB components.
 */
export function hexToRgb(hex: string): RgbColor {
  const cleaned = hex.replace(/^#/, '');
  if (!/^[0-9A-Fa-f]{3}$|^[0-9A-Fa-f]{6}$|^[0-9A-Fa-f]{8}$/.test(cleaned)) {
    throw new Error(`Invalid hex color string: "${hex}"`);
  }
  let fullHex = cleaned;
  if (cleaned.length === 3) {
    fullHex = cleaned
      .split('')
      .map((c) => c + c)
      .join('');
  }
  const r = parseInt(fullHex.substring(0, 2), 16);
  const g = parseInt(fullHex.substring(2, 4), 16);
  const b = parseInt(fullHex.substring(4, 6), 16);
  if (Number.isNaN(r) || Number.isNaN(g) || Number.isNaN(b)) {
    throw new Error(`Invalid hex color string: "${hex}"`);
  }
  return { r, g, b };
}

/**
 * Converts sRGB channel component [0, 255] to linear luminance component.
 */
export function channelLuminance(value: number): number {
  const c = value / 255;
  return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}

/**
 * Calculates WCAG 2.1 relative luminance for a given color.
 * L = 0.2126 * R + 0.7152 * G + 0.0722 * B
 */
export function calculateRelativeLuminance(color: string | RgbColor): number {
  const rgb = typeof color === 'string' ? hexToRgb(color) : color;
  const rLin = channelLuminance(rgb.r);
  const gLin = channelLuminance(rgb.g);
  const bLin = channelLuminance(rgb.b);
  return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
}

/**
 * Calculates the WCAG 2.1 contrast ratio between two colors:
 * Contrast = (L1 + 0.05) / (L2 + 0.05) where L1 >= L2.
 */
export function calculateContrastRatio(
  color1: string | RgbColor,
  color2: string | RgbColor,
): number {
  const lum1 = calculateRelativeLuminance(color1);
  const lum2 = calculateRelativeLuminance(color2);
  const lighter = Math.max(lum1, lum2);
  const darker = Math.min(lum1, lum2);
  const ratio = (lighter + 0.05) / (darker + 0.05);
  return Math.round(ratio * 100) / 100;
}

/**
 * Determines whether a color combination passes WCAG AA for normal text (>= 4.5:1).
 */
export function passesWcagAANormalText(foreground: string, background: string): boolean {
  return calculateContrastRatio(foreground, background) >= 4.5;
}

/**
 * Determines whether a color combination passes WCAG AA for large text or graphical objects (>= 3.0:1).
 */
export function passesWcagAALargeTextOrGraphics(foreground: string, background: string): boolean {
  return calculateContrastRatio(foreground, background) >= 3.0;
}

/**
 * Gets high-contrast text color ('#000000' or '#FFFFFF') that maximizes readability against a background color.
 */
export function getContrastingTextColor(backgroundColor: string): string {
  const whiteContrast = calculateContrastRatio('#FFFFFF', backgroundColor);
  const blackContrast = calculateContrastRatio('#000000', backgroundColor);
  return whiteContrast >= blackContrast ? '#FFFFFF' : '#000000';
}

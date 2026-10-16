export const ENGINE_VERSION: string;
export type Point = [number, number];
export interface ROI { x: number; y: number; width: number; height: number }
export interface RecognitionOptions {
  sensitivity: number; contrast: number; denoise: number; minArea: number;
  detectSurface: boolean; closeGaps: boolean; reconstruct: boolean; maxGap: number;
}
export const DEFAULT_OPTIONS: Readonly<RecognitionOptions>;
export interface ViewOptions { pathMode?: 'main' | 'full'; smoothing?: 0 | 1 | 2 | 3; includeInference?: boolean }
export interface RecognitionInput extends ViewOptions {
  rgba: Uint8Array | Uint8ClampedArray;
  width: number; height: number; roi?: ROI;
  image?: { name?: string; width?: number; height?: number };
  options?: Partial<RecognitionOptions>;
}
export interface NetworkNode {
  id: string; x: number; y: number; type: string; boundary: boolean;
  clusterPixels: Point[]; [key: string]: unknown;
}
export interface NetworkEdge {
  id: string; source: string; target: string; points: Point[]; rawPoints: Point[];
  inferredSegments: boolean[]; lengthPx: number; rawLengthPx: number;
  meanWidthPx: number; provenance: string; [key: string]: unknown;
}
export interface Network {
  schemaVersion: '2.5'; engine: string; engineVersion: string; createdAt: string;
  image: { name: string; width: number; height: number };
  coordinateSystem: { origin: 'top-left'; units: 'px'; space: 'original-image'; pixelCoordinates: string };
  roi: ROI;
  processing: {
    width: number; height: number; scaleToOriginal: {x: number; y: number};
    roi: ROI; parameters: RecognitionOptions; manualEdits: number;
    surfaceDetection: unknown; pathSmoothing: Record<string, unknown>; pathSelection: Record<string, unknown>;
  };
  quality: { reviewRequired: boolean; includesInferredConnections: boolean; physicalConnectivityVerified: boolean;
    groundTruthAccuracyValidated: boolean; metricDepth: boolean; [key: string]: unknown };
  stats: Record<string, number>; inferredConnections: Record<string, unknown>[];
  nodes: NetworkNode[]; edges: NetworkEdge[];
}
/** Internal processing-resolution data. Use network for a stable downstream JSON contract. */
export interface Analysis {
  mask: Uint8Array; inferredMask: Uint8Array; directMask: Uint8Array;
  bridges: Record<string, unknown>[]; [key: string]: unknown;
}
export interface RecognitionContext {
  width: number; height: number; originalWidth: number; originalHeight: number;
  name: string; roi: ROI; parameters: RecognitionOptions; manualEdits: number;
  pathMode: 'main' | 'full'; smoothing: 0 | 1 | 2 | 3; includeInference: boolean;
}
export interface RecognitionPacket { analysis: Analysis; context: RecognitionContext; network: Network }
export function recognize(input: RecognitionInput): RecognitionPacket;
export function exportNetwork(analysis: Analysis, context: RecognitionContext, view?: ViewOptions): Network;
export function updateMask(packet: RecognitionPacket, mask: Uint8Array): RecognitionPacket;

export interface RealtimeMessage {
  kind: string;
  entityId: string;
  occurredAt: string;
}

export type RealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

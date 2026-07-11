import axios, { AxiosError, type AxiosInstance, type AxiosRequestConfig, type AxiosResponse } from 'axios'
import { reportBackendRequestFailure, reportBackendRequestSuccess } from '@/composables/useBackendConnectionMonitor'

export interface BackendRequest {
  method: string
  url: string
  data?: unknown
  params?: unknown
  headers?: Record<string, string>
}

export interface BackendResponse<T = unknown> {
  status: number
  data: T
}

export interface BackendProvider {
  request<T = unknown>(request: BackendRequest): Promise<BackendResponse<T>>
}

export class HttpBackendProvider implements BackendProvider {
  private readonly client: AxiosInstance

  constructor() {
    this.client = axios.create({
      baseURL: '/api',
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    })

    this.client.interceptors.response.use(
      (response) => {
        reportBackendRequestSuccess(response.status)
        return response
      },
      (error: AxiosError) => {
        const message = (error.response?.data as { message?: string })?.message || error.message
        console.error('API Error:', message)
        const status = error.response?.status
        if (!status || status >= 500) {
          reportBackendRequestFailure(error)
        } else {
          reportBackendRequestSuccess(status)
        }
        return Promise.reject(error)
      }
    )
  }

  async request<T = unknown>(request: BackendRequest): Promise<BackendResponse<T>> {
    const config: AxiosRequestConfig = {
      method: request.method,
      url: request.url,
      data: request.data,
      params: request.params,
      headers: request.headers,
    }
    const response = await this.client.request<T>(config)
    return response
  }
}

export class ElectronBackendProvider implements BackendProvider {
  constructor(private readonly bridge: DesktopBridge) {}

  async request<T = unknown>(request: BackendRequest): Promise<BackendResponse<T>> {
    const response = await this.bridge.request<T>(toCloneableValue(request) as BackendRequest)
    if (response.status >= 400) {
      const message = String((response.data as { error?: string })?.error || `Backend request failed (${response.status})`)
      const error = new Error(message)
      reportBackendRequestFailure(error)
      throw error
    } else {
      reportBackendRequestSuccess(response.status)
    }
    return response
  }
}

function toCloneableValue(value: unknown): unknown {
  const seen = new WeakSet<object>()
  const serialized = JSON.stringify(value, (_key, nested) => {
    if (typeof nested === 'bigint') return nested.toString()
    if (typeof nested === 'function' || typeof nested === 'symbol') return undefined
    if (nested instanceof Error) {
      return { name: nested.name, message: nested.message, stack: nested.stack }
    }
    if (nested && typeof nested === 'object') {
      if (seen.has(nested)) return undefined
      seen.add(nested)
    }
    return nested
  })

  return serialized === undefined ? null : JSON.parse(serialized)
}

interface DesktopBridge {
  platform: 'electron'
  request<T = unknown>(request: BackendRequest): Promise<BackendResponse<T>>
  getBackendBaseUrl(): Promise<string>
  openDialog(options: NativeDialogOptions): Promise<string | null>
  openExternal(url: string): Promise<void>
  setWindowTitle(title: string): Promise<void>
  notifyReady(): void
  appendConsoleEntry(entry: EditorConsoleEntry): void
  getConsoleEntries(): Promise<EditorConsoleEntry[]>
  clearConsoleEntries(): Promise<void>
  onConsoleEntry(listener: (entry: EditorConsoleEntry) => void): () => void
  onConsoleCleared(listener: () => void): () => void
}

export interface NativeDialogOptions {
  kind: 'file' | 'directory'
  title?: string
  defaultPath?: string
  extensions?: string[]
  filterName?: string
}

export interface EditorConsoleEntry {
  id?: string
  timestamp: string
  level: 'debug' | 'info' | 'success' | 'warning' | 'error'
  source: string
  message: string
}

declare global {
  interface Window {
    visunoviaDesktop?: DesktopBridge
  }
}

export function createBackendProvider(): BackendProvider {
  if (window.visunoviaDesktop?.platform === 'electron') {
    return new ElectronBackendProvider(window.visunoviaDesktop)
  }

  return new HttpBackendProvider()
}

export function toAxiosLikeResponse<T>(response: BackendResponse<T>): AxiosResponse<T> {
  return {
    data: response.data,
    status: response.status,
    statusText: String(response.status),
    headers: {},
    config: {},
  } as AxiosResponse<T>
}

import { Component, type ReactNode } from 'react'

interface AppErrorBoundaryProps {
  children: ReactNode
}

interface AppErrorBoundaryState {
  hasError: boolean
  error: Error | null
}

export default class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  constructor(props: AppErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): AppErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error('[AppErrorBoundary] Application crashed:', error, info.componentStack)
  }

  handleRefresh = () => {
    window.location.reload()
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex items-center justify-center h-screen bg-gray-950 text-white">
          <div className="text-center max-w-md px-6">
            <div className="text-5xl mb-4">⚠</div>
            <h1 className="text-xl font-semibold mb-2">Application Error</h1>
            <p className="text-sm text-gray-400 mb-1">An unexpected error occurred:</p>
            <pre className="text-xs text-red-400 bg-gray-900 p-3 rounded mb-4 whitespace-pre-wrap break-all max-h-32 overflow-y-auto">
              {this.state.error?.message ?? 'Unknown error'}
            </pre>
            <button
              onClick={this.handleRefresh}
              className="px-4 py-2 bg-blue-600 text-white rounded text-sm
                hover:bg-blue-500 transition-colors cursor-pointer border-none"
            >
              Refresh Page
            </button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}

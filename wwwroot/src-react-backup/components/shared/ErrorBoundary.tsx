import { Component, type ReactNode } from 'react'

interface ErrorBoundaryProps {
  children: ReactNode
  panelName?: string
}

interface ErrorBoundaryState {
  hasError: boolean
  error: Error | null
}

export default class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error(`[ErrorBoundary${this.props.panelName ? `:${this.props.panelName}` : ''}]`, error, info.componentStack)
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null })
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex flex-col items-center justify-center h-full bg-gray-900 text-center px-4">
          <div className="text-red-400 text-lg mb-2">⚠</div>
          <p className="text-xs text-red-400 mb-1 font-semibold">
            {this.props.panelName ?? 'Component'} Error
          </p>
          <p className="text-[11px] text-gray-500 mb-3 max-w-[280px] break-words">
            {this.state.error?.message ?? 'Unknown error'}
          </p>
          <button
            onClick={this.handleRetry}
            className="px-3 py-1 bg-gray-700 text-gray-300 rounded text-xs
              hover:bg-gray-600 transition-colors cursor-pointer border-none"
          >
            Retry
          </button>
        </div>
      )
    }

    return this.props.children
  }
}

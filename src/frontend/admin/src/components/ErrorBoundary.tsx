import { Component, type ErrorInfo, type ReactNode } from 'react'
import { logger } from '@/utils/logger'

interface Props {
  children: ReactNode
}

interface State {
  hasError: boolean
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false }

  static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    logger.exception(error, { componentStack: info.componentStack ?? '' })
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex h-screen flex-col items-center justify-center gap-3 text-center">
          <h1 className="text-lg font-semibold text-ink">Something went wrong.</h1>
          <p className="text-sm text-ink-muted">
            The error has been logged. Please refresh the page to continue.
          </p>
          <button className="btn-primary" onClick={() => window.location.reload()}>
            Reload
          </button>
        </div>
      )
    }

    return this.props.children
  }
}

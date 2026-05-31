import { useState, useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import './i18n'
import AppErrorBoundary from './components/shared/AppErrorBoundary'

export default function App() {
  const { t, ready: i18nReady } = useTranslation()
  const [isReady, setIsReady] = useState(false)
  const [i18nTimedOut, setI18nTimedOut] = useState(false)

  const isPopup = window.opener !== null

  useEffect(() => {
    console.log('[App] Initializing, popup:', isPopup, 'i18nReady:', i18nReady)
  }, [])

  useEffect(() => {
    if (isPopup) {
      console.log('[App] Popup mode detected, skipping initialization wait')
      setIsReady(true)
      return
    }

    if (i18nReady) {
      console.log('[App] i18n ready, rendering application')
      setIsReady(true)
    }
  }, [isPopup, i18nReady])

  useEffect(() => {
    if (i18nReady || isPopup) return

    const timeout = setTimeout(() => {
      if (!i18nReady) {
        console.warn('[App] i18n initialization timeout (3s), forcing render with fallback')
        setI18nTimedOut(true)
        setIsReady(true)
      }
    }, 3000)

    return () => clearTimeout(timeout)
  }, [i18nReady, isPopup])

  if (!isReady && !i18nTimedOut) {
    return (
      <div className="loading-screen flex items-center justify-center h-screen bg-gray-950">
        <div className="text-center">
          <div className="loading-spinner mx-auto mb-4 h-8 w-8 animate-spin rounded-full border-4 border-gray-600 border-t-blue-400" />
          <p className="text-gray-400 text-sm">{t('app.loading')}</p>
        </div>
      </div>
    )
  }

  return (
    <AppErrorBoundary>
      <Outlet />
    </AppErrorBoundary>
  )
}

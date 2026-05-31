import React from 'react'
import ReactDOM from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import App from './App'
import EditorPage from './pages/EditorPage'
import PreferencesPage from './pages/PreferencesPage'
import AboutPage from './pages/AboutPage'
import ProjectSettingsPage from './pages/ProjectSettingsPage'
import './index.css'

const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      {
        index: true,
        element: <EditorPage />,
      },
      {
        path: 'Preferences',
        element: <PreferencesPage />,
      },
      {
        path: 'About',
        element: <AboutPage />,
      },
      {
        path: 'ProjectSettings',
        element: <ProjectSettingsPage />,
      },
    ],
  },
])

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <RouterProvider router={router} />
  </React.StrictMode>,
)
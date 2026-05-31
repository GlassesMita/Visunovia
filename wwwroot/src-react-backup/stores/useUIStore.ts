import { create } from 'zustand'

interface UIState {
  isLeftPanelOpen: boolean
  isRightPanelOpen: boolean
  isBottomPanelOpen: boolean
  activeLeftPanel: 'project' | 'hierarchy'
  activeRightPanel: 'inspector' | 'preview'
  paletteExpanded: Record<string, boolean>
  theme: 'light' | 'dark'

  toggleLeftPanel: () => void
  toggleRightPanel: () => void
  toggleBottomPanel: () => void
  setActiveLeftPanel: (panel: 'project' | 'hierarchy') => void
  setActiveRightPanel: (panel: 'inspector' | 'preview') => void
  setPaletteExpanded: (expanded: Record<string, boolean>) => void
  setTheme: (theme: 'light' | 'dark') => void
  toggleTheme: () => void
}

export const useUIStore = create<UIState>((set) => ({
  isLeftPanelOpen: true,
  isRightPanelOpen: true,
  isBottomPanelOpen: false,
  activeLeftPanel: 'project',
  activeRightPanel: 'inspector',
  paletteExpanded: { flowControl: true, dialogue: true, events: false, logic: false },
  theme: 'dark',

  toggleLeftPanel: () => set((s) => ({ isLeftPanelOpen: !s.isLeftPanelOpen })),
  toggleRightPanel: () => set((s) => ({ isRightPanelOpen: !s.isRightPanelOpen })),
  toggleBottomPanel: () => set((s) => ({ isBottomPanelOpen: !s.isBottomPanelOpen })),
  setActiveLeftPanel: (panel) => set({ activeLeftPanel: panel }),
  setActiveRightPanel: (panel) => set({ activeRightPanel: panel }),
  setPaletteExpanded: (expanded) => set({ paletteExpanded: expanded }),
  setTheme: (theme) => set({ theme }),
  toggleTheme: () => set((s) => ({ theme: s.theme === 'light' ? 'dark' : 'light' })),
}))

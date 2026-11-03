import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import AsyncStorage from '@react-native-async-storage/async-storage'

/** The POS terminal this phone sells from. Remembered, so the cashier is not asked before every sale; cleared on sign-out. */
interface TerminalState {
  terminalId: string | null
  terminalName: string | null
  choose: (id: string, name: string) => void
  clear: () => void
}

export const useTerminalStore = create<TerminalState>()(
  persist(
    (set) => ({
      terminalId: null,
      terminalName: null,
      choose: (terminalId, terminalName) => set({ terminalId, terminalName }),
      clear: () => set({ terminalId: null, terminalName: null }),
    }),
    { name: 'erp-staff-terminal', storage: createJSONStorage(() => AsyncStorage) },
  ),
)

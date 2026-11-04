import { create } from 'zustand'

interface MenuState {
  open: boolean
  setOpen: (open: boolean) => void
}

/** Whether the side menu is open: the header button opens it, the menu closes itself. */
export const useMenuStore = create<MenuState>()((set) => ({ open: false, setOpen: (open) => set({ open }) }))

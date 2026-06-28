import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'
import { useLogout } from '@/hooks/useAuth'

export function Header() {
  const user = useAuthStore((s) => s.user)
  const { toggleSidebar } = useUiStore()
  const logout = useLogout()

  return (
    <header className="flex h-16 items-center justify-between border-b border-gray-200 bg-white px-6">
      <button
        onClick={toggleSidebar}
        className="rounded-md p-2 text-gray-500 hover:bg-gray-100"
        aria-label="Toggle sidebar"
      >
        ☰
      </button>

      <div className="flex items-center gap-4">
        <span className="text-sm text-gray-600">
          {user?.firstName} {user?.lastName}
        </span>
        <button
          onClick={logout}
          className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
        >
          Sign out
        </button>
      </div>
    </header>
  )
}

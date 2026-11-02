import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { axe } from '@/test/axe'

// The sign-in pages talk to the auth hooks and the SSO SDKs; here they are fakes so only the page itself is under test.
const login = { mutate: vi.fn(), isPending: false, error: null as unknown }
const verify = { mutate: vi.fn(), isPending: false, isError: false, data: undefined as undefined | { succeeded: boolean; errors?: string[] } }
const resend = { mutate: vi.fn(), isPending: false, isError: false, data: undefined as undefined | { succeeded: boolean; errors?: string[] } }
const twitterCallback = vi.fn()

vi.mock('@/hooks/useAuth', () => ({
  useLogin: () => login,
  useCompleteLogin: () => vi.fn(),
  useVerifyTwoFactor: () => verify,
  useResendTwoFactor: () => resend,
}))
vi.mock('@/api/auth.api', () => ({
  ADMIN_USER_TYPE: 2,
  authApi: { getLoginOptions: () => Promise.resolve({ googleEnabled: false, microsoftEnabled: false, facebookEnabled: false, twitterEnabled: false, twoFactorEnabled: false }), twitterCallback: (...a: unknown[]) => twitterCallback(...a) },
}))
vi.mock('@react-oauth/google', () => ({ GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>, GoogleLogin: () => null }))
vi.mock('@/lib/msalConfig', () => ({ getMsalInstance: () => ({}) }))
vi.mock('@/lib/facebookSdk', () => ({ loadFacebookSdk: () => Promise.resolve() }))

import { LoginPage } from './LoginPage'
import { TwoFactorPage } from './TwoFactorPage'
import { TwitterCallbackPage } from './TwitterCallbackPage'

function renderAt(ui: React.ReactElement, url = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[url]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  login.mutate.mockReset(); login.error = null; login.isPending = false
  verify.mutate.mockReset(); verify.isError = false; verify.data = undefined
  resend.mutate.mockReset(); resend.isError = false; resend.data = undefined
  twitterCallback.mockReset()
})

describe('Sign-in', () => {
  it('has labelled required fields, no accessibility violation and signs in with what was typed', async () => {
    const { container } = renderAt(<LoginPage />)
    expect(await axe(container)).toHaveNoViolations()
    const email = screen.getByLabelText(/Email/)
    const password = screen.getByLabelText(/Password/)
    expect(email).toBeRequired()

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(login.mutate).not.toHaveBeenCalled()

    await userEvent.type(email, 'admin@shop.test')
    await userEvent.type(password, 'secret')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(login.mutate).toHaveBeenCalledWith({ email: 'admin@shop.test', password: 'secret', userType: 2 })
  })

  it('a refused sign-in is announced as an alert', async () => {
    login.error = new Error('401')
    renderAt(<LoginPage />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
  })
})

describe('Two-factor', () => {
  it('sends the challenge and the code, and shows a wrong code as an alert', async () => {
    const { container, rerender } = renderAt(<TwoFactorPage />, '/login/2fa?challengeId=c1&method=Email')
    expect(await axe(container)).toHaveNoViolations()
    await userEvent.type(screen.getByLabelText(/Verification Code/), '123456')
    await userEvent.click(screen.getByRole('button', { name: 'Verify' }))
    expect(verify.mutate).toHaveBeenCalledWith({ challengeId: 'c1', code: '123456' })

    verify.data = { succeeded: false, errors: ['Invalid or expired code. Please try again.'] }
    rerender(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter initialEntries={['/login/2fa?challengeId=c1&method=Email']}>
          <TwoFactorPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )
    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid or expired code')
  })
})

describe('Twitter callback', () => {
  it('without the authorization parameters it says so and offers the sign-in page', async () => {
    const { container } = renderAt(<TwitterCallbackPage />, '/auth/twitter/callback')
    expect(await screen.findByRole('alert')).toHaveTextContent('Missing authorization parameters from Twitter.')
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
    expect(twitterCallback).not.toHaveBeenCalled()
  })

  it('with the parameters it completes the sign-in with a status message while it works', async () => {
    twitterCallback.mockReturnValue(new Promise(() => {}))
    renderAt(<TwitterCallbackPage />, '/auth/twitter/callback?code=a&state=b')
    expect(await screen.findByRole('status')).toBeInTheDocument()
    await waitFor(() => expect(twitterCallback).toHaveBeenCalledWith({ code: 'a', state: 'b' }))
  })
})

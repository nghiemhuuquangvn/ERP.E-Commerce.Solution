# Frontend Refactoring & Best Practices Guide

## Overview

This document outlines refactoring strategies, best practices, and modernizations for the React Admin and Vue Storefront applications to align with the latest backend microservices and ensure consistency.

---

## 📋 Audit & Assessment

### Current Stack

**Admin Dashboard (React):**
- React 18.3.1 ✅ (Latest stable)
- TypeScript 5.5.3 ✅ (Latest)
- Vite 5.4.8 ✅ (Latest)
- Zustand 5.0.0 ✅ (State management)
- React Router 6.26.2 ✅ (Routing)
- React Hook Form 7.53.0 ✅ (Form handling)
- Axios 1.7.7 ✅ (HTTP client)
- TanStack Query 5.56.2 ✅ (Data fetching)
- Tailwind CSS 3.4.12 ✅ (Styling)
- OAuth2 integrations: Google, Microsoft, Facebook ✅

**Storefront (Vue):**
- Vue 3.5.8 ✅ (Latest stable)
- TypeScript 5.5.3 ✅ (Latest)
- Vite 5.4.8 ✅ (Latest)
- Pinia 2.2.2 ✅ (State management)
- Vue Router 4.4.5 ✅ (Routing)
- Axios 1.7.7 ✅ (HTTP client)
- TanStack Vue Query 5.56.2 ✅ (Data fetching)
- Tailwind CSS 3.4.12 ✅ (Styling)

**Assessment:** Both apps are on latest/near-latest versions. Focus is on architecture, consistency, and feature alignment.

---

## 🎯 Refactoring Priorities

### Priority 1: API Layer Consistency

#### Issue
Different API client setups between admin and storefront.

#### Solution: Create Shared API Types

```typescript
// src/shared/types/api.types.ts (shared between frontend apps)
export interface PaginatedResponse<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

export interface ApiError {
  message: string
  errors?: Record<string, string[]>
  statusCode?: number
  timestamp?: string
}

export interface LoginRequest {
  email: string
  password: string
  userType?: number
}

export interface User {
  id: string
  email: string
  fullName?: string
  roles: string[]
  permissions: string[]
}

export interface TokenResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
}
```

**Implementation:**
1. Extract common types from existing API clients
2. Create `src/shared/types/` directory in both apps
3. Update API clients to use shared types
4. Establish linting rules to prevent duplication

---

### Priority 2: State Management Unification

#### Admin App (Zustand) - Refactor to Modules

```typescript
// src/stores/auth.store.ts
import { create } from 'zustand'
import { User, TokenResponse } from '@/shared/types/api.types'

interface AuthStore {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isLoading: boolean
  error: string | null
  
  setTokens: (access: string, refresh: string) => void
  setUser: (user: User) => void
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  refreshAccessToken: () => Promise<void>
}

export const useAuthStore = create<AuthStore>((set, get) => ({
  user: null,
  accessToken: localStorage.getItem('accessToken'),
  refreshToken: localStorage.getItem('refreshToken'),
  isLoading: false,
  error: null,
  
  setTokens: (access, refresh) => {
    localStorage.setItem('accessToken', access)
    localStorage.setItem('refreshToken', refresh)
    set({ accessToken: access, refreshToken: refresh })
  },
  
  setUser: (user) => set({ user }),
  
  login: async (email, password) => {
    set({ isLoading: true, error: null })
    try {
      // API call
      const response = await loginAPI(email, password)
      set({
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        user: response.user,
      })
    } catch (error) {
      set({ error: (error as Error).message })
      throw error
    } finally {
      set({ isLoading: false })
    }
  },
  
  logout: () => {
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    set({ user: null, accessToken: null, refreshToken: null })
  },
  
  refreshAccessToken: async () => {
    // Token refresh logic
  },
}))
```

#### Storefront (Pinia) - Align with Admin Patterns

```typescript
// src/stores/auth.ts (Pinia store)
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User, TokenResponse } from '@/shared/types/api.types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const accessToken = ref<string | null>(localStorage.getItem('accessToken'))
  const refreshToken = ref<string | null>(localStorage.getItem('refreshToken'))
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const isAuthenticated = computed(() => !!user.value && !!accessToken.value)

  const setTokens = (access: string, refresh: string) => {
    localStorage.setItem('accessToken', access)
    localStorage.setItem('refreshToken', refresh)
    accessToken.value = access
    refreshToken.value = refresh
  }

  const setUser = (newUser: User) => {
    user.value = newUser
  }

  const login = async (email: string, password: string) => {
    isLoading.value = true
    error.value = null
    try {
      const response = await loginAPI(email, password)
      setTokens(response.accessToken, response.refreshToken)
      setUser(response.user)
    } catch (err) {
      error.value = (err as Error).message
      throw err
    } finally {
      isLoading.value = false
    }
  }

  const logout = () => {
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    user.value = null
    accessToken.value = null
    refreshToken.value = null
  }

  return {
    user,
    accessToken,
    refreshToken,
    isLoading,
    error,
    isAuthenticated,
    setTokens,
    setUser,
    login,
    logout,
  }
})
```

**Benefits:**
- Consistent API across frameworks
- Same business logic in both apps
- Easier to debug and maintain

---

### Priority 3: API Client Standardization

#### Admin App - Improve Error Handling

```typescript
// src/api/client.ts
import axios, { AxiosError, AxiosInstance } from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'
import type { ApiError } from '@/shared/types/api.types'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'
const LANGUAGE_MAP: Record<string, string> = { en: 'en-US', vi: 'vi-VN' }

let isRefreshing = false
let refreshQueue: Array<(token: string) => void> = []

export const createApiClient = (): AxiosInstance => {
  const client = axios.create({
    baseURL: BASE_URL,
    timeout: 30000,
    headers: { 'Content-Type': 'application/json' },
  })

  // Request interceptor
  client.interceptors.request.use((config) => {
    const authStore = useAuthStore.getState()
    if (authStore.accessToken) {
      config.headers.Authorization = `Bearer ${authStore.accessToken}`
    }

    const uiStore = useUiStore.getState()
    config.headers['Accept-Language'] = LANGUAGE_MAP[uiStore.locale] ?? 'en-US'

    return config
  })

  // Response interceptor with proper error handling
  client.interceptors.response.use(
    (response) => response,
    async (error: AxiosError<ApiError>) => {
      const originalRequest = error.config as typeof error.config & { _retry?: boolean }

      // 401: Unauthorized - try to refresh token
      if (error.response?.status === 401 && !originalRequest?._retry) {
        if (!isRefreshing) {
          originalRequest._retry = true
          isRefreshing = true

          try {
            const authStore = useAuthStore.getState()
            const { data } = await axios.post<TokenResponse>(
              `${BASE_URL}/v1/auth/refresh`,
              { refreshToken: authStore.refreshToken }
            )

            authStore.setTokens(data.accessToken, data.refreshToken)
            refreshQueue.forEach((cb) => cb(data.accessToken))
            refreshQueue = []

            originalRequest.headers.Authorization = `Bearer ${data.accessToken}`
            return client(originalRequest)
          } catch (refreshError) {
            useAuthStore.getState().logout()
            return Promise.reject(refreshError)
          } finally {
            isRefreshing = false
          }
        }

        return new Promise((resolve) => {
          refreshQueue.push((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`
            resolve(client(originalRequest))
          })
        })
      }

      // 403: Forbidden
      if (error.response?.status === 403) {
        // Show permission denied error
      }

      // Network errors
      if (!error.response) {
        console.error('Network error:', error.message)
      }

      return Promise.reject(error)
    }
  )

  return client
}

export const apiClient = createApiClient()
```

---

### Priority 4: Form Management Consistency

#### Admin (React Hook Form) - Add Validation Layer

```typescript
// src/lib/validation.ts
import { z } from 'zod'

export const loginSchema = z.object({
  email: z.string().email('Invalid email address'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
})

export type LoginFormData = z.infer<typeof loginSchema>

// In component
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'

function LoginForm() {
  const { register, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <input {...register('email')} />
      {errors.email && <span>{errors.email.message}</span>}
    </form>
  )
}
```

#### Storefront (Vue) - Similar Pattern

```typescript
// src/lib/validation.ts
export const loginSchema = z.object({
  email: z.string().email('Invalid email address'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
})

// In component
import { ref } from 'vue'
import { loginSchema } from '@/lib/validation'

export default {
  setup() {
    const form = ref({ email: '', password: '' })
    const errors = ref({})

    const submit = async () => {
      try {
        await loginSchema.parseAsync(form.value)
        // Proceed with login
      } catch (error) {
        // Handle validation errors
      }
    }

    return { form, errors, submit }
  },
}
```

---

### Priority 5: Component Library Standardization

#### Create Shared Component Specs

```typescript
// src/components/Button.tsx (Admin - React)
interface ButtonProps {
  variant?: 'primary' | 'secondary' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  disabled?: boolean
  loading?: boolean
  onClick?: () => void
}

export const Button: React.FC<ButtonProps> = ({
  variant = 'primary',
  size = 'md',
  disabled = false,
  loading = false,
  onClick,
  children,
}) => {
  const baseClasses = 'font-medium rounded-lg transition-colors'
  const variantClasses = {
    primary: 'bg-blue-600 text-white hover:bg-blue-700',
    secondary: 'bg-gray-200 text-gray-900 hover:bg-gray-300',
    danger: 'bg-red-600 text-white hover:bg-red-700',
  }
  const sizeClasses = {
    sm: 'px-3 py-1.5 text-sm',
    md: 'px-4 py-2 text-base',
    lg: 'px-6 py-3 text-lg',
  }

  return (
    <button
      className={`${baseClasses} ${variantClasses[variant]} ${sizeClasses[size]} disabled:opacity-50`}
      disabled={disabled || loading}
      onClick={onClick}
    >
      {loading ? '...' : children}
    </button>
  )
}
```

```typescript
// src/components/Button.vue (Storefront - Vue)
<template>
  <button
    :class="[baseClasses, variantClasses, sizeClasses]"
    :disabled="disabled || loading"
    @click="$emit('click')"
  >
    {{ loading ? '...' : $slots.default }}
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue'

type Variant = 'primary' | 'secondary' | 'danger'
type Size = 'sm' | 'md' | 'lg'

interface Props {
  variant?: Variant
  size?: Size
  disabled?: boolean
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
})

const baseClasses = 'font-medium rounded-lg transition-colors'
const variantClasses = computed(() => ({
  primary: 'bg-blue-600 text-white hover:bg-blue-700',
  secondary: 'bg-gray-200 text-gray-900 hover:bg-gray-300',
  danger: 'bg-red-600 text-white hover:bg-red-700',
}[props.variant]))

const sizeClasses = computed(() => ({
  sm: 'px-3 py-1.5 text-sm',
  md: 'px-4 py-2 text-base',
  lg: 'px-6 py-3 text-lg',
}[props.size]))
</script>
```

---

## 🧪 Testing Strategy

### Unit Tests

```typescript
// src/stores/__tests__/auth.store.test.ts (Admin)
import { useAuthStore } from '../auth.store'
import { loginAPI } from '@/api/auth.api'

vi.mock('@/api/auth.api')

describe('AuthStore', () => {
  it('should set tokens on successful login', async () => {
    const store = useAuthStore()
    vi.mocked(loginAPI).mockResolvedValue({
      accessToken: 'token123',
      refreshToken: 'refresh123',
      user: { id: '1', email: 'test@example.com' },
    })

    await store.login('test@example.com', 'password123')

    expect(store.accessToken).toBe('token123')
    expect(store.user?.email).toBe('test@example.com')
  })
})
```

### E2E Tests

```typescript
// e2e/login.spec.ts
import { test, expect } from '@playwright/test'

test('user can login with valid credentials', async ({ page }) => {
  await page.goto('http://localhost:3000/login')
  
  await page.fill('input[type="email"]', 'superadmin@system.local')
  await page.fill('input[type="password"]', 'SuperAdmin@123')
  await page.click('button[type="submit"]')

  await page.waitForURL('http://localhost:3000/dashboard')
  await expect(page).toHaveTitle(/Dashboard/)
})
```

---

## 📦 Build & Deployment

### Optimize Build Output

```json
{
  "build": {
    "target": "es2020",
    "minify": "terser",
    "sourcemap": false,
    "rollupOptions": {
      "output": {
        "manualChunks": {
          "vendor": ["react", "react-dom"],
          "router": ["react-router-dom"],
          "forms": ["react-hook-form"]
        }
      }
    }
  }
}
```

### Docker Optimization

**Multi-stage build (Dockerfile):**
```dockerfile
# Build stage
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Runtime stage
FROM node:20-alpine
WORKDIR /app
RUN npm install -g http-server
COPY --from=builder /app/dist ./dist
EXPOSE 3000
CMD ["http-server", "dist", "-p", "3000", "-c-1"]
```

---

## ✅ Refactoring Checklist

### Phase 1: Foundation (Week 1)
- [ ] Extract shared API types
- [ ] Create shared validation schemas
- [ ] Standardize error handling
- [ ] Setup component design specs
- [ ] Document API patterns

### Phase 2: Admin App (Week 2)
- [ ] Refactor API client
- [ ] Update store structure
- [ ] Add Zod validation
- [ ] Implement shared components
- [ ] Add unit tests

### Phase 3: Storefront (Week 3)
- [ ] Align stores with admin patterns
- [ ] Update API client
- [ ] Add shared components
- [ ] Implement validation
- [ ] Add tests

### Phase 4: Integration & Testing (Week 4)
- [ ] E2E tests (Playwright)
- [ ] Cross-app integration tests
- [ ] Performance testing
- [ ] Accessibility audit
- [ ] Security review

### Phase 5: Optimization & Deployment (Week 5)
- [ ] Build optimization
- [ ] Lazy loading & code splitting
- [ ] Performance monitoring
- [ ] Docker builds
- [ ] CI/CD pipeline setup

---

## 📊 Metrics & Monitoring

### Performance Metrics
- Largest Contentful Paint (LCP): < 2.5s
- First Input Delay (FID): < 100ms
- Cumulative Layout Shift (CLS): < 0.1
- Time to Interactive (TTI): < 3.8s

### Code Quality
- TypeScript strict mode: enabled
- ESLint: zero errors
- Test coverage: > 80%
- Bundle size: < 500KB (gzip)

---

## 🚀 Next Steps

1. **Start with Priority 1:** Unify API types and client setup
2. **Implement Priority 2:** Standardize state management
3. **Test each phase:** Unit + E2E tests before moving on
4. **Document patterns:** Keep team aligned on conventions
5. **Performance tune:** Monitor metrics after each phase

---

**Status:** 📝 Ready for Implementation  
**Estimated Timeline:** 5 weeks  
**Last Updated:** June 30, 2026

import { apiClient, PRD_BASE } from './prdClient'

// CategoryDto (GetCategoriesQuery)
export interface Category {
  id: string
  name: string
  code?: string | null
  parentCategoryId?: string | null
  sortOrder: number
  languageCode: string
  createdAt: string
}

export interface ListCategoriesParams {
  /** Omit to get root-level categories only (ParentCategoryId == null) — see listAll(). */
  parentId?: string
  languageCode?: string
}

// CreateCategoryCommand(Name, ParentCategoryId, Code)
export interface CreateCategoryRequest {
  name: string
  parentCategoryId?: string | null
  code?: string
}

export interface CreateCategoryResponse {
  categoryId: string
  name: string
}

// UpdateCategoryCommand(Name, ParentCategoryId, Code) — all optional; the handler only
// applies a field when it is non-empty/non-null, so ParentCategoryId can be changed to
// another category but NOT cleared back to root (no API support for un-parenting).
export interface UpdateCategoryRequest {
  name?: string
  parentCategoryId?: string | null
  code?: string
}

// SetCategoryTranslationCommand(LanguageCode, Name) — categoryId is in the route.
// Not wired into CategoriesPage's UI yet (out of this task's scope), exposed here
// for a future translations tab.
export interface SetCategoryTranslationRequest {
  languageCode: string
  name: string
}

export const categoriesApi = {
  // GetCategoriesQuery: parentId omitted => root-level only; parentId set => direct
  // children of that category only. There is NO endpoint that returns every category
  // in one call.
  list: (params?: ListCategoriesParams) =>
    apiClient
      .get<Category[]>(`${PRD_BASE}/categories`, {
        params: { parentId: params?.parentId, languageCode: params?.languageCode },
      })
      .then((r) => r.data),

  /**
   * Client-side aggregation of the full category tree into a flat list, built by
   * walking level-by-level (root, then each root's children, then their children,
   * ...) since the backend has no "get all categories" endpoint. Fine for
   * reference-data-sized category trees; a visited-set guards against cycles.
   */
  listAll: async (languageCode?: string): Promise<Category[]> => {
    const all: Category[] = []
    const visited = new Set<string>()
    let frontier = await categoriesApi.list({ languageCode })

    while (frontier.length > 0) {
      const next: Category[] = []
      for (const cat of frontier) {
        if (visited.has(cat.id)) continue
        visited.add(cat.id)
        all.push(cat)
        const children = await categoriesApi.list({ parentId: cat.id, languageCode })
        next.push(...children)
      }
      frontier = next
    }

    return all
  },

  create: (body: CreateCategoryRequest) =>
    apiClient.post<CreateCategoryResponse>(`${PRD_BASE}/categories`, body).then((r) => r.data),

  update: (id: string, body: UpdateCategoryRequest) => apiClient.put(`${PRD_BASE}/categories/${id}`, body),

  setTranslation: (id: string, body: SetCategoryTranslationRequest) =>
    apiClient.put(`${PRD_BASE}/categories/${id}/translations`, body),
}

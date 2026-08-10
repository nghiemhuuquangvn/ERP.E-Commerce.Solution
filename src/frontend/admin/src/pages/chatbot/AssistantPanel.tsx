import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { chatbotApi } from '@/api/chatbot/chatbot.api'
import { useChatbotStore } from '@/stores/chatbot.store'
import { chatSignalR } from '@/services/chatSignalR'
import { usePermission } from '@/hooks/usePermission'

export function AssistantPanel() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canUse = hasPermission('chatbot:assistant:use')

  const {
    conversation,
    messages,
    connectionStatus,
    toolIndicator,
    error,
    isSending,
    isLoadingHistory,
    startConversation,
    openConversation,
    sendMessage,
  } = useChatbotStore()

  const [draft, setDraft] = useState('')
  const [showHistory, setShowHistory] = useState(false)
  const bottomRef = useRef<HTMLDivElement>(null)
  const initialized = useRef(false)

  const { data: conversations, refetch: refetchConversations } = useQuery({
    queryKey: ['chatbot', 'conversations'],
    queryFn: () => chatbotApi.listMyConversations(),
    enabled: canUse,
    staleTime: 15_000,
  })

  useEffect(() => {
    if (!canUse || initialized.current) return
    initialized.current = true
    void startConversation()
    return () => {
      void chatSignalR.disconnect()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canUse])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, toolIndicator])

  if (!canUse) {
    return <div className="flex h-40 items-center justify-center text-gray-400">{t('common.noPermission')}</div>
  }

  const conversationEnded = conversation != null && conversation.status !== 1

  const handleSend = async () => {
    if (!draft.trim() || isSending) return
    const content = draft
    setDraft('')
    await sendMessage(content)
  }

  const handleSelectConversation = async (id: string) => {
    setShowHistory(false)
    await openConversation(id)
  }

  const handleNewConversation = async () => {
    setShowHistory(false)
    await startConversation()
    void refetchConversations()
  }

  return (
    <div className="flex h-[calc(100vh-8rem)] gap-4">
      <div className="flex flex-1 flex-col card">
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <div>
            <h1 className="text-lg font-semibold text-gray-900">{t('chatbot.title')}</h1>
            <p className="text-xs text-gray-500">{t(`chatbot.status.${connectionStatus}`)}</p>
          </div>
          <div className="flex items-center gap-2">
            <button type="button" className="btn-outline" onClick={() => setShowHistory((v) => !v)}>
              {t('chatbot.history.toggle')}
            </button>
            <button type="button" className="btn-primary" onClick={handleNewConversation}>
              {t('chatbot.newConversation')}
            </button>
          </div>
        </div>

        {conversationEnded && (
          <div className="bg-yellow-50 px-4 py-2 text-xs text-yellow-700">{t('chatbot.conversationEnded')}</div>
        )}

        <div className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
          {isLoadingHistory && (
            <div className="flex justify-center text-sm text-gray-400">{t('common.loading')}</div>
          )}

          {!isLoadingHistory && messages.length === 0 && (
            <div className="flex h-full items-center justify-center text-sm text-gray-400">
              {t('chatbot.emptyState')}
            </div>
          )}

          {messages.map((m) => (
            <div key={m.id} className={`flex ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}>
              <div
                className={`max-w-[70%] whitespace-pre-wrap rounded-lg px-3 py-2 text-sm ${
                  m.role === 'user'
                    ? 'bg-primary-600 text-white'
                    : `bg-gray-100 text-gray-900 ${m.streaming ? 'border border-primary-300' : ''}`
                }`}
              >
                {m.content}
                {m.streaming && <span className="ml-0.5 inline-block animate-pulse">▍</span>}
              </div>
            </div>
          ))}

          {toolIndicator && (
            <div className="flex justify-start">
              <div className="flex items-center gap-2 rounded-lg bg-gray-50 px-3 py-2 text-xs text-gray-500">
                <span className="h-2 w-2 animate-pulse rounded-full bg-primary-400" />
                {t(toolIndicator.labelKey, { toolName: toolIndicator.toolName })}
              </div>
            </div>
          )}

          <div ref={bottomRef} />
        </div>

        {error && (
          <div className="border-t border-gray-200 bg-red-50 px-4 py-2 text-sm text-red-600">{t(error)}</div>
        )}

        <div className="flex items-end gap-2 border-t border-gray-200 p-3">
          <textarea
            className="input min-h-[44px] flex-1 resize-none"
            placeholder={t('chatbot.inputPlaceholder') as string}
            value={draft}
            disabled={conversationEnded}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                void handleSend()
              }
            }}
            rows={1}
          />
          <button
            type="button"
            className="btn-primary"
            disabled={!draft.trim() || isSending || conversationEnded}
            onClick={() => void handleSend()}
          >
            {t('chatbot.send')}
          </button>
        </div>
      </div>

      {showHistory && (
        <div className="w-72 shrink-0 overflow-y-auto card p-3">
          <h2 className="mb-2 text-sm font-semibold text-gray-900">{t('chatbot.history.title')}</h2>
          {!conversations?.length && <p className="text-xs text-gray-400">{t('common.noRecordsFound')}</p>}
          <ul className="space-y-1">
            {conversations?.map((c) => (
              <li key={c.id}>
                <button
                  type="button"
                  onClick={() => void handleSelectConversation(c.id)}
                  className={`w-full rounded-md px-2 py-2 text-left text-xs hover:bg-gray-50 ${
                    conversation?.id === c.id ? 'bg-primary-50 text-primary-700' : 'text-gray-600'
                  }`}
                >
                  <div className="font-medium">{new Date(c.startedAt).toLocaleString()}</div>
                  <div className="text-gray-400">{t(`chatbot.history.status.${c.status}`)}</div>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

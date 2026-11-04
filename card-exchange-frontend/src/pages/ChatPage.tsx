import { useState, useEffect, useRef, useCallback, useLayoutEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  ArrowLeft, Send, MessageSquare, Loader2, ArrowLeftRight,
} from 'lucide-react';
import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { messages } from '../api';
import { useAuth } from '../context/AuthContext';
import type { ConversationPreview, ChatMessage } from '../types';
import EmptyState from '../components/ui/EmptyState';
import { useFormat } from '../i18n/format';

const CHAT_HUB_URL = '/hubs/chat';

export default function ChatPage() {
  const { t } = useTranslation('chat');
  const { date, time } = useFormat();
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const preselectedUserId = searchParams.get('userId') ? Number(searchParams.get('userId')) : null;
  const preselectedTradeId = searchParams.get('tradeOfferId') ? Number(searchParams.get('tradeOfferId')) : null;
  // Apertura diretta da notifica push: /chat?conversationId=123
  const preselectedConversationId = searchParams.get('conversationId') ? Number(searchParams.get('conversationId')) : null;

  const [conversations, setConversations] = useState<ConversationPreview[]>([]);
  const [activeConvId, setActiveConvId] = useState<number | null>(null);
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
  const [newMessage, setNewMessage] = useState('');
  const [isLoadingConvs, setIsLoadingConvs] = useState(true);
  const [isLoadingMessages, setIsLoadingMessages] = useState(false);
  const [isSending, setIsSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const hubRef = useRef<HubConnection | null>(null);
  const prevConvRef = useRef<number | null>(null);

  const getToken = useCallback(() => {
    return localStorage.getItem('token') || '';
  }, []);

  // Conversazione aperta letta dai gestori SignalR (registrati una volta sola: evita valori obsoleti)
  const activeConvIdRef = useRef<number | null>(null);
  const conversationsRef = useRef<ConversationPreview[]>([]);
  useLayoutEffect(() => {
    activeConvIdRef.current = activeConvId;
    conversationsRef.current = conversations;
  }, [activeConvId, conversations]);

  const reloadConversations = useCallback(async () => {
    try {
      const { data } = await messages.getConversations();
      const list = Array.isArray(data) ? data : (data as any)?.conversations ?? [];
      setConversations(list);
      return list as ConversationPreview[];
    } catch (err) {
      console.error('Errore caricamento conversazioni:', err);
      return null;
    }
  }, []);

  /** Aggiunge un messaggio alla chat aperta (se è di quella conversazione) e aggiorna l'anteprima nella lista. */
  const applyIncomingMessage = useCallback((msg: any) => {
    if (!user || !msg) return;
    const isFromMe = msg.senderId === user.id;
    const chatMsg: ChatMessage = {
      id: msg.id, senderId: msg.senderId, senderUsername: msg.senderUsername,
      content: msg.content, isRead: msg.isRead ?? false,
      sentAt: msg.sentAt, isFromMe,
    };
    const isActive = msg.conversationId === activeConvIdRef.current;
    if (isActive) {
      setChatMessages((prev) => (prev.some((m) => m.id === chatMsg.id) ? prev : [...prev, chatMsg]));
      if (!isFromMe) hubRef.current?.invoke('MarkAsRead', msg.conversationId).catch(() => {});
    }

    const known = conversationsRef.current.some((c) => c.conversationId === msg.conversationId);
    setConversations((prev) => {
      if (!prev.some((c) => c.conversationId === msg.conversationId)) return prev;
      const updated = prev.map((c) =>
        c.conversationId === msg.conversationId
          ? { ...c, lastMessage: { content: msg.content, sentAt: msg.sentAt, isFromMe } }
          : c
      );
      // La conversazione più recente sale in cima
      return [...updated].sort((a, b) =>
        (b.conversationId === msg.conversationId ? 1 : 0) - (a.conversationId === msg.conversationId ? 1 : 0));
    });
    // Conversazione nuova (es. primo messaggio ricevuto): ricarica la lista
    if (!known) {
      reloadConversations().then((list) => {
        // Chat "nuova" già aperta con il mittente: si apre da sola la conversazione appena nata
        const otherId = isFromMe ? null : msg.senderId;
        if (list && !activeConvIdRef.current && preselectedUserId && otherId === preselectedUserId) {
          setActiveConvId(msg.conversationId);
        }
      });
    }
  }, [user, reloadConversations, preselectedUserId]);

  const applyIncomingRef = useRef(applyIncomingMessage);
  useLayoutEffect(() => { applyIncomingRef.current = applyIncomingMessage; }, [applyIncomingMessage]);

  const joinActiveConversation = useCallback(() => {
    const hub = hubRef.current;
    const convId = activeConvIdRef.current;
    if (!hub || hub.state !== 'Connected' || !convId) return;
    hub.invoke('JoinConversation', convId).catch(() => {});
    hub.invoke('MarkAsRead', convId).catch(() => {});
  }, []);

  // SignalR connection
  useEffect(() => {
    if (!user) return;
    const baseUrl = import.meta.env.VITE_API_URL || '';
    const connection = new HubConnectionBuilder()
      .withUrl(`${baseUrl}${CHAT_HUB_URL}`, {
        accessTokenFactory: () => getToken(),
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ReceiveMessage', (msg: any) => applyIncomingRef.current(msg));

    connection.on('NewMessageAlert', (alert: any) => {
      // La chat aperta viene già aggiornata da ReceiveMessage: qui si contano solo le altre
      if (alert.conversationId === activeConvIdRef.current) return;
      setConversations((prev) => prev.map((c) =>
        c.conversationId === alert.conversationId ? { ...c, unreadCount: c.unreadCount + 1 } : c
      ));
    });

    connection.on('MessagesRead', (data: any) => {
      if (data.conversationId === activeConvIdRef.current) {
        setChatMessages((prev) => prev.map((m) => m.isFromMe ? { ...m, isRead: true } : m));
      }
    });

    // Dopo una riconnessione si recupera quanto perso nel frattempo
    connection.onreconnected(() => {
      joinActiveConversation();
      reloadConversations();
      const convId = activeConvIdRef.current;
      if (convId) {
        messages.getMessages(convId).then(({ data }) => {
          if (convId !== activeConvIdRef.current) return;
          const msgs = data?.messages ?? [];
          setChatMessages(Array.isArray(msgs) ? [...msgs].reverse() : []);
        }).catch(() => {});
      }
    });

    hubRef.current = connection;
    connection.start()
      .then(() => joinActiveConversation())
      .catch((err) => console.warn('ChatHub connection failed:', err));

    return () => { connection.stop().catch(() => {}); hubRef.current = null; };
  }, [user, getToken, joinActiveConversation, reloadConversations]);

  // Join/leave conversation group
  useEffect(() => {
    const hub = hubRef.current;
    if (hub && hub.state === 'Connected' && prevConvRef.current && prevConvRef.current !== activeConvId) {
      hub.invoke('LeaveConversation', prevConvRef.current).catch(() => {});
    }
    joinActiveConversation();
    prevConvRef.current = activeConvId;
  }, [activeConvId, joinActiveConversation]);

  // Load conversations
  useEffect(() => {
    if (!user) return;
    setIsLoadingConvs(true);
    messages.getConversations()
      .then(({ data }) => {
        const list = Array.isArray(data) ? data : (data as any)?.conversations ?? [];
        setConversations(list);
        // Auto-open preselected or first conversation
        if (preselectedConversationId && list.some((c: ConversationPreview) => c.conversationId === preselectedConversationId)) {
          setActiveConvId(preselectedConversationId);
        } else if (preselectedUserId) {
          const match = list.find((c: ConversationPreview) => c.otherUser.id === preselectedUserId);
          if (match) setActiveConvId(match.conversationId);
        }
      })
      .catch((err) => console.error('Errore caricamento conversazioni:', err))
      .finally(() => setIsLoadingConvs(false));
  }, [user, preselectedUserId, preselectedConversationId]);

  // Load messages for active conversation
  useEffect(() => {
    if (!activeConvId) { setChatMessages([]); return; }
    setIsLoadingMessages(true);
    messages.getMessages(activeConvId)
      .then(({ data }) => {
        const msgs = data?.messages ?? (data as any)?.Messages ?? [];
        setChatMessages(Array.isArray(msgs) ? [...msgs].reverse() : []);
        // Update unread count
        setConversations((prev) => prev.map((c) =>
          c.conversationId === activeConvId ? { ...c, unreadCount: 0 } : c
        ));
      })
      .catch((err) => console.error('Errore caricamento messaggi:', err))
      .finally(() => setIsLoadingMessages(false));
  }, [activeConvId]);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [chatMessages]);

  const handleSend = async () => {
    if (!newMessage.trim() || isSending) return;
    const content = newMessage.trim();
    setNewMessage('');
    setSendError(null);

    const showError = (err: any) => {
      // HubException: il messaggio arriva come "... HubException: <testo>"
      const hubMsg = typeof err?.message === 'string' ? err.message.split('HubException: ').pop() : null;
      setSendError(err?.response?.data?.message || hubMsg || t('sendError'));
      setNewMessage(content);
    };

    const hub = hubRef.current;
    const activeConv = conversations.find((c) => c.conversationId === activeConvId);

    if (hub && hub.state === 'Connected' && activeConv) {
      // Via SignalR
      setIsSending(true);
      try {
        // L'hub restituisce il messaggio salvato: lo mostriamo subito senza attendere il broadcast
        const saved = await hub.invoke('SendMessage', activeConv.otherUser.id, content, null);
        applyIncomingMessage(saved);
      } catch (err: any) {
        showError(err);
      }
      setIsSending(false);
    } else if (preselectedUserId && !activeConvId) {
      // New conversation via REST
      setIsSending(true);
      try {
        const { data } = await messages.send(preselectedUserId, content, preselectedTradeId ?? undefined);
        await reloadConversations();
        // Il cambio di conversazione carica i messaggi, compreso quello appena inviato
        setActiveConvId(data.conversationId);
      } catch (err: any) {
        console.error('Errore invio messaggio:', err);
        showError(err);
      }
      setIsSending(false);
    } else if (activeConv) {
      setIsSending(true);
      try {
        const convId = activeConvId!;
        await messages.send(activeConv.otherUser.id, content);
        // Reload messages
        const { data } = await messages.getMessages(convId);
        const msgs = data?.messages ?? [];
        if (convId === activeConvIdRef.current) setChatMessages(Array.isArray(msgs) ? [...msgs].reverse() : []);
        reloadConversations();
      } catch (err: any) {
        showError(err);
      }
      setIsSending(false);
    }
  };

  const activeConv = conversations.find((c) => c.conversationId === activeConvId);

  // Responsive: on mobile show either conversation list or chat, on desktop show both
  const showingChat = !!activeConvId || (!!preselectedUserId && !activeConvId);

  return (
    <div className="flex flex-col h-[calc(100dvh-10.5rem)] md:flex-row md:gap-0 md:h-[calc(100vh-8rem)]">
      {/* === CONVERSATION LIST (sidebar on desktop, full on mobile when no chat open) === */}
      <div className={`${showingChat ? 'hidden md:flex' : 'flex'} flex-col w-full md:w-80 md:border-r md:border-border shrink-0`}>
        <div className="p-3 border-b border-border">
          <h1 className="text-lg font-bold text-text">{t('title')}</h1>
        </div>

        {isLoadingConvs ? (
          <div className="flex justify-center py-12">
            <Loader2 size={24} className="animate-spin text-primary" />
          </div>
        ) : conversations.length === 0 ? (
          <div className="flex-1 flex items-center justify-center p-4">
            <EmptyState
              icon={MessageSquare}
              title={t('empty.title')}
              description={t('empty.description')}
            />
          </div>
        ) : (
          <div className="flex-1 overflow-y-auto">
            {conversations.map((conv) => (
              <button
                key={conv.conversationId}
                onClick={() => setActiveConvId(conv.conversationId)}
                className={`w-full flex items-center gap-3 p-3 text-left hover:bg-surface-dark transition-colors border-b border-border/30 ${
                  activeConvId === conv.conversationId ? 'bg-primary/5 border-l-2 border-l-primary' : ''
                }`}
              >
                <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                  {conv.otherUser.avatarUrl ? (
                    <img src={conv.otherUser.avatarUrl} alt="" className="w-10 h-10 rounded-full object-cover" />
                  ) : (
                    <span className="text-sm font-bold text-primary">
                      {conv.otherUser.username[0].toUpperCase()}
                    </span>
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-semibold truncate">@{conv.otherUser.username}</span>
                    {conv.lastMessage && (
                      <span className="text-[10px] text-text-muted shrink-0">
                        {date(conv.lastMessage.sentAt, { day: 'numeric', month: 'short' })}
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-1 mt-0.5">
                    {conv.tradeOfferId && <ArrowLeftRight size={10} className="text-primary shrink-0" />}
                    <p className="text-xs text-text-muted truncate">
                      {conv.lastMessage
                        ? `${conv.lastMessage.isFromMe ? t('youPrefix') : ''}${conv.lastMessage.content}`
                        : t('startConversation')}
                    </p>
                    {conv.unreadCount > 0 && (
                      <span className="ml-auto bg-primary text-white text-[10px] font-bold min-w-[18px] h-[18px] rounded-full flex items-center justify-center px-1 shrink-0">
                        {conv.unreadCount}
                      </span>
                    )}
                  </div>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      {/* === CHAT AREA === */}
      <div className={`${showingChat ? 'flex' : 'hidden md:flex'} flex-col flex-1 min-w-0`}>
        {/* Chat header */}
        {(activeConv || preselectedUserId) ? (
          <>
            <div className="flex items-center gap-3 p-3 border-b border-border bg-white">
              <button
                onClick={() => setActiveConvId(null)}
                className="w-8 h-8 rounded-full bg-surface-dark flex items-center justify-center hover:bg-gray-200 md:hidden shrink-0"
              >
                <ArrowLeft size={16} />
              </button>
              <div className="w-9 h-9 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                <span className="text-sm font-bold text-primary">
                  {(activeConv?.otherUser.username || 'U')[0].toUpperCase()}
                </span>
              </div>
              <div className="min-w-0">
                <p className="text-sm font-semibold truncate">
                  @{activeConv?.otherUser.username || t('userFallback', { id: preselectedUserId })}
                </p>
                {activeConv?.tradeOfferId && (
                  <p className="text-[10px] text-primary flex items-center gap-1">
                    <ArrowLeftRight size={10} /> {t('linkedTrade', { id: activeConv.tradeOfferId })}
                  </p>
                )}
              </div>
            </div>

            {/* Messages */}
            <div className="flex-1 overflow-y-auto p-3 space-y-2 bg-gray-50/50">
              {isLoadingMessages ? (
                <div className="flex justify-center py-12">
                  <Loader2 size={24} className="animate-spin text-primary" />
                </div>
              ) : chatMessages.length === 0 ? (
                <div className="flex items-center justify-center h-full">
                  <p className="text-sm text-text-muted text-center">
                    {t('noMessages')}
                  </p>
                </div>
              ) : (
                <>
                  {chatMessages.map((msg) => (
                    <div
                      key={msg.id}
                      className={`flex ${msg.isFromMe ? 'justify-end' : 'justify-start'}`}
                    >
                      <div
                        className={`max-w-[75%] rounded-2xl px-3.5 py-2 ${
                          msg.isFromMe
                            ? 'bg-primary text-white rounded-br-md'
                            : 'bg-white text-text border border-border/50 rounded-bl-md'
                        }`}
                      >
                        <p className="text-sm whitespace-pre-wrap break-words">{msg.content}</p>
                        <div className={`flex items-center gap-1 mt-0.5 ${msg.isFromMe ? 'justify-end' : ''}`}>
                          <span className={`text-[10px] ${msg.isFromMe ? 'text-white/70' : 'text-text-muted'}`}>
                            {time(msg.sentAt, { hour: '2-digit', minute: '2-digit' })}
                          </span>
                          {msg.isFromMe && msg.isRead && (
                            <span className="text-[10px] text-white/70">✓✓</span>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                  <div ref={messagesEndRef} />
                </>
              )}
            </div>

            {/* Input */}
            <div className="p-3 border-t border-border bg-white">
              {sendError && (
                <p className="mb-2 text-xs text-red-600">{sendError}</p>
              )}
              <div className="flex items-end gap-2">
                <textarea
                  value={newMessage}
                  onChange={(e) => setNewMessage(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && !e.shiftKey) {
                      e.preventDefault();
                      handleSend();
                    }
                  }}
                  placeholder={t('inputPlaceholder')}
                  rows={1}
                  className="flex-1 px-4 py-2.5 rounded-2xl border border-border bg-surface-dark text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary/30 max-h-24"
                  style={{ minHeight: '40px' }}
                />
                <button
                  onClick={handleSend}
                  disabled={!newMessage.trim() || isSending}
                  className="w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed transition-colors shrink-0"
                >
                  <Send size={16} />
                </button>
              </div>
            </div>
          </>
        ) : (
          <div className="hidden md:flex flex-1 items-center justify-center bg-gray-50/30">
            <div className="text-center">
              <MessageSquare size={48} className="mx-auto text-text-muted mb-3" />
              <p className="text-sm text-text-muted">{t('selectConversation')}</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

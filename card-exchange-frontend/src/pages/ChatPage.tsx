import { useState, useEffect, useRef, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  ArrowLeft, Send, MessageSquare, Loader2, ArrowLeftRight,
} from 'lucide-react';
import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { messages } from '../api';
import { useAuth } from '../context/AuthContext';
import type { ConversationPreview, ChatMessage } from '../types';
import EmptyState from '../components/ui/EmptyState';

const CHAT_HUB_URL = '/hubs/chat';

export default function ChatPage() {
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const preselectedUserId = searchParams.get('userId') ? Number(searchParams.get('userId')) : null;
  const preselectedTradeId = searchParams.get('tradeOfferId') ? Number(searchParams.get('tradeOfferId')) : null;

  const [conversations, setConversations] = useState<ConversationPreview[]>([]);
  const [activeConvId, setActiveConvId] = useState<number | null>(null);
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
  const [newMessage, setNewMessage] = useState('');
  const [isLoadingConvs, setIsLoadingConvs] = useState(true);
  const [isLoadingMessages, setIsLoadingMessages] = useState(false);
  const [isSending, setIsSending] = useState(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const hubRef = useRef<HubConnection | null>(null);
  const prevConvRef = useRef<number | null>(null);

  const getToken = useCallback(() => {
    const raw = localStorage.getItem('auth_tokens');
    if (!raw) return '';
    try { return JSON.parse(raw).accessToken || ''; } catch { return ''; }
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

    connection.on('ReceiveMessage', (msg: any) => {
      const chatMsg: ChatMessage = {
        id: msg.id, senderId: msg.senderId, senderUsername: msg.senderUsername,
        content: msg.content, isRead: msg.isRead ?? false,
        sentAt: msg.sentAt, isFromMe: msg.senderId === user.id,
      };
      setChatMessages((prev) => [...prev, chatMsg]);
    });

    connection.on('NewMessageAlert', (alert: any) => {
      setConversations((prev) => prev.map((c) =>
        c.conversationId === alert.conversationId
          ? { ...c, unreadCount: c.unreadCount + 1, lastMessage: { content: alert.preview, sentAt: new Date().toISOString(), isFromMe: false } }
          : c
      ));
    });

    connection.on('MessagesRead', (data: any) => {
      if (data.conversationId === activeConvId) {
        setChatMessages((prev) => prev.map((m) => m.isFromMe ? { ...m, isRead: true } : m));
      }
    });

    connection.start().catch((err) => console.warn('ChatHub connection failed:', err));
    hubRef.current = connection;

    return () => { connection.stop().catch(() => {}); hubRef.current = null; };
  }, [user, getToken]);

  // Join/leave conversation group
  useEffect(() => {
    const hub = hubRef.current;
    if (!hub || hub.state !== 'Connected') return;

    if (prevConvRef.current && prevConvRef.current !== activeConvId) {
      hub.invoke('LeaveConversation', prevConvRef.current).catch(() => {});
    }
    if (activeConvId) {
      hub.invoke('JoinConversation', activeConvId).catch(() => {});
      hub.invoke('MarkAsRead', activeConvId).catch(() => {});
    }
    prevConvRef.current = activeConvId;
  }, [activeConvId]);

  // Load conversations
  useEffect(() => {
    if (!user) return;
    setIsLoadingConvs(true);
    messages.getConversations()
      .then(({ data }) => {
        const list = Array.isArray(data) ? data : (data as any)?.conversations ?? [];
        setConversations(list);
        // Auto-open preselected or first conversation
        if (preselectedUserId) {
          const match = list.find((c: ConversationPreview) => c.otherUser.id === preselectedUserId);
          if (match) setActiveConvId(match.conversationId);
        }
      })
      .catch((err) => console.error('Errore caricamento conversazioni:', err))
      .finally(() => setIsLoadingConvs(false));
  }, [user, preselectedUserId]);

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

    const hub = hubRef.current;
    const activeConv = conversations.find((c) => c.conversationId === activeConvId);

    if (hub && hub.state === 'Connected' && activeConv) {
      // Via SignalR
      setIsSending(true);
      try {
        await hub.invoke('SendMessage', activeConv.otherUser.id, content, null);
      } catch {
        // Fallback to REST
        await messages.send(activeConv.otherUser.id, content);
      }
      setIsSending(false);
    } else if (preselectedUserId && !activeConvId) {
      // New conversation via REST
      setIsSending(true);
      try {
        const { data } = await messages.send(preselectedUserId, content, preselectedTradeId ?? undefined);
        setActiveConvId(data.conversationId);
        // Reload conversations
        const { data: convs } = await messages.getConversations();
        setConversations(Array.isArray(convs) ? convs : []);
      } catch (err: any) {
        console.error('Errore invio messaggio:', err);
      }
      setIsSending(false);
    } else if (activeConv) {
      setIsSending(true);
      try {
        await messages.send(activeConv.otherUser.id, content);
        // Reload messages
        const { data } = await messages.getMessages(activeConvId!);
        const msgs = data?.messages ?? [];
        setChatMessages(Array.isArray(msgs) ? [...msgs].reverse() : []);
      } catch {}
      setIsSending(false);
    }
  };

  const activeConv = conversations.find((c) => c.conversationId === activeConvId);

  // Responsive: on mobile show either conversation list or chat, on desktop show both
  const showingChat = !!activeConvId || (!!preselectedUserId && !activeConvId);

  return (
    <div className="flex flex-col h-[calc(100vh-8rem)] md:flex-row md:gap-0 md:h-[calc(100vh-8rem)]">
      {/* === CONVERSATION LIST (sidebar on desktop, full on mobile when no chat open) === */}
      <div className={`${showingChat ? 'hidden md:flex' : 'flex'} flex-col w-full md:w-80 md:border-r md:border-border shrink-0`}>
        <div className="p-3 border-b border-border">
          <h1 className="text-lg font-bold text-text">Chat</h1>
        </div>

        {isLoadingConvs ? (
          <div className="flex justify-center py-12">
            <Loader2 size={24} className="animate-spin text-primary" />
          </div>
        ) : conversations.length === 0 ? (
          <div className="flex-1 flex items-center justify-center p-4">
            <EmptyState
              icon={MessageSquare}
              title="Nessuna conversazione"
              description="Le chat appariranno qui quando inizierai a messaggiare con altri utenti"
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
                        {new Date(conv.lastMessage.sentAt).toLocaleDateString('it-IT', { day: 'numeric', month: 'short' })}
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-1 mt-0.5">
                    {conv.tradeOfferId && <ArrowLeftRight size={10} className="text-primary shrink-0" />}
                    <p className="text-xs text-text-muted truncate">
                      {conv.lastMessage
                        ? `${conv.lastMessage.isFromMe ? 'Tu: ' : ''}${conv.lastMessage.content}`
                        : 'Inizia una conversazione'}
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
                  @{activeConv?.otherUser.username || `Utente #${preselectedUserId}`}
                </p>
                {activeConv?.tradeOfferId && (
                  <p className="text-[10px] text-primary flex items-center gap-1">
                    <ArrowLeftRight size={10} /> Collegata a scambio #{activeConv.tradeOfferId}
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
                    Nessun messaggio. Scrivi per iniziare la conversazione.
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
                            {new Date(msg.sentAt).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
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
                  placeholder="Scrivi un messaggio..."
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
              <p className="text-sm text-text-muted">Seleziona una conversazione</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

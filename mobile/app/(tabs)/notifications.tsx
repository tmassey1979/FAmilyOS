import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { Bell, CheckCheck } from 'lucide-react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { notificationsApi, type NotificationDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';
import { withObserveScreen } from '../../src/analytics/withObserveScreen';

type Filter = 'all' | 'unread';

function routeForNotification(n: NotificationDto): string {
  const kind = (n.relatedEntityType ?? n.type ?? '').toLowerCase();
  if (kind.includes('request')) return '/(tabs)/requests';
  if (kind.includes('task')) return '/(tabs)/tasks';
  if (kind.includes('procure') || kind.includes('product') || kind.includes('cart') || kind.includes('shop')) return '/(tabs)/family';
  if (kind.includes('calendar') || kind.includes('event')) return '/(tabs)/calendar';
  if (kind.includes('chat')) return '/(tabs)/chat';
  if (kind.includes('meal')) return '/(tabs)/meals';
  return '/(tabs)';
}

function NotificationInboxScreen() {
  const router = useRouter();
  const [items, setItems] = useState<NotificationDto[]>([]);
  const [filter, setFilter] = useState<Filter>('all');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true);
    else setLoading(true);
    setError(null);
    try {
      setItems(await notificationsApi.mine());
    } catch (e) {
      setError((e as { error?: string })?.error ?? 'Could not load notifications.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const openItem = async (item: NotificationDto) => {
    if (!item.isRead) {
      try {
        await notificationsApi.markRead(item.id);
        setItems((current) => current.map((x) => x.id === item.id ? { ...x, isRead: true } : x));
      } catch {
        setError('Could not mark this notification as read.');
      }
    }
    router.push(routeForNotification(item) as never);
  };

  const markAllRead = async () => {
    setBusy(true);
    setError(null);
    try {
      await notificationsApi.markAllRead();
      setItems((current) => current.map((item) => ({ ...item, isRead: true })));
    } catch (e) {
      setError((e as { error?: string })?.error ?? 'Could not mark notifications as read.');
    } finally {
      setBusy(false);
    }
  };

  const unreadCount = items.filter((item) => !item.isRead).length;
  const visible = filter === 'unread' ? items.filter((item) => !item.isRead) : items;

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} />}
    >
      <View style={styles.headingRow}>
        <View style={styles.headingIcon}><Bell color={colors.primary} size={22} /></View>
        <View style={styles.headingCopy}>
          <SectionTitle>Notifications</SectionTitle>
          <Text style={typography.caption}>{unreadCount} unread · latest {items.length}</Text>
        </View>
        <Button title={busy ? '…' : 'Mark all read'} variant="secondary" onPress={() => void markAllRead()} disabled={busy || unreadCount === 0} />
      </View>

      <View style={styles.filters}>
        <Pressable accessibilityRole="button" accessibilityState={{ selected: filter === 'all' }} onPress={() => setFilter('all')} style={[styles.filter, filter === 'all' && styles.filterActive]}>
          <Text style={[styles.filterText, filter === 'all' && styles.filterTextActive]}>All</Text>
        </Pressable>
        <Pressable accessibilityRole="button" accessibilityState={{ selected: filter === 'unread' }} onPress={() => setFilter('unread')} style={[styles.filter, filter === 'unread' && styles.filterActive]}>
          <Text style={[styles.filterText, filter === 'unread' && styles.filterTextActive]}>Unread {unreadCount > 0 ? `· ${unreadCount}` : ''}</Text>
        </Pressable>
      </View>

      {error ? <Text style={styles.error}>{error}</Text> : null}
      {loading ? <ActivityIndicator color={colors.primary} /> : visible.length === 0 ? (
        <EmptyState title={filter === 'unread' ? 'All caught up' : 'No notifications yet'} />
      ) : visible.map((item) => (
        <Pressable key={item.id} accessibilityRole="button" onPress={() => void openItem(item)}>
          <Card style={[styles.card, !item.isRead ? styles.unreadCard : undefined]}>
            <View style={styles.itemTop}>
              <Text style={styles.itemTitle}>{item.title}</Text>
              {!item.isRead ? <View style={styles.unreadDot} /> : null}
            </View>
            <Text style={styles.body}>{item.body}</Text>
            <View style={styles.meta}>
              <Badge label={item.type} />
              <Text style={typography.caption}>{new Date(item.createdAtUtc).toLocaleString()}</Text>
            </View>
          </Card>
        </Pressable>
      ))}
      {items.length > 0 ? (
        <View style={styles.footer}>
          <CheckCheck color={colors.textMuted} size={16} />
          <Text style={typography.caption}>Showing the 50 most recent notifications</Text>
        </View>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.md },
  headingRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  headingIcon: { width: 42, height: 42, borderRadius: 12, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surfaceAlt },
  headingCopy: { flex: 1 },
  filters: { flexDirection: 'row', gap: spacing.sm },
  filter: { paddingHorizontal: spacing.md, paddingVertical: spacing.sm, borderRadius: 18, borderWidth: 1, borderColor: colors.border },
  filterActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  filterText: { ...typography.caption, color: colors.text },
  filterTextActive: { color: '#fff', fontWeight: '700' },
  card: { marginBottom: spacing.sm, gap: spacing.sm },
  unreadCard: { borderWidth: 1, borderColor: colors.primary },
  itemTop: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  itemTitle: { ...typography.body, fontWeight: '700', flex: 1 },
  unreadDot: { width: 8, height: 8, borderRadius: 4, backgroundColor: colors.primary },
  body: { ...typography.body, color: colors.textMuted },
  meta: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm, flexWrap: 'wrap' },
  footer: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.xs, paddingVertical: spacing.sm },
  error: { ...typography.caption, color: colors.danger },
});
export default withObserveScreen(NotificationInboxScreen);

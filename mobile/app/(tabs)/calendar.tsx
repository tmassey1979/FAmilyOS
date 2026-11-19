import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  calendarApi,
  tasksApi,
  type CalendarEventDto,
  type TaskDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';
import { withObserveScreen } from '../../src/analytics/withObserveScreen';

const OPEN_TASK_STATUSES = new Set([
  'Assigned',
  'Reassigned',
  'Accepted',
  'InProgress',
  'Paused',
  'Deferred',
]);

function CalendarScreen() {
  const router = useRouter();
  const [events, setEvents] = useState<CalendarEventDto[]>([]);
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingEvent, setEditingEvent] = useState<CalendarEventDto | null>(null);
  const [title, setTitle] = useState('');
  const [location, setLocation] = useState('');
  const [startLocal, setStartLocal] = useState('');
  const [endLocal, setEndLocal] = useState('');
  const [allDay, setAllDay] = useState(false);
  const [linkedTaskId, setLinkedTaskId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [windowStart, setWindowStart] = useState(() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
  });

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const rangeEnd = new Date(windowStart.getTime());
      rangeEnd.setDate(rangeEnd.getDate() + 14);
      const [eventList, taskList] = await Promise.all([
        calendarApi.range(windowStart.toISOString(), rangeEnd.toISOString()),
        tasksApi.family().catch(() => [] as TaskDto[]),
      ]);
      setEvents(eventList);
      setTasks(taskList);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load calendar');
      setEvents([]);
    } finally {
      setRefreshing(false);
    }
  }, [windowStart]);

  useEffect(() => {
    void load();
  }, [load]);

  const openTasks = useMemo(
    () =>
      tasks
        .filter((task) => OPEN_TASK_STATUSES.has(task.status))
        .sort((a, b) => a.title.localeCompare(b.title)),
    [tasks],
  );

  const taskTitleById = useMemo(() => {
    const map = new Map<string, string>();
    for (const task of tasks) map.set(task.id, task.title);
    return map;
  }, [tasks]);

  const moveWindow = (days: number) =>
    setWindowStart((current) => {
      const next = new Date(current);
      next.setDate(next.getDate() + days);
      return next;
    });

  const goToToday = () =>
    setWindowStart(() => {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      return today;
    });

  const labelEnd = new Date(windowStart.getTime());
  labelEnd.setDate(labelEnd.getDate() + 13);
  const rangeLabel = `${windowStart.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} – ${labelEnd.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })}`;

  const groupedEvents = events.reduce<Record<string, CalendarEventDto[]>>((groups, event) => {
    const date = new Date(event.startUtc);
    const key = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
    (groups[key] ??= []).push(event);
    return groups;
  }, {});
  const eventDays = Object.keys(groupedEvents).sort();

  const formatWhen = (iso: string, isAllDay: boolean) => {
    const d = new Date(iso);
    if (isAllDay) return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
    return d.toLocaleString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  const toLocalInput = (value: Date, asAllDay = false) => {
    const pad = (n: number) => String(n).padStart(2, '0');
    if (asAllDay) {
      return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
    }
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())} ${pad(value.getHours())}:${pad(value.getMinutes())}`;
  };

  const parseLocalInput = (value: string): string | null => {
    const match = /^(\d{4})-(\d{2})-(\d{2})(?: (\d{2}):(\d{2}))?$/.exec(value.trim());
    if (!match) return null;
    const [, year, month, day, hour = '00', minute = '00'] = match;
    const parsed = new Date(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute));
    if (
      parsed.getFullYear() !== Number(year) ||
      parsed.getMonth() !== Number(month) - 1 ||
      parsed.getDate() !== Number(day) ||
      parsed.getHours() !== Number(hour) ||
      parsed.getMinutes() !== Number(minute)
    ) {
      return null;
    }
    return parsed.toISOString();
  };

  const openCreate = () => {
    const start = new Date(Date.now() + 2 * 3600_000);
    const end = new Date(start.getTime() + 3600_000);
    setEditingEvent(null);
    setAllDay(false);
    setLinkedTaskId(null);
    setTitle('');
    setLocation('');
    setStartLocal(toLocalInput(start));
    setEndLocal(toLocalInput(end));
    setError(null);
    setEditorOpen(true);
  };

  const openEdit = (event: CalendarEventDto) => {
    setEditingEvent(event);
    setAllDay(event.allDay);
    setLinkedTaskId(event.linkedTaskId ?? null);
    setTitle(event.title);
    setLocation(event.location ?? '');
    setStartLocal(toLocalInput(new Date(event.startUtc), event.allDay));
    setEndLocal(event.endUtc ? toLocalInput(new Date(event.endUtc), event.allDay) : '');
    setError(null);
    setEditorOpen(true);
  };

  const saveEvent = async () => {
    if (!title.trim()) {
      setError('Enter an event title.');
      return;
    }
    if (
      !allDay &&
      (!/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/.test(startLocal.trim()) ||
        !/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/.test(endLocal.trim()))
    ) {
      setError('Timed events need start and end in YYYY-MM-DD HH:MM format.');
      return;
    }

    let startUtc = parseLocalInput(startLocal);
    let endUtc = endLocal.trim() ? parseLocalInput(endLocal) : null;
    if (!startUtc || (!allDay && !endUtc) || (endLocal.trim() && !endUtc)) {
      setError(
        allDay
          ? 'Enter valid dates using YYYY-MM-DD.'
          : 'Enter valid local dates and times using YYYY-MM-DD HH:MM.',
      );
      return;
    }

    if (allDay) {
      const startDate = new Date(startUtc);
      startDate.setHours(0, 0, 0, 0);
      startUtc = startDate.toISOString();
      if (endUtc) {
        const endDate = new Date(endUtc);
        endDate.setHours(0, 0, 0, 0);
        if (endDate.getTime() === startDate.getTime()) endDate.setDate(endDate.getDate() + 1);
        endUtc = endDate.toISOString();
      }
    }

    if (endUtc && new Date(endUtc).getTime() <= new Date(startUtc).getTime()) {
      setError('The event end must be after its start.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const body = {
        title: title.trim(),
        startUtc,
        endUtc,
        allDay,
        location: location.trim() || null,
        ...(!editingEvent ? { linkedTaskId: linkedTaskId || null } : {}),
      };
      if (editingEvent) await calendarApi.update(editingEvent.id, body);
      else await calendarApi.create(body);
      setEditorOpen(false);
      setEditingEvent(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? (editingEvent ? 'Update failed' : 'Create failed'));
    } finally {
      setSaving(false);
    }
  };

  const cancelEvent = async (id: string) => {
    try {
      await calendarApi.cancel(id);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Cancel failed');
    }
  };

  const renderEvent = (e: CalendarEventDto) => {
    const linkedTitle = e.linkedTaskId ? taskTitleById.get(e.linkedTaskId) : null;
    return (
      <Card key={e.id} style={styles.card}>
        <Text style={typography.heading}>
          {formatWhen(e.startUtc, e.allDay)} · {e.title}
        </Text>
        {e.location ? <Text style={typography.caption}>{e.location}</Text> : null}
        {e.linkedTaskId ? (
          <Pressable onPress={() => router.push('/(tabs)/tasks')}>
            <Text style={[typography.caption, styles.linkText]}>
              Linked task{linkedTitle ? `: ${linkedTitle}` : ''}
            </Text>
          </Pressable>
        ) : null}
        {e.linkedRequestId ? (
          <Pressable onPress={() => router.push('/(tabs)/requests')}>
            <Text style={[typography.caption, styles.linkText]}>Linked request</Text>
          </Pressable>
        ) : null}
        <View style={styles.eventActions}>
          <Button title="Edit" variant="secondary" onPress={() => openEdit(e)} />
          <Button title="Cancel event" variant="secondary" onPress={() => void cancelEvent(e.id)} />
        </View>
      </Card>
    );
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      <View style={styles.toolbar}>
        <SectionTitle>Calendar</SectionTitle>
        <Button title="New event" onPress={openCreate} />
      </View>
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {refreshing && events.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}

      <View style={styles.rangeToolbar}>
        <Button title="‹ Back 14 days" variant="secondary" onPress={() => moveWindow(-14)} />
        <View style={styles.rangeLabel}>
          <Text style={typography.body}>{rangeLabel}</Text>
          <Button title="Today" variant="secondary" onPress={goToToday} />
        </View>
        <Button title="Next 14 days ›" variant="secondary" onPress={() => moveWindow(14)} />
      </View>

      {events.length === 0 ? (
        <EmptyState title="No events in this period" subtitle="Try another date range or add an event." />
      ) : (
        eventDays.map((day) => {
          const date = new Date(`${day}T12:00:00`);
          return (
            <View key={day}>
              <SectionTitle>
                {date.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
              </SectionTitle>
              {groupedEvents[day].map(renderEvent)}
            </View>
          );
        })
      )}

      <Modal visible={editorOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>{editingEvent ? 'Edit event' : 'New event'}</Text>
            <TextInput
              style={styles.input}
              placeholder="Title"
              placeholderTextColor={colors.textMuted}
              value={title}
              onChangeText={setTitle}
            />
            <TextInput
              style={styles.input}
              placeholder="Location (optional)"
              placeholderTextColor={colors.textMuted}
              value={location}
              onChangeText={setLocation}
            />

            <View style={styles.allDayRow}>
              <Text style={typography.body}>All-day event</Text>
              <Switch
                value={allDay}
                onValueChange={(value) => {
                  setAllDay(value);
                  if (value) {
                    setStartLocal((current) => current.slice(0, 10));
                    setEndLocal((current) => (current ? current.slice(0, 10) : ''));
                  }
                }}
                trackColor={{ true: colors.primary, false: colors.border }}
              />
            </View>

            {!editingEvent ? (
              <View style={{ gap: spacing.xs }}>
                <Text style={typography.caption}>Link to an open family task (optional)</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.taskChoices}>
                  <Pressable
                    onPress={() => setLinkedTaskId(null)}
                    style={[styles.chip, linkedTaskId == null && styles.chipActive]}
                  >
                    <Text style={typography.body}>None</Text>
                  </Pressable>
                  {openTasks.map((task) => (
                    <Pressable
                      key={task.id}
                      onPress={() => {
                        setLinkedTaskId(task.id);
                        if (!title.trim()) setTitle(task.title);
                      }}
                      style={[styles.chip, linkedTaskId === task.id && styles.chipActive]}
                    >
                      <Text style={typography.body} numberOfLines={1}>
                        {task.title}
                      </Text>
                    </Pressable>
                  ))}
                </ScrollView>
              </View>
            ) : editingEvent.linkedTaskId ? (
              <Text style={typography.caption}>
                Linked task
                {taskTitleById.get(editingEvent.linkedTaskId)
                  ? `: ${taskTitleById.get(editingEvent.linkedTaskId)}`
                  : ''}
                . Task links are fixed after create.
              </Text>
            ) : null}

            <Text style={typography.caption}>
              {allDay
                ? 'All-day dates use your device local calendar day. Leave the end blank for a single-day event.'
                : "Times use your device's local time."}
            </Text>
            <TextInput
              style={styles.input}
              placeholder={allDay ? 'Start date · YYYY-MM-DD' : 'Start · YYYY-MM-DD HH:MM'}
              placeholderTextColor={colors.textMuted}
              value={startLocal}
              onChangeText={setStartLocal}
              autoCapitalize="none"
            />
            <TextInput
              style={styles.input}
              placeholder={allDay ? 'End date · YYYY-MM-DD (optional)' : 'End · YYYY-MM-DD HH:MM'}
              placeholderTextColor={colors.textMuted}
              value={endLocal}
              onChangeText={setEndLocal}
              autoCapitalize="none"
            />
            {error ? <Text style={styles.hint}>{error}</Text> : null}
            <View style={styles.modalActions}>
              <Button
                title="Cancel"
                variant="secondary"
                onPress={() => {
                  setEditorOpen(false);
                  setEditingEvent(null);
                }}
              />
              <Button
                title={saving ? 'Saving…' : editingEvent ? 'Save changes' : 'Create'}
                onPress={() => void saveEvent()}
                disabled={saving}
              />
            </View>
          </View>
        </View>
      </Modal>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  toolbar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  rangeToolbar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.xs,
    marginBottom: spacing.md,
  },
  rangeLabel: { alignItems: 'center', gap: spacing.xs, flex: 1 },
  card: { marginBottom: spacing.md, gap: spacing.xs },
  eventActions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  linkText: { color: colors.primary },
  allDayRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  taskChoices: { gap: spacing.sm, paddingVertical: spacing.xs },
  chip: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    backgroundColor: colors.bg,
    maxWidth: 220,
  },
  chipActive: {
    borderColor: colors.primary,
    backgroundColor: colors.surface,
  },
  hint: { ...typography.caption, marginBottom: spacing.md, color: colors.danger },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    padding: spacing.lg,
    gap: spacing.md,
    maxHeight: '92%',
  },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: spacing.sm,
    color: colors.text,
    backgroundColor: colors.bg,
  },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
});

export default withObserveScreen(CalendarScreen);

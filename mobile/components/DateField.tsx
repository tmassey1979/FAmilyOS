import { useMemo, useState } from 'react';
import { Modal, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import DateTimePicker, { type DateTimePickerEvent } from '@react-native-community/datetimepicker';
import { colors, spacing, typography } from '../src/theme';

const radius = { md: 10, lg: 16 } as const;

type Mode = 'date' | 'time' | 'datetime';

type Props = {
  label?: string;
  value: string;
  /** date → YYYY-MM-DD; time → HH:MM; datetime → YYYY-MM-DD HH:MM (local) */
  mode?: Mode;
  placeholder?: string;
  onChange: (next: string) => void;
  allowClear?: boolean;
};

function pad(n: number) {
  return n.toString().padStart(2, '0');
}

function formatLocal(d: Date, mode: Mode): string {
  const y = d.getFullYear();
  const m = pad(d.getMonth() + 1);
  const day = pad(d.getDate());
  const h = pad(d.getHours());
  const min = pad(d.getMinutes());
  if (mode === 'date') return `${y}-${m}-${day}`;
  if (mode === 'time') return `${h}:${min}`;
  return `${y}-${m}-${day} ${h}:${min}`;
}

function parseLocal(value: string, mode: Mode): Date {
  const now = new Date();
  if (!value.trim()) return now;
  if (mode === 'time') {
    const m = value.trim().match(/^(\d{1,2}):(\d{2})$/);
    if (m) {
      const d = new Date();
      d.setHours(Number(m[1]), Number(m[2]), 0, 0);
      return d;
    }
    return now;
  }
  if (mode === 'date') {
    const m = value.trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), 12, 0, 0, 0);
    return now;
  }
  const m = value.trim().match(/^(\d{4})-(\d{2})-(\d{2})[ T](\d{1,2}):(\d{2})$/);
  if (m) {
    return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), Number(m[4]), Number(m[5]), 0, 0);
  }
  const dOnly = value.trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (dOnly) return new Date(Number(dOnly[1]), Number(dOnly[2]) - 1, Number(dOnly[3]), 9, 0, 0, 0);
  return now;
}

/** Simple tap-to-open native date/time control — no typed YYYY-MM-DD. */
export function DateField({
  label,
  value,
  mode = 'date',
  placeholder,
  onChange,
  allowClear = true,
}: Props) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(() => parseLocal(value, mode));
  const display = useMemo(() => (value.trim() ? value.trim() : ''), [value]);

  const openPicker = () => {
    setDraft(parseLocal(value, mode));
    setOpen(true);
  };

  const commit = (d: Date) => {
    onChange(formatLocal(d, mode));
    setOpen(false);
  };

  const onAndroidChange = (event: DateTimePickerEvent, selected?: Date) => {
    if (event.type === 'dismissed') {
      setOpen(false);
      return;
    }
    if (selected) commit(selected);
  };

  const pickerMode = mode === 'datetime' ? (Platform.OS === 'ios' ? 'datetime' : 'date') : mode;

  return (
    <View style={styles.wrap}>
      {label ? <Text style={styles.label}>{label}</Text> : null}
      <Pressable onPress={openPicker} style={styles.field} accessibilityRole="button">
        <Text style={display ? styles.value : styles.placeholder}>
          {display || placeholder || (mode === 'time' ? 'Pick a time' : mode === 'datetime' ? 'Pick date & time' : 'Pick a date')}
        </Text>
      </Pressable>
      {allowClear && display ? (
        <Pressable onPress={() => onChange('')} style={styles.clear} accessibilityRole="button">
          <Text style={styles.clearText}>Clear</Text>
        </Pressable>
      ) : null}

      {open && Platform.OS === 'android' ? (
        <DateTimePicker value={draft} mode={pickerMode === 'datetime' ? 'date' : pickerMode} onChange={onAndroidChange} />
      ) : null}

      {open && Platform.OS === 'ios' ? (
        <Modal transparent animationType="slide" visible onRequestClose={() => setOpen(false)}>
          <View style={styles.modalBackdrop}>
            <View style={styles.modalCard}>
              <Text style={styles.modalTitle}>{label || 'Choose'}</Text>
              <DateTimePicker
                value={draft}
                mode={pickerMode}
                display="spinner"
                onChange={(_, selected) => {
                  if (selected) setDraft(selected);
                }}
              />
              <View style={styles.modalActions}>
                <Pressable onPress={() => setOpen(false)} style={styles.modalBtn}>
                  <Text style={styles.modalBtnText}>Cancel</Text>
                </Pressable>
                <Pressable onPress={() => commit(draft)} style={[styles.modalBtn, styles.modalBtnPrimary]}>
                  <Text style={[styles.modalBtnText, styles.modalBtnPrimaryText]}>Done</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { gap: spacing.xs },
  label: { ...typography.caption, color: colors.textMuted },
  field: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm + 2,
    backgroundColor: colors.surface,
  },
  value: { ...typography.body, color: colors.text },
  placeholder: { ...typography.body, color: colors.textMuted },
  clear: { alignSelf: 'flex-start', paddingVertical: 2 },
  clearText: { ...typography.caption, color: colors.primary },
  modalBackdrop: {
    flex: 1,
    justifyContent: 'flex-end',
    backgroundColor: 'rgba(0,0,0,0.35)',
  },
  modalCard: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: radius.lg,
    borderTopRightRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.sm,
  },
  modalTitle: { ...typography.heading, color: colors.text },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
  modalBtn: { paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  modalBtnPrimary: { backgroundColor: colors.primary, borderRadius: radius.md },
  modalBtnText: { ...typography.body, color: colors.text },
  modalBtnPrimaryText: { color: '#fff', fontWeight: '600' },
});

import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
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

/**
 * Lightweight date/time field without native picker dependency.
 * Keeps package-lock in sync for CI (`npm ci`). Native picker can be
 * restored once `@react-native-community/datetimepicker` is added with a
 * regenerated lockfile.
 */
export function DateField({
  label,
  value,
  mode = 'date',
  placeholder,
  onChange,
  allowClear = true,
}: Props) {
  const hint =
    placeholder ||
    (mode === 'time'
      ? 'HH:MM'
      : mode === 'datetime'
        ? 'YYYY-MM-DD HH:MM'
        : 'YYYY-MM-DD');

  return (
    <View style={styles.wrap}>
      {label ? <Text style={styles.label}>{label}</Text> : null}
      <TextInput
        value={value}
        onChangeText={onChange}
        placeholder={hint}
        placeholderTextColor={colors.textMuted}
        style={styles.field}
        autoCapitalize="none"
        autoCorrect={false}
        keyboardType={mode === 'time' ? 'numbers-and-punctuation' : 'numbers-and-punctuation'}
      />
      {allowClear && value.trim() ? (
        <Pressable onPress={() => onChange('')} style={styles.clear} accessibilityRole="button">
          <Text style={styles.clearText}>Clear</Text>
        </Pressable>
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
    ...typography.body,
    color: colors.text,
  },
  clear: { alignSelf: 'flex-start', paddingVertical: 2 },
  clearText: { ...typography.caption, color: colors.primary },
});

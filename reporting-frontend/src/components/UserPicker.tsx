import { useDirectoryUsers } from '../lib/useDirectory';
import { Select } from './ui';

export function UserPicker({
  value,
  onChange,
  placeholder = 'اختر مستخدمًا…',
}: {
  value: string;
  onChange: (id: string) => void;
  placeholder?: string;
}) {
  const { data: users = [] } = useDirectoryUsers();
  return (
    <Select value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">{placeholder}</option>
      {users.map((u) => (
        <option key={u.id} value={u.id}>
          {u.fullName} — {u.email}
        </option>
      ))}
    </Select>
  );
}

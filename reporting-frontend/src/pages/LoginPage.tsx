import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import { apiErrorMessage } from '../lib/api';
import { Alert, Button, Field, Input } from '../components/ui';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await login(email, password);
      navigate('/app');
    } catch (err) {
      setError(apiErrorMessage(err, 'تعذّر تسجيل الدخول، تحقق من البيانات.'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid min-h-screen place-items-center bg-offwhite px-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <img src="/logo-mark.png" alt="" className="mx-auto h-16" />
          <h1 className="mt-4 text-xl font-bold text-navy">تسجيل الدخول</h1>
          <p className="mt-1 text-sm text-ink-2">نظام تقارير الأداء والتشغيل الداخلي</p>
        </div>
        <form onSubmit={onSubmit} className="space-y-4 rounded-xl border border-line bg-white p-6">
          {error && <Alert tone="alert">{error}</Alert>}
          <Field label="البريد الإلكتروني">
            <Input
              type="email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </Field>
          <Field label="كلمة المرور">
            <Input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </Field>
          <Button type="submit" disabled={busy} className="w-full">
            {busy ? 'جارٍ الدخول…' : 'دخول'}
          </Button>
        </form>
        <p className="mt-4 text-center text-sm">
          <Link to="/" className="text-navy underline">
            رجوع للصفحة الرئيسية
          </Link>
        </p>
      </div>
    </div>
  );
}

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthProvider, useAuth } from './auth';
import { tokenStore } from './tokenStore';
import { api } from './api';

function Probe() {
  const { user, login } = useAuth();
  return (
    <div>
      <span data-testid="name">{user?.fullName ?? 'anon'}</span>
      <button onClick={() => login('a@b.c', 'pw')}>login</button>
    </div>
  );
}

describe('AuthProvider', () => {
  beforeEach(() => {
    tokenStore.clear();
    vi.restoreAllMocks();
  });

  it('starts anonymous when no token', async () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('name')).toHaveTextContent('anon'));
  });

  it('sets user and tokens on login', async () => {
    vi.spyOn(api, 'post').mockResolvedValue({
      data: {
        accessToken: 'acc',
        refreshToken: 'ref',
        accessTokenExpiresUtc: '2030-01-01T00:00:00Z',
        userId: 'u1',
        fullName: 'مدير تجريبي',
        email: 'a@b.c',
        roles: ['Admin'],
      },
    });
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    await screen.findByTestId('name');
    await userEvent.click(screen.getByText('login'));
    await waitFor(() => expect(screen.getByTestId('name')).toHaveTextContent('مدير تجريبي'));
    expect(tokenStore.access).toBe('acc');
    expect(tokenStore.refresh).toBe('ref');
  });
});

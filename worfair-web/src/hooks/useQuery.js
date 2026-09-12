import { useCallback, useEffect, useRef, useState } from 'react';

// GET simples com loading/error/refetch. Sem cache global nesta fase.
export function useQuery(fn, { immediate = true } = {}) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(immediate);
  const [error, setError] = useState(null);
  const alive = useRef(true);

  const run = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fn();
      if (alive.current) setData(res?.data ?? res);
      return res?.data ?? res;
    } catch (err) {
      if (alive.current) setError(err);
      throw err;
    } finally {
      if (alive.current) setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    alive.current = true;
    if (immediate) run().catch(() => {});
    return () => {
      alive.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { data, loading, error, refetch: run };
}

export function apiMessage(err, fallback = 'Operação falhou. Tente novamente.') {
  const data = err?.response?.data;
  if (!data) return err?.message ?? fallback;
  if (typeof data === 'string') return data;
  return data.message ?? data.title ?? data.error ?? fallback;
}

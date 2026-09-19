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

// Converte erro HTTP em texto amigável. Mensagens cruas do backend
// (títulos de ProblemDetails, códigos internos) nunca chegam à tela:
// cada status tem um texto próprio, e o `fallback` da página prevalece.
export function apiMessage(err, fallback = 'Operação falhou. Tente novamente.') {
  const status = err?.response?.status;
  const data = err?.response?.data;

  if (status === 400) {
    // Erros de domínio vêm em { message } em português — esses podem aparecer.
    if (data && typeof data === 'object' && typeof data.message === 'string' && data.message.trim()) {
      return data.message;
    }
    if (typeof data === 'string' && data.trim()) return data;
    return fallback;
  }
  if (status === 401) return 'Sua sessão expirou. Entre novamente.';
  if (status === 403)
    return 'Você não tem acesso a isso aqui. Entre no espaço certo ou peça acesso ao responsável.';
  if (status === 404) return 'Não encontrado. Pode ter sido removido.';
  if (status === 409) return 'Isso já existe. Atualize a página e tente de novo.';
  if (typeof status === 'number' && status >= 500)
    return 'O serviço está instável agora. Tente de novo em instantes.';
  if (err?.response) return fallback;
  if (err?.message) return 'Sem resposta do servidor. Confira sua conexão e tente de novo.';
  return fallback;
}

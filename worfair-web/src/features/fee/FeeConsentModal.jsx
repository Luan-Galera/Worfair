import { useState } from 'react';
import { money } from '../../utils/money';

// Modal de aceite explícito da taxa. A ação com dinheiro só prossegue após o
// aceite (checkbox obrigatória). Sem aceite, a faixa amarela volta a aparecer.
export function FeeConsentModal({ open, onAccept, onClose }) {
  const [checked, setChecked] = useState(false);

  if (!open) return null;

  const accept = () => {
    if (!checked) return;
    setChecked(false);
    onAccept();
  };

  const close = () => {
    setChecked(false);
    onClose();
  };

  return (
    <div className="modal d-block" tabIndex="-1" role="dialog" aria-modal="true" aria-label="Aceite da taxa">
      <div className="modal-dialog">
        <div className="modal-content">
          <div className="modal-header">
            <h5 className="modal-title">Taxa da plataforma: 15%</h5>
            <button type="button" className="btn-close" aria-label="Fechar" onClick={close} />
          </div>
          <div className="modal-body">
            <p>
              Sobre o valor do trabalho há um acréscimo de <strong>15%</strong> pago pelo
              contratante. O prestador recebe o valor integral ({money(1000)} → taxa{' '}
              {money(150)} → contratante paga {money(1150)}). A taxa remunera o mantenedor
              da plataforma.
            </p>
            <div className="form-check">
              <input
                className="form-check-input"
                type="checkbox"
                id="fee-consent-check"
                checked={checked}
                onChange={(e) => setChecked(e.target.checked)}
              />
              <label className="form-check-label" htmlFor="fee-consent-check">
                Li e concordo com a taxa de 15% sobre o valor dos trabalhos.
              </label>
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-outline-secondary" onClick={close}>
              Cancelar
            </button>
            <button type="button" className="btn btn-primary" disabled={!checked} onClick={accept}>
              Concordar e continuar
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

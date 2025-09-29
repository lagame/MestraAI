// JS nativo: alterna o bloco de detalhes sem dependências.
function toggleRollDetails(btn) {
  const container = btn.closest('.roll-msg');
  const details = container.querySelector('.roll-details');
  if (!details) return;
  details.hidden = !details.hidden;
  btn.textContent = details.hidden ? 'detalhes' : 'ocultar';
}

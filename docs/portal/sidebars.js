// Sidebar gerada a partir das pastas de docs (arquitetura + governança).
/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
module.exports = {
  docs: [
    'arquitetura',
    'mapa-implementacao',
    {
      type: 'category',
      label: 'Governança',
      items: [
        'governanca/dados-pii-lgpd',
        'governanca/continuidade-rpo-rto',
        'governanca/avaliacao-ia',
        'governanca/multi-tenant',
        'governanca/modelo-de-ativos-isa95',
        'governanca/oee-kpi-manufatura',
        'governanca/carga-operacional',
        'governanca/seguranca-runtime-siem',
        'governanca/sincronizacao-tempo-ot',
        'governanca/motor-query-lake',
      ],
    },
  ],
};
